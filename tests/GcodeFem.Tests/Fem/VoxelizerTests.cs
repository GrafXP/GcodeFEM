using System.Numerics;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Slicing;

namespace GcodeFem.Tests.Fem;

public class VoxelizerTests
{
    const float Width = 0.42f, Height = 0.2f;

    static Toolpath Beads(params (Vector2 From, Vector2 To)[] beads) => new()
    {
        Segments = beads.Select(b => new ExtrusionSegment(
            new Vector3(b.From, Height), new Vector3(b.To, Height), Width, Height,
            Width * Height * Vector2.Distance(b.From, b.To), 0, 0, 0, FeatureType.InnerWall, 220, 0)).ToArray(),
        Layers = [new ToolpathLayer(0, Height, Height, 0, beads.Length, 0, 0)],
        Settings = new Dictionary<string, string> { ["line_width"] = "0.42" },
        UnknownFeatures = new HashSet<string>(),
        FilamentDiameter = 1.75f,
        TotalTime = 0,
        ExtruderOffset = Vector3.Zero,
    };

    static readonly Placement Identity = new(Matrix4x4.Identity, Vector3.Zero);
    static readonly Box3 Area = new(Vector3.Zero, new Vector3(20 * Width, 20 * Width, Height));

    [Fact]
    public void A_bead_along_a_cell_row_fills_exactly_that_row()
    {
        // Centred on row j = 3 (y = 3.5 pitches), running across cells 2..9.
        var y = 3.5f * Width;
        var grid = Voxelizer.Voxelize(Beads((new Vector2(2 * Width, y), new Vector2(10 * Width, y))), Identity, Area);

        for (var i = 0; i < 20; i++)
        for (var j = 0; j < 20; j++)
            Assert.Equal(j == 3 && i is >= 2 and < 10 ? 1f : 0f, grid.Fill(i, j, 0), 4);
    }

    [Fact]
    public void A_diagonal_bead_stays_face_connected_and_keeps_its_volume()
    {
        var toolpath = Beads((new Vector2(1, 1), new Vector2(7, 7)));
        var grid = Voxelizer.Voxelize(toolpath, Identity, Area);

        Assert.Equal(toolpath.Segments[0].Volume, grid.TotalVolume, 4);
        Assert.Equal(0, grid.LostVolume);
        var mesh = FemMesh.Build(grid, 0.05f, [new Fixture(p => p.X < 1.5f && p.Y < 1.5f)]);
        Assert.Equal(0, mesh.DroppedCells);
        Assert.True(mesh.Cells.Length > 20);
    }

    [Fact]
    public void Occupied_bounds_follow_the_cells_not_the_model()
    {
        // A bead across cells 2..9 of row 3: the model area is 20 cells wide, the material is not.
        var y = 3.5f * Width;
        var grid = Voxelizer.Voxelize(Beads((new Vector2(2 * Width, y), new Vector2(10 * Width, y))), Identity, Area);

        var bounds = grid.OccupiedBounds(0.05f);

        Assert.Equal(new Vector3(2 * Width, 3 * Width, 0), bounds.Min);
        Assert.Equal(new Vector3(10 * Width, 4 * Width, Height), bounds.Max);
    }

    [Fact]
    public void The_printed_cube_becomes_one_connected_body()
    {
        var toolpath = GcodeParser.Parse(TestPaths.Sample("cube20_X1C_PLA.gcode"));
        var report = SlicerReport.Read(TestPaths.Sample("cube20_X1C_PLA.result.json"));
        var model = Stl.Read(TestPaths.Sample("cube20.stl")).Bounds;
        var placement = Placement.FromSlice(Matrix4x4.Identity, model, report.ObjectBounds);

        var grid = Voxelizer.Voxelize(toolpath, placement, model);
        var mesh = FemMesh.Build(grid, 0.05f, [new Fixture(p => p.Z < 1e-3f)]);

        Assert.Equal((48, 48, 100), (grid.SizeX, grid.SizeY, grid.SizeZ)); // 20 mm / 0.42 → 48 columns
        Assert.Equal(toolpath.Segments.Sum(s => (double)s.Volume), grid.TotalVolume, 0);
        Assert.Equal(0, grid.LostVolume, 3);
        Assert.Equal(0, mesh.DroppedCells);
    }

    [Fact]
    public void The_printed_cube_carries_a_load()
    {
        var toolpath = GcodeParser.Parse(TestPaths.Sample("cube20_X1C_PLA.gcode"));
        var report = SlicerReport.Read(TestPaths.Sample("cube20_X1C_PLA.result.json"));
        var model = Stl.Read(TestPaths.Sample("cube20.stl")).Bounds;
        var grid = Voxelizer.Voxelize(toolpath, Placement.FromSlice(Matrix4x4.Identity, model, report.ObjectBounds), model);
        var loadCase = LoadCase.ClampAndPush(model, Axis.Z, fixMax: false, new Vector3(0, 0, -100));

        var result = StaticAnalysis.Run(grid, IsotropicMaterial.Pla, loadCase, LinearSolvers.Best());

        Assert.True(result.Solve.Converged, $"{result.Solve.Solver}: residual {result.Solve.Residual} after {result.Solve.Iterations}");
        var top = Enumerable.Range(0, result.Mesh.NodeCount).Where(n => result.Mesh.NodePosition(n).Z > 19.99f).ToList();
        Assert.All(top, n => Assert.True(result.Displacement(n).Z < 0));
        // A solid PLA cube would shorten by F L / (E A) = 100·20 / (2500·400) = 0.002 mm; walls + 15 % infill are softer.
        Assert.InRange(top.Average(n => -result.Displacement(n).Z), 0.002, 0.02);
    }

