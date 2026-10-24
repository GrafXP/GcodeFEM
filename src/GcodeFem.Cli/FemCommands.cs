using System.Diagnostics;
using System.Numerics;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Slicing;
using GcodeFem.Core.Study;
using GcodeFem.Core.Visual;

namespace GcodeFem.Cli;

static class FemCommands
{
    /// <summary>A sliced, voxelized part with its mounts and loads.</summary>
    /// <param name="Toolpath">What the cells were filled from, laid in the print frame by <paramref name="Placement"/>.</param>
    /// <param name="Bonding">What the bond of its layer lines is measured against.</param>
    /// <param name="Strength">What its cells are checked against; null if the material gives no strength.</param>
    public sealed record Problem(Toolpath Toolpath, Placement Placement, CellGrid Grid, OrthotropicMaterial Material, LoadCase LoadCase, float MinFill, BondReference Bonding, PrintedStrength? Strength)
    {
        /// <summary>The cells' strengths on <paramref name="mesh"/>, each layer line as strong as it has bonded; null without strengths.</summary>
        public CellStrength? CellStrength(FemMesh mesh) =>
            Strength is { } strength ? new CellStrength(mesh, strength, new BondModel().Cells(mesh.Grid, mesh.Cells, Bonding, MinFill).Factor) : null;

        /// <summary>Nodes of <paramref name="mesh"/> that carry load.</summary>
        public List<int> LoadedNodes(FemMesh mesh)
        {
            var loads = Assembler.Loads(mesh, LoadCase);
            return [.. Enumerable.Range(0, mesh.NodeCount).Where(n => loads[3 * n] != 0 || loads[3 * n + 1] != 0 || loads[3 * n + 2] != 0)];
        }
    }

    /// <summary>
    /// Slice → voxelize → mounts and loads. With --study they are the study's interfaces in one of its
    /// load cases; without, one outer face of the printed cells is clamped and the opposite one loaded.
    /// </summary>
    public static async Task<Problem> Prepare(CommandLine commandLine)
    {
        var study = commandLine.Option("study") is { } path ? StudyFile.Load(path) : null;
        var (result, toolpath) = await SliceCommands.SliceAndParse(commandLine, study);
        var (material, bonding, strength) = Material(commandLine, toolpath);

        var watch = Stopwatch.StartNew();
        var grid = Voxelize(result, toolpath);
        var minFill = (float)commandLine.Number("min-fill", 0.05);
        var voxelized = watch.Elapsed;

        LoadCase loadCase;
        Console.WriteLine();
        if (study is null)
        {
            var (axis, fixMax) = ParseFace(commandLine.Option("fix") ?? "zmin");
            var force = Text.Triple(commandLine.Option("force") ?? "0,0,-100", "force");
            var bounds = grid.OccupiedBounds(minFill); // the cells' own outer faces, so clamp and load land on node planes
            loadCase = LoadCase.ClampAndPush(bounds, axis, fixMax, force);
            Console.WriteLine($"load case    clamp {commandLine.Option("fix") ?? "zmin"} face, {Text.Format(force)} N on the opposite face");
        }
        else
        {
            var chosen = StudyCommands.Case(study, commandLine.Option("case"));
            loadCase = InterfaceMapper.Map(result.PrintMesh, result.Placement.Rotation, grid.Pitch, study.Interfaces, chosen);
            Console.WriteLine($"load case    '{chosen.Name}' of {Path.GetFileName(commandLine.Option("study"))}");
            foreach (var item in study.Interfaces)
                Console.WriteLine($"  {item.Name,-16} {item.Kind,-9} {(item.Kind.IsLoad() ? StudyCommands.Describe(item.Kind, chosen.Of(item)) : "")}");
        }
        Console.WriteLine($"cells        grid {grid.SizeX} x {grid.SizeY} x {grid.SizeZ}, {grid.BlockCount} blocks of {CellGrid.BlockSize}^3; " +
                          $"voxelized in {voxelized.TotalSeconds:0.00} s, {grid.TotalVolume:0.0} mm3 deposited, {grid.LostVolume:0.000} mm3 outside the grid");
        return new Problem(toolpath, result.Placement, grid, material, loadCase, minFill, bonding, strength);
    }

