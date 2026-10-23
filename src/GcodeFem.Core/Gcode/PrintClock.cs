namespace GcodeFem.Core.Gcode;

/// <summary>
/// When each segment of a toolpath was printed. The parser's times come from the feed rates alone,
/// but the printer also has to speed up and slow down, and on short lines that takes a lot longer:
/// on the cube, solid infill and the top and bottom surfaces take about twice their feed-rate time,
/// walls and sparse infill a tenth more, travel three times as long. Bambu Studio's result.json has
/// its own time per line type, acceleration included, and the calibrated clock stretches the
/// segments of each line type to match it, and the moves between them to match its travel time.
/// </summary>
public sealed class PrintClock
{
    /// <summary>A stretch outside this range is taken for a mismatch of names or of what was counted, and not applied.</summary>
    const double MaxFactor = 4;

    readonly Toolpath toolpath;
    readonly double[] start;
    readonly double[] factor; // by feature type

    PrintClock(Toolpath toolpath, double[] factor, double travelFactor)
    {
        this.toolpath = toolpath;
        this.factor = factor;
        TravelFactor = travelFactor;
        var segments = toolpath.Segments;
        start = new double[segments.Length];
        for (var s = 0; s < segments.Length; s++)
        {
            if (s == 0)
            {
                start[s] = segments[s].StartTime;
                continue;
            }
            var gap = Math.Max(0, segments[s].StartTime - segments[s - 1].EndTime);
            start[s] = start[s - 1] + Duration(s - 1) + gap * travelFactor;
        }
    }

    /// <summary>The parser's own clock: feed rates, no acceleration.</summary>
    public static PrintClock FromFeedRates(Toolpath toolpath) => new(toolpath, Enumerable.Repeat(1.0, Enum.GetValues<FeatureType>().Length).ToArray(), 1);

    /// <summary>
    /// Stretched to the slicer's times. <paramref name="slicerSeconds"/> is result.json's
    /// feature_type_times: seconds per line type by its G-code name, and "Travel".
    /// </summary>
    public static PrintClock Calibrated(Toolpath toolpath, IReadOnlyDictionary<string, double> slicerSeconds)
    {
        var count = Enum.GetValues<FeatureType>().Length;
        var feedRate = new double[count];
        double gaps = 0;
        var segments = toolpath.Segments;
        for (var s = 0; s < segments.Length; s++)
        {
            feedRate[(int)segments[s].Feature] += segments[s].Duration;
            if (s > 0) gaps += Math.Max(0, segments[s].StartTime - segments[s - 1].EndTime);
        }

        var factor = new double[count];
        for (var f = 0; f < count; f++)
        {
            var feature = (FeatureType)f;
            factor[f] = feature.IsPartMaterial() ? Stretch(slicerSeconds.GetValueOrDefault(feature.DisplayName()), feedRate[f]) : 1;
        }
        return new PrintClock(toolpath, factor, Stretch(slicerSeconds.GetValueOrDefault("Travel"), gaps));
    }

    /// <summary>How much longer than its feed-rate time a line type takes, from 1 (no change) to <see cref="MaxFactor"/>.</summary>
    public double Factor(FeatureType feature) => factor[(int)feature];

    /// <summary>The same for the moves between extrusions.</summary>
    public double TravelFactor { get; }

    /// <summary>Seconds since the start of the file at which the segment begins.</summary>
    public double Start(int segment) => start[segment];

    public double Duration(int segment) => toolpath.Segments[segment].Duration * factor[(int)toolpath.Segments[segment].Feature];

    static double Stretch(double slicer, double feedRate) =>
        slicer > 0 && feedRate > 0 && slicer / feedRate is var stretch && stretch is >= 1 and <= MaxFactor ? stretch : 1;
}
