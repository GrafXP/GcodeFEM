using System.Numerics;

namespace GcodeFem.Core.Geometry;

/// <summary>
/// The convex hull of a set of points, by quickhull: start from a tetrahedron of four extreme points,
/// then keep taking the point farthest outside a facet and replacing the facets it sees by a fan from
/// it to their rim. A point within <see cref="Tolerance"/> of a facet counts as on it. The flat faces
/// of a model are coplanar only to the rounding of its floats, and without that margin they would
/// break into slivers that see each other. Worked in double precision.
/// </summary>
public sealed class ConvexHull
{
    /// <summary>Three point indices per facet, counter-clockwise seen from outside.</summary>
    public required int[] Facets { get; init; }

    public required Vector3[] Points { get; init; }

    /// <summary>How far outside a facet a point must lie to count (mm): a millionth of the points' extent, and at least a nanometre.</summary>
    public required double Tolerance { get; init; }

    public int FacetCount => Facets.Length / 3;

    /// <summary>The facet's outward unit normal.</summary>
    public Vector3 Normal(int facet)
    {
        var n = Cross(facet);
        return Vector3.Normalize(new Vector3((float)n.X, (float)n.Y, (float)n.Z));
    }

    public double Area(int facet) => Cross(facet).Length / 2;

    D3 Cross(int facet)
    {
        D3 a = Points[Facets[3 * facet]], b = Points[Facets[3 * facet + 1]], c = Points[Facets[3 * facet + 2]];
        return D3.Cross(b - a, c - a);
    }

    /// <summary>The hull; no facets if the points do not span a volume (fewer than four, or all on one plane).</summary>
    public static ConvexHull Of(IReadOnlyList<Vector3> points)
    {
        var p = new D3[points.Count];
        for (var i = 0; i < p.Length; i++) p[i] = points[i];
        var tolerance = p.Length == 0 ? 1e-6 : Math.Max(1e-6, 1e-6 * Extent(p));
        var empty = new ConvexHull { Facets = [], Points = [.. points], Tolerance = tolerance };
        if (Simplex(p, tolerance) is not { } corners) return empty;

        var builder = new Builder(p, tolerance);
        builder.Start(corners);
        builder.Run();
        return new ConvexHull { Facets = builder.Facets(), Points = [.. points], Tolerance = tolerance };
    }

    static double Extent(D3[] p)
    {
        D3 low = p[0], high = p[0];
        foreach (var q in p) (low, high) = (D3.Min(low, q), D3.Max(high, q));
        return (high - low).Length;
    }

    /// <summary>Four points that span a tetrahedron of some size, from the extremes along the axes; null if there are none.</summary>
    static (int, int, int, int)? Simplex(D3[] p, double tolerance)
    {
        if (p.Length < 4) return null;
        Span<int> extremes = stackalloc int[6];
        for (var i = 0; i < p.Length; i++)
            for (var axis = 0; axis < 3; axis++)
            {
                if (p[i][axis] < p[extremes[2 * axis]][axis]) extremes[2 * axis] = i;
                if (p[i][axis] > p[extremes[2 * axis + 1]][axis]) extremes[2 * axis + 1] = i;
            }

        // The two extremes farthest apart, the point farthest from their line, the point farthest from their plane.
        var (a, b, best) = (0, 0, -1.0);
        foreach (var i in extremes)
        foreach (var j in extremes)
            if ((p[i] - p[j]).Length is var d && d > best) (a, b, best) = (i, j, d);
        if (best <= tolerance) return null;

        var line = (p[b] - p[a]) / best;
        var (c, far) = (-1, tolerance);
        for (var i = 0; i < p.Length; i++)
            if (D3.Cross(p[i] - p[a], line).Length is var d && d > far) (c, far) = (i, d);
        if (c < 0) return null;

        var normal = D3.Cross(p[b] - p[a], p[c] - p[a]).Unit();
        var (e, off) = (-1, tolerance);
        for (var i = 0; i < p.Length; i++)
            if (Math.Abs(D3.Dot(p[i] - p[a], normal)) is var d && d > off) (e, off) = (i, d);
        return e < 0 ? null : (a, b, c, e);
    }

    sealed class Face(int a, int b, int c, D3 normal, double offset)
    {
        public readonly int A = a, B = b, C = c;
        public readonly D3 Normal = normal;
        public readonly double Offset = offset;
        public List<int>? Outside;
        public int Farthest = -1;
        public double FarthestDistance;
        public bool Alive = true;

        public double Distance(D3 point) => D3.Dot(Normal, point) - Offset;

        public void Add(int point, double distance)
        {
            (Outside ??= []).Add(point);
            if (distance > FarthestDistance) (Farthest, FarthestDistance) = (point, distance);
        }
    }

