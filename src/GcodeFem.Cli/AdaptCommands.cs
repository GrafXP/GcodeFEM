using System.Numerics;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Geometry;

namespace GcodeFem.Cli;

static class AdaptCommands
{
    /// <summary>Slice → voxelize → adaptive octree solve. With --reference it also solves at bead resolution and compares.</summary>
    public static async Task<int> Adapt(CommandLine commandLine)
    {
        var problem = await FemCommands.Prepare(commandLine);
        var solver = LinearSolvers.ByName(commandLine.Option("solver") ?? "auto");
        var mesh = FemMesh.Build(problem.Grid, problem.MinFill, problem.LoadCase);
        FemCommands.PrintMesh(mesh);
        FemCommands.PrintLines(mesh);
        FemCommands.PrintBonds(mesh, problem);
        FemCommands.PrintReach(mesh, problem.LoadCase);
        if (commandLine.Option("sweep") is { } variants) return Sweep(variants, commandLine, problem, mesh, solver);
        var options = Resolve(Options(commandLine, problem.MinFill), mesh, out var suggested);
        // Refined where the part is nearest to breaking, unless --mark vonmises asks for the stress as in M3.
        var cells = problem.CellStrength(mesh);
        var byStress = commandLine.Option("mark") switch
        {
            null or "failure" => false,
            "vonmises" => true,
            var other => throw new FormatException($"--mark expects failure or vonmises but got '{other}'."),
        };

        Console.WriteLine(byStress || cells is null
            ? "refining     where the von Mises stress is above the peak / k"
            : $"refining     where the von Mises stress is above the peak / k, and where the safety factor is under {options.FailureRefineFactor:0.##} (kf) x the lowest");
        PrintHeader(options, suggested);
        var adaptive = AdaptiveAnalysis.Run(mesh, problem.Material, problem.LoadCase, solver, options, pass => Console.WriteLine(Row(pass, options)), byStress ? null : cells);
        Console.WriteLine($"stopped      {adaptive.StopReason}");
        var memory = FemCommands.PeakWorkingSet();
        var peak = Enumerable.Range(0, mesh.Cells.Length).MaxBy(e => adaptive.VonMises[e]);
        var loaded = problem.LoadedNodes(mesh);
        var mean = loaded.Select(adaptive.Displacement).Aggregate(Vector3.Zero, (a, b) => a + b) / Math.Max(1, loaded.Count);
        Console.WriteLine($"result       max von Mises {adaptive.Final.MaxVonMises:0.00} MPa at {Text.Format(FemCommands.CellCentre(mesh, peak))} (fill {mesh.Fill[peak]:0.00}); " +
                          $"loaded face mean displacement {Text.Format(mean)} mm");
        var failure = cells is null ? null : adaptive.Failure ?? cells.Check(adaptive.Stress);
        if (failure is not null)
        {
            FemCommands.PrintFailure(mesh, cells!, failure, problem.LoadCase);
            var level = adaptive.Octree.Leaves[adaptive.Octree.CellLeaf[failure.Weakest]].Level;
            if (level > 0) Console.WriteLine($"  WARNING    the weakest spot sits in an element of {1 << level}^3 cells: coarse elements read the stress too low, so the real safety factor is lower");
        }
        Console.WriteLine($"cost         {adaptive.Final.Dofs:N0} DOF in the last of {adaptive.Passes.Count} passes, {adaptive.Time.TotalSeconds:0.00} s in total, peak working set {Text.Megabytes(memory)}");
        if (commandLine.Flag("reference"))
            PrintComparison(adaptive, StaticAnalysis.Run(mesh, problem.Material, problem.LoadCase, solver, new AnalysisOptions(problem.MinFill)), problem, cells, failure);
        return 0;
    }