    /// <summary>The part's cells, with times stretched to the slicer's own (acceleration included).</summary>
    static CellGrid Voxelize(SliceResult result, Toolpath toolpath)
    {
        var clock = PrintClock.Calibrated(toolpath, result.Report.FeatureSeconds);
        var stretched = Enum.GetValues<FeatureType>().Where(f => f.IsPartMaterial() && clock.Factor(f) > 1 && toolpath.Segments.Any(s => s.Feature == f))
            .Select(f => $"{f.DisplayName().ToLowerInvariant()} x{clock.Factor(f):0.00}").Append($"travel x{clock.TravelFactor:0.00}");
        Console.WriteLine($"clock        feed-rate times stretched to the slicer's: {string.Join(", ", stretched)}");
        return Voxelizer.Voxelize(toolpath, result.Placement, result.PrintMesh.Bounds, clock);
    }

    /// <summary>
    /// The stiffness to solve with: that of the material of the filament the part was sliced with
    /// (or of --material), unless --isotropic, --E or --nu say otherwise; what the bond of the layer
    /// lines is measured against; and the strengths the cells are checked against.
    /// </summary>
    static (OrthotropicMaterial Stiffness, BondReference Bonding, PrintedStrength? Strength) Material(CommandLine commandLine, Toolpath toolpath, bool print = true)
    {
        var match = MaterialCommands.For(commandLine, toolpath);
        var changes = new List<string>();
        // Given both numbers, the material need not have any.
        var material = commandLine.Option("E") is not null && commandLine.Option("nu") is not null ? default : match.Material.Stiffness();
        if (commandLine.Flag("isotropic"))
        {
            material = new OrthotropicMaterial(material.AlongLine, material.AlongLine, material.AlongLine, material.PoissonRatio);
            changes.Add("the same every way (--isotropic)");
        }
        if (commandLine.Option("E") is not null)
        {
            var e = commandLine.Number("E", 0);
            material = new OrthotropicMaterial(e, e, e, material.PoissonRatio);
            changes.Add("E as given by --E");
        }
        if (commandLine.Option("nu") is not null)
        {
            material = material with { PoissonRatio = commandLine.Number("nu", 0) };
            changes.Add("nu as given by --nu");
        }
        if (commandLine.Option("across-line") is not null)
        {
            material = material with { AcrossLine = commandLine.Number("across-line", 0) };
            changes.Add("across the lines as given by --across-line");
        }
        if (print)
        {
            Console.WriteLine();
            MaterialCommands.Print(match, material, changes);
        }
        return (material, match.Material.Bonding(toolpath), match.Material.Strength(out _));
    }

    /// <summary>What the cells are made of: the share of each line type, and of cells where lines run more than one way.</summary>
    public static void PrintLines(FemMesh mesh)
    {
        var grid = mesh.Grid;
        var counts = new int[FeatureTypes.ClassCount];
        var crossing = 0;
        foreach (var c in mesh.Cells)
        {
            if (grid.Feature(c.I, c.J, c.K) is { } feature) counts[(int)feature]++;
            if (grid.Direction(c.I, c.J, c.K).Spread >= CellColours.CrossingSpread) crossing++;
        }
        var shares = Enumerable.Range(0, counts.Length).Where(f => counts[f] > 0).OrderByDescending(f => counts[f])
            .Select(f => $"{((FeatureClass)f).DisplayName().ToLowerInvariant()} {(double)counts[f] / mesh.Cells.Length:0%}");
        Console.WriteLine($"lines        cells by line type: {string.Join(", ", shares)}; lines cross in {(double)crossing / mesh.Cells.Length:0%} of the cells");
    }

    /// <summary>How well the layer lines have bonded, against the specimens of the data sheet; and what that rests on.</summary>
    public static void PrintBonds(FemMesh mesh, Problem problem) => PrintBonds(mesh, problem.Bonding, problem.MinFill);

