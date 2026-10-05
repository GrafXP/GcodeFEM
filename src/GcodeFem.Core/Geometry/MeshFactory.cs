using System.Numerics;

namespace GcodeFem.Core.Geometry;

/// <summary>Simple test and benchmark shapes.</summary>
public static class MeshFactory
{
    /// <summary>Axis-aligned box with outward (counter-clockwise) winding.</summary>
    public static TriangleMesh Box(Vector3 min, Vector3 max)
    {
        Vector3[] corners =
        [
            new(min.X, min.Y, min.Z), new(max.X, min.Y, min.Z), new(max.X, max.Y, min.Z), new(min.X, max.Y, min.Z),
            new(min.X, min.Y, max.Z), new(max.X, min.Y, max.Z), new(max.X, max.Y, max.Z), new(min.X, max.Y, max.Z),
        ];
        int[] indices =
        [
            0, 2, 1, 0, 3, 2, // bottom
            4, 5, 6, 4, 6, 7, // top
            0, 1, 5, 0, 5, 4, // front (-Y)
            1, 2, 6, 1, 6, 5, // right (+X)
            2, 3, 7, 2, 7, 6, // back (+Y)
            3, 0, 4, 3, 4, 7, // left (-X)
        ];
        return new TriangleMesh(corners, indices);
    }
}
