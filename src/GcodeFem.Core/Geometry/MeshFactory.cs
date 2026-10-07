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

    /// <summary>
    /// The L-bracket with <paramref name="holes"/> round holes through its X leg, for bolts: one in the
    /// middle of each <paramref name="width"/>-long stretch, counted from the end of the leg.
    /// </summary>
    /// <param name="segments">Flat pieces per hole wall; a multiple of 4.</param>
    public static TriangleMesh LBracket(float leg, float width, float height, int holes, float holeDiameter, int segments = 32)
    {
        if (holes < 0 || holes * width > leg - width) throw new ArgumentException($"{holes} holes do not fit in the leg.", nameof(holes));
        if (holes > 0 && !(holeDiameter > 0 && holeDiameter < width)) throw new ArgumentException("The hole must be narrower than the leg.", nameof(holeDiameter));
        if (segments < 8 || segments % 4 != 0) throw new ArgumentException("A multiple of 4, at least 8.", nameof(segments));

        // Rectangles that meet along whole sides, so the caps come out as one connected face.
        var free = leg - holes * width;
        var cells = new List<(Vector2 Min, Vector2 Max, float Hole)>
        {
            (new(0, 0), new(width, width), 0),
            (new(0, width), new(width, leg), 0),
        };
        if (free > width) cells.Add((new(width, 0), new(free, width), 0));
        for (var n = 0; n < holes; n++)
            cells.Add((new(free + n * width, 0), new(free + (n + 1) * width, width), holeDiameter / 2));
        return Plate(cells, height, segments);
    }

    /// <summary>
    /// A plate from z = 0 to <paramref name="height"/> made of rectangles side by side. A rectangle
    /// with a <c>Hole</c> radius must be square and gets a round hole through its middle.
    /// Neighbours must share whole sides, or the caps and walls will not join up.
    /// </summary>
    static TriangleMesh Plate(IReadOnlyList<(Vector2 Min, Vector2 Max, float Hole)> cells, float height, int segments)
    {
        var points = new List<Vector2>();
        var lookup = new Dictionary<Vector2, int>();
        int Point(Vector2 p)
        {
            if (!lookup.TryGetValue(p, out var index))
            {
                lookup[p] = index = points.Count;
                points.Add(p);
            }
            return index;
        }

        // The outline of one cap, counter-clockwise seen from above.
        var cap = new List<(int A, int B, int C)>();
        foreach (var (min, max, hole) in cells)
        {
            int[] corners = [Point(min), Point(new(max.X, min.Y)), Point(max), Point(new(min.X, max.Y))];
            if (hole <= 0)
            {
                cap.Add((corners[0], corners[1], corners[2]));
                cap.Add((corners[0], corners[2], corners[3]));
                continue;
            }

            var centre = (min + max) / 2;
            var rim = Enumerable.Range(0, segments).Select(k => Point(centre + hole * new Vector2(MathF.Cos(2 * MathF.PI * k / segments), MathF.Sin(2 * MathF.PI * k / segments)))).ToArray();
            for (var side = 0; side < 4; side++)
            {
                // Corner 'side' looks at the eighth of the rim before its diagonal and the one after it;
                // between two corners, a triangle to the rim point straight out from the centre.
                var corner = corners[(side + 2) % 4];
                var first = side * segments / 4;
                for (var k = first; k < first + segments / 4; k++) cap.Add((corner, rim[(k + 1) % segments], rim[k]));
                cap.Add((corners[(side + 1) % 4], corner, rim[first]));
            }
        }

        var count = points.Count;
        var positions = new Vector3[2 * count];
        for (var i = 0; i < count; i++)
        {
            positions[i] = new Vector3(points[i], 0);
            positions[count + i] = new Vector3(points[i], height);
        }

        var indices = new List<int>();
        var edges = new HashSet<(int, int)>();
        foreach (var (a, b, c) in cap)
        {
            indices.AddRange([a, c, b]);                         // bottom, facing down
            indices.AddRange([count + a, count + b, count + c]); // top
            edges.UnionWith([(a, b), (b, c), (c, a)]);
        }
        // An edge that no neighbouring triangle runs back along is on the outline or on a hole: it gets a wall.
        foreach (var (a, b) in edges)
            if (!edges.Contains((b, a)))
                indices.AddRange([a, b, count + b, a, count + b, count + a]);
        return new TriangleMesh(positions, [.. indices]);
    }

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
