using System.Globalization;
using System.Numerics;
using GcodeFem.Core.Geometry;

namespace GcodeFem.Core.Gcode;

/// <summary>One straight piece of deposited bead, in bed coordinates (mm, s, °C); see <see cref="Toolpath.ExtruderOffset"/>.</summary>
/// <param name="Volume">Deposited plastic in mm³ (extruded filament length × filament cross-section).</param>
/// <param name="StartTime">Seconds since the start of the file, from feed rates (no acceleration).</param>
/// <param name="Fan">Part-cooling fan, 0–255.</param>
public readonly record struct ExtrusionSegment(
    Vector3 Start,
    Vector3 End,
    float Width,
    float Height,
    float Volume,
    float StartTime,
    float Duration,
    int Layer,
    FeatureType Feature,
    ushort NozzleTemperature,
    byte Fan)
{
    public float Length => Vector3.Distance(Start, End);
    public float EndTime => StartTime + Duration;
}

/// <param name="Z">Top of the layer (Bambu's Z_HEIGHT).</param>
/// <param name="FirstSegment">Index into <see cref="Toolpath.Segments"/>; a layer's segments are contiguous.</param>
public sealed record ToolpathLayer(int Index, float Z, float Height, int FirstSegment, int SegmentCount, double StartTime, double EndTime);

/// <summary>Everything deposited by a G-code file, plus the slicer settings embedded in it.</summary>
public sealed class Toolpath
{
    public required ExtrusionSegment[] Segments { get; init; }
    public required ToolpathLayer[] Layers { get; init; }
    public required IReadOnlyDictionary<string, string> Settings { get; init; }

    /// <summary>Feature names the parser didn't recognise; their lines are typed Unknown and excluded from the part.</summary>
    public required IReadOnlySet<string> UnknownFeatures { get; init; }

    public required float FilamentDiameter { get; init; }

    /// <summary>Already added to every segment: G-code coordinates + this = bed coordinates.</summary>
    public required Vector3 ExtruderOffset { get; init; }

    /// <summary>Feed-rate time of the whole file in seconds. Shorter than the slicer's estimate, which includes acceleration.</summary>
    public required double TotalTime { get; init; }

    public ReadOnlySpan<ExtrusionSegment> LayerSegments(ToolpathLayer layer) =>
        Segments.AsSpan(layer.FirstSegment, layer.SegmentCount);

    public string? Setting(string key) => Settings.GetValueOrDefault(key);

    /// <summary>A numeric setting; takes the first value of comma lists and ignores a trailing '%'.</summary>
    public float? SettingNumber(string key)
    {
        if (Setting(key) is not { } text) return null;
        var first = text.Split(',')[0].Trim().TrimEnd('%');
        return float.TryParse(first, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    /// <summary>Bounds of the part's bead centrelines (skirt, support etc. excluded); null if there are none.</summary>
    public Box3? PartBounds()
    {
        var points = new List<Vector3>();
        foreach (var s in Segments)
            if (s.Feature.IsPartMaterial())
            {
                points.Add(s.Start);
                points.Add(s.End);
            }
        return points.Count == 0 ? null : Box3.Of(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(points));
    }
}
