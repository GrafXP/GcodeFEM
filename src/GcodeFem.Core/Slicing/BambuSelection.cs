using System.Text.Json;

namespace GcodeFem.Core.Slicing;

/// <summary>The printer, process and filament currently selected in Bambu Studio.</summary>
/// <param name="UserPresetFolder">Folder under %APPDATA%\BambuStudio\user holding the logged-in account's presets.</param>
public sealed record BambuSelection(string Machine, string Process, string Filament, string? UserPresetFolder)
{
    /// <summary>
    /// Reads BambuStudio.conf. The file is one JSON object followed by an "# MD5 checksum" line,
    /// so only the first JSON value is parsed.
    /// </summary>
    public static BambuSelection Read(BambuInstallation bambu)
    {
        var path = Path.Combine(bambu.AppDataDirectory, "BambuStudio.conf");
        var reader = new Utf8JsonReader(File.ReadAllBytes(path), new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        var presets = root.GetProperty("presets");
        var filaments = presets.GetProperty("filaments");
        string? userFolder = null;
        if (root.TryGetProperty("app", out var app) && app.TryGetProperty("preset_folder", out var folder))
            userFolder = folder.GetString();

        return new BambuSelection(
            presets.GetProperty("machine").GetString() ?? throw new InvalidDataException("No machine selected in BambuStudio.conf."),
            presets.GetProperty("process").GetString() ?? throw new InvalidDataException("No process selected in BambuStudio.conf."),
            filaments.GetArrayLength() > 0 ? filaments[0].GetString()! : throw new InvalidDataException("No filament selected in BambuStudio.conf."),
            string.IsNullOrEmpty(userFolder) ? null : userFolder);
    }
}
