using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using GcodeFem.Core.Geometry;

namespace GcodeFem.Core.Slicing;

/// <param name="Rotation">Part frame → print frame. Applied by us; the slicer never auto-orients.</param>
public sealed record SliceRequest(TriangleMesh Mesh, Matrix4x4 Rotation, string Machine, string Process, string Filament);

/// <param name="PrintMesh">The model rotated into the print frame, exactly as handed to the slicer.</param>
public sealed record SliceResult(
    string Directory,
    string GcodePath,
    SlicerReport Report,
    Placement Placement,
    TriangleMesh PrintMesh,
    bool FromCache,
    TimeSpan Elapsed);

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

        var directory = Path.Combine(cacheDirectory, CacheKey(stl, machine, process, filament));
        var fromCache = useCache && File.Exists(Path.Combine(directory, CompleteMarker));
        if (!fromCache)
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(Path.Combine(directory, ModelFile), stl, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "machine.json"), machine, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "process.json"), process, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "filament.json"), filament, cancellationToken);
            await RunAsync(directory, cancellationToken);
        }

        var report = SlicerReport.Read(Path.Combine(directory, ResultFile));
        if (report.ReturnCode != 0)
            throw new SlicerException($"Bambu Studio failed (return code {report.ReturnCode}): {report.Error}");
        var gcode = Path.Combine(directory, GcodeFile);
        if (!File.Exists(gcode))
            throw new SlicerException($"Bambu Studio reported success but wrote no {GcodeFile} in '{directory}'.");

        var placement = Placement.FromSlice(request.Rotation, printMesh.Bounds, report.ObjectBounds);
        if (!fromCache) await File.WriteAllTextAsync(Path.Combine(directory, CompleteMarker), DateTime.UtcNow.ToString("O"), cancellationToken);

        return new SliceResult(directory, gcode, report, placement, printMesh, fromCache, watch.Elapsed);
    }

    async Task RunAsync(string directory, CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;
        var info = new ProcessStartInfo(bambu.ExecutablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = directory,
        };
        foreach (var argument in new[]
                 {
                     "--debug", "2",
                     "--arrange", "1",
                     "--load-settings", $"{Path.Combine(directory, "machine.json")};{Path.Combine(directory, "process.json")}",
                     "--load-filaments", Path.Combine(directory, "filament.json"),
                     "--slice", "0",
                     "--outputdir", directory,
                     "--export-3mf", "plate.gcode.3mf",
                     Path.Combine(directory, ModelFile),
                 })
            info.ArgumentList.Add(argument);

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
