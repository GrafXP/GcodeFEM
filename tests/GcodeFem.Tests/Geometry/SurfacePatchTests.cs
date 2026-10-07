using System.Numerics;
using GcodeFem.Core.Geometry;

namespace GcodeFem.Tests.Geometry;

public class SurfacePatchTests
{
    static readonly TriangleMesh Bracket = MeshFactory.LBracket(leg: 40, width: 10, height: 8, holes: 2, holeDiameter: 5);
    static readonly MeshTopology Topology = MeshTopology.Of(Bracket);

    static int[] Face(float x, float y, float z) =>
        FaceRegions.Grow(Bracket, Topology, MeshPicker.Nearest(Bracket, new Vector3(x, y, z)).Triangle, maxAngleDegrees: 20);

    static readonly int[] FirstWall = Face(27.5f, 5, 4), SecondWall = Face(37.5f, 5, 4), Top = Face(5, 5, 8);

    /// <summary>Distance from a point to the nearest of some triangles, by trying them all.</summary>
    static float Distance(TriangleMesh mesh, IEnumerable<int> triangles, Vector3 point) =>
        MeshPicker.Nearest(new TriangleMesh(mesh.Positions, [.. triangles.SelectMany(t => mesh.Indices.AsSpan(3 * t, 3).ToArray())]), point).Distance;

    [Fact]
    public void A_flat_patch_has_its_area_its_middle_and_one_normal()
    {
        var box = MeshFactory.Box(Vector3.Zero, new Vector3(6, 4, 2));

        var top = new SurfacePatch(box, FaceRegions.OnPlane(box, Vector3.UnitZ, 2));

        Assert.Equal(24, top.Area, 4);
        Assert.Equal(new Vector3(3, 2, 2), top.Centroid);
        Assert.Equal(Vector3.UnitZ, top.MeanNormal);
        Assert.Equal(new Box3(new Vector3(0, 0, 2), new Vector3(6, 4, 2)), top.Bounds);
    }

    [Fact]
    public void A_hole_wall_faces_every_way()
    {
        var wall = new SurfacePatch(Bracket, FirstWall);

        // 32 flat pieces, each a chord of the Ø5 circle wide and 8 high.
        Assert.Equal(32 * 5 * MathF.Sin(MathF.PI / 32) * 8, wall.Area, 2);
        Assert.True(wall.MeanNormal.Length() < 1e-4f);
        Assert.Equal(new Vector3(25, 5, 4), wall.Centroid, (a, b) => Vector3.Distance(a, b) < 1e-3f);
    }

    [Fact]
    public void Two_holes_in_one_patch_come_apart_again()
    {
        var parts = new SurfacePatch(Bracket, FirstWall.Concat(SecondWall)).Components();

        Assert.Equal(2, parts.Length);
        Assert.Contains(parts, part => part.SequenceEqual(FirstWall));
        Assert.Contains(parts, part => part.SequenceEqual(SecondWall));
    }

    [Fact]
    public void The_locator_agrees_with_trying_every_triangle()
    {
        var patch = new SurfacePatch(Bracket, FirstWall.Concat(SecondWall).Concat(Top));
        var locator = patch.Locator(reach: 0.5f);
        var random = new Random(5);
        var (found, missed) = (0, 0);

        for (var n = 0; n < 4000; n++)
        {
            var point = new Vector3(18 + 24 * random.NextSingle(), -1 + 12 * random.NextSingle(), -1 + 10 * random.NextSingle());
            var expected = Distance(Bracket, patch.Triangles, point);
            var nearest = locator.Nearest(point);

            if (nearest < 0)
            {
                Assert.True(expected > 0.5f - 1e-4f, $"Missed a triangle {expected} mm away from {point}.");
                missed++;
            }
            else
            {
                Assert.Equal(expected, Distance(Bracket, [patch.Triangles[nearest]], point), 4);
                Assert.True(expected <= 0.5f + 1e-4f);
                found++;
            }
        }
        Assert.True(found > 200 && missed > 200, $"The points should fall on both sides: {found} found, {missed} missed.");
    }

    [Fact]
    public void The_locator_copes_with_a_triangle_far_bigger_than_its_reach()
    {
        Vector3 a = new(0, 0, 0), b = new(100, 0, 50), c = new(0, 100, 50);
        var mesh = new TriangleMesh([a, b, c], [0, 1, 2]);
        var locator = new SurfacePatch(mesh, [0]).Locator(reach: 0.5f);
        var normal = mesh.Normal(0);
        var random = new Random(7);

        for (var n = 0; n < 500; n++)
        {
            float u = random.NextSingle(), v = random.NextSingle();
            if (u + v > 1) (u, v) = (1 - u, 1 - v);
            var on = a + u * (b - a) + v * (c - a);
            Assert.Equal(0, locator.Nearest(on + 0.3f * normal));
            Assert.Equal(0, locator.Nearest(on - 0.45f * normal));
            Assert.Equal(-1, locator.Nearest(on + 0.7f * normal));
        }
        Assert.Equal(-1, locator.Nearest(new Vector3(200, 200, 200)));
    }

