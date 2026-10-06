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
        var mesh = FemMesh.Build(problem.Grid, problem.MinFill, problem.LoadCase.Fixtures);
        FemCommands.PrintMesh(mesh);
        if (commandLine.Option("sweep") is { } variants) return Sweep(variants, commandLine, problem, mesh, solver);
        var options = Resolve(Options(commandLine, problem.MinFill), mesh, out var suggested);

        PrintHeader(options, suggested);
        var adaptive = AdaptiveAnalysis.Run(mesh, problem.Material, problem.LoadCase, solver, options, pass => Console.WriteLine(Row(pass, options)));
        Console.WriteLine($"stopped      {adaptive.StopReason}");
        var memory = FemCommands.PeakWorkingSet();
        var peak = adaptive.Final.PeakCell;
        var loaded = problem.LoadedNodes(mesh);
        var mean = loaded.Select(adaptive.Displacement).Aggregate(Vector3.Zero, (a, b) => a + b) / Math.Max(1, loaded.Count);
        Console.WriteLine($"result       max von Mises {adaptive.Final.MaxVonMises:0.00} MPa at {Text.Format(FemCommands.CellCentre(mesh, peak))} (fill {mesh.Fill[peak]:0.00}); " +
                          $"loaded face mean displacement {Text.Format(mean)} mm");
        Console.WriteLine($"cost         {adaptive.Final.Dofs:N0} DOF in the last of {adaptive.Passes.Count} passes, {adaptive.Time.TotalSeconds:0.00} s in total, peak working set {Text.Megabytes(memory)}");
        if (commandLine.Flag("reference"))
            PrintComparison(adaptive, StaticAnalysis.Run(mesh, problem.Material, problem.LoadCase, solver, new AnalysisOptions(problem.MinFill)), problem);
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
        Console.WriteLine($"reference    {reference.Dofs:N0} DOF, {reference.Solve.Iterations} iterations, {time:0.00} s; max von Mises {peak:0.00} MPa; {hot.Count:N0} cells above half of it, {top.Count:N0} above 90 %");
        Console.WriteLine($"{"variant",-34} {"root",4} {"passes",6} {"DOF",9} {"of ref",7} {"time s",7} {"of ref",7} {"vM/ref",7} {"u/ref",7} {"C/ref",7} {"hot err",8} {"top err",8}  stopped because");

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
                    "buffer" => options with { Buffer = (int)value },
                    "energy" => options with { EnergyFraction = value },
                    "change" => options with { ConvergedChange = value },
                    "passes" => options with { MaxPasses = (int)value },
                    _ => throw new FormatException($"--sweep knows level, k, buffer, energy, change and passes, not '{key}'."),
                };
            }
            var adaptive = AdaptiveAnalysis.Run(mesh, problem.Material, problem.LoadCase, solver, options);
            double Error(int cell) => Math.Abs(adaptive.VonMises[cell] - reference.VonMises[cell]) / peak;
            Console.WriteLine($"{variant,-34} {adaptive.RootLevel,4} {adaptive.Passes.Count,6} {adaptive.Final.Dofs,9:N0} {(double)adaptive.Final.Dofs / reference.Dofs,7:0.0%} " +
                              $"{adaptive.Time.TotalSeconds,7:0.00} {adaptive.Time.TotalSeconds / time,7:0.0%} {adaptive.Final.MaxVonMises / peak,7:0.000} " +
                              $"{loaded.Select(adaptive.Displacement).Aggregate(Vector3.Zero, (a, b) => a + b).Length() / displacement,7:0.000} {adaptive.Final.Compliance / Compliance(reference),7:0.000} " +
                              $"{hot.Max(Error),8:0.0%} {top.Max(Error),8:0.0%}  {adaptive.StopReason}");
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
        Console.WriteLine($"{"pass",5} {"elements",9}  {levels.PadRight(LevelsWidth(options))} {"DOF",10} {"hanging",8} {"build s",8} {"setup s",8} {"solve s",8} {"iters",6} {"max vM",8} {"lvl",3} {"marked",7}{extraColumns}");
    }

    static string Row(AdaptivePass p, AdaptiveOptions options) =>
        $"{p.Index,5} {p.Leaves,9:N0}  {string.Join("/", p.LeavesByLevel).PadRight(LevelsWidth(options))} {p.Dofs,10:N0} {p.HangingNodes,8:N0} {p.BuildTime.TotalSeconds,8:0.00} " +
        $"{p.Solve.Setup.TotalSeconds,8:0.00} {p.Solve.Solve.TotalSeconds,8:0.00} {p.Solve.Iterations,6} {p.MaxVonMises,8:0.000} {p.PeakLevel,3} {p.Marked,7:N0}{(p.Solve.Converged ? "" : " NOT CONVERGED")}";

    static void PrintComparison(AdaptiveResult adaptive, FemResult reference, FemCommands.Problem problem)
    {
        var mesh = reference.Mesh;
        var worst = Enumerable.Range(0, mesh.Cells.Length).MaxBy(e => reference.VonMises[e]);
        var peak = reference.VonMises[worst];
        var time = (reference.AssemblyTime + reference.Solve.Setup + reference.Solve.Solve + reference.StressTime).TotalSeconds;
        Console.WriteLine($"reference    bead resolution: {reference.Dofs:N0} DOF, {reference.Solve.Iterations} iterations, {time:0.00} s; " +
                          $"max von Mises {peak:0.00} MPa at {Text.Format(FemCommands.CellCentre(mesh, worst))} (fill {mesh.Fill[worst]:0.00})");

        var apart = Vector3.Distance(FemCommands.CellCentre(mesh, worst), FemCommands.CellCentre(mesh, adaptive.Final.PeakCell));
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
    }
}
