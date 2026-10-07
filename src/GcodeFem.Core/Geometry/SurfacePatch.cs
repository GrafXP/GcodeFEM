using System.Numerics;

namespace GcodeFem.Core.Geometry;

/// <summary>Some of a mesh's triangles taken together as one surface, such as a picked face.</summary>
public sealed class SurfacePatch
{
    public SurfacePatch(TriangleMesh mesh, IEnumerable<int> triangles)
    {
        Mesh = mesh;
        Triangles = [.. triangles.Distinct().Order()];
        if (Triangles.Length == 0) throw new ArgumentException("A patch needs at least one triangle.", nameof(triangles));
        if (Triangles[0] < 0 || Triangles[^1] >= mesh.TriangleCount)
            throw new ArgumentException("The patch names triangles that the model does not have.", nameof(triangles));

        Normals = new Vector3[Triangles.Length];
        double area = 0, cx = 0, cy = 0, cz = 0, nx = 0, ny = 0, nz = 0;
        Vector3 low = new(float.PositiveInfinity), high = new(float.NegativeInfinity);
        for (var n = 0; n < Triangles.Length; n++)
        {
            var (a, b, c) = mesh.Triangle(Triangles[n]);
            Normals[n] = mesh.Normal(Triangles[n]);
            var cross = Vector3.Cross(b - a, c - a);
            var share = cross.Length() / 2.0;
            var centre = (a + b + c) / 3;
            area += share;
            (cx, cy, cz) = (cx + share * centre.X, cy + share * centre.Y, cz + share * centre.Z);
            (nx, ny, nz) = (nx + cross.X / 2.0, ny + cross.Y / 2.0, nz + cross.Z / 2.0);
            low = Vector3.Min(low, Vector3.Min(a, Vector3.Min(b, c)));
            high = Vector3.Max(high, Vector3.Max(a, Vector3.Max(b, c)));
        }
        Area = (float)area;
        Bounds = new Box3(low, high);
        Centroid = area > 0 ? new Vector3((float)(cx / area), (float)(cy / area), (float)(cz / area)) : Bounds.Center;
        MeanNormal = area > 0 ? new Vector3((float)(nx / area), (float)(ny / area), (float)(nz / area)) : Vector3.Zero;
    }

    public TriangleMesh Mesh { get; }

    /// <summary>Indices into the mesh's triangles, ascending.</summary>
    public int[] Triangles { get; }

    /// <summary>Outward unit normal of each of <see cref="Triangles"/>.</summary>
    public Vector3[] Normals { get; }

    /// <summary>mm².</summary>
    public float Area { get; }

    public Vector3 Centroid { get; }

    /// <summary>
    /// The outward normals averaged by area. Its length is 1 for a flat patch, shorter the more the
    /// patch curves, and near zero for the wall of a hole, which faces every way.
    /// </summary>
    public Vector3 MeanNormal { get; }

    public Box3 Bounds { get; }

    /// <summary>A search structure for points up to <paramref name="reach"/> away from the patch.</summary>
    public PatchLocator Locator(float reach) => new(this, reach);

    /// <summary>The patch split into the parts that hang together by shared corners: two holes picked into one patch come apart again.</summary>
    public int[][] Components()
    {
        // Union-find over the triangles, joined through the corners they share.
        var parent = Enumerable.Range(0, Triangles.Length).ToArray();
        int Root(int n)
        {
            while (parent[n] != n) n = parent[n] = parent[parent[n]];
            return n;
        }

        var firstAtCorner = new Dictionary<int, int>();
        for (var n = 0; n < Triangles.Length; n++)
        for (var corner = 0; corner < 3; corner++)
        {
            var vertex = Mesh.Indices[3 * Triangles[n] + corner];
            if (firstAtCorner.TryGetValue(vertex, out var other)) parent[Root(n)] = Root(other);
            else firstAtCorner[vertex] = n;
        }
        return [.. Enumerable.Range(0, Triangles.Length).GroupBy(Root).Select(part => part.Select(n => Triangles[n]).ToArray())];
    }

