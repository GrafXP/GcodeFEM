using System.Diagnostics;
using System.Numerics;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Slicing;
using GcodeFem.Core.Study;
using GcodeFem.Core.Visual;

namespace GcodeFem.Cli;

/// <summary>Results saved beside a study: solved and saved as the app does it, and looked into cell by cell.</summary>
static class ResultCommands
{
    public static async Task<int> Run(CommandLine commandLine) => commandLine.Positional[0] switch
    {
        "solve" when commandLine.Positional.Count == 2 => await Solve(commandLine),
        "show" when commandLine.Positional.Count == 2 => Show(commandLine),
        var action => throw new ArgumentException($"Unknown results command '{action}' (use solve <study> or show <results or study>)."),
    };

    /// <summary>Slices and solves one load case of a study as the app does, and saves the result beside the study.</summary>
    static async Task<int> Solve(CommandLine commandLine)
    {
        var path = commandLine.Positional[1];
        var study = StudyFile.Load(path);
        var model = StudyCommands.Model(study);
        var loadCase = StudyCommands.Case(study, commandLine.Option("case"));
        if (commandLine.Option("level") is { } level) study.MeshLevel = level == "auto" ? null : int.Parse(level);

        var bambu = BambuInstallation.Locate();
        var selection = BambuSelection.Read(bambu);
        var presets = PresetLibrary.Load(bambu, selection.UserPresetFolder);
        // The study names what it was sliced with, so that the result says it too.
        study.Process ??= selection.Process;
        study.Filament ??= selection.Filament;
        var slicer = new BambuSlicer(bambu, presets, commandLine.Option("cache") ?? BambuSlicer.DefaultCacheDirectory);
        var watch = Stopwatch.StartNew();
        var part = await PrintedPart.SliceAsync(slicer, new SliceRequest(model, Orientation.FromEulerDegrees(study.Rotation.X, study.Rotation.Y, study.Rotation.Z),
            selection.Machine, study.Process, study.Filament));
        Console.WriteLine($"sliced       {study.Process}, {study.Filament}, rotation {Text.Format(study.Rotation)} in {watch.Elapsed.TotalSeconds:0.0} s{(part.Slice.FromCache ? " (from the cache)" : "")}");

        var library = MaterialCommands.Library(commandLine);
        var options = new AdaptiveOptions(RootLevel: study.MeshLevel, MinFill: PrintedPart.MinFill, KeepPassFields: true);
        watch.Restart();
        var solution = PartSolution.Solve(part, study, loadCase, library.Match(part.Toolpath), LinearSolvers.Best(), options,
            pass => Console.WriteLine($"  pass {pass.Index + 1,-3} {pass.Dofs,9:N0} DOF, safety factor {pass.SafetyFactor:0.000}, max von Mises {pass.MaxVonMises:0.00} MPa, {pass.Time.TotalSeconds:0.0} s"));
        Console.WriteLine($"solved       '{loadCase.Name}' in {watch.Elapsed.TotalSeconds:0.0} s: safety factor {solution.SafetyFactor:0.000}, max displacement {solution.MaxDisplacement:0.###} mm");

        var file = commandLine.Option("out") ?? ResultFile.BesideStudy(path);
        watch.Restart();
        ResultFile.Save(file, model, part, solution);
        Console.WriteLine($"saved        {file} ({new FileInfo(file).Length / 1024.0:0} kB, {watch.Elapsed.TotalSeconds:0.0} s)");
        return 0;
    }

