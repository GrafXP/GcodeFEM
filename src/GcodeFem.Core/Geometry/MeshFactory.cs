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

    /// <summary>
    /// An L-shaped bracket lying flat: two legs of <paramref name="width"/> along +X and +Y, each
    /// <paramref name="leg"/> long from the outer corner at the origin, <paramref name="height"/> thick.
    /// Bending it concentrates stress in the inner corner.
    /// </summary>
    public static TriangleMesh LBracket(float leg, float width, float height) =>
        Prism([new(0, 0), new(leg, 0), new(leg, width), new(width, width), new(width, leg), new(0, leg)], height);

    /// <summary>A simple polygon in XY (counter-clockwise, no holes) extruded from z = 0 to <paramref name="height"/>.</summary>
    public static TriangleMesh Prism(IReadOnlyList<Vector2> outline, float height)
    {
        var n = outline.Count;
        var positions = new Vector3[2 * n];
        for (var i = 0; i < n; i++)
        {
            positions[i] = new Vector3(outline[i], 0);
            positions[n + i] = new Vector3(outline[i], height);
        }

        var indices = new List<int>();
        foreach (var (a, b, c) in Triangulate(outline))
        {
            indices.AddRange([a, c, b]);             // bottom, facing down
            indices.AddRange([n + a, n + b, n + c]); // top
        }
        for (var i = 0; i < n; i++)
        {
            var j = (i + 1) % n;
            indices.AddRange([i, j, n + j, i, n + j, n + i]);
        }
        return new TriangleMesh(positions, [.. indices]);
    }

    /// <summary>Ear clipping: cuts off convex corners that hold no other vertex until a triangle is left.</summary>
    static IEnumerable<(int A, int B, int C)> Triangulate(IReadOnlyList<Vector2> p)
    {
        static float Cross(Vector2 u, Vector2 v) => u.X * v.Y - u.Y * v.X;
        bool Inside(int q, int a, int b, int c) =>
            Cross(p[b] - p[a], p[q] - p[a]) >= 0 && Cross(p[c] - p[b], p[q] - p[b]) >= 0 && Cross(p[a] - p[c], p[q] - p[c]) >= 0;

        var left = Enumerable.Range(0, p.Count).ToList();
        while (left.Count > 3)
        {
            var ear = -1;
            for (var m = 0; m < left.Count && ear < 0; m++)
            {
                int a = left[(m + left.Count - 1) % left.Count], b = left[m], c = left[(m + 1) % left.Count];
                if (Cross(p[b] - p[a], p[c] - p[b]) > 0 && !left.Any(q => q != a && q != b && q != c && Inside(q, a, b, c)))
                    ear = m;
            }
            if (ear < 0) throw new ArgumentException("The outline is not a simple counter-clockwise polygon.", nameof(p));
            yield return (left[(ear + left.Count - 1) % left.Count], left[ear], left[(ear + 1) % left.Count]);
            left.RemoveAt(ear);
        }
        yield return (left[0], left[1], left[2]);
    }
}
