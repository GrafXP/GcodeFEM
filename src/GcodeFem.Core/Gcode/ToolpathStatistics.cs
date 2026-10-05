namespace GcodeFem.Core.Gcode;

public sealed record FeatureStatistics(FeatureType Feature, int Segments, double Length, double Volume, double Seconds);

public static class ToolpathStatistics
{
    /// <summary>Per-feature totals, largest volume first.</summary>
    public static IReadOnlyList<FeatureStatistics> ByFeature(Toolpath toolpath) =>
        toolpath.Segments
            .GroupBy(s => s.Feature)
            .Select(g => new FeatureStatistics(g.Key, g.Count(), g.Sum(s => (double)s.Length), g.Sum(s => (double)s.Volume), g.Sum(s => (double)s.Duration)))
            .OrderByDescending(f => f.Volume)
            .ToList();

    /// <summary>Range of a per-segment value over the part's own lines.</summary>
    public static (float Min, float Max)? PartRange(Toolpath toolpath, Func<ExtrusionSegment, float> value)
    {
        float min = float.MaxValue, max = float.MinValue;
        foreach (var s in toolpath.Segments)
        {
            if (!s.Feature.IsPartMaterial()) continue;
            var v = value(s);
            min = Math.Min(min, v);
            max = Math.Max(max, v);
        }
        return min > max ? null : (min, max);
    }
}
