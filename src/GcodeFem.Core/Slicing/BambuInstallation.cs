using System.Diagnostics;

namespace GcodeFem.Core.Slicing;

/// <summary>An installed Bambu Studio plus its per-user data folder.</summary>
public sealed class BambuInstallation
{
    public const string DefaultDirectory = @"C:\Program Files\Bambu Studio";

    BambuInstallation(string directory, string appDataDirectory)
    {
        Directory = directory;
        AppDataDirectory = appDataDirectory;
        Version = FileVersionInfo.GetVersionInfo(ExecutablePath).FileVersion ?? "unknown";
    }

    public string Directory { get; }
    public string ExecutablePath => Path.Combine(Directory, "bambu-studio.exe");
    public string ProfilesDirectory => Path.Combine(Directory, "resources", "profiles");

    /// <summary>%APPDATA%\BambuStudio: BambuStudio.conf, user presets, logs.</summary>
    public string AppDataDirectory { get; }

    public string LogDirectory => Path.Combine(AppDataDirectory, "log");

    /// <summary>File version of the executable, e.g. "02.08.02.61". Part of every slice cache key.</summary>
    public string Version { get; }

    /// <summary>
    /// Finds Bambu Studio in <paramref name="directory"/>, else in GCODEFEM_BAMBU_DIR, else in the
    /// default install folder. Returns null when the executable is missing.
    /// </summary>
    public static BambuInstallation? TryLocate(string? directory = null)
    {
        directory ??= Environment.GetEnvironmentVariable("GCODEFEM_BAMBU_DIR") ?? DefaultDirectory;
        if (!File.Exists(Path.Combine(directory, "bambu-studio.exe"))) return null;

        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BambuStudio");
        return new BambuInstallation(directory, appData);
    }

    public static BambuInstallation Locate(string? directory = null) =>
        TryLocate(directory) ?? throw new FileNotFoundException(
            $"Bambu Studio not found in '{directory ?? DefaultDirectory}'. Set GCODEFEM_BAMBU_DIR to its install folder.");
}
