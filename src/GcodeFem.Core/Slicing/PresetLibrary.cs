using System.Text.Json;
using System.Text.Json.Nodes;

namespace GcodeFem.Core.Slicing;

public enum PresetKind { Machine, Process, Filament }

public sealed record PresetInfo(string Name, PresetKind Kind, string Path, bool IsUser);

/// <summary>Display data for one filament preset, taken from its resolved settings.</summary>
/// <remarks>No vendor: user presets usually keep their parent's filament_vendor, so "eSun ABS+" would claim to be Bambu Lab.</remarks>
public sealed record FilamentSummary(string Name, bool IsUser, string Type, string NozzleTemperature, string Density);

/// <summary>
/// Bambu's system presets (BBL vendor) plus the logged-in user's presets, with inheritance resolved.
/// </summary>
/// <remarks>
/// The Bambu Studio CLI does NOT follow "inherits" or "include" when loading preset files: it silently
/// falls back to built-in defaults for every inherited key (verified 2026-10-05: PLA Basic sliced at
/// 200 °C with 2 mm³/s instead of 220 °C and 21 mm³/s). Every preset handed to the slicer must
/// therefore be flattened with <see cref="Resolve"/> first.
/// </remarks>
public sealed class PresetLibrary
{
    static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    readonly Dictionary<string, PresetInfo> presets;
    readonly Dictionary<string, JsonObject> resolved = new(StringComparer.Ordinal);

    PresetLibrary(Dictionary<string, PresetInfo> presets) => this.presets = presets;

    public IEnumerable<PresetInfo> All => presets.Values;

    public bool Contains(string name) => presets.ContainsKey(name);

    public PresetInfo Get(string name) =>
        presets.TryGetValue(name, out var info) ? info : throw new KeyNotFoundException($"Unknown Bambu preset '{name}'.");

    /// <summary>Loads the BBL system index and, when given, the user's preset folder (user presets win on name clashes).</summary>
    public static PresetLibrary Load(BambuInstallation bambu, string? userPresetFolder)
    {
        var presets = new Dictionary<string, PresetInfo>(StringComparer.Ordinal);

        var vendorDirectory = Path.Combine(bambu.ProfilesDirectory, "BBL");
        using (var index = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(bambu.ProfilesDirectory, "BBL.json"))))
        {
            foreach (var (list, kind) in new[] { ("machine_list", PresetKind.Machine), ("process_list", PresetKind.Process), ("filament_list", PresetKind.Filament) })
                foreach (var entry in index.RootElement.GetProperty(list).EnumerateArray())
                {
                    var name = entry.GetProperty("name").GetString()!;
                    var path = Path.Combine(vendorDirectory, entry.GetProperty("sub_path").GetString()!);
                    presets[name] = new PresetInfo(name, kind, path, IsUser: false);
                }
        }

        if (userPresetFolder != null)
        {
            var userDirectory = Path.Combine(bambu.AppDataDirectory, "user", userPresetFolder);
            foreach (var (folder, kind) in new[] { ("machine", PresetKind.Machine), ("process", PresetKind.Process), ("filament", PresetKind.Filament) })
            {
                var directory = Path.Combine(userDirectory, folder);
                if (!Directory.Exists(directory)) continue;
                foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
                {
                    var name = ReadObject(path)["name"]?.GetValue<string>() ?? Path.GetFileNameWithoutExtension(path);
                    presets[name] = new PresetInfo(name, kind, path, IsUser: true);
                }
            }
        }

        return new PresetLibrary(presets);
    }

    /// <summary>
    /// Fully flattened settings: parent chain first, then "include" templates (e.g. the machine
    /// start G-code), then the preset's own keys. "inherits" and "include" are removed.
    /// </summary>
    public JsonObject Resolve(string name) => (JsonObject)ResolveCached(name, []).DeepClone();

    /// <summary>The flattened preset as JSON text, ready for --load-settings / --load-filaments.</summary>
    public string ResolveJson(string name) => ResolveCached(name, []).ToJsonString(Indented);

    JsonObject ResolveCached(string name, HashSet<string> visiting)
    {
        if (resolved.TryGetValue(name, out var done)) return done;
        if (!visiting.Add(name)) throw new InvalidDataException($"Preset inheritance cycle at '{name}'.");

        var own = ReadObject(Get(name).Path);
        var result = new JsonObject();
        if (Text(own, "inherits") is { Length: > 0 } parent)
            Merge(result, ResolveCached(parent, visiting));
        foreach (var include in Includes(own))
            Merge(result, ResolveCached(include, visiting));
        Merge(result, own);
        result.Remove("inherits");
        result.Remove("include");

        visiting.Remove(name);
        resolved[name] = result;
        return result;
    }

    /// <summary>Presets the user can pick for a machine: system instances and user presets whose resolved compatible_printers allow it.</summary>
    public IEnumerable<PresetInfo> CompatibleWith(string machine, PresetKind kind)
    {
        foreach (var info in presets.Values)
        {
            if (info.Kind != kind) continue;
            var own = ReadObject(info.Path);
            if (!info.IsUser && Text(own, "instantiation") != "true") continue;

            // System instances carry their own list; user presets inherit it.
            var printers = own["compatible_printers"] as JsonArray is { Count: > 0 } ownList
                ? ownList
                : ResolveCached(info.Name, [])["compatible_printers"] as JsonArray;
            if (printers is null || printers.Count == 0 || printers.Any(p => p?.GetValue<string>() == machine))
                yield return info;
        }
    }

    public FilamentSummary SummarizeFilament(string name)
    {
        var settings = ResolveCached(name, []);
        return new FilamentSummary(name, Get(name).IsUser,
            First(settings, "filament_type") ?? "?",
            First(settings, "nozzle_temperature") ?? "?",
            First(settings, "filament_density") ?? "?");
    }

    /// <summary>First element of an array setting, or the plain string value.</summary>
    public static string? First(JsonObject settings, string key) => settings[key] switch
    {
        JsonArray { Count: > 0 } array => array[0]?.GetValue<string>(),
        JsonValue value when value.TryGetValue<string>(out var text) => text,
        _ => null,
    };

    static IEnumerable<string> Includes(JsonObject own) => own["include"] switch
    {
        JsonArray array => array.Select(n => n!.GetValue<string>()),
        JsonValue value when value.TryGetValue<string>(out var single) && single.Length > 0 => [single],
        _ => [],
    };

    static string? Text(JsonObject json, string key) =>
        json[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    static void Merge(JsonObject target, JsonObject source)
    {
        foreach (var (key, value) in source)
            target[key] = value?.DeepClone();
    }

    static JsonObject ReadObject(string path) =>
        JsonNode.Parse(File.ReadAllBytes(path)) as JsonObject ?? throw new InvalidDataException($"'{path}' is not a JSON object.");
}
