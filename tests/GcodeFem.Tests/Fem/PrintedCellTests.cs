using System.Numerics;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Slicing;

namespace GcodeFem.Tests.Fem;

/// <summary>What the cells keep of how they were printed: line direction, line type, time, nozzle and fan.</summary>
public class PrintedCellTests
{
    const float Width = 0.42f, Height = 0.2f;
    static readonly Placement Identity = new(Matrix4x4.Identity, Vector3.Zero);
    static readonly Box3 Area = new(Vector3.Zero, new Vector3(20 * Width, 20 * Width, 2 * Height));

    /// <param name="share">Of the plastic a bead of full width and height would hold.</param>
    public static ExtrusionSegment Bead(Vector2 from, Vector2 to, int layer = 0, float start = 0, float duration = 1, FeatureType feature = FeatureType.InnerWall,
        ushort nozzle = 220, byte fan = 255, float share = 1) =>
        new(new Vector3(from, (layer + 1) * Height), new Vector3(to, (layer + 1) * Height), Width, Height, share * Width * Height * Vector2.Distance(from, to),
            start, duration, layer, feature, nozzle, fan);

    /// <summary>Beads given layer by layer, in the order they are printed.</summary>
    public static Toolpath Print(params ExtrusionSegment[] beads)
    {
        var layers = new List<ToolpathLayer>();
        for (var k = 0; k <= beads.Max(b => b.Layer); k++)
        {
            var first = Array.FindIndex(beads, b => b.Layer == k);
            var count = beads.Count(b => b.Layer == k);
            layers.Add(new ToolpathLayer(k, (k + 1) * Height, Height, Math.Max(0, first), count, 0, 0));
        }
        return new Toolpath
        {
            Segments = beads,
            Layers = [.. layers],
            Settings = new Dictionary<string, string> { ["line_width"] = "0.42" },
            UnknownFeatures = new HashSet<string>(),
            FilamentDiameter = 1.75f,
            TotalTime = beads.Max(b => b.EndTime),
            ExtruderOffset = Vector3.Zero,
        };
    }

    static Vector2 Row(float i, int j) => new(i * Width, (j + 0.5f) * Width);

    [Fact]
    public void A_bead_tells_its_cells_which_way_it_runs_and_when()
    {
        // Across cells 2..9 of row 3, 100 s into the print, one cell per second.
        var grid = Voxelizer.Voxelize(Print(Bead(Row(2, 3), Row(10, 3), start: 100, duration: 8, fan: 128)), Identity, Area);

        Assert.Equal(new LineDirection(0, 0), grid.Direction(5, 3, 0));
        Assert.Equal(FeatureClass.InnerWall, grid.Feature(5, 3, 0));
        Assert.Equal(103.5, grid.DepositionTime(5, 3, 0), 4); // the middle of the cell is passed 3.5 s in
        Assert.Equal(220, grid.NozzleTemperature(5, 3, 0), 3);
        Assert.Equal(128 / 255f, grid.Fan(5, 3, 0), 5);
    }

    [Fact]
    public void A_diagonal_bead_runs_at_45_degrees_in_every_cell_it_touches()
    {
        var grid = Voxelizer.Voxelize(Print(Bead(new Vector2(1, 1), new Vector2(7, 7))), Identity, Area);

        var cells = grid.Occupied(0.05f).ToList();
        Assert.True(cells.Count > 20);
        Assert.All(cells, c => Assert.Equal(MathF.PI / 4, grid.Direction(c.I, c.J, c.K).Angle, 4));
        Assert.All(cells, c => Assert.Equal(0, grid.Direction(c.I, c.J, c.K).Spread, 4));
    }

    [Fact]
    public void Where_two_lines_cross_in_equal_parts_the_cell_runs_both_ways()
    {
        var across = Bead(Row(0, 3), Row(10, 3));
        var up = Bead(new Vector2(5.5f * Width, 0), new Vector2(5.5f * Width, 10 * Width), feature: FeatureType.SparseInfill);
        var grid = Voxelizer.Voxelize(Print(across, up), Identity, Area);

        Assert.Equal(0.5, grid.Direction(5, 3, 0).Spread, 4);
        Assert.Equal(new LineDirection(MathF.PI / 2, 0), grid.Direction(5, 6, 0));
    }

    [Fact]
    public void The_line_type_holding_most_of_the_plastic_names_the_cell()
    {
        var wall = Bead(Row(0, 3), Row(10, 3), feature: FeatureType.OuterWall);
        var infill = Bead(Row(4, 3), Row(6, 3), feature: FeatureType.SparseInfill, share: 0.4f);
        var bridge = Bead(Row(0, 5), Row(10, 5), feature: FeatureType.InternalBridge);
        var grid = Voxelizer.Voxelize(Print(wall, infill, bridge), Identity, Area);

        Assert.Equal(FeatureClass.OuterWall, grid.Feature(5, 3, 0));
        Assert.Equal(FeatureClass.Bridge, grid.Feature(5, 5, 0));
    }

    [Fact]
    public void Plastic_that_does_not_say_how_it_was_printed_is_the_same_every_way()
    {
        var grid = CellGrid.Solid(2, 2, 2, Width, Height);

        Assert.Equal(new LineDirection(0, 0.5f), grid.Direction(1, 1, 1));
        Assert.Null(grid.Feature(1, 1, 1));
        Assert.True(double.IsNaN(grid.DepositionTime(1, 1, 1)));
        Assert.True(float.IsNaN(grid.Fan(1, 1, 1)));
    }

    [Fact]
    public void The_printed_cubes_outer_walls_run_along_its_sides()
    {
        var toolpath = GcodeParser.Parse(TestPaths.Sample("cube20_X1C_PLA.gcode"));
        var report = SlicerReport.Read(TestPaths.Sample("cube20_X1C_PLA.result.json"));
        var model = Stl.Read(TestPaths.Sample("cube20.stl")).Bounds;
        var grid = Voxelizer.Voxelize(toolpath, Placement.FromSlice(Matrix4x4.Identity, model, report.ObjectBounds), model);

        // Layer 50, half way along the −X and the −Y side.
        Assert.Equal(FeatureClass.OuterWall, grid.Feature(0, 24, 50));
        Assert.Equal(MathF.PI / 2, grid.Direction(0, 24, 50).Angle, 2);
        Assert.Equal(FeatureClass.OuterWall, grid.Feature(24, 0, 50));
        Assert.Equal(0, MathF.Sin(grid.Direction(24, 0, 50).Angle), 2);
        Assert.InRange(grid.Direction(0, 24, 50).Spread, 0, 0.05f);
    }
}
