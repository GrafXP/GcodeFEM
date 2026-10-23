using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GcodeFem.Core.Materials;

/// <summary>
/// A <see cref="Material"/> as a JSON file, "*.material.json". Every value is written with its unit
/// and its source, so the file can be read and edited by hand; a value with another unit than its
/// property's, or with neither a source nor the mark "assumed", is refused when the file is read.
/// </summary>
public static class MaterialFile
{
    public const string Extension = ".material.json";

    const int Format = 1;

    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // "°C" and "±" as they are: the file is for reading, not for a web page
    };

    sealed record FileDto(int Format, string Name, string? Vendor, string? Type, List<string>? Filaments, bool? StandsInForType,
        List<DocumentDto>? Documents, string? SpecimenNote, Dictionary<string, ValueDto>? Properties);

    sealed record DocumentDto(string Id, string? Title, string? File, string? Url, string? Sha256);

    sealed record ValueDto(double Value, double? Spread, string? Unit, string? Method, string? Source, int? Page, string? Assumed);

    public static void Save(Material material, string path)
    {
        if (material.Problems() is { Count: > 0 } problems)
            throw new InvalidOperationException($"The material cannot be saved: {string.Join(" ", problems)}");

        var file = new FileDto(
            Format,
            material.Name.Trim(),
            Text(material.Vendor),
            Text(material.Type),
            material.Filaments.Count > 0 ? material.Filaments : null,
            material.StandsInForType ? true : null,
            material.Documents.Count > 0 ? [.. material.Documents.Select(d => new DocumentDto(d.Id, Text(d.Title), d.File?.Replace('\\', '/'), d.Url, d.Sha256))] : null,
            Text(material.SpecimenNote),
            material.Values.ToDictionary(
                pair => pair.Key.Key(),
                pair => new ValueDto(pair.Value.Value, pair.Value.Spread, pair.Key.Unit(), Text(pair.Value.Method), Text(pair.Value.Source), pair.Value.Page, Text(pair.Value.Assumed))));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(file, Options) + Environment.NewLine);
        material.Path = Path.GetFullPath(path);
    }

    public static Material Load(string path)
    {
        FileDto file;
        try
        {
            file = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(path), Options) ?? throw new InvalidDataException($"'{path}' is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"'{path}' is not a material file: {ex.Message}", ex);
        }
        if (file.Format != Format) throw new InvalidDataException($"'{path}' is a material of format {file.Format}; this version reads format {Format}.");

        var material = new Material
        {
            Name = file.Name ?? "",
            Vendor = file.Vendor ?? "",
            Type = file.Type ?? "",
            StandsInForType = file.StandsInForType ?? false,
            SpecimenNote = file.SpecimenNote ?? "",
            Path = Path.GetFullPath(path),
        };
        material.Filaments.AddRange(file.Filaments ?? []);
        material.Documents.AddRange((file.Documents ?? []).Select(d => new MaterialDocument(d.Id, d.Title ?? "", d.File, d.Url, d.Sha256)));
        foreach (var (key, value) in file.Properties ?? [])
        {
            if (!MaterialProperties.TryParse(key, out var property))
                throw new InvalidDataException($"'{path}' has a property '{key}', which is not one a material has ({string.Join(", ", MaterialProperties.All.Select(p => p.Key()))}).");
            // A number typed in GPa under a property that is in MPa would pass for a thousandth of itself.
            if (value.Unit is not null && value.Unit != property.Unit())
                throw new InvalidDataException($"'{path}' gives {key} in '{value.Unit}'; it is in '{property.Unit()}'.");
            material.Values[property] = new MaterialValue(value.Value, value.Spread, value.Method, value.Source, value.Page, value.Assumed);
        }
        if (material.Problems() is { Count: > 0 } problems)
            throw new InvalidDataException($"'{path}' is not in order: {string.Join(" ", problems)}");
        return material;
    }

    static string? Text(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