    /// <summary>
    /// Tuning aid: solves the reference once, then the octree for each variant of the options.
    /// Variants are separated by ';' and each lists overrides, e.g. "k=2;k=3,buffer=2;level=4,energy=0.8".
    /// </summary>
    static int Sweep(string variants, CommandLine commandLine, FemCommands.Problem problem, FemMesh mesh, ILinearSolver solver)
    {
        var reference = StaticAnalysis.Run(mesh, problem.Material, problem.LoadCase, solver, new AnalysisOptions(problem.MinFill));
        var peak = reference.VonMises.Max();
        var time = (reference.AssemblyTime + reference.Solve.Setup + reference.Solve.Solve + reference.StressTime).TotalSeconds;
        var hot = Enumerable.Range(0, mesh.Cells.Length).Where(e => reference.VonMises[e] >= peak / 2).ToList();
        var top = hot.Where(e => reference.VonMises[e] >= 0.9 * peak).ToList();
        var loaded = problem.LoadedNodes(mesh);
        var displacement = loaded.Select(reference.Displacement).Aggregate(Vector3.Zero, (a, b) => a + b).Length();
        // With strengths, each variant refines where the part is nearest to breaking too, and its safety factor is compared.
        var cells = problem.CellStrength(mesh);
        var full = cells?.Check(reference.Stress);
        Console.WriteLine($"reference    {reference.Dofs:N0} DOF, {reference.Solve.Iterations} iterations, {time:0.00} s; max von Mises {peak:0.00} MPa; {hot.Count:N0} cells above half of it, {top.Count:N0} above 90 %" +
                          (full is null ? "" : $"; safety factor {full.SafetyFactor:0.000}"));
        Console.WriteLine($"{"variant",-34} {"root",4} {"passes",6} {"DOF",9} {"of ref",7} {"time s",7} {"of ref",7} {"vM/ref",7} {"u/ref",7} {"C/ref",7} {"hot err",8} {"top err",8} {"ref/SF",7}  stopped because");

        foreach (var variant in variants.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var options = Options(commandLine, problem.MinFill);
            foreach (var setting in variant.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var (key, value) = setting.Split('=') is [var k, var v] ? (k, double.Parse(v)) : throw new FormatException($"--sweep expects key=value but got '{setting}'.");
                options = key switch
                {
                    "level" => options with { RootLevel = (int)value },
                    "k" => options with { RefineFactor = value },
                    "kf" => options with { FailureRefineFactor = value },
                    "buffer" => options with { Buffer = (int)value },
                    "energy" => options with { EnergyFraction = value },
                    "change" => options with { ConvergedChange = value },
                    "passes" => options with { MaxPasses = (int)value },
                    _ => throw new FormatException($"--sweep knows level, k, kf, buffer, energy, change and passes, not '{key}'."),
                };
            }
            var adaptive = AdaptiveAnalysis.Run(mesh, problem.Material, problem.LoadCase, solver, options, strength: cells);
            double Error(int cell) => Math.Abs(adaptive.VonMises[cell] - reference.VonMises[cell]) / peak;
            var safety = full is null || adaptive.Failure is null ? "-" : (full.SafetyFactor / adaptive.Failure.SafetyFactor).ToString("0.000");
            Console.WriteLine($"{variant,-34} {adaptive.RootLevel,4} {adaptive.Passes.Count,6} {adaptive.Final.Dofs,9:N0} {(double)adaptive.Final.Dofs / reference.Dofs,7:0.0%} " +
                              $"{adaptive.Time.TotalSeconds,7:0.00} {adaptive.Time.TotalSeconds / time,7:0.0%} {adaptive.Final.MaxVonMises / peak,7:0.000} " +
                              $"{loaded.Select(adaptive.Displacement).Aggregate(Vector3.Zero, (a, b) => a + b).Length() / displacement,7:0.000} {adaptive.Final.Compliance / Compliance(reference),7:0.000} " +
                              $"{hot.Max(Error),8:0.0%} {top.Max(Error),8:0.0%} {safety,7}  {adaptive.StopReason}");
        }
        return 0;
    }

