using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using GcodeFem.Core.Geometry;

namespace GcodeFem.Core.Slicing;

/// <param name="Rotation">Part frame → print frame. Applied by us; the slicer never auto-orients.</param>
public sealed record SliceRequest(TriangleMesh Mesh, Matrix4x4 Rotation, string Machine, string Process, string Filament);

/// <param name="PrintMesh">The model rotated into the print frame, exactly as handed to the slicer.</param>
/// <param name="Plate">The build plate that was named to the slicer; null if it was left to assume its Cool Plate.</param>
public sealed record SliceResult(
    string Directory,
    string GcodePath,
    SlicerReport Report,
    Placement Placement,
    TriangleMesh PrintMesh,
    bool FromCache,
    TimeSpan Elapsed,
    string? Plate = null);

/// <summary>
/// Runs the Bambu Studio CLI on one rotated part. Results are cached by a hash of everything that
/// influences the G-code: Bambu version, rotated mesh and the flattened presets.
/// </summary>
public sealed class BambuSlicer(BambuInstallation bambu, PresetLibrary presets, string cacheDirectory)
{
    const string ModelFile = "model.stl";
    const string GcodeFile = "plate_1.gcode";
    const string ResultFile = "result.json";
    const string CompleteMarker = "complete";
    const string PlateFile = "plate.gcode.3mf";

    public static string DefaultCacheDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GcodeFem", "cache", "slices");

    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(10);