    public static void PrintBonds(FemMesh mesh, BondReference reference, float minFill)
    {
        var bonds = new BondModel().Cells(mesh.Grid, mesh.Cells, reference, minFill);
        var lined = Enumerable.Range(0, mesh.Cells.Length).Where(e => !float.IsNaN(bonds.Gap[e])).ToList();
        if (lined.Count == 0)
        {
            Console.WriteLine("layer bond   no cell lies on another");
            return;
        }
        var sorted = lined.Select(e => bonds.Factor[e]).Order().ToList();
        var layers = lined.GroupBy(e => mesh.Cells[e].K).Select(g => (Layer: g.Key, Factor: g.Average(e => bonds.Factor[e]), Gap: g.Average(e => bonds.Gap[e]))).ToList();
        var weakest = layers.MinBy(l => l.Factor);
        Console.WriteLine($"layer bond   {sorted[0]:0.00} to {sorted[^1]:0.00} of the specimens' (median {sorted[sorted.Count / 2]:0.00}, {sorted.Count(f => f < 0.9f) / (double)sorted.Count:0%} of the layer lines below 0.9); " +
                          $"weakest layer {weakest.Layer + 1}: {weakest.Factor:0.00}, printed {weakest.Gap:0.0} s after the one below");
        Console.WriteLine($"  against    the specimens: nozzle {reference.NozzleTemperature:0} C, layers {reference.LayerTime:0.#} s apart, fan {reference.Fan:0%}, glass transition {reference.GlassTransition:0} C");
        foreach (var assumption in reference.Assumed) Console.WriteLine($"  assumed    {Text.Ascii(assumption)}");
        Console.WriteLine("  (the stiffness does not depend on it; the failure check takes the strength across each layer line as the sheet's x its bond)");
    }