    /// <summary>Solid cantilevers of growing size: the octree against the bead-resolution solve.</summary>
    public static int Bench(CommandLine commandLine)
    {
        var scales = (commandLine.Option("scales") ?? "2,4,6,8").Split(',').Select(int.Parse).ToArray();
        var solver = LinearSolvers.ByName(commandLine.Option("solver") ?? "auto");
        var given = Options(commandLine);
        var material = IsotropicMaterial.Pla;
        const float pitch = 0.42f, layer = 0.2f;

        Console.WriteLine($"adaptive cantilever benchmark: 25s x 4s x 8s bead cells ({pitch} x {pitch} x {layer} mm), clamped at x = 0, 1 N down at the tip, {Environment.ProcessorCount} threads, {solver.Name}");
        Console.WriteLine($"{"scale",5} {"cells",9} | {"ref DOF",10} {"ref s",7} | {"root",4} {"passes",6} {"DOF",9} {"of ref",7} {"time s",7} {"of ref",7} | {"tip/ref",7} {"vM/ref",7} {"coarse C/ref",12}  stopped because");
        foreach (var scale in scales)
        {
            int nx = 25 * scale, ny = 4 * scale, nz = 8 * scale;
            var bounds = new Box3(Vector3.Zero, new Vector3(nx * pitch, ny * pitch, nz * layer));
            var loadCase = LoadCase.ClampAndPush(bounds, Axis.X, fixMax: false, new Vector3(0, 0, -1));
            var mesh = FemMesh.Build(CellGrid.Solid(nx, ny, nz, pitch, layer), given.MinFill, loadCase.Fixtures);
            var options = Resolve(given, mesh, out var suggested);
            var tipNodes = Enumerable.Range(0, mesh.NodeCount).Where(n => MathF.Abs(mesh.NodePosition(n).X - bounds.Max.X) < 1e-3f).ToList();

            var reference = StaticAnalysis.Run(mesh, material, loadCase, solver);
            var adaptive = AdaptiveAnalysis.Run(mesh, material, loadCase, solver, options);

            var referenceTime = (reference.AssemblyTime + reference.Solve.Setup + reference.Solve.Solve + reference.StressTime).TotalSeconds;
            var tip = tipNodes.Average(n => adaptive.Displacement(n).Z) / tipNodes.Average(n => reference.Displacement(n).Z);
            Console.WriteLine($"{scale,5} {mesh.Cells.Length,9:N0} | {reference.Dofs,10:N0} {referenceTime,7:0.00} | {adaptive.RootLevel,4} {adaptive.Passes.Count,6} {adaptive.Final.Dofs,9:N0} " +
                              $"{(double)adaptive.Final.Dofs / reference.Dofs,7:0.0%} {adaptive.Time.TotalSeconds,7:0.00} {adaptive.Time.TotalSeconds / referenceTime,7:0.0%} | " +
                              $"{tip,7:0.000} {adaptive.Final.MaxVonMises / reference.VonMises.Max(),7:0.000} {adaptive.Passes[0].Compliance / Compliance(reference),12:0.000}  {adaptive.StopReason}");
            if (!commandLine.Flag("verbose")) continue;
            PrintHeader(options, suggested, " vM/ref   C/ref");
            foreach (var pass in adaptive.Passes)
                Console.WriteLine($"{Row(pass, options)} {pass.MaxVonMises / reference.VonMises.Max(),7:0.000} {pass.Compliance / Compliance(reference),7:0.000}");
        }
        return 0;
    }

    public static AdaptiveOptions Options(CommandLine commandLine, float minFill = 0.05f)
    {
        var defaults = new AdaptiveOptions();
        return defaults with
        {
            RootLevel = commandLine.Option("level") is { } level and not "auto" ? int.Parse(level) : null,
            RefineFactor = commandLine.Number("k", defaults.RefineFactor),
            FailureRefineFactor = commandLine.Number("kf", defaults.FailureRefineFactor),
            Buffer = (int)commandLine.Number("buffer", defaults.Buffer),
            RefineInterfaces = !commandLine.Flag("no-interfaces"),
            EnergyFraction = commandLine.Number("energy", defaults.EnergyFraction),
            ConvergedChange = commandLine.Number("change", defaults.ConvergedChange),
            MaxPasses = (int)commandLine.Number("passes", defaults.MaxPasses),
            MaxDofs = commandLine.Option("max-dofs") is null ? null : (int)commandLine.Number("max-dofs", 0),
            MinFill = minFill,
            Tolerance = commandLine.Number("tolerance", defaults.Tolerance),
        };
    }

