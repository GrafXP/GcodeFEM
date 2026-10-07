using System.Numerics;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Study;

namespace GcodeFem.Tests.Study;

public class InterfaceMapperTests
{
    const float Pitch = 0.42f, Layer = 0.2f;

    /// <summary>A grid over <paramref name="bounds"/> with every cell filled whose centre is <paramref name="inside"/>.</summary>
    static CellGrid Fill(Box3 bounds, int sizeX, int sizeY, int sizeZ, Func<Vector3, bool>? inside = null)
    {
        var z = Enumerable.Range(0, sizeZ + 1).Select(k => bounds.Min.Z + k * Layer).ToArray();
        var grid = new CellGrid(new Vector2(bounds.Min.X, bounds.Min.Y), Pitch, z, sizeX, sizeY);
        for (var k = 0; k < sizeZ; k++)
        for (var j = 0; j < sizeY; j++)
        for (var i = 0; i < sizeX; i++)
            if (inside is null || inside(grid.NodePosition(i, j, k) + new Vector3(Pitch / 2, Pitch / 2, Layer / 2)))
                grid.Deposit(i, j, k, grid.CellVolume(k));
        return grid;
    }

    static PartInterface Interface(string name, InterfaceKind kind, int[] triangles) => new() { Name = name, Kind = kind, Triangles = triangles };

    static StudyLoadCase Case(params (PartInterface Item, LoadValue Value)[] loads)
    {
        var loadCase = new StudyLoadCase { Name = "Test" };
        foreach (var (item, value) in loads) loadCase.Loads[item] = value;
        return loadCase;
    }

    static Vector3 Sum(double[] loads)
    {
        double x = 0, y = 0, z = 0;
        for (var n = 0; n < loads.Length; n += 3) (x, y, z) = (x + loads[n], y + loads[n + 1], z + loads[n + 2]);
        return new Vector3((float)x, (float)y, (float)z);
    }

    static void Close(Vector3 expected, Vector3 actual, float tolerance = 1e-4f) =>
        Assert.True(Vector3.Distance(expected, actual) <= tolerance, $"Expected {expected}, got {actual}.");

    // ---- a solid block, 20 × 4 × 6 cells ----

    static readonly Vector3 BlockSize = new(20 * Pitch, 4 * Pitch, 6 * Layer);
    static readonly TriangleMesh Block = MeshFactory.Box(Vector3.Zero, BlockSize);

    static int[] BlockSide(Vector3 normal) => FaceRegions.OnPlane(Block, normal, Vector3.Dot(normal, normal.X + normal.Y + normal.Z > 0 ? BlockSize : Vector3.Zero));

    [Fact]
    public void A_fixed_end_and_a_force_on_the_other_are_the_clamp_and_push_load_case()
    {
        var grid = CellGrid.Solid(20, 4, 6, Pitch, Layer);
        var clamp = Interface("Clamp", InterfaceKind.Fixed, BlockSide(-Vector3.UnitX));
        var push = Interface("Push", InterfaceKind.Force, BlockSide(Vector3.UnitX));

        var mapped = InterfaceMapper.Map(Block, Matrix4x4.Identity, Pitch, [clamp, push], Case((push, new LoadValue(new Vector3(0, 0.25f, -1)))));
        var expected = LoadCase.ClampAndPush(new Box3(Vector3.Zero, BlockSize), Axis.X, fixMax: false, new Vector3(0, 0.25f, -1));

        var mesh = FemMesh.Build(grid, 0.05f, mapped);
        Assert.Equal(20 * 4 * 6, mesh.Cells.Length);
        Assert.Equal(Assembler.FixedDofs(mesh, expected), Assembler.FixedDofs(mesh, mapped));
        var (wanted, loads) = (Assembler.Loads(mesh, expected), Assembler.Loads(mesh, mapped));
        for (var d = 0; d < loads.Length; d++) Assert.Equal(wanted[d], loads[d], 7);
    }