    public async Task<SliceResult> SliceAsync(SliceRequest request, bool useCache = true, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        var printMesh = request.Mesh.Transformed(request.Rotation);
        var stl = Stl.ToBinary(printMesh);
        var machine = presets.ResolveJson(request.Machine);
        var process = presets.ResolveJson(request.Process);
        var filament = presets.ResolveJson(request.Filament);
        var plate = PlateFor(presets.Resolve(request.Filament));

        // A plate that was not named leaves the key as it was before plates were named, so those slices stay in the cache.
        var directory = Path.Combine(cacheDirectory, plate is null ? CacheKey(stl, machine, process, filament) : CacheKey(stl, machine, process, filament, plate));
        var fromCache = useCache && File.Exists(Path.Combine(directory, CompleteMarker));
        if (!fromCache)
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(Path.Combine(directory, ModelFile), stl, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "machine.json"), machine, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "process.json"), process, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "filament.json"), filament, cancellationToken);
            await RunAsync(directory, plate, cancellationToken);
        }

        var report = SlicerReport.Read(Path.Combine(directory, ResultFile));
        if (report.ReturnCode != 0)
            throw new SlicerException($"Bambu Studio failed (return code {report.ReturnCode}): {report.Error}");
        var gcode = Path.Combine(directory, GcodeFile);
        if (!File.Exists(gcode))
            throw new SlicerException($"Bambu Studio reported success but wrote no {GcodeFile} in '{directory}'.");

        // The plate's own record of the part: a half turn keeps the size of its box, which the placement checks.
        if (TurnOnPlate(Path.Combine(directory, PlateFile)) is { } turn && turn > 0.01f)
        {
            // Sliced before the arrange was told not to turn parts.
            if (fromCache) return await SliceAsync(request, useCache: false, cancellationToken);
            throw new SlicerException($"Bambu Studio turned the part on the plate by {turn:0.#} deg, which would break every mapping back to the model.");
        }

        var placement = Placement.FromSlice(request.Rotation, printMesh.Bounds, report.ObjectBounds);
        if (!fromCache) await File.WriteAllTextAsync(Path.Combine(directory, CompleteMarker), DateTime.UtcNow.ToString("O"), cancellationToken);

        return new SliceResult(directory, gcode, report, placement, printMesh, fromCache, watch.Elapsed, plate);
    }

    /// <summary>
    /// The build plate to name to the slicer for a filament, given its flattened preset. The CLI
    /// assumes the Cool Plate and refuses a filament that does not print on it (PETG, ABS, PC and
    /// most others: plate temperature 0). Such a filament gets the first plate it does print on,
    /// the textured one first, as it comes with the printers. A filament that prints on the Cool
    /// Plate gets none named (null), as before. The plate chosen in Bambu Studio itself is not read.
    /// </summary>
    public static string? PlateFor(JsonObject filament)
    {
        static bool PrintsOn(JsonObject settings, string key) =>
            PresetLibrary.First(settings, key) is { } text && double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var temperature) && temperature > 0;

        if (PrintsOn(filament, "cool_plate_temp")) return null;
        foreach (var (key, plate) in new[]
                 {
                     ("textured_plate_temp", "Textured PEI Plate"), ("hot_plate_temp", "High Temp Plate"),
                     ("eng_plate_temp", "Engineering Plate"), ("supertack_plate_temp", "Supertack Plate"),
                 })
            if (PrintsOn(filament, key)) return plate;
        return null;
    }

    /// <summary>
    /// How far Bambu Studio turned the part on the plate (degrees, about whatever axis), from the
    /// placement of the part in the 3mf it exports beside the G-code; null if there is no such file.
    /// result.json's box cannot tell: after a turn it gives the box of the part's turned box.
    /// </summary>
    public static float? TurnOnPlate(string threeMf)
    {
        if (!File.Exists(threeMf)) return null;
        using var archive = ZipFile.OpenRead(threeMf);
        if (archive.GetEntry("3D/3dmodel.model") is not { } entry) return null;
        using var reader = new StreamReader(entry.Open());
        // The build item: a 3 × 3 turn, row by row, then where it goes.
        if (Regex.Match(reader.ReadToEnd(), @"<item\b[^>]*\btransform=""([^""]+)""") is not { Success: true } item) return 0;
        var m = item.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        if (m.Length != 12) throw new SlicerException($"Bambu Studio's 3mf places the part by '{item.Groups[1].Value}', which is not a 3D transform.");
        var trace = m[0] + m[4] + m[8];
        return (float)(Math.Acos(Math.Clamp((trace - 1) / 2, -1, 1)) * 180 / Math.PI);
    }

    async Task RunAsync(string directory, string? plate, CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;
        var info = new ProcessStartInfo(bambu.ExecutablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = directory,
        };
        string[] arguments =
        [
            "--debug", "2",
            // Placed on the plate, but not turned: the arrange turns a part to the smallest box around its footprint unless told not to.
            "--arrange", "1", "--allow-rotations=0",
            .. plate is null ? [] : new[] { "--curr-bed-type", plate },
            "--load-settings", $"{Path.Combine(directory, "machine.json")};{Path.Combine(directory, "process.json")}",
            "--load-filaments", Path.Combine(directory, "filament.json"),
            "--slice", "0",
            "--outputdir", directory,
            "--export-3mf", PlateFile,
            Path.Combine(directory, ModelFile),
        ];
        foreach (var argument in arguments) info.ArgumentList.Add(argument);

        using var process = Process.Start(info) ?? throw new SlicerException($"Could not start '{bambu.ExecutablePath}'.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            cancellationToken.ThrowIfCancellationRequested();
            throw new SlicerException($"Bambu Studio did not finish within {Timeout.TotalMinutes:0.#} min.{LogTail(start)}");
        }

        // The GUI-subsystem exe prints nothing; result.json and the log file are the only diagnostics.
        if (process.ExitCode != 0 || !File.Exists(Path.Combine(directory, ResultFile)))
        {
            var reason = File.Exists(Path.Combine(directory, ResultFile))
                ? SlicerReport.Read(Path.Combine(directory, ResultFile)).Error
                : "no result.json written";
            throw new SlicerException($"Bambu Studio exited with code {process.ExitCode}: {reason}.{LogTail(start)}");
        }
    }

    string CacheKey(byte[] stl, params string[] presetJson)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(bambu.Version));
        hash.AppendData(stl);
        foreach (var json in presetJson) hash.AppendData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash.GetHashAndReset())[..20].ToLowerInvariant();
    }

    /// <summary>Last lines of the newest Bambu log written since <paramref name="since"/>, for error messages.</summary>
    string LogTail(DateTime since)
    {
        try
        {
            var log = new DirectoryInfo(bambu.LogDirectory).EnumerateFiles("debug_*.log*")
                .Where(f => f.LastWriteTimeUtc >= since.AddSeconds(-2) && !f.Name.EndsWith(".enc", StringComparison.Ordinal))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();
            if (log is null) return "";

            using var stream = new FileStream(log.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var lines = new StreamReader(stream).ReadToEnd().Split('\n');
            return $"\nLast lines of {log.Name}:\n" + string.Join('\n', lines.TakeLast(30));
        }
        catch (IOException)
        {
            return "";
        }
    }
}