    /// <summary>The cells a line through these points (in pitches) runs through, on a grid of 20 × 20 cells.</summary>
    static List<(int I, int J, float From, float To)> Walk(Vector2 from, Vector2 to)
    {
        var grid = new CellGrid(Vector2.Zero, Width, [0, Height], 20, 20);
        var pieces = new List<(int I, int J, float From, float To)>();
        Voxelizer.Walk(grid, new Vector3(from * Width, Height / 2), new Vector3(to * Width, Height / 2), pieces);
        return pieces;
    }

    [Fact]
    public void The_walk_cuts_a_line_where_it_crosses_into_the_next_cell()
    {
        // Along row 3 from the middle of cell 2 to the middle of cell 6: boundaries at 3, 4, 5 and 6 pitches.
        var forth = Walk(new Vector2(2.5f, 3.5f), new Vector2(6.5f, 3.5f));
        var back = Walk(new Vector2(6.5f, 3.5f), new Vector2(2.5f, 3.5f));

        Assert.Equal([2, 3, 4, 5, 6], forth.Select(p => p.I));
        Assert.Equal([6, 5, 4, 3, 2], back.Select(p => p.I));
        Assert.All(forth.Concat(back), p => Assert.Equal(3, p.J));
        float[] cuts = [0, 0.125f, 0.375f, 0.625f, 0.875f, 1];
        for (var n = 0; n < 5; n++)
        {
            Assert.Equal(cuts[n], forth[n].From, 5);
            Assert.Equal(cuts[n + 1], forth[n].To, 5);
            Assert.Equal(cuts[n], back[n].From, 5);
            Assert.Equal(cuts[n + 1], back[n].To, 5);
        }
    }

    [Theory]
    [InlineData(1.3f, 0.7f, 17.9f, 5.2f)]
    [InlineData(17.9f, 5.2f, 1.3f, 0.7f)]
    [InlineData(4.2f, 19.1f, 6.6f, 0.4f)]
    [InlineData(3.5f, 3.5f, 3.9f, 3.2f)]
    public void The_walk_goes_from_cell_to_neighbouring_cell_without_gaps(float x0, float y0, float x1, float y1)
    {
        var (from, to) = (new Vector2(x0, y0), new Vector2(x1, y1));

        var pieces = Walk(from, to);

        Assert.Equal((0f, 1f), (pieces[0].From, pieces[^1].To));
        Assert.Equal(Math.Abs((int)x1 - (int)x0) + Math.Abs((int)y1 - (int)y0) + 1, pieces.Count);
        for (var n = 0; n < pieces.Count; n++)
        {
            var middle = Vector2.Lerp(from, to, (pieces[n].From + pieces[n].To) / 2);
            Assert.Equal(((int)middle.X, (int)middle.Y), (pieces[n].I, pieces[n].J));
            if (n == 0) continue;
            Assert.Equal(pieces[n - 1].To, pieces[n].From);
            Assert.Equal(1, Math.Abs(pieces[n].I - pieces[n - 1].I) + Math.Abs(pieces[n].J - pieces[n - 1].J));
        }
    }

    [Fact]
    public void The_walk_leaves_no_slivers_at_corners_and_boundaries()
    {
        // Through two corners of the grid, and from one boundary to another.
        var diagonal = Walk(new Vector2(0.5f, 0.5f), new Vector2(2.5f, 2.5f));
        var between = Walk(new Vector2(1, 0.5f), new Vector2(3, 0.5f));

        Assert.Equal([(0, 0), (1, 1), (2, 2)], diagonal.Select(p => (p.I, p.J)));
        Assert.Equal([(1, 0), (2, 0)], between.Select(p => (p.I, p.J)));
        Assert.All(diagonal.Concat(between), p => Assert.True(p.To - p.From > 0.1f));
    }

    [Fact]
    public void The_walk_runs_through_cells_the_bead_filled()
    {
        var toolpath = Beads((new Vector2(1, 1), new Vector2(7, 5.3f)), (new Vector2(6, 0.5f), new Vector2(0.7f, 6.1f)), (new Vector2(2, 7.7f), new Vector2(8, 7.7f)));
        var grid = Voxelizer.Voxelize(toolpath, Identity, Area);

        // Where a line only clips the corner of a cell, the samples need not have put plastic there.
        foreach (var segment in toolpath.Segments)
        {
            var pieces = new List<(int I, int J, float From, float To)>();
            Voxelizer.Walk(grid, segment.Start, segment.End, pieces);
            Assert.All(pieces.Where(p => (p.To - p.From) * segment.Length >= Width / 2),
                p => Assert.True(grid.Volume(p.I, p.J, 0) > 0, $"cell ({p.I}, {p.J}) is empty"));
        }
    }
}