    [Fact]
    public void A_pressure_pushes_on_its_face_with_pressure_times_area()
    {
        var mesh = FemMesh.Build(CellGrid.Solid(20, 4, 6, Pitch, Layer), 0.05f, [new Fixture(_ => true)]);
        var bottom = Interface("Bed", InterfaceKind.Fixed, BlockSide(-Vector3.UnitZ));
        var top = Interface("Top", InterfaceKind.Pressure, BlockSide(Vector3.UnitZ));

        var mapped = InterfaceMapper.Map(Block, Matrix4x4.Identity, Pitch, [bottom, top], Case((top, new LoadValue(Pressure: 0.5f))));

        Close(new Vector3(0, 0, -0.5f * BlockSize.X * BlockSize.Y), Sum(Assembler.Loads(mesh, mapped)));
        var reach = Assembler.Reach(mesh, mapped);
        Assert.Equal(new[] { "Bed", "Top" }, reach.Select(r => r.Name));
        Assert.All(reach, r => Assert.Equal(20 * 4, r.Faces));
        Assert.Equal(BlockSize.X * BlockSize.Y, reach[1].Area, 3);
    }

    [Fact]
    public void A_force_is_given_in_the_model_s_directions_and_turns_with_the_part()
    {
        // Printed turned a quarter about Z: the model's X is the print's Y, its Y the print's −X.
        var rotation = Orientation.FromEulerDegrees(0, 0, 90);
        var print = Block.Transformed(rotation);
        Assert.Equal(new Vector3(-BlockSize.Y, 0, 0), print.Bounds.Min, (a, b) => Vector3.Distance(a, b) < 1e-5f);
        var grid = Fill(print.Bounds, 4, 20, 6);
        var clamp = Interface("Clamp", InterfaceKind.Fixed, BlockSide(-Vector3.UnitX));
        var push = Interface("Push", InterfaceKind.Force, BlockSide(Vector3.UnitX));

        var mapped = InterfaceMapper.Map(print, rotation, Pitch, [clamp, push], Case((push, new LoadValue(new Vector3(0, 2, -1)))));
        var mesh = FemMesh.Build(grid, 0.05f, mapped);

        Close(new Vector3(-2, 0, -1), Sum(Assembler.Loads(mesh, mapped)));
        var held = Assembler.FixedDofs(mesh, mapped);
        for (var n = 0; n < mesh.NodeCount; n++) Assert.Equal(MathF.Abs(mesh.NodePosition(n).Y) < 1e-4f, held[3 * n]);
    }

    [Fact]
    public void A_force_along_the_normal_pushes_onto_the_face()
    {
        var mesh = FemMesh.Build(CellGrid.Solid(20, 4, 6, Pitch, Layer), 0.05f, [new Fixture(_ => true)]);
        var clamp = Interface("Clamp", InterfaceKind.Fixed, BlockSide(-Vector3.UnitX));
        var push = Interface("Push", InterfaceKind.Force, BlockSide(Vector3.UnitX));

        var mapped = InterfaceMapper.Map(Block, Matrix4x4.Identity, Pitch, [clamp, push], Case((push, new LoadValue(NormalForce: 3))));

        Close(new Vector3(-3, 0, 0), Sum(Assembler.Loads(mesh, mapped)));
    }

    [Fact]
    public void A_sliding_face_is_held_against_the_face_only_and_that_alone_is_not_enough()
    {
        var mesh = FemMesh.Build(CellGrid.Solid(20, 4, 6, Pitch, Layer), 0.05f, [new Fixture(_ => true)]);
        var slide = Interface("Slide", InterfaceKind.Sliding, BlockSide(-Vector3.UnitZ));
        var stop = Interface("Stop", InterfaceKind.Fixed, BlockSide(-Vector3.UnitX));
        var push = Interface("Push", InterfaceKind.Pressure, BlockSide(Vector3.UnitZ));
        var loads = Case((push, new LoadValue(Pressure: 1)));

        var both = Assembler.FixedDofs(mesh, InterfaceMapper.Map(Block, Matrix4x4.Identity, Pitch, [slide, stop, push], loads));
        for (var n = 0; n < mesh.NodeCount; n++)
        {
            var p = mesh.NodePosition(n);
            var atStop = MathF.Abs(p.X) < 1e-4f;
            Assert.Equal(atStop, both[3 * n]);
            Assert.Equal(atStop, both[3 * n + 1]);
            Assert.Equal(atStop || MathF.Abs(p.Z) < 1e-4f, both[3 * n + 2]);
        }

        var alone = InterfaceMapper.Map(Block, Matrix4x4.Identity, Pitch, [slide, push], loads);
        var error = Assert.Throws<InvalidOperationException>(() => Assembler.FixedDofs(mesh, alone));
        Assert.Contains("free to", error.Message);
    }

