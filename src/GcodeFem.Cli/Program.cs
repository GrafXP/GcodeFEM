using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Slicing;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
Console.OutputEncoding = System.Text.Encoding.UTF8;

var commandLine = CommandLine.Parse(args);
try
{
    return commandLine.Command switch
    {
        "info" when commandLine.Positional.Count == 1 => Info(commandLine.Positional[0]),
        "presets" => Presets(commandLine),
        "slice" when commandLine.Positional.Count == 1 => await Slice(commandLine),
        "parse" when commandLine.Positional.Count == 1 => Parse(commandLine.Positional[0]),
        _ => Usage(),
    };
}
catch (Exception ex) when (ex is IOException or InvalidDataException or SlicerException or KeyNotFoundException or FormatException)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return 1;
}

static int Usage()
{
    Console.Error.WriteLine("""
        usage:
          gcodefem info <model.stl>
          gcodefem presets [--machine <name>] [--all]
          gcodefem slice <model.stl> [--rot x,y,z] [--filament <name>] [--process <name>] [--machine <name>] [--no-cache]
          gcodefem parse <file.gcode>

        Machine, process and filament default to the current Bambu Studio selection.
        --rot rotates about X, then Y, then Z (degrees) before slicing.
        """);
    return 2;
}

static int Info(string path)
{
    var mesh = Stl.Read(path);
    var b = mesh.Bounds;
    Console.WriteLine(Path.GetFileName(path));
    Console.WriteLine($"  triangles  {mesh.TriangleCount}");
    Console.WriteLine($"  vertices   {mesh.Positions.Length}");
    Console.WriteLine($"  bounds     {Format(b.Min)} .. {Format(b.Max)} mm");
    Console.WriteLine($"  size       {b.Size.X:0.###} x {b.Size.Y:0.###} x {b.Size.Z:0.###} mm");
    return 0;
}

static int Presets(CommandLine commandLine)
{
    var bambu = BambuInstallation.Locate();
    var selection = BambuSelection.Read(bambu);
    var presets = PresetLibrary.Load(bambu, selection.UserPresetFolder);
    var machine = commandLine.Option("machine") ?? selection.Machine;

    Console.WriteLine($"Bambu Studio {bambu.Version}, selected:");
    Console.WriteLine($"  machine   {selection.Machine}");
    Console.WriteLine($"  process   {selection.Process}");
    Console.WriteLine($"  filament  {selection.Filament}");
    Console.WriteLine($"  account   {selection.UserPresetFolder ?? "(not logged in: no user presets)"}");

    var processes = presets.CompatibleWith(machine, PresetKind.Process).OrderBy(p => !p.IsUser).ThenBy(p => p.Name).ToList();
    Console.WriteLine();
    Console.WriteLine($"processes for {machine}: {processes.Count} ({processes.Count(p => p.IsUser)} yours)");
    foreach (var p in processes.Where(p => p.IsUser || commandLine.Flag("all")))
        Console.WriteLine($"  {(p.IsUser ? "user  " : "system")}  {p.Name}");

    var filaments = presets.CompatibleWith(machine, PresetKind.Filament)
        .Select(p => presets.SummarizeFilament(p.Name))
        .OrderBy(f => !f.IsUser).ThenBy(f => f.Type).ThenBy(f => f.Name)
        .ToList();
    Console.WriteLine();
    Console.WriteLine($"filaments for {machine}: {filaments.Count} ({filaments.Count(f => f.IsUser)} yours)");
    foreach (var f in filaments.Where(f => f.IsUser || commandLine.Flag("all")))
        Console.WriteLine($"  {(f.IsUser ? "user  " : "system")}  {f.Name,-44} {f.Type,-8} {f.NozzleTemperature,4} C  {f.Density,5} g/cm3");
    if (!commandLine.Flag("all")) Console.WriteLine("  (--all lists system presets too)");
    return 0;
}

