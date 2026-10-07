using System.Numerics;
using GcodeFem.Core.Geometry;

namespace GcodeFem.Tests.Geometry;

public class FaceRegionTests
{
    /// <summary>40 mm legs, 10 wide, 8 thick; holes of Ø5 through the X leg at (25, 5) and (35, 5).</summary>
    static readonly TriangleMesh Bracket = MeshFactory.LBracket(leg: 40, width: 10, height: 8, holes: 2, holeDiameter: 5);
    static readonly MeshTopology Topology = MeshTopology.Of(Bracket);

    static int TriangleAt(float x, float y, float z) => MeshPicker.Nearest(Bracket, new Vector3(x, y, z)).Triangle;

    static float FromAxis(Vector3 p, float x, float y) => new Vector2(p.X - x, p.Y - y).Length();

    [Fact]
    public void Every_triangle_of_a_closed_solid_has_three_neighbours()
    {
        for (var t = 0; t < Bracket.TriangleCount; t++) Assert.Equal(3, Topology.Neighbours(t).Length);
    }

    [Fact]
    public void A_flat_face_grows_up_to_its_edges()
    {
        var top = FaceRegions.Grow(Bracket, Topology, TriangleAt(5, 5, 8), maxAngleDegrees: 20);

        Assert.Equal(FaceRegions.OnPlane(Bracket, Vector3.UnitZ, 8), top);
        // Two per plain rectangle, and a ring of 32 + 4 around each hole.
        Assert.Equal(3 * 2 + 2 * 36, top.Length);
        Assert.All(top, t => Assert.Equal(1, Bracket.Normal(t).Z, 5));
    }

    [Fact]
    public void A_hole_wall_grows_all_the_way_round_and_stops_at_its_rims()
    {
        var wall = FaceRegions.Grow(Bracket, Topology, TriangleAt(27.5f, 5, 4), maxAngleDegrees: 20);

        Assert.Equal(2 * 32, wall.Length);
        foreach (var t in wall)
        {
            var (a, b, c) = Bracket.Triangle(t);
            Assert.All(new[] { a, b, c }, p => Assert.Equal(2.5f, FromAxis(p, 25, 5), 4));
        }
    }

    [Fact]
    public void A_tight_angle_keeps_to_one_flat_piece_of_the_wall()
    {
        // Neighbouring pieces of a 32-sided hole differ by 11.25°.
        var piece = FaceRegions.Grow(Bracket, Topology, TriangleAt(27.5f, 5, 4), maxAngleDegrees: 5);

        Assert.Equal(2, piece.Length);
    }

    [Fact]
    public void A_sliver_with_a_meaningless_normal_does_not_stop_a_flat_face()
    {
        // Between the triangle above the X axis and the two below it lies a sliver a millionth high, standing on edge.
        Vector3[] positions = [new(0, 0, 0), new(1, 0, 0), new(0.5f, 1, 0), new(0.5f, -1, 0), new(0.5f, 0, 1e-6f)];
        var mesh = new TriangleMesh(positions, [0, 1, 2, 0, 3, 4, 4, 3, 1, 0, 4, 1]);
        Assert.True(MathF.Abs(mesh.Normal(3).Y) > 0.99f);

        var face = FaceRegions.Grow(mesh, MeshTopology.Of(mesh), seed: 0, maxAngleDegrees: 10);

        Assert.Equal(new[] { 0, 1, 2, 3 }, face);
    }

    [Fact]
    public void A_ray_finds_the_first_triangle_it_meets()
    {
        var hit = MeshPicker.Pick(Bracket, new Vector3(5, 5, 50), new Vector3(0, 0, -3));

        Assert.NotNull(hit);
        Assert.Equal(42, hit.Value.Distance, 4);
        Assert.Equal(1, Bracket.Normal(hit.Value.Triangle).Z, 5);
    }

    [Fact]
    public void A_ray_down_the_middle_of_a_hole_passes_through()
    {
        Assert.Null(MeshPicker.Pick(Bracket, new Vector3(25, 5, 50), -Vector3.UnitZ));
    }

    [Fact]
    public void A_ray_into_a_hole_at_an_angle_finds_its_wall()
    {
        var origin = new Vector3(25, 5, 20);
        var hit = MeshPicker.Pick(Bracket, origin, new Vector3(27.4f, 5, 4) - origin);

        Assert.NotNull(hit);
        var (a, b, c) = Bracket.Triangle(hit.Value.Triangle);
        Assert.All(new[] { a, b, c }, p => Assert.Equal(2.5f, FromAxis(p, 25, 5), 4));
    }

    [Fact]
    public void The_side_of_a_box_is_found_by_its_plane()
    {
        var box = MeshFactory.Box(Vector3.Zero, new Vector3(3, 2, 1));

        var side = FaceRegions.OnPlane(box, Vector3.UnitX, 3);

        Assert.Equal(2, side.Length);
        Assert.All(side, t => Assert.Equal(Vector3.UnitX, box.Normal(t)));
    }
}
