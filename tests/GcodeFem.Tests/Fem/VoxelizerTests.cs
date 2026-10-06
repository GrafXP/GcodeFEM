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
}