static async Task<int> Slice(CommandLine commandLine)
{
    var bambu = BambuInstallation.Locate();
    var selection = BambuSelection.Read(bambu);
    var presets = PresetLibrary.Load(bambu, selection.UserPresetFolder);
    var machine = commandLine.Option("machine") ?? selection.Machine;
    var process = commandLine.Option("process") ?? selection.Process;
    var filament = commandLine.Option("filament") ?? selection.Filament;
    var (rx, ry, rz) = ParseRotation(commandLine.Option("rot"));
    var mesh = Stl.Read(commandLine.Positional[0]);

    var summary = presets.SummarizeFilament(filament);
    Console.WriteLine($"Bambu Studio {bambu.Version}");
    Console.WriteLine($"  machine    {machine}");
    Console.WriteLine($"  process    {process}");
    Console.WriteLine($"  filament   {filament} ({summary.Type}, {summary.NozzleTemperature} C)");
    Console.WriteLine($"  rotation   {rx:0.##}, {ry:0.##}, {rz:0.##} deg (X, then Y, then Z)");

    var slicer = new BambuSlicer(bambu, presets, commandLine.Option("cache") ?? BambuSlicer.DefaultCacheDirectory);
    var result = await slicer.SliceAsync(
        new SliceRequest(mesh, Orientation.FromEulerDegrees(rx, ry, rz), machine, process, filament),
        useCache: !commandLine.Flag("no-cache"));

    Console.WriteLine();
    Console.WriteLine($"slice        {result.Elapsed.TotalSeconds:0.0} s{(result.FromCache ? " (from cache)" : "")} -> {result.Directory}");
    Console.WriteLine($"  slicer     {FormatDuration(result.Report.PrintSeconds)}, {result.Report.FilamentGrams:0.00} g{(result.Report.Warning.Length > 0 ? $", warning: {result.Report.Warning}" : "")}");
    Console.WriteLine($"  placement  print frame + {Format(result.Placement.BedOffset)} = bed");

    var watch = Stopwatch.StartNew();
    var toolpath = GcodeParser.Parse(result.GcodePath);
    watch.Stop();
    Console.WriteLine();
    PrintToolpath(toolpath, watch.Elapsed, result.Report);
    PrintFit(toolpath, result);
    return 0;
}

static int Parse(string path)
{
    var watch = Stopwatch.StartNew();
    var toolpath = GcodeParser.Parse(path);
    watch.Stop();
    PrintToolpath(toolpath, watch.Elapsed, report: null);
    return 0;
}

static void PrintToolpath(Toolpath toolpath, TimeSpan parseTime, SlicerReport? report)
{
    var layers = toolpath.Layers;
    Console.WriteLine($"toolpath     {layers.Length} layers, z {layers.FirstOrDefault()?.Z:0.00} .. {layers.LastOrDefault()?.Z:0.00} mm, " +
                      $"{toolpath.Segments.Length} extrusion segments, parsed in {parseTime.TotalSeconds:0.00} s");
    Console.WriteLine($"  settings   {toolpath.Setting("filament_settings_id")}, line width {toolpath.Setting("line_width")}, " +
                      $"{toolpath.Setting("wall_loops")} walls, {toolpath.Setting("sparse_infill_density")} {toolpath.Setting("sparse_infill_pattern")}");

    if (ToolpathStatistics.PartRange(toolpath, s => s.NozzleTemperature) is (var tMin, var tMax))
        Console.WriteLine($"  nozzle     {tMin:0}-{tMax:0} C");
    if (ToolpathStatistics.PartRange(toolpath, s => s.Fan) is (var fMin, var fMax))
        Console.WriteLine($"  fan        {fMin / 2.55:0}-{fMax / 2.55:0} %");
    var layerTimes = layers.Where(l => l.SegmentCount > 0).Select(l => l.EndTime - l.StartTime).ToList();
    if (layerTimes.Count > 0)
        Console.WriteLine($"  layer time {layerTimes.Min():0.0}-{layerTimes.Max():0.0} s");
    Console.WriteLine($"  time       {FormatDuration(toolpath.TotalTime)} from feed rates" +
                      (report is null ? "" : $" (slicer with acceleration and start G-code: {FormatDuration(report.PrintSeconds)})"));
    if (toolpath.UnknownFeatures.Count > 0)
        Console.WriteLine($"  WARNING    unknown features, excluded from the part: {string.Join(", ", toolpath.UnknownFeatures)}");

    Console.WriteLine();
    Console.WriteLine($"  {"feature",-24}{"segments",9}{"length mm",12}{"volume mm3",12}{"time s",9}{(report is null ? "" : $"{"slicer s",10}")}  part");
    foreach (var f in ToolpathStatistics.ByFeature(toolpath))
    {
        var slicer = report?.FeatureSeconds.GetValueOrDefault(f.Feature.DisplayName()) is { } s ? $"{s,10:0}" : "";
        Console.WriteLine($"  {f.Feature.DisplayName(),-24}{f.Segments,9}{f.Length,12:0}{f.Volume,12:0.0}{f.Seconds,9:0}{slicer}  {(f.Feature.IsPartMaterial() ? "yes" : "no")}");
    }
}