    // ---- a slanted face: a wedge whose long side runs at 45° across the grid ----

    [Fact]
    public void On_a_slanted_face_the_steps_carry_what_the_face_would()
    {
        const int n = 10, layers = 3;
        float side = n * Pitch, height = layers * Layer;
        var wedge = MeshFactory.Prism([new(0, 0), new(side, 0), new(0, side)], height);
        var grid = Fill(wedge.Bounds, n, n, layers, centre => centre.X + centre.Y < side - Pitch / 4);
        var slope = FaceRegions.Grow(wedge, MeshTopology.Of(wedge), MeshPicker.Nearest(wedge, new Vector3(side / 2, side / 2, height / 2)).Triangle, 20);
        var back = Interface("Back", InterfaceKind.Fixed, FaceRegions.OnPlane(wedge, Vector3.UnitX, 0));
        var face = Interface("Slope", InterfaceKind.Pressure, slope);
        var mesh = FemMesh.Build(grid, 0.05f, [new Fixture(_ => true)]);

        var pressed = InterfaceMapper.Map(wedge, Matrix4x4.Identity, Pitch, [back, face], Case((face, new LoadValue(Pressure: 2))));

        // The steps run from cell centre to cell centre, so they span n − 1 cells each way; all of them are loaded, and nothing else.
        var stepped = (n - 1) * Pitch * height;
        Close(new Vector3(-2 * stepped, -2 * stepped, 0), Sum(Assembler.Loads(mesh, pressed)), 1e-3f);
        Assert.Equal(2 * (n - 1) * layers, Assembler.Reach(mesh, pressed)[1].Faces);

        face.Kind = InterfaceKind.Force;
        var pushed = InterfaceMapper.Map(wedge, Matrix4x4.Identity, Pitch, [back, face], Case((face, new LoadValue(NormalForce: 5))));
        Close(-5 * Vector3.Normalize(new Vector3(1, 1, 0)), Sum(Assembler.Loads(mesh, pushed)));
    }

    // ---- a bracket with one bolt hole of Ø5 at (15, 5) ----

    static readonly TriangleMesh Bracket = MeshFactory.LBracket(leg: 20, width: 10, height: 2, holes: 1, holeDiameter: 5);
    static readonly Vector2 HoleAxis = new(15, 5);

    static readonly int[] HoleWall = FaceRegions.Grow(Bracket, MeshTopology.Of(Bracket), MeshPicker.Nearest(Bracket, new Vector3(17.5f, 5, 1)).Triangle, 20);
    static readonly int[] LegEnd = FaceRegions.OnPlane(Bracket, Vector3.UnitY, 20);

    static CellGrid BracketCells() => Fill(Bracket.Bounds, 48, 48, 10, centre =>
        ((centre.X < 20 && centre.Y < 10) || (centre.X < 10 && centre.Y < 20)) && Vector2.Distance(new(centre.X, centre.Y), HoleAxis) > 2.5f);

    static FemMesh BracketMesh() => FemMesh.Build(BracketCells(), 0.05f, [new Fixture(_ => true)]);

    [Fact]
    public void A_bearing_load_presses_on_the_side_of_the_hole_it_pushes_against()
    {
        var mesh = BracketMesh();
        var clamp = Interface("End", InterfaceKind.Fixed, LegEnd);
        var pin = Interface("Pin", InterfaceKind.Bearing, HoleWall);
        var loaded = new HashSet<int>();

        // A pin cannot push along its hole: of (30, 0, 7) only the 30 across it arrives.
        var mapped = InterfaceMapper.Map(Bracket, Matrix4x4.Identity, Pitch, [clamp, pin], Case((pin, new LoadValue(new Vector3(30, 0, 7)))));
        var sum = Sum(Assembler.Loads(mesh, mapped, loaded));

        Assert.InRange(sum.X, 29.7f, 30.001f);
        Assert.InRange(sum.Y, -2.5f, 2.5f); // the steps round the hole are not quite the same on both sides
        Assert.Equal(0, sum.Z, 5);
        Assert.NotEmpty(loaded);
        Assert.All(loaded, cell => Assert.True(mesh.Grid.NodePosition(mesh.Cells[cell].I, 0, 0).X + Pitch / 2 > HoleAxis.X - Pitch));
        Close(sum, Assembler.Reach(mesh, mapped)[1].Force, 1e-3f);
    }

