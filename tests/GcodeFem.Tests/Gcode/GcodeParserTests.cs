using System.Numerics;
using GcodeFem.Core.Gcode;

namespace GcodeFem.Tests.Gcode;

public class GcodeParserTests
{
    const string Config = """
        ; CONFIG_BLOCK_START
        ; filament_diameter = 1.75
        ; line_width = 0.42
        ; layer_height = 0.2
        ; empty_value =
        ; CONFIG_BLOCK_END
        """;

    static readonly float FilamentArea = MathF.PI * 1.75f * 1.75f / 4;

    static Toolpath Parse(string body) => GcodeParser.Parse(new StringReader(Config + "\n" + body));

    [Fact]
    public void Records_an_extrusion_with_its_bead_and_machine_state()
    {
        var toolpath = Parse("""
            M83
            G1 X0 Y0 F600
            G1 X10 Y0 E1 ; purge line before the first layer is not part of the print
            ; CHANGE_LAYER
            ; Z_HEIGHT: 0.2
            ; LAYER_HEIGHT: 0.2
            G1 Z0.2
            ; FEATURE: Outer wall
            ; LINE_WIDTH: 0.45
            M104 S230
            M106 S127.5
            G1 X20 Y0 E0.5 F600
            G1 E-0.8
            G1 X30 Y10
            G1 E0.8
            """);

        var s = Assert.Single(toolpath.Segments);
        Assert.Equal(new Vector3(10, 0, 0.2f), s.Start);
        Assert.Equal(new Vector3(20, 0, 0.2f), s.End);
        Assert.Equal(0.45f, s.Width);
        Assert.Equal(0.2f, s.Height);
        Assert.Equal(0.5f * FilamentArea, s.Volume, 5);
        Assert.Equal(1f, s.Duration, 5); // 10 mm at 600 mm/min
        Assert.Equal(0, s.Layer);
        Assert.Equal(FeatureType.OuterWall, s.Feature);
        Assert.Equal(230, s.NozzleTemperature);
        Assert.Equal(127, s.Fan);

        var layer = Assert.Single(toolpath.Layers);
        Assert.Equal(0.2f, layer.Z);
        Assert.Equal(0.2f, layer.Height);
        Assert.Equal("", toolpath.Settings["empty_value"]);
    }

    [Fact]
    public void Absolute_extrusion_uses_differences_and_respects_G92()
    {
        var toolpath = Parse("""
            M82
            ; CHANGE_LAYER
            G92 E0
            G1 X10 E1 F1200
            G1 X20 E1.5
            G92 E0
            G1 X30 E0.25
            """);

        Assert.Equal([1f, 0.5f, 0.25f], toolpath.Segments.Select(s => MathF.Round(s.Volume / FilamentArea, 5)));
    }

    [Fact]
    public void Lines_without_width_markers_fall_back_to_the_config()
    {
        var s = Assert.Single(Parse("""
            M83
            ; CHANGE_LAYER
            G1 X5 E0.2 F600
            """).Segments);

        Assert.Equal(0.42f, s.Width);
        Assert.Equal(0.2f, s.Height);
    }

    [Fact]
    public void Ignores_extrusion_in_the_end_gcode()
    {
        var toolpath = Parse("""
            M83
            ; CHANGE_LAYER
            G1 X10 E1 F600
            ; MACHINE_END_GCODE_START
            G1 X20 E1
            """);

        Assert.Single(toolpath.Segments);
    }

    [Fact]
    public void Splits_extruding_arcs_into_short_chords()
    {
        // Quarter circle, counter-clockwise around the origin, radius 10.
        var toolpath = Parse("""
            M83
            ; CHANGE_LAYER
            G1 X10 Y0 F600
            G3 X0 Y10 I-10 J0 E2
            """);

        var segments = toolpath.Segments;
        Assert.True(segments.Length > 10);
        Assert.Equal(MathF.PI * 10 / 2, segments.Sum(s => s.Length), 2);
        Assert.Equal(2 * FilamentArea, segments.Sum(s => s.Volume), 4);
        Assert.Equal(new Vector3(0, 10, 0), segments[^1].End);
        Assert.All(segments, s => Assert.InRange(s.End.Length(), 9.99f, 10.01f));
    }