    /// <summary>
    /// Up to <paramref name="count"/> points on the patch, as far from each other as they will go, each
    /// with the outward normal there: where to put markers so that they cover the patch evenly.
    /// </summary>
    public (Vector3 Point, Vector3 Normal)[] Spread(int count)
    {
        // Candidates: the centres of pieces small enough that a few of them fit between two markers.
        var candidates = new List<(Vector3 Point, Vector3 Normal)>();
        var limit = Area / (4 * Math.Max(1, count));
        for (var n = 0; n < Triangles.Length; n++)
        {
            var (a, b, c) = Mesh.Triangle(Triangles[n]);
            Split(a, b, c, Normals[n], 0);
        }

        void Split(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, int depth)
        {
            if (depth < 6 && Vector3.Cross(b - a, c - a).Length() / 2 > limit)
            {
                Vector3 ab = (a + b) / 2, bc = (b + c) / 2, ca = (c + a) / 2;
                Split(a, ab, ca, normal, depth + 1);
                Split(ab, b, bc, normal, depth + 1);
                Split(ca, bc, c, normal, depth + 1);
                Split(ab, bc, ca, normal, depth + 1);
            }
            else candidates.Add(((a + b + c) / 3, normal));
        }

        // Start in the middle, then always take the candidate furthest from all taken so far.
        var chosen = new List<(Vector3 Point, Vector3 Normal)>();
        var distance = candidates.Select(candidate => Vector3.DistanceSquared(candidate.Point, Centroid)).ToArray();
        var next = Array.IndexOf(distance, distance.Min());
        Array.Fill(distance, float.PositiveInfinity);
        while (chosen.Count < Math.Min(count, candidates.Count))
        {
            chosen.Add(candidates[next]);
            var furthest = -1;
            for (var n = 0; n < candidates.Count; n++)
            {
                distance[n] = MathF.Min(distance[n], Vector3.DistanceSquared(candidates[n].Point, candidates[next].Point));
                if (furthest < 0 || distance[n] > distance[furthest]) furthest = n;
            }
            if (distance[furthest] <= 0) break;
            next = furthest;
        }
        return [.. chosen];
    }
}

/// <summary>
/// Finds the triangle of a <see cref="SurfacePatch"/> next to a point. The triangles are sorted into
/// a grid of boxes, so a lookup only measures the few that can be within reach. Lookups may run in parallel.
/// </summary>
public sealed class PatchLocator
{
    /// <summary>A triangle whose box, with the reach added, spans more grid boxes than this is cut up first.</summary>
    const int MaxBoxesPerPiece = 64;

    readonly SurfacePatch patch;
    readonly Dictionary<long, int[]> boxes = [];
    readonly Vector3 origin;
    readonly float size;
    readonly int countX, countY, countZ;

    internal PatchLocator(SurfacePatch patch, float reach)
    {
        if (!(reach > 0)) throw new ArgumentOutOfRangeException(nameof(reach));
        this.patch = patch;
        Reach = reach;
        size = 2 * reach;
        origin = patch.Bounds.Min - new Vector3(reach);
        var extent = patch.Bounds.Size + new Vector3(2 * reach);
        countX = (int)(extent.X / size) + 1;
        countY = (int)(extent.Y / size) + 1;
        countZ = (int)(extent.Z / size) + 1;

        var lists = new Dictionary<long, List<int>>();
        for (var n = 0; n < patch.Triangles.Length; n++)
        {
            var (a, b, c) = patch.Mesh.Triangle(patch.Triangles[n]);
            Insert(lists, n, a, b, c, 0);
        }
        foreach (var (key, list) in lists) boxes[key] = [.. list];
    }

    public float Reach { get; }

