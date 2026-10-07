using System.Numerics;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Slicing;
using GcodeFem.Core.Visual;

namespace GcodeFem.Tests.Visual;

public class ToolpathGeometryTests
{
    const float Width = 0.42f, Height = 0.2f;
    static readonly Placement OnBed = new(Matrix4x4.Identity, new Vector3(100, 100, 0));

    static ExtrusionSegment Bead(Vector3 from, Vector3 to, int layer, FeatureType feature, ushort temperature = 220) =>
        new(from, to, Width, Height, Width * Height * Vector3.Distance(from, to), 0, 1, layer, feature, temperature, 0);

    static Toolpath Print(params ExtrusionSegment[] segments) => new()
    {
        Segments = segments,
        Layers = [.. segments.GroupBy(s => s.Layer).Select(g => new ToolpathLayer(g.Key, (g.Key + 1) * Height, Height, 0, g.Count(), 0, 0))],
        Settings = new Dictionary<string, string> { ["line_width"] = "0.42" },
        UnknownFeatures = new HashSet<string>(),
        FilamentDiameter = 1.75f,
        TotalTime = 0,
        ExtruderOffset = Vector3.Zero,
    };

    /// <summary>Two layers: a wall and an infill line in the first, a wall and a skirt line in the second.</summary>
    static Toolpath TwoLayers() => Print(
        Bead(new(100, 100, 0.2f), new(110, 100, 0.2f), 0, FeatureType.OuterWall),
        Bead(new(100, 101, 0.2f), new(110, 101, 0.2f), 0, FeatureType.SparseInfill, 230),
        Bead(new(100, 100, 0.4f), new(110, 100, 0.4f), 1, FeatureType.OuterWall, 240),
        Bead(new(90, 90, 0.4f), new(95, 90, 0.4f), 1, FeatureType.Skirt, 250));

    [Fact]
    public void A_bead_becomes_a_pointed_tube_of_its_width_and_height_below_the_nozzle()
    {
        var toolpath = Print(Bead(new(100, 100, 0.2f), new(110, 100, 0.2f), 0, FeatureType.OuterWall));

        var geometry = ToolpathGeometry.Build(toolpath, OnBed);

        Assert.True(geometry.Tubes);
        Assert.Equal(10, geometry.Positions.Length);
        // The plastic reaches half a line width beyond both ends of the nozzle's path.
        var bounds = Box3.Of(geometry.Positions);
        Assert.Equal(new Vector3(-Width / 2, -Width / 2, 0), bounds.Min);
        Assert.Equal(new Vector3(10 + Width / 2, Width / 2, Height), bounds.Max);
        Assert.All(geometry.Normals!, n => Assert.Equal(1, n.Length(), 5));
        Assert.Equal(48, geometry.Indices(toolpath, 0, 1, _ => true).Length);
    }