    [Fact]
    public void Clockwise_arcs_sweep_the_other_way()
    {
        var toolpath = Parse("""
            M83
            ; CHANGE_LAYER
            G1 X10 Y0 F600
            G2 X0 Y-10 I-10 J0 E1
            """);

        Assert.All(toolpath.Segments, s => Assert.True(s.End.Y <= 1e-4f));
    }

    [Fact]
    public void Spiral_lift_moves_up_without_drifting_sideways()
    {
        // Bambu's z-hop: a full travel circle that climbs.
        var toolpath = Parse("""
            M83
            ; CHANGE_LAYER
            G1 X5 Y5 Z0.2 F600
            G3 Z0.6 I1 J0 P1 F42000
            G1 X6 Y5 E0.1 F600
            """);

        var s = Assert.Single(toolpath.Segments);
        Assert.Equal(5, s.Start.X, 4);
        Assert.Equal(5, s.Start.Y, 4);
        Assert.Equal(0.6f, s.Start.Z, 4);
    }

    [Fact]
    public void Only_the_part_cooling_fan_counts()
    {
        var toolpath = Parse("""
            M83
            ; CHANGE_LAYER
            M106 P1 S51
            M106 P2 S255
            M106 P3 S200
            G1 X1 E0.1 F600
            M107
            G1 X2 E0.1
            """);

        Assert.Equal([51, 0], toolpath.Segments.Select(s => (int)s.Fan));
    }

    [Fact]
    public void Unknown_features_are_reported_and_kept_out_of_the_part()
    {
        var toolpath = Parse("""
            M83
            ; CHANGE_LAYER
            ; FEATURE: Fuzzy skin
            G1 X1 E0.1 F600
            """);

        Assert.Contains("Fuzzy skin", toolpath.UnknownFeatures);
        Assert.False(Assert.Single(toolpath.Segments).Feature.IsPartMaterial());
    }

    [Fact]
    public void Extruder_offset_moves_segments_into_bed_coordinates()
    {
        var toolpath = GcodeParser.Parse(new StringReader("""
            ; CONFIG_BLOCK_START
            ; extruder_offset = 0x2
            ; CONFIG_BLOCK_END
            M83
            ; CHANGE_LAYER
            G1 X10 Y10 F600
            G1 X20 Y10 E0.1
            """));

        Assert.Equal(new Vector3(0, 2, 0), toolpath.ExtruderOffset);
        Assert.Equal(new Vector3(10, 12, 0), Assert.Single(toolpath.Segments).Start);
    }

    [Fact]
    public void Layers_record_their_segment_range_and_times()
    {
        var toolpath = Parse("""
            M83
            ; CHANGE_LAYER
            ; Z_HEIGHT: 0.2
            ; LAYER_HEIGHT: 0.2
            G1 X10 E0.5 F600
            G1 X20 E0.5
            ; CHANGE_LAYER
            ; Z_HEIGHT: 0.4
            ; LAYER_HEIGHT: 0.2
            G1 X10 E0.5
            """);

        Assert.Equal(2, toolpath.Layers.Length);
        Assert.Equal((0, 2), (toolpath.Layers[0].FirstSegment, toolpath.Layers[0].SegmentCount));
        Assert.Equal((2, 1), (toolpath.Layers[1].FirstSegment, toolpath.Layers[1].SegmentCount));
        Assert.Equal(2.0, toolpath.Layers[0].EndTime - toolpath.Layers[0].StartTime, 5);
        Assert.Equal(0.4f, toolpath.Layers[1].Z);
        Assert.Equal(1, toolpath.LayerSegments(toolpath.Layers[1]).Length);
    }
}
