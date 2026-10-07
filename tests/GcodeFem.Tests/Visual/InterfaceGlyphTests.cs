using System.Numerics;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Study;
using GcodeFem.Core.Visual;

namespace GcodeFem.Tests.Visual;

public class InterfaceGlyphTests
{
    /// <summary>Area of the 16-sided outline that stands in for a circle of radius 1.</summary>
    static readonly float Outline = 0.5f * 16 * MathF.Sin(2 * MathF.PI / 16);

    static readonly Vector4 Red = new(1, 0, 0, 1);

    /// <summary>What a closed surface encloses, counted positive if its triangles face outwards.</summary>
    static float Volume(GlyphMesh mesh)
    {
        var volume = 0.0;
        for (var t = 0; t < mesh.TriangleCount; t++)
            volume += Vector3.Dot(mesh.Positions[3 * t], Vector3.Cross(mesh.Positions[3 * t + 1], mesh.Positions[3 * t + 2])) / 6;
        return (float)volume;
    }

    static InterfaceMark Mark(InterfaceKind kind, int[] triangles, LoadValue? value = null, bool selected = true) => new(kind, triangles, value ?? new LoadValue(), selected);

    [Fact]
    public void A_cone_is_a_closed_solid_that_faces_outwards()
    {
        var mesh = new GlyphMesh();

        mesh.AddCone(tip: new Vector3(1, 2, 7), foot: new Vector3(1, 2, 4), radius: 0.5f, Red);

        Assert.Equal(2 * 16, mesh.TriangleCount);
        Assert.Equal(Outline * 0.25f * 3 / 3, Volume(mesh), 3);
        Assert.Contains(new Vector3(1, 2, 7), mesh.Positions);
        Assert.All(mesh.Normals, normal => Assert.Equal(1, normal.Length(), 4));
        Assert.All(mesh.Colours, colour => Assert.Equal(Red, colour));
    }

    [Fact]
    public void A_rod_is_a_closed_solid_whichever_way_it_points()
    {
        foreach (var direction in new[] { Vector3.UnitX, Vector3.UnitZ, -Vector3.UnitZ, Vector3.Normalize(new Vector3(1, -2, 3)) })
        {
            var mesh = new GlyphMesh();

            mesh.AddRod(new Vector3(3, 1, 2), new Vector3(3, 1, 2) + 5 * direction, radius: 0.2f, Red);

            Assert.Equal(4 * 16, mesh.TriangleCount);
            Assert.Equal(Outline * 0.04f * 5, Volume(mesh), 3);
        }
    }

    [Fact]
    public void An_arrow_reaches_from_its_start_to_its_point()
    {
        var mesh = new GlyphMesh();

        mesh.AddArrow(new Vector3(0, 0, 10), new Vector3(0, 0, 4), radius: 0.1f, Red);

        var bounds = Box3.Of([.. mesh.Positions]);
        Assert.Equal(4, bounds.Min.Z, 5);
        Assert.Equal(10, bounds.Max.Z, 5);
        Assert.Equal(0.25f, bounds.Max.X, 2); // the head is two and a half times as wide as the shaft
        Assert.Single(mesh.Positions.Distinct(), p => p.Z < 4.0001f);
        Assert.True(Volume(mesh) > 0);
    }

    [Fact]
    public void The_interface_being_edited_shows_in_full_colour_over_the_others()
    {
        var colours = InterfaceGlyphs.TriangleColours(6,
        [
            Mark(InterfaceKind.Force, [1, 2], selected: true),
            Mark(InterfaceKind.Fixed, [2, 3], selected: false),
            Mark(InterfaceKind.Fixed, [99], selected: false), // a triangle the model does not have is passed over
        ]);

        Assert.Equal(InterfaceGlyphs.Plain, colours[0]);
        Assert.Equal(InterfaceGlyphs.Load, colours[1]);
        Assert.Equal(InterfaceGlyphs.Load, colours[2]);
        Assert.NotEqual(InterfaceGlyphs.Mount, colours[3]);
        Assert.True(Vector4.Distance(colours[3], InterfaceGlyphs.Mount) < Vector4.Distance(colours[3], InterfaceGlyphs.Load));
        Assert.Equal(InterfaceGlyphs.Plain, colours[5]);
    }

    [Fact]
    public void A_mount_on_a_flat_face_gets_cones_standing_on_it()
    {
        var box = MeshFactory.Box(Vector3.Zero, new Vector3(30, 20, 10));
        var top = FaceRegions.OnPlane(box, Vector3.UnitZ, 10);

        var glyphs = InterfaceGlyphs.Build(box, Matrix4x4.Identity, [Mark(InterfaceKind.Fixed, top)]);

        Assert.Equal(12 * 2 * 16, glyphs.TriangleCount);
        Assert.All(glyphs.Positions, p => Assert.True(p.Z >= 10 - 1e-4f));
        Assert.Equal(12, glyphs.Positions.Distinct().Count(p => MathF.Abs(p.Z - 10) < 1e-4f));
    }