    /// <summary>The safety factor, how and where the part breaks first, the load it breaks at, its weakest layer line, and the Tsai-Wu cross-check.</summary>
    public static void PrintFailure(FemMesh mesh, CellStrength cells, CellFailure failure, LoadCase loadCase)
    {
        var grid = mesh.Grid;
        var (factor, weakest) = (failure.SafetyFactor, failure.Weakest);
        string Describe(int e)
        {
            var c = mesh.Cells[e];
            var feature = grid.Feature(c.I, c.J, c.K)?.DisplayName().ToLowerInvariant() ?? "plastic";
            var direction = grid.Direction(c.I, c.J, c.K);
            var lines = direction.Spread >= CellColours.CrossingSpread ? "lines crossing" : $"lines at {direction.Angle * 180 / MathF.PI:0} deg";
            return $"layer {c.K + 1} at {Text.Format(CellCentre(mesh, e))} ({feature}, {lines}, fill {mesh.Fill[e]:0.00}, bond {cells.Bond(e):0.00})";
        }
        Console.WriteLine($"failure      safety factor {factor:0.000}: {failure.Mode[weakest].DisplayName().ToLowerInvariant()} first, {Describe(weakest)}");

        // Linear: every load times the safety factor is where it starts to break.
        var loads = Assembler.Reach(mesh, loadCase).Where(r => r.IsLoad).Select(r => (r.Name, r.Force))
            .Concat(loadCase.Loads.Select(l => (Name: "the load", Force: l.TotalForce))).ToList();
        Console.WriteLine($"  breaks at  {factor:0.###} x the loads: {string.Join(", ", loads.Select(l => $"{l.Name} {factor * l.Force.Length():0.#} N"))}");

        var line = failure.WeakestLayerLine;
        Console.WriteLine($"  layer line weakest under layer {mesh.Cells[line].K + 1}, {grid.ZBoundaries[mesh.Cells[line].K] - grid.ZBoundaries[0]:0.##} mm above the bed: " +
                          $"safety factor {failure.LayerLineSafetyFactor:0.000} against it pulling apart, {Describe(line)}");

        var near = Enumerable.Range(0, mesh.Cells.Length).Where(e => failure.Index[e] >= failure.Index[weakest] / 2).ToList();
        var modes = near.GroupBy(e => failure.Mode[e]).OrderByDescending(g => g.Count()).Select(g => $"{g.Key.DisplayName().ToLowerInvariant()} {g.Count() / (double)near.Count:0%}");
        Console.WriteLine($"  modes      of the {near.Count:N0} cells within twice the lowest safety factor: {string.Join(", ", modes)}");
        var tsaiWu = Enumerable.Range(0, mesh.Cells.Length).MinBy(e => failure.TsaiWu[e]);
        Console.WriteLine($"  Tsai-Wu    {failure.TsaiWu[tsaiWu]:0.000} at its lowest (cross-check), {Describe(tsaiWu)}");
    }

    /// <summary>What each interface of a study got hold of on the cells: how many cell faces, their area, and the force that arrives.</summary>
    public static void PrintReach(FemMesh mesh, LoadCase loadCase)
    {
        var held = Assembler.FixedDofs(mesh, loadCase);
        var loads = Assembler.Loads(mesh, loadCase);
        var applied = Vector3.Zero;
        for (var n = 0; n < mesh.NodeCount; n++) applied += new Vector3((float)loads[3 * n], (float)loads[3 * n + 1], (float)loads[3 * n + 2]);
        Console.WriteLine($"mounts+loads {Enumerable.Range(0, mesh.NodeCount).Count(n => held[3 * n] || held[3 * n + 1] || held[3 * n + 2]):N0} nodes held, " +
                          $"{Enumerable.Range(0, mesh.NodeCount).Count(n => loads[3 * n] != 0 || loads[3 * n + 1] != 0 || loads[3 * n + 2] != 0):N0} nodes loaded with {Text.Format(applied)} N in all");
        foreach (var reach in Assembler.Reach(mesh, loadCase))
            Console.WriteLine($"  {reach.Name,-16} {(reach.IsLoad ? "loads" : "holds"),-9} {reach.Faces:N0} cell faces, {reach.Area:0.0} mm2{(reach.IsLoad ? $", {Text.Format(reach.Force)} N in the print's directions" : "")}");
    }

    /// <summary>Solves the part at full bead resolution, with a timing breakdown.</summary>
    public static async Task<int> Solve(CommandLine commandLine)
    {
        var problem = await Prepare(commandLine);
        var solver = LinearSolvers.ByName(commandLine.Option("solver") ?? "auto");
        var fem = StaticAnalysis.Run(problem.Grid, problem.Material, problem.LoadCase, solver,
            new AnalysisOptions(MinFill: problem.MinFill, MaxIterations: (int)commandLine.Number("max-iterations", 50_000)));

        PrintAnalysis(fem);
        PrintLines(fem.Mesh);
        PrintBonds(fem.Mesh, problem);
        PrintReach(fem.Mesh, problem.LoadCase);
        var face = problem.LoadedNodes(fem.Mesh).Select(fem.Displacement).ToList();
        if (face.Count > 0)
        {
            var mean = face.Aggregate(Vector3.Zero, (a, b) => a + b) / face.Count;
            Console.WriteLine($"  loaded face mean displacement {Text.Format(mean)} mm");
        }
        if (problem.CellStrength(fem.Mesh) is { } cells)
        {
            var failure = cells.Check(fem.Stress);
            PrintFailure(fem.Mesh, cells, failure, problem.LoadCase);
            PrintWeakestLines(fem.Mesh, failure, problem);
        }
        return 0;
    }

    /// <summary>
    /// The result on the print lines: the lines cut at the cells they run through, and the weakest piece
    /// of each line type, as the results view shows it with the other line types switched off.
    /// </summary>
    public static void PrintWeakestLines(FemMesh mesh, CellFailure failure, Problem problem)
    {
        var watch = Stopwatch.StartNew();
        var pieces = BeadPieces.Cut(problem.Toolpath, problem.Placement, mesh);
        var cut = watch.Elapsed;
        var segments = problem.Toolpath.Segments;
        var solved = Enumerable.Range(0, pieces.Count).Where(p => pieces.Cells[p] >= 0).ToList();
        Console.WriteLine($"on the lines {pieces.Count:N0} pieces of line, {solved.Count:N0} of them in a cell of the solve ({(double)solved.Count / mesh.Cells.Length:0.00} per cell), " +
                          $"cut in {cut.TotalSeconds:0.00} s; the weakest of each line type:");
        foreach (var group in solved.GroupBy(p => segments[pieces.Segments[p]].Feature.Class()).OrderBy(g => -g.Max(p => failure.Index[pieces.Cells[p]])))
        {
            var p = group.MaxBy(p => failure.Index[pieces.Cells[p]]);
            var (e, segment) = (pieces.Cells[p], segments[pieces.Segments[p]]);
            Console.WriteLine($"  {group.Key.DisplayName().ToLowerInvariant(),-16} safety factor {failure.SafetyFactorOf(e):0.000}, {failure.Mode[e].DisplayName().ToLowerInvariant()}, " +
                              $"layer {segment.Layer + 1} at {Text.Format(BeadPieces.Middle(pieces.Ends, p))}");
        }
        // A cell can be weakest that holds only the edges of beads whose middles run through its neighbours.
        if (!solved.Any(p => pieces.Cells[p] == failure.Weakest))
            Console.WriteLine($"  note       no line runs through the middle of the weakest cell (fill {mesh.Fill[failure.Weakest]:0.00}): on the lines, the lowest safety factor is " +
                              $"{solved.Min(p => failure.SafetyFactorOf(pieces.Cells[p])):0.000} against {failure.SafetyFactor:0.000}");
    }

    /// <summary>
    /// A bar sliced lying flat and standing up, each pulled along its length: the ratio of their
    /// stiffnesses should be the data sheet's, in the plane of the layers over across them. Each is
    /// also solved with the in-plane modulus every way, which leaves only what the slicer's lines and
    /// the cells make of the two bars; the ratio of the two ratios is then what the material adds.
    /// The strain is read between a quarter and three quarters of the length, away from the clamp and the load.
    /// </summary>
    public static async Task<int> Bars(CommandLine commandLine)
    {
        float length = (float)commandLine.Number("length", 60), width = (float)commandLine.Number("width", 4), height = (float)commandLine.Number("height", 1.6);
        var force = (float)commandLine.Number("force", 20);
        var minFill = (float)commandLine.Number("min-fill", 0.05);
        var solver = LinearSolvers.ByName(commandLine.Option("solver") ?? "auto");
        var bar = MeshFactory.Box(Vector3.Zero, new Vector3(length, width, height));

        var moduli = new List<(string Name, double Printed, double Isotropic, double Filled)>();
        var breaking = new List<(double Force, FailureMode Mode)>();
        OrthotropicMaterial material = default;
        PrintedStrength? strength = null;
        foreach (var (name, turned, axis) in new[] { ("flat", Vector3.Zero, Axis.X), ("upright", new Vector3(0, -90, 0), Axis.Z) })
        {
            Console.WriteLine($"=== {name} ===");
            var (result, toolpath) = await SliceCommands.SliceAndParse(commandLine, model: bar, turned: turned, brief: true);
            (material, var bonding, strength) = Material(commandLine, toolpath, print: moduli.Count == 0);
            Console.WriteLine();
            var grid = Voxelize(result, toolpath);
            var loadCase = LoadCase.ClampAndPush(grid.OccupiedBounds(minFill), axis, fixMax: false, force * LoadCase.Unit(axis));
            var options = new AnalysisOptions(MinFill: minFill);
            var fem = StaticAnalysis.Run(grid, material, loadCase, solver, options);
            PrintAnalysis(fem);
            PrintLines(fem.Mesh);
            PrintBonds(fem.Mesh, bonding, minFill);
            var isotropic = new OrthotropicMaterial(material.AlongLine, material.AlongLine, material.AlongLine, material.PoissonRatio);
            var same = StaticAnalysis.Run(fem.Mesh, isotropic, loadCase, solver, options);
            var model = result.PrintMesh.Bounds;
            moduli.Add((name, Modulus(fem, model, axis, force), Modulus(same, model, axis, force), fem.Mesh.Cells.Sum(c => (double)grid.Volume(c.I, c.J, c.K)) / (model.Size.X * model.Size.Y * model.Size.Z)));
            if (strength is not null)
            {
                // Where it breaks away from the clamp and the load, in the same middle half the strain is read on.
                var failure = new CellStrength(fem.Mesh, strength, new BondModel().Cells(grid, fem.Mesh.Cells, bonding, minFill).Factor).Check(fem.Stress);
                var (low, span) = (LoadCase.Component(model.Min, axis), LoadCase.Component(model.Size, axis));
                var middle = Enumerable.Range(0, fem.Mesh.Cells.Length)
                    .Where(e => LoadCase.Component(CellCentre(fem.Mesh, e), axis) is var at && at > low + span / 4 && at < low + 3 * span / 4)
                    .MaxBy(e => failure.Index[e]);
                breaking.Add((force / failure.Index[middle], failure.Mode[middle]));
            }
            Console.WriteLine();
        }

        var (flat, upright) = (moduli[0], moduli[1]);
        Console.WriteLine($"bars         {length:0.##} x {width:0.##} x {height:0.##} mm, pulled along their length with {force:0.##} N; strain between a quarter and three quarters of it, on the bar's own section");
        Console.WriteLine($"  {"",-10} {"E, MPa",8} {"same every way",15} {"plastic",8}");
        foreach (var (name, printed, same, filled) in moduli)
            Console.WriteLine($"  {name,-10} {printed,8:0} {same,15:0} {filled,8:0%}");
        Console.WriteLine($"  ratio      flat / upright {flat.Printed / upright.Printed:0.000}, the same every way {flat.Isotropic / upright.Isotropic:0.000}; " +
                          $"what the material adds {flat.Printed / upright.Printed / (flat.Isotropic / upright.Isotropic):0.000}, its in-plane / across-layers modulus {material.AlongLine / material.AcrossLayers:0.000}");
        if (breaking.Count == 2 && strength is not null)
        {
            var section = width * height;
            Console.WriteLine($"  breaking   in the middle half: flat {breaking[0].Force:0} N ({breaking[0].Mode.DisplayName().ToLowerInvariant()}), upright {breaking[1].Force:0} N ({breaking[1].Mode.DisplayName().ToLowerInvariant()}); " +
                              $"over the section {breaking[0].Force / section:0.0} and {breaking[1].Force / section:0.0} MPa, the strengths along the lines {strength.AlongLine:0.#} and across the layers {strength.AcrossLayers:0.#}");
        }
        return 0;
    }

    /// <summary>
    /// Apparent Young's modulus along <paramref name="axis"/>: force over the section of <paramref name="model"/>,
    /// over the strain between the node planes nearest a quarter and three quarters of the bar.
    /// </summary>
    static double Modulus(FemResult fem, Box3 model, Axis axis, float force)
    {
        var mesh = fem.Mesh;
        float Along(Vector3 v) => LoadCase.Component(v, axis);
        var planes = Enumerable.Range(0, mesh.NodeCount).Select(n => Along(mesh.NodePosition(n))).Distinct().ToArray();
        float Nearest(float at) => planes.MinBy(plane => MathF.Abs(plane - at));
        var (low, span) = (Along(model.Min), Along(model.Size));
        var (from, to) = (Nearest(low + span / 4), Nearest(low + 3 * span / 4));
        double Shift(float plane) => Enumerable.Range(0, mesh.NodeCount).Where(n => MathF.Abs(Along(mesh.NodePosition(n)) - plane) < 1e-4f).Average(n => Along(fem.Displacement(n)));
        var strain = (Shift(to) - Shift(from)) / (to - from);
        var section = model.Size.X * model.Size.Y * model.Size.Z / span;
        return force / section / strain;
    }

    /// <summary>Solid cantilevers of growing size: DOF vs time vs memory, tip deflection vs Timoshenko.</summary>
    public static int Bench(CommandLine commandLine)
    {
        var scales = (commandLine.Option("scales") ?? "2,4,6,8").Split(',').Select(int.Parse).ToArray();
        var which = commandLine.Option("solver") ?? "both";
        var solvers = which == "both"
            ? (AmgclSolver.IsAvailable ? new ILinearSolver[] { new AmgclSolver(), new PcgSolver() } : [new PcgSolver()])
            : [LinearSolvers.ByName(which)];
        var pcgMaxDofs = (int)commandLine.Number("pcg-max-dofs", 300_000);
        var material = IsotropicMaterial.Pla;
        const float pitch = 0.42f, layer = 0.2f;

        Console.WriteLine($"cantilever benchmark: 25s x 4s x 8s bead cells ({pitch} x {pitch} x {layer} mm), clamped at x = 0, 1 N down at the tip, {Environment.ProcessorCount} threads");
        Console.WriteLine($"{"scale",5} {"cells",9} {"DOF",10} {"nnz M",7} {"matrix",8} {"mesh+asm s",11} {"setup s",8} {"solve s",8} {"iters",6} {"tip/theory",10} {"peak RAM",9}  solver");
        foreach (var scale in scales)
        {
            int nx = 25 * scale, ny = 4 * scale, nz = 8 * scale;
            var grid = CellGrid.Solid(nx, ny, nz, pitch, layer);
            var bounds = new Box3(Vector3.Zero, new Vector3(nx * pitch, ny * pitch, nz * layer));
            var loadCase = LoadCase.ClampAndPush(bounds, Axis.X, fixMax: false, new Vector3(0, 0, -1));
            foreach (var solver in solvers)
            {
                if (solver is PcgSolver && 3L * (nx + 1) * (ny + 1) * (nz + 1) > pcgMaxDofs)
                {
                    Console.WriteLine($"{scale,5} {"",9} {"",10} {"",7} {"",8} {"",11} {"",8} {"",8} {"",6} {"",10} {"",9}  {solver.Name}: skipped (> --pcg-max-dofs)");
                    continue;
                }
                GC.Collect();
                GC.WaitForPendingFinalizers();
                var fem = StaticAnalysis.Run(grid, material, loadCase, solver);
                var tip = Enumerable.Range(0, fem.Mesh.NodeCount)
                    .Where(n => MathF.Abs(fem.Mesh.NodePosition(n).X - bounds.Max.X) < 1e-3f)
                    .Average(n => -fem.Displacement(n).Z);
                var theory = Timoshenko(material, bounds.Size.X, bounds.Size.Y, bounds.Size.Z);
                Console.WriteLine($"{scale,5} {fem.Mesh.Cells.Length,9:N0} {fem.Dofs,10:N0} {fem.System.NonZeros / 1e6,7:0.0} {Text.Megabytes(fem.System.Bytes),8} " +
                                  $"{(fem.MeshTime + fem.AssemblyTime).TotalSeconds,11:0.00} {fem.Solve.Setup.TotalSeconds,8:0.00} {fem.Solve.Solve.TotalSeconds,8:0.00} " +
                                  $"{fem.Solve.Iterations,6} {tip / theory,10:0.000} {Text.Megabytes(PeakWorkingSet()),9}  {solver.Name}{(fem.Solve.Converged ? "" : " NOT CONVERGED")}");
            }
        }
        return 0;
    }

    public static void PrintMesh(FemMesh mesh) =>
        Console.WriteLine($"bead mesh    {mesh.Cells.Length:N0} active cells, {mesh.DroppedCells:N0} dropped ({mesh.DroppedVolume:0.0} mm3 not connected to the clamp), {3 * mesh.NodeCount:N0} DOF at bead resolution");

    public static void PrintAnalysis(FemResult fem)
    {
        var mesh = fem.Mesh;
        PrintMesh(mesh);
        Console.WriteLine($"system       {mesh.NodeCount:N0} nodes, {fem.Dofs:N0} DOF, {fem.System.NonZeros / 1e6:0.0} M non-zeros, {Text.Megabytes(fem.System.Bytes)}; " +
                          $"mesh {fem.MeshTime.TotalSeconds:0.00} s, assembly {fem.AssemblyTime.TotalSeconds:0.00} s");
        Console.WriteLine($"solver       {fem.Solve.Solver}: {fem.Solve.Iterations} iterations, residual {fem.Solve.Residual:0.0e+0}{(fem.Solve.Converged ? "" : " NOT CONVERGED")}{(fem.Solve.Retried ? " (solved again: the first try gave NaN)" : "")}, " +
                          $"setup {fem.Solve.Setup.TotalSeconds:0.00} s, solve {fem.Solve.Solve.TotalSeconds:0.00} s; stresses {fem.StressTime.TotalSeconds:0.00} s");

        var worst = Enumerable.Range(0, mesh.Cells.Length).MaxBy(e => fem.VonMises[e]);
        Console.WriteLine($"result       max |u| {fem.MaxDisplacement():0.0000} mm, max von Mises {fem.VonMises[worst]:0.00} MPa at {Text.Format(CellCentre(mesh, worst))} (fill {mesh.Fill[worst]:0.00}); " +
                          $"applied {Text.Format(fem.AppliedForce())} N");
        Console.WriteLine($"memory       peak working set {Text.Megabytes(PeakWorkingSet())}");
    }

    public static Vector3 CellCentre(FemMesh mesh, int cell)
    {
        var (c, grid) = (mesh.Cells[cell], mesh.Grid);
        return grid.NodePosition(c.I, c.J, c.K) + new Vector3(grid.Pitch / 2, grid.Pitch / 2, grid.CellHeight(c.K) / 2);
    }

    public static double Timoshenko(IsotropicMaterial m, double length, double width, double height)
    {
        var inertia = width * height * height * height / 12;
        var kappa = 10 * (1 + m.PoissonRatio) / (12 + 11 * m.PoissonRatio);
        return length * length * length / (3 * m.YoungsModulus * inertia) + length / (kappa * m.ShearModulus * width * height);
    }

    static (Axis Axis, bool Max) ParseFace(string text) => text.ToLowerInvariant() switch
    {
        "xmin" => (Axis.X, false), "xmax" => (Axis.X, true),
        "ymin" => (Axis.Y, false), "ymax" => (Axis.Y, true),
        "zmin" => (Axis.Z, false), "zmax" => (Axis.Z, true),
        _ => throw new FormatException($"--fix expects xmin, xmax, ymin, ymax, zmin or zmax but got '{text}'."),
    };

    public static long PeakWorkingSet()
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        return process.PeakWorkingSet64;
    }
}
