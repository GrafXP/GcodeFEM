using GcodeFem.Core.Gcode;
using GcodeFem.Core.Slicing;

namespace GcodeFem.Tests.Gcode;

public class PrintClockTests
{
    static readonly Toolpath Cube = GcodeParser.Parse(TestPaths.Sample("cube20_X1C_PLA.gcode"));
    static readonly SlicerReport Report = SlicerReport.Read(TestPaths.Sample("cube20_X1C_PLA.result.json"));

    [Fact]
    public void By_feed_rates_it_is_the_parsers_clock()
    {
        var clock = PrintClock.FromFeedRates(Cube);

        for (var s = 0; s < Cube.Segments.Length; s += 97)
        {
            Assert.Equal(Cube.Segments[s].StartTime, clock.Start(s), 1);
            Assert.Equal(Cube.Segments[s].Duration, clock.Duration(s), 6);
        }
    }

    [Fact]
    public void Calibrated_each_line_type_takes_as_long_as_the_slicer_says()
    {
        var clock = PrintClock.Calibrated(Cube, Report.FeatureSeconds);

        foreach (var feature in new[] { FeatureType.OuterWall, FeatureType.InnerWall, FeatureType.SparseInfill, FeatureType.InternalSolidInfill, FeatureType.TopSurface, FeatureType.BottomSurface })
        {
            var total = Enumerable.Range(0, Cube.Segments.Length).Where(s => Cube.Segments[s].Feature == feature).Sum(clock.Duration);
            Assert.Equal(Report.FeatureSeconds[feature.DisplayName()], total, 1);
        }
        // Measured on this cube: solid infill and the surfaces about twice their feed-rate time, walls a little more.
        Assert.InRange(clock.Factor(FeatureType.InternalSolidInfill), 1.7, 2.0);
        Assert.InRange(clock.Factor(FeatureType.InnerWall), 1.0, 1.1);
        Assert.InRange(clock.TravelFactor, 2.5, 3.5);
    }

    [Fact]
    public void Calibrated_time_runs_forward_and_ends_later()
    {
        var clock = PrintClock.Calibrated(Cube, Report.FeatureSeconds);

        for (var s = 1; s < Cube.Segments.Length; s++) Assert.True(clock.Start(s) >= clock.Start(s - 1) + clock.Duration(s - 1) - 1e-9);
        var last = Cube.Segments.Length - 1;
        Assert.True(clock.Start(last) > Cube.Segments[last].StartTime);
    }

    [Fact]
    public void A_line_type_the_slicer_has_no_time_for_keeps_its_feed_rate_time()
    {
        var clock = PrintClock.Calibrated(Cube, Report.FeatureSeconds);

        Assert.Equal(1, clock.Factor(FeatureType.FloatingVerticalShell)); // result.json counts it as 0 s
        Assert.Equal(1, clock.Factor(FeatureType.Custom));
        Assert.All(Enum.GetValues<FeatureType>(), f => Assert.Equal(1, PrintClock.Calibrated(Cube, new Dictionary<string, double>()).Factor(f)));
    }
}