    sealed class Builder(D3[] p, double tolerance)
    {
        readonly List<Face> faces = [];

        // The face each directed edge belongs to; the face across an edge owns it reversed.
        readonly Dictionary<long, int> edges = [];

        static long Key(int from, int to) => ((long)from << 32) | (uint)to;

        public void Start((int, int, int, int) corners)
        {
            var (a, b, c, d) = corners;
            var inside = (p[a] + p[b] + p[c] + p[d]) / 4;
            foreach (var (x, y, z) in new[] { (a, b, c), (a, b, d), (a, c, d), (b, c, d) })
            {
                // Each face turned to have the middle of the tetrahedron behind it.
                var normal = D3.Cross(p[y] - p[x], p[z] - p[x]).Unit();
                if (D3.Dot(normal, inside - p[x]) > 0) Add(x, z, y);
                else Add(x, y, z);
            }
            var start = new HashSet<int> { a, b, c, d };
            var all = Enumerable.Range(0, faces.Count).ToList();
            for (var i = 0; i < p.Length; i++)
                if (!start.Contains(i)) Assign(i, all);
        }

        public void Run()
        {
            var pending = new Stack<int>(Enumerable.Range(0, faces.Count));
            var visible = new List<int>();
            var seen = new HashSet<int>();
            var horizon = new List<(int From, int To)>();
            var created = new List<int>();
            while (pending.TryPop(out var start))
            {
                var face = faces[start];
                if (!face.Alive || face.Outside is null || face.Outside.Count == 0) continue;
                var eye = face.Farthest;

                // The faces the point sees, found across their edges from the first, and the rim around them.
                visible.Clear();
                seen.Clear();
                horizon.Clear();
                visible.Add(start);
                seen.Add(start);
                for (var n = 0; n < visible.Count; n++)
                {
                    var f = faces[visible[n]];
                    foreach (var (from, to) in new[] { (f.A, f.B), (f.B, f.C), (f.C, f.A) })
                    {
                        var across = edges[Key(to, from)];
                        if (seen.Contains(across)) continue;
                        if (faces[across].Distance(p[eye]) > tolerance)
                        {
                            seen.Add(across);
                            visible.Add(across);
                        }
                    }
                }
                foreach (var v in visible)
                {
                    var f = faces[v];
                    foreach (var (from, to) in new[] { (f.A, f.B), (f.B, f.C), (f.C, f.A) })
                        if (!seen.Contains(edges[Key(to, from)])) horizon.Add((from, to));
                }

                var orphans = new List<int>();
                foreach (var v in visible)
                {
                    var f = faces[v];
                    f.Alive = false;
                    edges.Remove(Key(f.A, f.B));
                    edges.Remove(Key(f.B, f.C));
                    edges.Remove(Key(f.C, f.A));
                    if (f.Outside is not null) orphans.AddRange(f.Outside);
                }

                created.Clear();
                foreach (var (from, to) in horizon) created.Add(Add(from, to, eye));
                foreach (var orphan in orphans)
                    if (orphan != eye) Assign(orphan, created);
                foreach (var f in created) pending.Push(f);
            }
        }

        public int[] Facets() => [.. faces.Where(f => f.Alive).SelectMany(f => new[] { f.A, f.B, f.C })];

        int Add(int a, int b, int c)
        {
            var cross = D3.Cross(p[b] - p[a], p[c] - p[a]);
            var normal = cross.Length > 0 ? cross / cross.Length : new D3(0, 0, 1);
            faces.Add(new Face(a, b, c, normal, D3.Dot(normal, p[a])));
            var index = faces.Count - 1;
            edges[Key(a, b)] = index;
            edges[Key(b, c)] = index;
            edges[Key(c, a)] = index;
            return index;
        }

        /// <summary>To the face it lies farthest outside of; a point outside none of them is inside the hull for good.</summary>
        void Assign(int point, List<int> candidates)
        {
            var (best, farthest) = (-1, tolerance);
            foreach (var f in candidates)
                if (faces[f].Distance(p[point]) is var d && d > farthest) (best, farthest) = (f, d);
            if (best >= 0) faces[best].Add(point, farthest);
        }
    }

    readonly record struct D3(double X, double Y, double Z)
    {
        public double this[int axis] => axis == 0 ? X : axis == 1 ? Y : Z;
        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
        public D3 Unit() => this / Length;

        public static implicit operator D3(Vector3 v) => new(v.X, v.Y, v.Z);
        public static D3 operator +(D3 a, D3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static D3 operator -(D3 a, D3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static D3 operator /(D3 a, double s) => new(a.X / s, a.Y / s, a.Z / s);
        public static double Dot(D3 a, D3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static D3 Cross(D3 a, D3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public static D3 Min(D3 a, D3 b) => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
        public static D3 Max(D3 a, D3 b) => new(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
    }
}