    [Fact]
    public void A_facing_direction_picks_the_side_that_faces_that_way()
    {
        var box = MeshFactory.Box(Vector3.Zero, new Vector3(6, 4, 2));
        var patch = new SurfacePatch(box, FaceRegions.OnPlane(box, Vector3.UnitZ, 2).Concat(FaceRegions.OnPlane(box, Vector3.UnitX, 6)));
        var locator = patch.Locator(reach: 0.5f);
        var nearTheEdge = new Vector3(6.2f, 2, 1.9f); // 0.2 from the side and 0.22 from the edge of the top: both within reach

        Assert.Equal(Vector3.UnitX, patch.Normals[locator.Nearest(nearTheEdge, Vector3.UnitX, 0.1f)]);
        Assert.Equal(Vector3.UnitZ, patch.Normals[locator.Nearest(nearTheEdge, Vector3.UnitZ, 0.1f)]);
        Assert.Equal(-1, locator.Nearest(nearTheEdge, -Vector3.UnitY, 0.1f));
    }

    [Fact]
    public void Markers_spread_over_the_whole_patch()
    {
        var box = MeshFactory.Box(Vector3.Zero, new Vector3(6, 4, 2));
        var top = new SurfacePatch(box, FaceRegions.OnPlane(box, Vector3.UnitZ, 2));

        var spots = top.Spread(12);

        Assert.Equal(12, spots.Length);
        Assert.All(spots, spot =>
        {
            Assert.Equal(2, spot.Point.Z, 5);
            Assert.InRange(spot.Point.X, 0, 6);
            Assert.InRange(spot.Point.Y, 0, 4);
            Assert.Equal(Vector3.UnitZ, spot.Normal);
        });
        for (var n = 0; n < spots.Length; n++)
        for (var m = n + 1; m < spots.Length; m++)
            Assert.True(Vector3.Distance(spots[n].Point, spots[m].Point) > 0.7f);
        // They reach into every quarter of the face.
        Assert.All(new[] { new Vector2(1.5f, 1), new(4.5f, 1), new(1.5f, 3), new(4.5f, 3) },
            quarter => Assert.Contains(spots, spot => Vector2.Distance(new(spot.Point.X, spot.Point.Y), quarter) < 1.5f));
    }

    [Fact]
    public void A_hole_is_recognised_with_its_axis_and_size()
    {
        var hole = Cylinder.Fit(Bracket, FirstWall);

        Assert.NotNull(hole);
        Assert.True(hole.IsHole);
        Assert.Equal(Vector3.UnitZ, hole.Axis, (a, b) => Vector3.Distance(a, b) < 1e-4f);
        Assert.Equal(new Vector3(25, 5, 4), hole.Centre, (a, b) => Vector3.Distance(a, b) < 1e-3f);
        Assert.Equal(2.5f, hole.Radius, 3);
        Assert.Equal(8, hole.Length, 4);
    }

    [Fact]
    public void Half_a_hole_wall_is_still_a_hole()
    {
        var half = FirstWall.Where(t => Bracket.Triangle(t) is var (a, b, c) && (a.X + b.X + c.X) / 3 > 25).ToArray();

        var hole = Cylinder.Fit(Bracket, half);

        Assert.NotNull(hole);
        Assert.Equal(2.5f, hole.Radius, 3);
        Assert.Equal(new Vector3(25, 5, 4), hole.Centre, (a, b) => Vector3.Distance(a, b) < 1e-2f);
    }

    [Fact]
    public void A_hole_in_a_turned_part_has_its_axis_turned_with_it()
    {
        var turned = Bracket.Transformed(Orientation.FromEulerDegrees(90, 0, 0)); // Z → Y

        var hole = Cylinder.Fit(turned, FirstWall);

        Assert.NotNull(hole);
        Assert.Equal(1, MathF.Abs(hole.Axis.Y), 4);
        Assert.Equal(2.5f, hole.Radius, 3);
    }

    [Fact]
    public void A_wall_that_faces_outwards_is_a_pin()
    {
        // The same triangles wound the other way face away from the axis.
        var flipped = new TriangleMesh(Bracket.Positions, [.. FirstWall.SelectMany(t => new[] { Bracket.Indices[3 * t], Bracket.Indices[3 * t + 2], Bracket.Indices[3 * t + 1] })]);

        var pin = Cylinder.Fit(flipped, [.. Enumerable.Range(0, flipped.TriangleCount)]);

        Assert.NotNull(pin);
        Assert.False(pin.IsHole);
    }

    [Fact]
    public void A_flat_face_is_no_cylinder()
    {
        Assert.Null(Cylinder.Fit(Bracket, Top));
        Assert.Null(Cylinder.Fit(Bracket, Top.Concat(FirstWall).ToArray()));
    }
}
