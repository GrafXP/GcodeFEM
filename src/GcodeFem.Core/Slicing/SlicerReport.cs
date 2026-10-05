using System.Numerics;
using System.Text.Json;
using GcodeFem.Core.Geometry;

namespace GcodeFem.Core.Slicing;

/// <summary>The parts of Bambu Studio's result.json that the loop uses.</summary>
/// <param name="ObjectBounds">Where the slicer placed the part, in bed coordinates.</param>
/// <param name="PrintSeconds">The slicer's own print-time estimate (includes acceleration).</param>
public sealed record SlicerReport(
    int ReturnCode,
    string Error,
    string Warning,
    Box3 ObjectBounds,
    double PrintSeconds,
    double FilamentGrams,
    IReadOnlyDictionary<string, double> FeatureSeconds)
{
    public static SlicerReport Read(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        var returnCode = root.GetProperty("return_code").GetInt32();
        var error = root.TryGetProperty("error_string", out var e) ? e.GetString() ?? "" : "";

        if (!root.TryGetProperty("sliced_plates", out var plates) || plates.GetArrayLength() == 0)
            return new SlicerReport(returnCode, error, "", default, 0, 0, new Dictionary<string, double>());

        var plate = plates[0];
        var objects = plate.GetProperty("objects");
        if (objects.GetArrayLength() != 1)
            throw new SlicerException($"Expected exactly one object on the plate, found {objects.GetArrayLength()}.");

        var box = objects[0].GetProperty("bbox");
        var min = new Vector3(Single(box, "x"), Single(box, "y"), Single(box, "z"));
        var size = new Vector3(Single(box, "width"), Single(box, "depth"), Single(box, "height"));

        var grams = plate.TryGetProperty("filaments", out var filaments)
            ? filaments.EnumerateArray().Sum(f => f.GetProperty("total_used_g").GetDouble())
            : 0;

        var featureSeconds = new Dictionary<string, double>(StringComparer.Ordinal);
        if (plate.TryGetProperty("feature_type_times", out var times))
            foreach (var feature in times.EnumerateObject())
                featureSeconds[feature.Name] = feature.Value.GetDouble();

        return new SlicerReport(
            returnCode,
            error,
            plate.TryGetProperty("warning_message", out var w) ? w.GetString() ?? "" : "",
            new Box3(min, min + size),
            plate.TryGetProperty("total_predication", out var t) ? t.GetDouble() : 0,
            grams,
            featureSeconds);
    }

    static float Single(JsonElement element, string name) => (float)element.GetProperty(name).GetDouble();
}

public sealed class SlicerException(string message) : Exception(message);