    static double Compliance(FemResult fem) => fem.System.RightHandSide.Zip(fem.Displacements, (f, u) => f * u).Sum();

    /// <summary>Fills in the root level and DOF budget that the analysis would otherwise pick itself, so they can be shown first.</summary>
    static AdaptiveOptions Resolve(AdaptiveOptions options, FemMesh mesh, out bool suggested)
    {
        suggested = options.RootLevel is null;
        var maxDofs = options.MaxDofs ?? OctreeAdvisor.DofBudget();
        return options with { MaxDofs = maxDofs, RootLevel = options.RootLevel ?? OctreeAdvisor.SuggestRootLevel(mesh, maxDofs) };
    }

    static int LevelsWidth(AdaptiveOptions options) => 7 * (options.RootLevel!.Value + 1);

    static void PrintHeader(AdaptiveOptions options, bool suggested, string extraColumns = "")
    {
        var root = options.RootLevel!.Value;
        Console.WriteLine($"octree       root level {root}{(suggested ? " (suggested)" : "")}: {(root == 0 ? "every bead cell is an element" : $"{1 << root}^3 cells per coarse element")}; " +
                          $"k {options.RefineFactor:0.##}, buffer {options.Buffer}, DOF budget {options.MaxDofs:N0}");
        var levels = string.Join("/", Enumerable.Range(0, root + 1).Select(l => $"L{l}"));
        Console.WriteLine($"{"pass",5} {"elements",9}  {levels.PadRight(LevelsWidth(options))} {"DOF",10} {"hanging",8} {"build s",8} {"setup s",8} {"solve s",8} {"iters",6} {"max vM",8} {"min SF",7} {"lvl",3} {"marked",7}{extraColumns}");
    }

    /// <summary>"lvl" is the level of the element holding the cell the refinement follows: the weakest, or the one with the highest stress.</summary>
    static string Row(AdaptivePass p, AdaptiveOptions options) =>
        $"{p.Index,5} {p.Leaves,9:N0}  {string.Join("/", p.LeavesByLevel).PadRight(LevelsWidth(options))} {p.Dofs,10:N0} {p.HangingNodes,8:N0} {p.BuildTime.TotalSeconds,8:0.00} " +
        $"{p.Solve.Setup.TotalSeconds,8:0.00} {p.Solve.Solve.TotalSeconds,8:0.00} {p.Solve.Iterations,6} {p.MaxVonMises,8:0.000} {(double.IsNaN(p.SafetyFactor) ? "-" : p.SafetyFactor.ToString("0.000")),7} " +
        $"{p.PeakLevel,3} {p.Marked,7:N0}{(p.Solve.Converged ? "" : " NOT CONVERGED")}{(p.Solve.Retried ? " (solved again: the first try gave NaN)" : "")}";

