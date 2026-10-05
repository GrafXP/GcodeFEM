using System.Numerics;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Slicing;

namespace GcodeFem.Tests.Gcode;

/// <summary>
/// samples/cube20_X1C_PLA.*: the 20 mm cube sliced unrotated by Bambu Studio 2.08.02.61 with
/// flattened presets (X1 Carbon 0.4, 0.20mm Standard @BBL X1C, Bambu PLA Basic @BBL X1C).
/// </summary>
public class CubeFixtureTests
{
    static readonly Toolpath Toolpath = GcodeParser.Parse(TestPaths.Sample("cube20_X1C_PLA.gcode"));
    static readonly SlicerReport Report = SlicerReport.Read(TestPaths.Sample("cube20_X1C_PLA.result.json"));

    [Fact]
    public void Has_one_layer_per_0_2_mm()
    {
        Assert.Equal(100, Toolpath.Layers.Length);
        Assert.Equal(0.2f, Toolpath.Layers[0].Z, 4);
        Assert.Equal(20f, Toolpath.Layers[^1].Z, 4);
        Assert.All(Toolpath.Layers, l => Assert.Equal(0.2f, l.Height, 4));
        Assert.All(Toolpath.Layers, l => Assert.True(l.SegmentCount > 0));
    }

    [Fact]
    public void Was_sliced_with_the_full_presets_not_cli_defaults()
    {
        Assert.Equal("\"Bambu PLA Basic @BBL X1C\"", Toolpath.Setting("filament_settings_id"));
        Assert.Equal("0.42", Toolpath.Setting("line_width"));
        Assert.Equal("grid", Toolpath.Setting("sparse_infill_pattern"));
        Assert.Equal((220f, 220f), ToolpathStatistics.PartRange(Toolpath, s => s.NozzleTemperature));
    }

    [Fact]
    public void Body_extrusion_matches_the_slicers_filament_total()
    {
        // Header: 1316.16 mm of filament = 109.71 mm purge line in the start G-code + 1206.45 mm
        // for the part (split checked with an independent script).
        var area = MathF.PI * Toolpath.FilamentDiameter * Toolpath.FilamentDiameter / 4;
        var filament = Toolpath.Segments.Sum(s => (double)s.Volume) / area;
        Assert.Equal(1206.45, filament, 1);
        Assert.Equal(3.99, Report.FilamentGrams, 2);
    }

    [Fact]
    public void Beads_sit_half_a_line_width_inside_the_model()
    {
        var model = Stl.Read(TestPaths.Sample("cube20.stl")).Bounds;
        var placement = Placement.FromSlice(Matrix4x4.Identity, model, Report.ObjectBounds);
        var beads = Toolpath.PartBounds()!.Value;

        var low = placement.BedToPrint(beads.Min) - model.Min;
        var high = model.Max - placement.BedToPrint(beads.Max);
        foreach (var inset in new[] { low.X, low.Y, high.X, high.Y })
            Assert.Equal(0.21f, inset, 2);
        Assert.Equal(0f, high.Z, 3);
    }

    [Fact]
    public void Contains_the_expected_line_types_only()
    {
        var features = Toolpath.Segments.Select(s => s.Feature).ToHashSet();
        Assert.Superset(new HashSet<FeatureType>
        {
            FeatureType.OuterWall, FeatureType.InnerWall, FeatureType.SparseInfill,
            FeatureType.InternalSolidInfill, FeatureType.TopSurface, FeatureType.BottomSurface,
        }, features);
        Assert.Empty(Toolpath.UnknownFeatures);
        Assert.All(Toolpath.Segments, s => Assert.True(s.Feature.IsPartMaterial()));
    }
}
