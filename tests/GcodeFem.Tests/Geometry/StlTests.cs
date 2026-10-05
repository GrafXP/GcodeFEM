using System.Numerics;
using GcodeFem.Core.Geometry;

namespace GcodeFem.Tests.Geometry;

public class StlTests
{
    [Fact]
    public void Reads_ascii_cube_and_welds_corners()
    {
        var mesh = Stl.Read(TestPaths.Sample("cube20.stl"));

        Assert.Equal(12, mesh.TriangleCount);
        Assert.Equal(8, mesh.Positions.Length);
        Assert.Equal(new Box3(Vector3.Zero, new Vector3(20)), mesh.Bounds);
    }

    [Fact]
    public void Cube_normals_point_outward()
    {
        var mesh = Stl.Read(TestPaths.Sample("cube20.stl"));
        var center = mesh.Bounds.Center;

        for (int t = 0; t < mesh.TriangleCount; t++)
        {
            var (a, b, c) = mesh.Triangle(t);
            var faceCenter = (a + b + c) / 3;
            Assert.True(Vector3.Dot(mesh.Normal(t), faceCenter - center) > 0, $"triangle {t} faces inward");
        }
    }

    [Fact]
    public void Binary_round_trip_keeps_geometry()
    {
        var original = Stl.Read(TestPaths.Sample("cube20.stl"));
        var path = Path.Combine(Path.GetTempPath(), $"gcodefem-{Guid.NewGuid():N}.stl");
        try
        {
            Stl.WriteBinary(original, path);
            var copy = Stl.Read(path);

            Assert.Equal(original.TriangleCount, copy.TriangleCount);
            Assert.Equal(original.Positions.Length, copy.Positions.Length);
            Assert.Equal(original.Bounds, copy.Bounds);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Rotation_moves_bounds_and_keeps_normals_outward()
    {
        var mesh = Stl.Read(TestPaths.Sample("cube20.stl"))
            .Transformed(Matrix4x4.CreateRotationX(MathF.PI / 2));

        var bounds = mesh.Bounds;
        Assert.Equal(0, bounds.Min.X, 4);
        Assert.Equal(-20, bounds.Min.Y, 4);
        Assert.Equal(0, bounds.Min.Z, 4);
        Assert.Equal(new Vector3(20), bounds.Size, new Vector3EqualityComparer(1e-4f));

        var center = bounds.Center;
        for (int t = 0; t < mesh.TriangleCount; t++)
        {
            var (a, b, c) = mesh.Triangle(t);
            Assert.True(Vector3.Dot(mesh.Normal(t), (a + b + c) / 3 - center) > 0);
        }
    }

    sealed class Vector3EqualityComparer(float tolerance) : IEqualityComparer<Vector3>
    {
        public bool Equals(Vector3 x, Vector3 y) => Vector3.Distance(x, y) <= tolerance;
        public int GetHashCode(Vector3 obj) => 0;
    }
}