    /// <summary>
    /// The patch triangle nearest to <paramref name="point"/>, as an index into
    /// <see cref="SurfacePatch.Triangles"/>, or −1 if none is within reach. With a
    /// <paramref name="facing"/> direction, only triangles whose normal has more than
    /// <paramref name="minDot"/> of it count.
    /// </summary>
    public int Nearest(Vector3 point, Vector3? facing = null, float minDot = 0)
    {
        var at = (point - origin) / size;
        int i = (int)MathF.Floor(at.X), j = (int)MathF.Floor(at.Y), k = (int)MathF.Floor(at.Z);
        if ((uint)i >= (uint)countX || (uint)j >= (uint)countY || (uint)k >= (uint)countZ) return -1;
        if (!boxes.TryGetValue(Key(i, j, k), out var candidates)) return -1;

        var (best, nearest) = (-1, Reach * Reach);
        foreach (var n in candidates)
        {
            if (facing is { } direction && Vector3.Dot(patch.Normals[n], direction) <= minDot) continue;
            var (a, b, c) = patch.Mesh.Triangle(patch.Triangles[n]);
            var distance = Vector3.DistanceSquared(point, TriangleMath.ClosestPoint(point, a, b, c));
            if (distance <= nearest) (best, nearest) = (n, distance);
        }
        return best;
    }

    /// <summary>Enters triangle <paramref name="index"/> into every box that the piece a, b, c of it can reach.</summary>
    void Insert(Dictionary<long, List<int>> lists, int index, Vector3 a, Vector3 b, Vector3 c, int depth)
    {
        var low = (Vector3.Min(a, Vector3.Min(b, c)) - new Vector3(Reach) - origin) / size;
        var high = (Vector3.Max(a, Vector3.Max(b, c)) + new Vector3(Reach) - origin) / size;
        int i0 = Math.Max(0, (int)MathF.Floor(low.X)), i1 = Math.Min(countX - 1, (int)MathF.Floor(high.X));
        int j0 = Math.Max(0, (int)MathF.Floor(low.Y)), j1 = Math.Min(countY - 1, (int)MathF.Floor(high.Y));
        int k0 = Math.Max(0, (int)MathF.Floor(low.Z)), k1 = Math.Min(countZ - 1, (int)MathF.Floor(high.Z));

        // A big slanted triangle's box holds far more grid boxes than the triangle comes near.
        if (depth < 16 && (long)(i1 - i0 + 1) * (j1 - j0 + 1) * (k1 - k0 + 1) > MaxBoxesPerPiece)
        {
            Vector3 ab = (a + b) / 2, bc = (b + c) / 2, ca = (c + a) / 2;
            Insert(lists, index, a, ab, ca, depth + 1);
            Insert(lists, index, ab, b, bc, depth + 1);
            Insert(lists, index, ca, bc, c, depth + 1);
            Insert(lists, index, ab, bc, ca, depth + 1);
            return;
        }

        for (var k = k0; k <= k1; k++)
        for (var j = j0; j <= j1; j++)
        for (var i = i0; i <= i1; i++)
        {
            var key = Key(i, j, k);
            if (!lists.TryGetValue(key, out var list)) lists[key] = list = [];
            if (list.Count == 0 || list[^1] != index) list.Add(index); // the pieces of one triangle arrive one after the other
        }
    }

    long Key(int i, int j, int k) => i + countX * (j + (long)countY * k);
}