    /// <summary>A saved result: what was solved, and the cells asked for with their stresses and how near each mode is to breaking them.</summary>
    static int Show(CommandLine commandLine)
    {
        var path = commandLine.Positional[1];
        if (!path.EndsWith(ResultFile.Extension, StringComparison.OrdinalIgnoreCase))
        {
            var beside = ResultFile.BesideStudy(path);
            if (!File.Exists(beside)) throw new FileNotFoundException($"There is no result saved beside '{path}' ({Path.GetFileName(beside)}).");
            if (File.Exists(path) && ResultFile.Mismatch(ResultFile.ReadHeader(beside), StudyFile.Load(path)) is { } why)
                Console.WriteLine($"NOTE         this result is not of the study as it is now: {why}");
            path = beside;
        }
        var watch = Stopwatch.StartNew();
        var saved = ResultFile.Load(path);
        var (header, solution) = (saved.Header, saved.Solution);
        var result = solution.Result;
        var mesh = result.Mesh;
        var study = header.Study;
        Console.WriteLine($"{Path.GetFileName(path)}: saved {header.SavedAt:yyyy-MM-dd HH:mm} by {header.Program}, read and rebuilt in {watch.Elapsed.TotalSeconds:0.0} s");
        Console.WriteLine($"  print      rotation {Text.Format(study.Rotation)} deg; {study.Process}; {study.Filament}; {saved.Part.Toolpath.Layers.Length} layers, " +
                          $"{saved.Part.Slice.Report.FilamentGrams:0.00} g{(saved.Part.Slice.Report.Warning is { Length: > 0 } warning ? $"; the slicer warned: {warning}" : "")}");
        Console.WriteLine($"  material   {Text.Ascii(solution.Material.Material.Name)}: {Strengths(solution)}");
        Console.WriteLine($"  load case  '{header.LoadCase}'");
        foreach (var reach in solution.Reach)
            Console.WriteLine($"    {reach.Name,-16} {(reach.IsLoad ? "loads" : "holds"),-6} {reach.Faces:N0} cell faces, {reach.Area:0.0} mm2{(reach.IsLoad ? $", {Text.Format(reach.Force)} N in the print's directions" : "")}");
        Console.WriteLine($"  solve      {mesh.Cells.Length:N0} cells, {result.Passes.Count} passes, {result.Final.Dofs:N0} DOF in the last; stopped because {result.StopReason}");
        Console.WriteLine($"  result     max von Mises {result.Final.MaxVonMises:0.00} MPa, max displacement {solution.MaxDisplacement:0.###} mm");
        if (result.Failure is not { } failure || solution.Strength is not { } strength)
        {
            Console.WriteLine($"  no safety factor: {solution.NoStrength}");
            return 0;
        }
        Console.WriteLine($"  failure    safety factor {failure.SafetyFactor:0.000} (saved {header.SafetyFactor:0.000}), weakest layer line {failure.LayerLineSafetyFactor:0.000}, Tsai-Wu {failure.TsaiWu.Min():0.000}");
        if (header.Summary is { Length: > 0 } summary)
        {
            Console.WriteLine("  as the app summed it up:");
            foreach (var line in summary.Split('\n')) Console.WriteLine($"    {Text.Ascii(line)}");
        }

        // Which cells: in a box, of a line type; ranked by the governing mode or by one of them.
        var cells = Enumerable.Range(0, mesh.Cells.Length);
        if (commandLine.Option("box") is { } box)
        {
            var corners = box.Split(';');
            if (corners.Length != 2) throw new FormatException($"--box expects two corners like \"0,0,0;10,10,10\" but got '{box}'.");
            var (a, b) = (Text.Triple(corners[0], "box"), Text.Triple(corners[1], "box"));
            var (low, high) = (Vector3.Min(a, b), Vector3.Max(a, b));
            cells = cells.Where(e => ResultMarks.Centre(mesh.Grid, mesh.Cells[e]) is var p && p == Vector3.Clamp(p, low, high));
        }
        if (commandLine.Option("feature") is { } feature)
            cells = cells.Where(e => mesh.Cells[e] is var c && string.Equals(mesh.Grid.Feature(c.I, c.J, c.K)?.DisplayName(), feature, StringComparison.OrdinalIgnoreCase));
        var by = commandLine.Option("by") ?? "governing";
        Func<int, double> rank = by switch
        {
            "governing" => e => failure.Index[e],
            "layers" => Mode(strength, result.Stress, FailureMode.LayerSeparation),
            "beads" => Mode(strength, result.Stress, FailureMode.BeadSeparation),
            "lines" => Mode(strength, result.Stress, FailureMode.LineBreak),
            "crushed" => Mode(strength, result.Stress, FailureMode.Crushing),
            "tsaiwu" => e => 1 / failure.TsaiWu[e],
            "vonmises" => e => result.VonMises[e],
            _ => throw new FormatException($"--by expects governing, layers, beads, lines, crushed, tsaiwu or vonmises but got '{by}'."),
        };

        var picked = new List<int>();
        if (commandLine.Option("cell") is { } cell) picked.Add(int.Parse(cell));
        if (commandLine.Option("at") is { } at) picked.Add(Nearest(mesh, Text.Triple(at, "at")));
        if (commandLine.Option("along") is { } along)
        {
            var ends = along.Split(';');
            if (ends.Length != 2) throw new FormatException($"--along expects two points like \"0,0,0;10,0,0\" but got '{along}'.");
            var (from, to) = (Text.Triple(ends[0], "along"), Text.Triple(ends[1], "along"));
            var steps = (int)commandLine.Number("steps", 20);
            // The nearest cell to each point; a point that lies away from the part finds none.
            foreach (var e in Enumerable.Range(0, steps + 1).Select(s => Vector3.Lerp(from, to, s / (float)steps)).Select(p => (Point: p, Cell: Nearest(mesh, p)))
                         .Where(n => Vector3.Distance(n.Point, ResultMarks.Centre(mesh.Grid, mesh.Cells[n.Cell])) <= mesh.Grid.Pitch).Select(n => n.Cell).Distinct())
                picked.Add(e);
        }
        if (picked.Count == 0 || commandLine.Option("top") is not null)
            picked.AddRange(cells.OrderByDescending(rank).Take((int)commandLine.Number("top", 10)));

        Console.WriteLine();
        Console.WriteLine($"cells        stresses in MPa in the frame of each cell's lines (1 along, 2 across in the layer, 3 across the layers), " +
                          "in its plastic (over its fill, at least half the cell); each mode's safety factor");
        Console.WriteLine($"  {"cell",7} {"layer",5} {"at (print frame, mm)",-26} {"line type",-16} {"line",5} {"fill",5} {"bond",5} {"SF",6} {"mode",-8} " +
                          $"{"s11",8} {"s22",8} {"s33",8} {"t12",8} {"t23",8} {"t31",8} {"vM",7} {"SF layers",9} {"beads",6} {"lines",6} {"crush",6} {"TsaiWu",6}");
        Span<double> local = stackalloc double[6];
        Span<double> modes = stackalloc double[4];
        foreach (var e in picked)
        {
            var c = mesh.Cells[e];
            var stress = result.Stress.AsSpan(6 * e, 6);
            strength.LocalStress(e, stress, local);
            var tsaiWu = strength.Evaluate(e, stress, modes);
            var direction = mesh.Grid.Direction(c.I, c.J, c.K);
            var line = direction.Spread >= CellColours.CrossingSpread ? "cross" : $"{direction.Angle * 180 / MathF.PI:0}";
            var type = Text.Ascii(mesh.Grid.Feature(c.I, c.J, c.K)?.DisplayName().ToLowerInvariant() ?? "plastic");
            Console.WriteLine($"  {e,7} {c.K + 1,5} {Text.Format(ResultMarks.Centre(mesh.Grid, c)),-26} {(type.Length > 16 ? type[..16] : type),-16} {line,5} {mesh.Fill[e],5:0.00} {strength.Bond(e),5:0.00} " +
                              $"{failure.SafetyFactorOf(e),6:0.000} {Short(failure.Mode[e]),-8} {local[0],8:0.00} {local[1],8:0.00} {local[2],8:0.00} {local[3],8:0.00} {local[4],8:0.00} {local[5],8:0.00} " +
                              $"{result.VonMises[e],7:0.0} {Factor(modes[0]),9} {Factor(modes[1]),6} {Factor(modes[2]),6} {Factor(modes[3]),6} {tsaiWu,6:0.000}");
        }
        return 0;
    }

    static string Strengths(PartSolution solution)
    {
        var s = solution.Strength?.Strength;
        var stiffness = solution.Material.Material.Stiffness();
        return $"E {stiffness.AlongLine:0} MPa in the layers, {stiffness.AcrossLayers:0} across, nu {stiffness.PoissonRatio:0.##}" +
               (s is null ? "" : $"; strength {s.AlongLine:0.#} MPa along the lines, {s.AcrossLine:0.#} across, {s.AcrossLayers:0.#} across the layers x the bond, {s.Compression:0.#} in compression");
    }

    static Func<int, double> Mode(CellStrength strength, float[] stress, FailureMode mode) => e =>
    {
        Span<double> modes = stackalloc double[4];
        strength.Evaluate(e, stress.AsSpan(6 * e, 6), modes);
        return modes[(int)mode];
    };

    static int Nearest(FemMesh mesh, Vector3 point) =>
        Enumerable.Range(0, mesh.Cells.Length).MinBy(e => Vector3.DistanceSquared(point, ResultMarks.Centre(mesh.Grid, mesh.Cells[e])));

    static string Factor(double index) => index > 0 ? $"{1 / index:0.000}" : "-";

    static string Short(FailureMode mode) => mode switch
    {
        FailureMode.LayerSeparation => "layers",
        FailureMode.BeadSeparation => "beads",
        FailureMode.LineBreak => "lines",
        _ => "crushed",
    };
}