    [Fact]
    public void A_bolt_hole_holds_its_wall_across_the_hole_and_along_it()
    {
        var mesh = BracketMesh();
        var bolt = Interface("Bolt", InterfaceKind.BoltHole, HoleWall);
        var tip = Interface("Tip", InterfaceKind.Force, LegEnd);
        var loads = Case((tip, new LoadValue(new Vector3(3, 0, 0))));

        var held = Assembler.FixedDofs(mesh, InterfaceMapper.Map(Bracket, Matrix4x4.Identity, Pitch, [bolt, tip], loads));

        var count = 0;
        for (var n = 0; n < mesh.NodeCount; n++)
        {
            if (!held[3 * n] && !held[3 * n + 1] && !held[3 * n + 2]) continue;
            count++;
            var p = mesh.NodePosition(n);
            Assert.InRange(Vector2.Distance(new(p.X, p.Y), HoleAxis), 2.5f - 2.2f * Pitch, 2.5f + 2.2f * Pitch);
            Assert.True(held[3 * n + 2], "A held node of the wall is held along the hole.");
            Assert.True(held[3 * n] || held[3 * n + 1]);
        }
        // Around the hole and through all 10 layers.
        Assert.InRange(count, 11 * 30, 11 * 80);
        var wall = Assembler.Reach(mesh, InterfaceMapper.Map(Bracket, Matrix4x4.Identity, Pitch, [bolt, tip], loads))[0];
        // The steps of a circle measure 8 r round, against the circle's own 2 π r.
        Assert.InRange(wall.Area, 2 * MathF.PI * 2.5f * 2 * 0.95f, 8 * 2.5f * 2 * 1.15f);
    }

    [Fact]
    public void A_pin_that_is_free_along_its_hole_does_not_hold_the_part_on_its_own()
    {
        var mesh = BracketMesh();
        var pin = new PartInterface { Name = "Pin", Kind = InterfaceKind.BoltHole, Triangles = HoleWall, Axial = false };
        var tip = Interface("Tip", InterfaceKind.Force, LegEnd);
        var mapped = InterfaceMapper.Map(Bracket, Matrix4x4.Identity, Pitch, [pin, tip], Case((tip, new LoadValue(new Vector3(3, 0, 0)))));

        var error = Assert.Throws<InvalidOperationException>(() => Assembler.FixedDofs(mesh, mapped));

        Assert.Contains("slide along Z", error.Message);
    }

    [Fact]
    public void A_bolt_hole_on_a_flat_face_is_refused()
    {
        var bolt = Interface("Bolt", InterfaceKind.BoltHole, LegEnd);
        var tip = Interface("Tip", InterfaceKind.Force, HoleWall);

        var error = Assert.Throws<InvalidOperationException>(() => InterfaceMapper.Map(Bracket, Matrix4x4.Identity, Pitch, [bolt, tip], Case((tip, new LoadValue(Vector3.UnitX)))));

        Assert.Contains("'Bolt'", error.Message);
        Assert.Contains("round hole", error.Message);
    }

    [Fact]
    public void A_mount_holds_only_the_cells_that_hang_together_with_it()
    {
        // A bar cut in two by an empty column; only the half with the fixed end is kept.
        var grid = Fill(new Box3(Vector3.Zero, BlockSize), 20, 4, 6, centre => centre.X is < 10 * Pitch or > 11 * Pitch);
        var clamp = Interface("Clamp", InterfaceKind.Fixed, BlockSide(-Vector3.UnitX));
        var push = Interface("Push", InterfaceKind.Pressure, BlockSide(Vector3.UnitZ));
        var mapped = InterfaceMapper.Map(Block, Matrix4x4.Identity, Pitch, [clamp, push], Case((push, new LoadValue(Pressure: 1))));

        var mesh = FemMesh.Build(grid, 0.05f, mapped);

        Assert.Equal(10 * 4 * 6, mesh.Cells.Length);
        Assert.Equal(9 * 4 * 6, mesh.DroppedCells);
    }