    static void PrintComparison(AdaptiveResult adaptive, FemResult reference, FemCommands.Problem problem, CellStrength? cells, CellFailure? failure)
    {
        var mesh = reference.Mesh;
        var worst = Enumerable.Range(0, mesh.Cells.Length).MaxBy(e => reference.VonMises[e]);
        var peak = reference.VonMises[worst];
        var time = (reference.AssemblyTime + reference.Solve.Setup + reference.Solve.Solve + reference.StressTime).TotalSeconds;
        Console.WriteLine($"reference    bead resolution: {reference.Dofs:N0} DOF, {reference.Solve.Iterations} iterations, {time:0.00} s; " +
                          $"max von Mises {peak:0.00} MPa at {Text.Format(FemCommands.CellCentre(mesh, worst))} (fill {mesh.Fill[worst]:0.00})");

        var apart = Vector3.Distance(FemCommands.CellCentre(mesh, worst), FemCommands.CellCentre(mesh, Enumerable.Range(0, mesh.Cells.Length).MaxBy(e => adaptive.VonMises[e])));
        var loaded = problem.LoadedNodes(mesh);
        var displacement = loaded.Select(adaptive.Displacement).Aggregate(Vector3.Zero, (a, b) => a + b).Length()
                           / loaded.Select(reference.Displacement).Aggregate(Vector3.Zero, (a, b) => a + b).Length();
        Console.WriteLine($"per pass     max von Mises / reference: {string.Join("  ", adaptive.Passes.Select(p => $"{p.MaxVonMises / peak:0.000}"))}; " +
                          $"compliance / reference: {string.Join("  ", adaptive.Passes.Select(p => $"{p.Compliance / Compliance(reference):0.000}"))}");
        Console.WriteLine($"adaptive/ref DOF {(double)adaptive.Final.Dofs / reference.Dofs:0.0%}, time {adaptive.Time.TotalSeconds / time:0.0%}, max von Mises {adaptive.Final.MaxVonMises / peak:0.000} " +
                          $"(peaks {apart:0.00} mm apart), loaded face displacement {displacement:0.000}, compliance {adaptive.Final.Compliance / Compliance(reference):0.000}");

        // How well the stress field is reproduced where it matters, not only its single highest value.
        var hot = Enumerable.Range(0, mesh.Cells.Length).Where(e => reference.VonMises[e] >= peak / 2).ToList();
        int Level(int cell) => adaptive.Octree.Leaves[adaptive.Octree.CellLeaf[cell]].Level;
        double Error(int cell) => Math.Abs(adaptive.VonMises[cell] - reference.VonMises[cell]) / peak;
        var off = hot.MaxBy(Error);
        var top = hot.Where(e => reference.VonMises[e] >= 0.9 * peak).ToList();
        Console.WriteLine($"hot cells    {hot.Count:N0} cells above half the reference peak, {hot.Count(e => Level(e) == 0):N0} of them at bead resolution; " +
                          $"largest error {Error(off):0.0%} of the peak (reference {reference.VonMises[off]:0.00} MPa, in a level-{Level(off)} leaf at {Text.Format(FemCommands.CellCentre(mesh, off))}), " +
                          $"mean {hot.Average(Error):0.00%}; within 10 % of the peak: {top.Count:N0} cells, largest error {top.Max(Error):0.0%}");
        if (cells is null || failure is null) return;

        // The same for the failure check: the lowest safety factor, where it is, and how well the cells near breaking come out.
        var full = cells.Check(reference.Stress);
        var weak = Enumerable.Range(0, mesh.Cells.Length).Where(e => full.Index[e] >= full.Index[full.Weakest] / 2).ToList();
        double IndexError(int cell) => Math.Abs(failure.Index[cell] - full.Index[cell]) / full.Index[full.Weakest];
        Console.WriteLine($"failure      reference safety factor {full.SafetyFactor:0.000} ({full.Mode[full.Weakest].DisplayName().ToLowerInvariant()}, layer {mesh.Cells[full.Weakest].K + 1}), " +
                          $"layer line {full.LayerLineSafetyFactor:0.000} under layer {mesh.Cells[full.WeakestLayerLine].K + 1}; " +
                          $"adaptive / reference {full.SafetyFactor / failure.SafetyFactor:0.000} (weakest spots {Vector3.Distance(FemCommands.CellCentre(mesh, full.Weakest), FemCommands.CellCentre(mesh, failure.Weakest)):0.00} mm apart), " +
                          $"layer line {full.LayerLineSafetyFactor / failure.LayerLineSafetyFactor:0.000}; {weak.Count:N0} cells within twice the lowest, " +
                          $"{weak.Count(e => Level(e) == 0):N0} at bead resolution, largest error {weak.Max(IndexError):0.0%} of the highest index");
    }
}