/// <summary>Bead centrelines should sit about half a line width inside the model's outline on every side.</summary>
static void PrintFit(Toolpath toolpath, SliceResult result)
{
    if (toolpath.PartBounds() is not { } bed) return;
    var model = result.PrintMesh.Bounds;
    var low = result.Placement.BedToPrint(bed.Min) - model.Min;
    var high = model.Max - result.Placement.BedToPrint(bed.Max);
    var width = toolpath.SettingNumber("line_width") ?? 0.4f;
    var ok = new[] { low.X, low.Y, high.X, high.Y }.All(v => v > -0.05f && v < width);
    Console.WriteLine();
    Console.WriteLine($"  fit        bead centrelines inside the model by -X {low.X:0.000}  +X {high.X:0.000}  -Y {low.Y:0.000}  +Y {high.Y:0.000} mm " +
                      $"(expect ~{width / 2:0.000}, half a line width){(ok ? "" : "  <-- MISMATCH")}");
    Console.WriteLine($"             first layer top at {low.Z:0.000} mm, last layer top {high.Z:0.000} mm below the model top");
}

static (float X, float Y, float Z) ParseRotation(string? text)
{
    if (text is null) return (0, 0, 0);
    var parts = text.Split(',');
    if (parts.Length != 3 || !parts.All(p => float.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
        throw new FormatException($"--rot expects three angles like 0,90,0 but got '{text}'.");
    var v = parts.Select(p => float.Parse(p, CultureInfo.InvariantCulture)).ToArray();
    return (v[0], v[1], v[2]);
}

static string Format(Vector3 v) => $"({v.X:0.###}, {v.Y:0.###}, {v.Z:0.###})";

static string FormatDuration(double seconds)
{
    var t = TimeSpan.FromSeconds(seconds);
    return t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}m" : $"{t.Minutes}m {t.Seconds:00}s";
}

/// <summary>"command positional... --option value --flag".</summary>
sealed class CommandLine
{
    readonly Dictionary<string, string?> options = new(StringComparer.OrdinalIgnoreCase);

    public string? Command { get; private set; }
    public List<string> Positional { get; } = [];

    public string? Option(string name) => options.GetValueOrDefault(name);
    public bool Flag(string name) => options.ContainsKey(name);

    public static CommandLine Parse(string[] args)
    {
        var result = new CommandLine();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--", StringComparison.Ordinal))
            {
                var hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
                result.options[args[i][2..]] = hasValue ? args[++i] : null;
            }
            else if (result.Command is null) result.Command = args[i];
            else result.Positional.Add(args[i]);
        }
        return result;
    }
}
