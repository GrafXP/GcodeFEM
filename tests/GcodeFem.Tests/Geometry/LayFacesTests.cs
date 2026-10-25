using System.Numerics;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Study;

namespace GcodeFem.Tests.Geometry;

/// <summary>The faces a part can be laid on, and the candidates of an orientation sweep.</summary>
public class LayFacesTests
{
    static readonly TriangleMesh Bracket = Stl.Read(TestPaths.Sample("bracket40_holes.stl"));

    /// <summary>The bracket's two holes are 32-sided, 5 mm across: each 16 × 2.5² × sin(2π / 32) mm².</summary>
    static readonly float Cap = 40 * 10 + 30 * 10 - 2 * 16 * 6.25f * MathF.Sin(2 * MathF.PI / 32);

    [Fact]
    public void A_cube_lies_on_each_of_its_six_sides()
    {
        var faces = LayFaces.Find(Stl.Read(TestPaths.Sample("cube20.stl")));

        Assert.Equal(6, faces.Count);
        Assert.All(faces, f => Assert.Equal(400, f.ContactArea, 2));
        Assert.Equal(6, faces.Select(f => (MathF.Round(f.Normal.X), MathF.Round(f.Normal.Y), MathF.Round(f.Normal.Z))).Distinct().Count());
    }

    [Fact]
    public void The_bracket_lies_on_its_caps_its_outer_sides_and_its_ends_but_not_across_its_corner()
    {
        var faces = LayFaces.Find(Bracket);

        // The hull also has a plane across the inner corner, from the end of one leg to the other: it touches edges only.
        Assert.Equal([Cap, Cap, 320, 320, 80, 80], faces.Select(f => MathF.Round(f.ContactArea, 1)), new Near(0.1f));
        Assert.DoesNotContain(faces, f => MathF.Abs(f.Normal.X) > 0.1f && MathF.Abs(f.Normal.Y) > 0.1f);
        Assert.All(faces, f => Assert.All(f.Contact, t => Assert.True(Vector3.Dot(Bracket.Normal(t), f.Normal) > 0.9998f)));
    }

    [Fact]
    public void A_face_a_little_off_flat_still_stands_on_the_bed()
    {
        // The box's bottom split in two, one half 0.03 mm higher at its far edge: within the tolerance, and under a degree.
        var box = MeshFactory.Box(Vector3.Zero, new Vector3(20, 10, 5));
        var positions = box.Positions.ToArray();
        positions[2] = positions[2] with { Z = 0.03f };

        var bottom = LayFaces.Find(new TriangleMesh(positions, box.Indices)).First(f => f.Normal.Z < -0.99f);

        Assert.Equal(200, bottom.ContactArea, 0);
    }

    [Fact]
    public void Candidates_lay_their_face_down_and_follow_the_settings()
    {
        var all = OrientationCandidates.Find(Bracket, Matrix4x4.Identity, new SweepSettings());
        var some = OrientationCandidates.Find(Bracket, Matrix4x4.Identity, new SweepSettings(MinContact: 100));
        var three = OrientationCandidates.Find(Bracket, Matrix4x4.Identity, new SweepSettings(MinContact: 50, Count: 3));

        Assert.False(new SweepSettings().CanStart);
        Assert.Equal(6, all.Count);
        Assert.Equal(4, some.Count);
        Assert.Equal([Cap, Cap, 320], three.Select(c => c.Face.ContactArea), new Near(0.1f));
        Assert.Equal([1, 2, 3, 4, 5, 6], all.Select(c => c.Number));
        foreach (var candidate in all)
        {
            // Its face down, and what stands on the bed at the bottom of the part.
            Assert.Equal(-1, Vector3.TransformNormal(candidate.Face.Normal, candidate.Matrix).Z, 4);
            var standing = Bracket.Transformed(candidate.Matrix);
            Assert.All(candidate.Face.Contact, t => Assert.Equal(standing.Bounds.Min.Z, standing.Triangle(t).A.Z, 3));
            Assert.Equal(standing.Bounds.Size.Z, candidate.Height, 3);
        }
        Assert.Equal([8, 8, 40, 40, 40, 40], all.Select(c => MathF.Round(c.Height, 3)));
        // Lying as modelled, it needs no turn at all.
        Assert.Equal(Vector3.Zero, all.Single(c => c.Face.Normal.Z < -0.99f).Rotation);
    }

    [Fact]
    public void Each_face_can_also_be_tried_spun_on_the_bed()
    {
        var plain = OrientationCandidates.Find(Bracket, Matrix4x4.Identity, new SweepSettings(MinContact: 300));
        var spun = OrientationCandidates.Find(Bracket, Matrix4x4.Identity, new SweepSettings(MinContact: 300, Spins: true));

        Assert.Equal(3 * plain.Count, spun.Count);
        Assert.Equal([0, 45, 90, 0, 45, 90], spun.Take(6).Select(c => c.Spin));
        foreach (var candidate in spun)
        {
            Assert.Equal(-1, Vector3.TransformNormal(candidate.Face.Normal, candidate.Matrix).Z, 4);
            Assert.Equal(plain.Single(c => c.Face.Normal == candidate.Face.Normal).Height, candidate.Height, 3);
        }
    }

    [Fact]
    public void Overhangs_are_what_faces_down_off_the_bed()
    {
        var candidates = OrientationCandidates.Find(Bracket, Matrix4x4.Identity, new SweepSettings());
        var flat = candidates.Single(c => c.Face.Normal.Z < -0.99f);
        var onSide = candidates.First(c => c.Face.ContactArea is > 300 and < 340);

        Assert.Equal(0, flat.OverhangArea);
        // On its side, the bolt holes run across: the tops of their walls face down.
        Assert.True(onSide.OverhangArea > 0);
        // On a box, only what lies on the bed faces down; a box held up beside a small one on the bed hangs over with its whole bottom.
        Assert.Equal(0, OrientationCandidates.OverhangArea(MeshFactory.Box(Vector3.Zero, Vector3.One)));
        var held = Join(MeshFactory.Box(new Vector3(0, 0, 2), new Vector3(1, 1, 3)), MeshFactory.Box(new Vector3(-1, -1, 0), new Vector3(-0.5f, -0.5f, 0.5f)));
        Assert.Equal(1, OrientationCandidates.OverhangArea(held), 3);
    }

    /// <summary>Two meshes as one.</summary>
    static TriangleMesh Join(TriangleMesh a, TriangleMesh b) =>
        new([.. a.Positions, .. b.Positions], [.. a.Indices, .. b.Indices.Select(i => i + a.Positions.Length)]);

    sealed class Near(float tolerance) : IEqualityComparer<float>
    {
        public bool Equals(float x, float y) => MathF.Abs(x - y) <= tolerance;
        public int GetHashCode(float obj) => 0;
    }
}