    [Fact]
    public void A_force_gets_an_arrow_that_stays_outside_the_part()
    {
        var box = MeshFactory.Box(Vector3.Zero, new Vector3(30, 20, 10));
        var top = FaceRegions.OnPlane(box, Vector3.UnitZ, 10);

        var push = Box3.Of([.. InterfaceGlyphs.Build(box, Matrix4x4.Identity, [Mark(InterfaceKind.Force, top, new LoadValue(new Vector3(0, 0, -50)))]).Positions]);
        var pull = Box3.Of([.. InterfaceGlyphs.Build(box, Matrix4x4.Identity, [Mark(InterfaceKind.Force, top, new LoadValue(new Vector3(0, 0, 50)))]).Positions]);
        var onto = Box3.Of([.. InterfaceGlyphs.Build(box, Matrix4x4.Identity, [Mark(InterfaceKind.Force, top, new LoadValue(NormalForce: 50))]).Positions]);

        // All three run along Z above the middle of the face, and are a fifth of the part's diagonal long.
        var length = 0.2f * new Vector3(30, 20, 10).Length();
        foreach (var arrow in new[] { push, pull, onto })
        {
            Assert.Equal(10, arrow.Min.Z, 4);
            Assert.Equal(10 + length, arrow.Max.Z, 3);
            Assert.Equal(new Vector2(15, 10), new Vector2(arrow.Center.X, arrow.Center.Y), (a, b) => Vector2.Distance(a, b) < 1e-3f);
        }
    }

    [Fact]
    public void A_force_in_the_model_s_directions_is_drawn_as_the_part_is_turned()
    {
        var rotation = Orientation.FromEulerDegrees(0, 90, 0); // the model's Z lies along the print's X
        var box = MeshFactory.Box(Vector3.Zero, new Vector3(30, 20, 10));
        var top = FaceRegions.OnPlane(box, Vector3.UnitZ, 10);

        var arrow = Box3.Of([.. InterfaceGlyphs.Build(box.Transformed(rotation), rotation, [Mark(InterfaceKind.Force, top, new LoadValue(new Vector3(0, 0, -50)))]).Positions]);

        Assert.True(arrow.Size.X > 5 * arrow.Size.Y && arrow.Size.X > 5 * arrow.Size.Z);
    }

    [Fact]
    public void A_bolt_hole_gets_a_rod_along_its_axis_and_a_bearing_load_an_arrow_at_each_mouth()
    {
        var bracket = MeshFactory.LBracket(leg: 40, width: 10, height: 8, holes: 2, holeDiameter: 5);
        var topology = MeshTopology.Of(bracket);
        int[] Wall(float x) => FaceRegions.Grow(bracket, topology, MeshPicker.Nearest(bracket, new Vector3(x + 2.5f, 5, 4)).Triangle, 20);

        var rods = InterfaceGlyphs.Build(bracket, Matrix4x4.Identity, [Mark(InterfaceKind.BoltHole, [.. Wall(25), .. Wall(35)])]);
        var arrows = InterfaceGlyphs.Build(bracket, Matrix4x4.Identity, [Mark(InterfaceKind.Bearing, Wall(25), new LoadValue(new Vector3(0, -40, 0)))]);

        Assert.Equal(2 * 4 * 16, rods.TriangleCount);
        var rod = Box3.Of([.. rods.Positions]);
        Assert.True(rod.Min.Z < 0 && rod.Max.Z > 8, "The rods stick out of the holes at both ends.");
        Assert.InRange(rod.Center.Y, 4.99f, 5.01f);

        Assert.Equal(2 * 6 * 16, arrows.TriangleCount);
        var arrow = Box3.Of([.. arrows.Positions]);
        Assert.True(arrow.Min.Z < 0 && arrow.Max.Z > 8);
        Assert.True(arrow.Min.Y < 5 - 8 && arrow.Max.Y < 5.5f, "The arrows point along the force, towards −Y.");
        Assert.InRange(arrow.Center.X, 24.5f, 25.5f);
    }

    [Fact]
    public void A_load_without_a_value_gets_no_arrow()
    {
        var box = MeshFactory.Box(Vector3.Zero, new Vector3(30, 20, 10));
        var top = FaceRegions.OnPlane(box, Vector3.UnitZ, 10);

        Assert.Equal(0, InterfaceGlyphs.Build(box, Matrix4x4.Identity, [Mark(InterfaceKind.Force, top), Mark(InterfaceKind.Pressure, top), Mark(InterfaceKind.Fixed, [])]).TriangleCount);
        Assert.Equal(9 * 6 * 16, InterfaceGlyphs.Build(box, Matrix4x4.Identity, [Mark(InterfaceKind.Pressure, top, new LoadValue(Pressure: 1))]).TriangleCount);
    }
}
