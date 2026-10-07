using System.Numerics;

namespace GcodeFem.Core.Geometry;

/// <summary>Which triangles of a mesh share an edge.</summary>
public sealed class MeshTopology
{
    readonly int[] start;
    readonly int[] across;

    MeshTopology(int[] start, int[] across) => (this.start, this.across) = (start, across);

    /// <summary>The triangles that share an edge with <paramref name="triangle"/>.</summary>
    public ReadOnlySpan<int> Neighbours(int triangle) => across.AsSpan(start[triangle], start[triangle + 1] - start[triangle]);

    public static MeshTopology Of(TriangleMesh mesh)
    {
        // One entry per triangle side, keyed by its two corners; sides with the same key share an edge.
        var sides = mesh.Indices.Length;
        var keys = new long[sides];
        var owners = new int[sides];
        for (var s = 0; s < sides; s++)
        {
            int a = mesh.Indices[s], b = mesh.Indices[s - s % 3 + (s + 1) % 3];
            keys[s] = ((long)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
            owners[s] = s / 3;
        }
        Array.Sort(keys, owners);

        var start = new int[mesh.TriangleCount + 1];
        for (int from = 0, to; from < sides; from = to)
        {
            for (to = from + 1; to < sides && keys[to] == keys[from]; to++) { }
            for (var s = from; s < to; s++) start[owners[s] + 1] += to - from - 1;
        }
        for (var t = 0; t < mesh.TriangleCount; t++) start[t + 1] += start[t];

        var across = new int[start[^1]];
        var next = (int[])start.Clone();
        for (int from = 0, to; from < sides; from = to)
        {
            for (to = from + 1; to < sides && keys[to] == keys[from]; to++) { }
            for (var s = from; s < to; s++)
            for (var other = from; other < to; other++)
                if (other != s) across[next[owners[s]]++] = owners[other];
        }
        return new MeshTopology(start, across);
    }
}

/// <summary>Picks the triangles that make up one face of a model.</summary>
public static class FaceRegions
{
    /// <summary>
    /// The triangles reached from <paramref name="seed"/> across shared edges, as long as the two
    /// triangles at an edge differ in direction by at most <paramref name="maxAngleDegrees"/>: a flat
    /// face up to its edges, or the wall of a round hole all the way round. Ascending.
    /// </summary>
    public static int[] Grow(TriangleMesh mesh, MeshTopology topology, int seed, float maxAngleDegrees)
    {
        var limit = MathF.Cos(Math.Clamp(maxAngleDegrees, 0, 180) * MathF.PI / 180) - 1e-5f;
        var region = new HashSet<int> { seed };
        var pending = new Queue<(int Triangle, Vector3 Normal)>();
        pending.Enqueue((seed, mesh.Normal(seed)));
        while (pending.TryDequeue(out var from))
            foreach (var next in topology.Neighbours(from.Triangle))
            {
                if (region.Contains(next)) continue;
                // A sliver has no direction to speak of: it goes with whatever it lies in, and passes that direction on.
                var normal = IsSliver(mesh, next) ? from.Normal : mesh.Normal(next);
                if (Vector3.Dot(from.Normal, normal) < limit) continue;
                region.Add(next);
                pending.Enqueue((next, normal));
            }
        return [.. region.Order()];
    }

    /// <summary>The triangles lying in the plane <paramref name="normal"/> · p = <paramref name="offset"/>, such as one side of the model's bounding box. Ascending.</summary>
    public static int[] OnPlane(TriangleMesh mesh, Vector3 normal, float offset, float tolerance = 1e-3f)
    {
        bool On(Vector3 p) => MathF.Abs(Vector3.Dot(normal, p) - offset) <= tolerance;
        var found = new List<int>();
        for (var t = 0; t < mesh.TriangleCount; t++)
        {
            var (a, b, c) = mesh.Triangle(t);
            if (On(a) && On(b) && On(c)) found.Add(t);
        }
        return [.. found];
    }

    /// <summary>Thinner than a ten-thousandth of its length: its computed normal is noise.</summary>
    static bool IsSliver(TriangleMesh mesh, int triangle)
    {
        var (a, b, c) = mesh.Triangle(triangle);
        var longest = MathF.Max((b - a).LengthSquared(), MathF.Max((c - b).LengthSquared(), (a - c).LengthSquared()));
        return Vector3.Cross(b - a, c - a).Length() <= 1e-4f * longest;
    }
}

/// <summary>Finds the triangle under the mouse, or the one next to a point.</summary>
public static class MeshPicker
{
    /// <summary>The first triangle a ray meets, from either side, and how far along the ray (mm); null if it misses the mesh.</summary>
    public static (int Triangle, float Distance)? Pick(TriangleMesh mesh, Vector3 origin, Vector3 direction)
    {
        direction = Vector3.Normalize(direction);
        const float edge = 1e-5f; // a ray through a shared edge must not slip between the two triangles
        var (best, nearest) = (-1, float.PositiveInfinity);
        for (var t = 0; t < mesh.TriangleCount; t++)
        {
            var (a, b, c) = mesh.Triangle(t);
            Vector3 e1 = b - a, e2 = c - a;
            var h = Vector3.Cross(direction, e2);
            var det = Vector3.Dot(e1, h);
            if (MathF.Abs(det) < 1e-12f) continue;
            var s = origin - a;
            var u = Vector3.Dot(s, h) / det;
            if (u < -edge || u > 1 + edge) continue;
            var q = Vector3.Cross(s, e1);
            var v = Vector3.Dot(direction, q) / det;
            if (v < -edge || u + v > 1 + edge) continue;
            var distance = Vector3.Dot(e2, q) / det;
            if (distance > 1e-6f && distance < nearest) (best, nearest) = (t, distance);
        }
        return best < 0 ? null : (best, nearest);
    }

    /// <summary>The triangle closest to a point, and how far away it is.</summary>
    public static (int Triangle, float Distance) Nearest(TriangleMesh mesh, Vector3 point)
    {
        var (best, nearest) = (-1, float.PositiveInfinity);
        for (var t = 0; t < mesh.TriangleCount; t++)
        {
            var (a, b, c) = mesh.Triangle(t);
            var distance = Vector3.DistanceSquared(point, TriangleMath.ClosestPoint(point, a, b, c));
            if (distance < nearest) (best, nearest) = (t, distance);
        }
        return (best, MathF.Sqrt(nearest));
    }
}

static class TriangleMath
{
    /// <summary>The point of triangle a, b, c closest to p (Ericson, Real-Time Collision Detection, 5.1.5).</summary>
    public static Vector3 ClosestPoint(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a, ac = c - a, ap = p - a;
        float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0) return a;

        var bp = p - b;
        float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3) return b;

        var vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0) return a + ab * (d1 / (d1 - d3));

        var cp = p - c;
        float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6) return c;

        var vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0) return a + ac * (d2 / (d2 - d6));

        var va = d3 * d6 - d5 * d4;
        if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return b + (c - b) * ((d4 - d3) / (d4 - d3 + d5 - d6));

        var sum = va + vb + vc;
        if (!(MathF.Abs(sum) > 0)) return a; // a triangle with no area
        return a + ab * (vb / sum) + ac * (vc / sum);
    }
}