/// <summary>A round hole or pin: what a patch of triangles around a common axis amounts to.</summary>
/// <param name="Centre">The middle of the axis, as far as the patch goes along it.</param>
/// <param name="Axis">Unit vector; its largest component is positive.</param>
/// <param name="IsHole">The surface faces the axis (a bore), not away from it (a pin).</param>
public sealed record Cylinder(Vector3 Centre, Vector3 Axis, float Radius, float Length, bool IsHole)
{
    /// <summary>The cylinder the triangles lie on, or null if they are not the wall of one (an arc of some 50° or more).</summary>
    public static Cylinder? Fit(TriangleMesh mesh, IReadOnlyCollection<int> triangles)
    {
        // Every normal of a cylinder's wall is square to its axis, so the axis is the direction the normals have nothing of.
        var spread = new double[9];
        double area = 0;
        foreach (var t in triangles)
        {
            var (a, b, c) = mesh.Triangle(t);
            var cross = Vector3.Cross(b - a, c - a);
            var twice = cross.Length();
            if (!(twice > 0)) continue;
            double[] n = [cross.X / twice, cross.Y / twice, cross.Z / twice];
            for (var r = 0; r < 3; r++)
            for (var col = 0; col < 3; col++)
                spread[3 * r + col] += twice / 2 * n[r] * n[col];
            area += twice / 2;
        }
        if (!(area > 0)) return null;
        var (values, vectors) = SymmetricEigen.Solve(spread, 3);
        if (values[0] > 0.02 * area || values[1] < 0.05 * area) return null; // not square to one axis, or flat

        var axis = Vector3.Normalize(new Vector3((float)vectors[0][0], (float)vectors[0][1], (float)vectors[0][2]));
        var largest = MathF.Abs(axis.X) >= MathF.Abs(axis.Y) && MathF.Abs(axis.X) >= MathF.Abs(axis.Z) ? axis.X : MathF.Abs(axis.Y) >= MathF.Abs(axis.Z) ? axis.Y : axis.Z;
        if (largest < 0) axis = -axis;
        var u = Vector3.Normalize(Vector3.Cross(axis, MathF.Abs(axis.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY));
        var v = Vector3.Cross(axis, u);

        // Seen along the axis the corners lie on a circle: least squares on x² + y² + a x + b y + c = 0 (Kåsa).
        var corners = triangles.SelectMany(t => new[] { mesh.Indices[3 * t], mesh.Indices[3 * t + 1], mesh.Indices[3 * t + 2] }).Distinct().Select(i => mesh.Positions[i]).ToList();
        var middle = corners.Aggregate(Vector3.Zero, (sum, p) => sum + p) / corners.Count;
        double sxx = 0, sxy = 0, syy = 0, sx = 0, sy = 0, sxz = 0, syz = 0, sz = 0;
        float low = float.PositiveInfinity, high = float.NegativeInfinity;
        foreach (var p in corners)
        {
            double x = Vector3.Dot(p - middle, u), y = Vector3.Dot(p - middle, v), z = x * x + y * y;
            (sxx, sxy, syy, sx, sy) = (sxx + x * x, sxy + x * y, syy + y * y, sx + x, sy + y);
            (sxz, syz, sz) = (sxz + x * z, syz + y * z, sz + z);
            var along = Vector3.Dot(p, axis);
            (low, high) = (MathF.Min(low, along), MathF.Max(high, along));
        }
        double count = corners.Count;
        var det = sxx * (syy * count - sy * sy) - sxy * (sxy * count - sy * sx) + sx * (sxy * sy - syy * sx);
        if (Math.Abs(det) < 1e-12 * Math.Max(1, sxx * syy * count)) return null;
        var qa = (-sxz * (syy * count - sy * sy) - sxy * (-syz * count + sy * sz) + sx * (-syz * sy + syy * sz)) / det;
        var qb = (sxx * (-syz * count + sz * sy) + sxz * (sxy * count - sy * sx) + sx * (-sxy * sz + syz * sx)) / det;
        var qc = (sxx * (-syy * sz + sy * syz) - sxy * (-sxy * sz + sx * syz) - sxz * (sxy * sy - syy * sx)) / det;
        var squared = qa * qa / 4 + qb * qb / 4 - qc;
        if (!(squared > 0)) return null;
        var radius = (float)Math.Sqrt(squared);
        var onAxis = middle + (float)(-qa / 2) * u + (float)(-qb / 2) * v;

        Vector3 Radial(Vector3 p)
        {
            var from = p - onAxis;
            return from - Vector3.Dot(from, axis) * axis;
        }
        if (corners.Any(p => MathF.Abs(Radial(p).Length() - radius) > 0.03f * radius + 1e-3f)) return null;

        double outward = 0;
        foreach (var t in triangles)
        {
            var (a, b, c) = mesh.Triangle(t);
            outward += Vector3.Dot(Vector3.Cross(b - a, c - a), Radial((a + b + c) / 3));
        }
        var centre = onAxis + (((low + high) / 2) - Vector3.Dot(onAxis, axis)) * axis;
        return new Cylinder(centre, axis, radius, high - low, IsHole: outward < 0);
    }
}
