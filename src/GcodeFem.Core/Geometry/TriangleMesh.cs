using System.Numerics;

namespace GcodeFem.Core.Geometry;

/// <summary>
/// Indexed triangle mesh, units mm. Vertices are welded: triangles that share a corner share
/// an index, which face-region picking relies on.
/// </summary>
public sealed class TriangleMesh
{
    public TriangleMesh(Vector3[] positions, int[] indices)
    {
        if (indices.Length % 3 != 0)
            throw new ArgumentException("Index count must be a multiple of 3.", nameof(indices));
        foreach (var i in indices)
            if ((uint)i >= (uint)positions.Length)
                throw new ArgumentException($"Index {i} is out of range.", nameof(indices));

        Positions = positions;
        Indices = indices;
        Bounds = Box3.Of(positions);
    }

    public Vector3[] Positions { get; }
    public int[] Indices { get; }
    public Box3 Bounds { get; }
    public int TriangleCount => Indices.Length / 3;

    public (Vector3 A, Vector3 B, Vector3 C) Triangle(int t) =>
        (Positions[Indices[3 * t]], Positions[Indices[3 * t + 1]], Positions[Indices[3 * t + 2]]);

    /// <summary>Unit normal from the winding (counter-clockwise = outward); +Z for slivers.</summary>
    public Vector3 Normal(int t)
    {
        var (a, b, c) = Triangle(t);
        var n = Vector3.Cross(b - a, c - a);
        var length = n.Length();
        return length > 1e-12f ? n / length : Vector3.UnitZ;
    }

    /// <summary>Applies an affine transform; a mirroring transform flips the winding back to outward.</summary>
    public TriangleMesh Transformed(Matrix4x4 transform)
    {
        var positions = new Vector3[Positions.Length];
        for (int i = 0; i < positions.Length; i++)
            positions[i] = Vector3.Transform(Positions[i], transform);

        var indices = (int[])Indices.Clone();
        if (transform.GetDeterminant() < 0)
            for (int t = 0; t < indices.Length; t += 3)
                (indices[t + 1], indices[t + 2]) = (indices[t + 2], indices[t + 1]);

        return new TriangleMesh(positions, indices);
    }
}