    [Fact]
    public void Tube_faces_point_outward()
    {
        var toolpath = Print(Bead(new(100, 100, 0.2f), new(103, 104, 0.2f), 0, FeatureType.OuterWall));
        var geometry = ToolpathGeometry.Build(toolpath, OnBed);
        var indices = geometry.Indices(toolpath, 0, 1, _ => true);
        var axis = new Vector3(1.5f, 2, Height / 2);

        for (var t = 0; t < indices.Length; t += 3)
        {
            Vector3 a = geometry.Positions[indices[t]], b = geometry.Positions[indices[t + 1]], c = geometry.Positions[indices[t + 2]];
            Assert.True(Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3 - axis) > 0);
        }
    }

    [Fact]
    public void The_layer_range_and_the_feature_filter_choose_what_is_drawn()
    {
        var toolpath = TwoLayers();
        var geometry = ToolpathGeometry.Build(toolpath, OnBed);

        Assert.Equal(4 * 48, geometry.Indices(toolpath, 0, 2, _ => true).Length);
        Assert.Equal(2 * 48, geometry.Indices(toolpath, 1, 2, _ => true).Length);
        Assert.Equal(3 * 48, geometry.Indices(toolpath, 0, 2, f => f.IsPartMaterial()).Length);

        var second = geometry.Indices(toolpath, 1, 2, f => f == FeatureType.OuterWall);
        Assert.Equal(48, second.Length);
        Assert.All(second, index => Assert.InRange(index, 20, 29)); // the third bead's vertices
    }

    [Fact]
    public void A_big_toolpath_can_be_drawn_as_lines()
    {
        var toolpath = TwoLayers();

        var geometry = ToolpathGeometry.Build(toolpath, OnBed, tubes: false);

        Assert.False(geometry.Tubes);
        Assert.Null(geometry.Normals);
        Assert.Equal(8, geometry.Positions.Length);
        Assert.Equal(new Vector3(0, 0, Height / 2), geometry.Positions[0]); // mid-height of the bead
        Assert.Equal([4, 5], geometry.Indices(toolpath, 1, 2, f => f.IsPartMaterial()));
    }

    [Fact]
    public void Feature_colours_come_with_a_legend_of_the_line_types_present()
    {
        var toolpath = TwoLayers();
        var geometry = ToolpathGeometry.Build(toolpath, OnBed);

        var (colours, legend) = ToolpathColours.Colour(toolpath, ToolpathColouring.Feature);
        var vertices = geometry.VertexColours(colours);

        Assert.Equal(["Outer wall", "Sparse infill", "Skirt"], legend.Entries.Select(e => e.Label));
        Assert.Null(legend.Scale);
        Assert.Equal(Palette.Categorical[0], colours[0]);
        Assert.Equal(Palette.Categorical[2], colours[1]);
        Assert.Equal(colours[0], colours[2]);
        Assert.Equal(Palette.Neutral, colours[3]);
        for (var v = 0; v < vertices.Length; v++) Assert.Equal(colours[v / geometry.VerticesPerBead], vertices[v]);
    }

    [Fact]
    public void A_scale_spans_the_parts_own_lines()
    {
        var (colours, legend) = ToolpathColours.Colour(TwoLayers(), ToolpathColouring.NozzleTemperature);

        // The skirt's 250 °C is outside the part's range and takes the end colour.
        Assert.Equal((220, 240), (legend.Scale!.Min, legend.Scale.Max));
        Assert.Equal("°C", legend.Unit);
        Assert.Equal(legend.Scale.Ramp[0], colours[0]);
        Assert.Equal(legend.Scale.Ramp[^1], colours[2]);
        Assert.Equal(colours[2], colours[3]);
    }

    [Fact]
    public void The_printed_cube_uses_every_identity_colour_at_most_once()
    {
        var toolpath = GcodeParser.Parse(TestPaths.Sample("cube20_X1C_PLA.gcode"));

        var (colours, legend) = ToolpathColours.Colour(toolpath, ToolpathColouring.Feature);

        Assert.Equal(toolpath.Segments.Length, colours.Length);
        Assert.Contains(legend.Entries, e => e.Label == "Outer wall");
        Assert.Contains(legend.Entries, e => e.Label == "Sparse infill");
        Assert.Equal(legend.Entries.Count, legend.Entries.Select(e => e.Colour).Distinct().Count());
        Assert.True(legend.Entries.Select(e => e.Key).SequenceEqual(legend.Entries.Select(e => e.Key).Order()));
    }
}

public class PaletteTests
{
    [Fact]
    public void The_sequential_ramp_gets_lighter_all_the_way()
    {
        var ramp = ColourScale.Sequential(0, 1).Ramp;

        for (var n = 1; n < ramp.Count; n++)
            Assert.True(Palette.Lightness(ramp[n]) >= Palette.Lightness(ramp[n - 1]) - 1e-4f, $"step {n} is darker than the one before");
        Assert.True(Palette.Lightness(ramp[^1]) - Palette.Lightness(ramp[0]) > 0.4f);
        // The low end must not sink into the background.
        Assert.True(Palette.Lightness(ramp[0]) - Palette.Lightness(Palette.Surface) > 0.15f);
    }

    [Fact]
    public void The_diverging_ramp_is_grey_at_zero_and_blue_and_red_at_its_ends()
    {
        var scale = ColourScale.Diverging(5);

        var zero = scale.Colour(0);
        Assert.InRange(zero.X - zero.Z, -0.02f, 0.02f);
        Assert.True(scale.Colour(-5).Z > scale.Colour(-5).X + 0.3f);
        Assert.True(scale.Colour(5).X > scale.Colour(5).Z + 0.3f);
        Assert.True(Palette.Lightness(scale.Colour(-2.5f)) > Palette.Lightness(zero));
        Assert.True(Palette.Lightness(scale.Colour(5)) > Palette.Lightness(scale.Colour(2.5f)));
        // A part that is near zero all over must not sink into the background.
        Assert.True(Palette.Lightness(zero) - Palette.Lightness(Palette.Surface) > 0.15f);
    }

    [Fact]
    public void Values_outside_the_scale_take_the_end_colours()
    {
        var scale = ColourScale.Sequential(10, 20);

        Assert.Equal(scale.Ramp[0], scale.Colour(-100));
        Assert.Equal(scale.Ramp[0], scale.Colour(float.NaN));
        Assert.Equal(scale.Ramp[^1], scale.Colour(1e9f));
        Assert.Equal(ColourScale.Sequential(3, 3).Colour(3), ColourScale.Sequential(3, 3).Colour(7));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(6)]
    public void Ordered_steps_stay_distinguishable(int count)
    {
        var steps = Palette.Ordinal(count);

        Assert.Equal(count, steps.Length);
        Assert.Equal(ColourScale.Sequential(0, 1).Ramp[^1], steps[0]);
        for (var n = 1; n < count; n++)
            Assert.True(Palette.Lightness(steps[n - 1]) - Palette.Lightness(steps[n]) >= 0.06f, $"steps {n - 1} and {n} are too close");
    }
}