    [Fact]
    public void What_is_missing_is_named()
    {
        var clamp = Interface("Clamp", InterfaceKind.Fixed, BlockSide(-Vector3.UnitX));
        var push = Interface("Push", InterfaceKind.Force, BlockSide(Vector3.UnitX));
        var unpicked = Interface("Later", InterfaceKind.Fixed, []);
        string Error(IReadOnlyList<PartInterface> interfaces, StudyLoadCase loads) =>
            Assert.Throws<InvalidOperationException>(() => InterfaceMapper.Map(Block, Matrix4x4.Identity, Pitch, interfaces, loads)).Message;

        Assert.Contains("Nothing holds", Error([push], Case((push, new LoadValue(Vector3.UnitZ)))));
        Assert.Contains("Nothing loads", Error([clamp, push], Case()));
        Assert.Contains("'Later' has no faces", Error([clamp, push, unpicked], Case((push, new LoadValue(Vector3.UnitZ)))));
    }

    [Fact]
    public void A_load_that_reaches_no_printed_face_is_named()
    {
        // Only the lower half of the block was printed, so its top face has nothing under it.
        var mesh = FemMesh.Build(CellGrid.Solid(20, 4, 2, Pitch, Layer), 0.05f, [new Fixture(_ => true)]);
        var clamp = Interface("Clamp", InterfaceKind.Fixed, BlockSide(-Vector3.UnitX));
        var push = Interface("Top", InterfaceKind.Pressure, BlockSide(Vector3.UnitZ));
        var mapped = InterfaceMapper.Map(Block, Matrix4x4.Identity, Pitch, [clamp, push], Case((push, new LoadValue(Pressure: 1))));

        var error = Assert.Throws<InvalidOperationException>(() => Assembler.Loads(mesh, mapped));

        Assert.Contains("'Top' reaches no face", error.Message);
    }

    [Fact]
    public void Cell_faces_are_told_from_those_of_the_next_face_round_the_corner()
    {
        var covers = InterfaceMapper.Covers(Block, BlockSide(Vector3.UnitZ), Pitch);
        var topEdge = new Vector3(BlockSize.X, BlockSize.Y / 2, BlockSize.Z);

        Assert.True(covers(topEdge - new Vector3(Pitch / 2, 0, 0), Vector3.UnitZ));
        Assert.False(covers(topEdge - new Vector3(0, 0, Layer / 2), Vector3.UnitX));
        Assert.False(covers(new Vector3(BlockSize.X / 2, BlockSize.Y / 2, 0), -Vector3.UnitZ));
        Assert.False(InterfaceMapper.Covers(Block, [], Pitch)(topEdge, Vector3.UnitZ));
    }

    [Fact]
    public void A_picked_face_is_described_by_what_it_is()
    {
        Assert.StartsWith("Hole Ø5 x 2 mm", FaceDescription.Of(Bracket, HoleWall));
        Assert.StartsWith("Flat face, 20 mm²", FaceDescription.Of(Bracket, LegEnd));
        Assert.Equal("No faces picked yet", FaceDescription.Of(Bracket, []));
    }
}

public class FreeMotionTests
{
    const float Pitch = 0.42f, Layer = 0.2f;

    static readonly FemMesh Mesh = FemMesh.Build(CellGrid.Solid(6, 4, 5, Pitch, Layer), 0.05f, [new Fixture(_ => true)]);

    static bool[] Held(Fixture fixture) =>
        [.. Enumerable.Range(0, Mesh.NodeCount).SelectMany(n => fixture.SelectsNode(Mesh.NodePosition(n)) ? new[] { fixture.FixX, fixture.FixY, fixture.FixZ } : new[] { false, false, false })];

    static bool Near(float a, float b) => MathF.Abs(a - b) < 1e-4f;

    [Fact]
    public void A_clamped_face_holds_the_part()
    {
        Assert.Null(Assembler.FreeMotion(Mesh, Held(new Fixture(p => Near(p.X, 0)))));
    }

    [Fact]
    public void A_face_held_two_ways_can_still_slide_the_third_way()
    {
        Assert.Equal("slide along Y", Assembler.FreeMotion(Mesh, Held(new Fixture(p => Near(p.X, 0), FixY: false))));
    }

    [Fact]
    public void A_part_held_along_one_edge_can_still_turn_about_it()
    {
        Assert.Equal("turn about an axis along Y", Assembler.FreeMotion(Mesh, Held(new Fixture(p => Near(p.X, 0) && Near(p.Z, 0)))));
    }
}
