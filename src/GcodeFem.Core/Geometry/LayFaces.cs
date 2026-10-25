using System.Numerics;

namespace GcodeFem.Core.Geometry;

/// <summary>A face the part can be laid on: a plane of its convex hull, and where the model touches it.</summary>
/// <param name="Normal">Outward, in the part frame; laid on the bed, it points straight down.</param>
/// <param name="ContactArea">mm² of the model's own triangles in the plane: what stands on the bed and holds the print to it.</param>
/// <param name="Contact">Those triangles, ascending.</param>
public sealed record LayFace(Vector3 Normal, float ContactArea, int[] Contact);

/// <summary>
/// The faces a part can rest on, as with Bambu Studio's "lay on face". A part rests on a plane of its
/// convex hull only: a flat face in a pocket or behind a step has something else in front of it. The
/// hull's facets are merged into planes where their normals differ by under a degree. What stands on
/// the bed is the model's own triangles in that plane; a part that rests on edges or points there (a
/// ring on its end touches with its rim, not with the disc its hull spans) has no contact.
/// </summary>
public static class LayFaces
{
    /// <summary>A triangle within this of a plane (mm), facing the same way, stands on the bed.</summary>
    public const float ContactTolerance = 0.05f;

    /// <summary>Hull facets whose normals differ by less than this make one plane, and a triangle must face the plane's way to within it.</summary>
    public const float MergeDegrees = 1;

    /// <summary>Every face with contact, the largest contact first.</summary>
    public static List<LayFace> Find(TriangleMesh part)
    {
        var hull = ConvexHull.Of(part.Positions);
        var close = MathF.Cos(MergeDegrees * MathF.PI / 180);

        // Facets merged into planes, the largest first so that each plane's normal is set by its main facets.
        var planes = new List<(Vector3 Sum, Vector3 First)>();
        foreach (var f in Enumerable.Range(0, hull.FacetCount).OrderByDescending(hull.Area))
        {
            var normal = hull.Normal(f);
            var weight = (float)hull.Area(f);
            var at = planes.FindIndex(plane => Vector3.Dot(plane.First, normal) > close);
            if (at < 0) planes.Add((weight * normal, normal));
            else planes[at] = (planes[at].Sum + weight * normal, planes[at].First);
        }

        // Each plane touches the part at its outermost points, which are corners of the hull.
        var corners = hull.Facets.Distinct().Select(i => part.Positions[i]).ToArray();
        var normals = planes.Where(plane => plane.Sum.LengthSquared() > 0).Select(plane => Vector3.Normalize(plane.Sum)).ToArray();
        var offsets = normals.Select(n => corners.Max(p => Vector3.Dot(n, p))).ToArray();

        // The planes by their normals on a coarse grid, so that each triangle looks only at the planes it may lie in.
        var near = new Dictionary<(int, int, int), List<int>>();
        for (var plane = 0; plane < normals.Length; plane++)
        {
            var (x, y, z) = Cell(normals[plane]);
            for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
            for (var dz = -1; dz <= 1; dz++)
            {
                if (!near.TryGetValue((x + dx, y + dy, z + dz), out var list)) near[(x + dx, y + dy, z + dz)] = list = [];
                list.Add(plane);
            }
        }

        var contact = normals.Select(_ => new List<int>()).ToArray();
        var area = new double[normals.Length];
        for (var t = 0; t < part.TriangleCount; t++)
        {
            var normal = part.Normal(t);
            if (!near.TryGetValue(Cell(normal), out var candidates)) continue;
            var (a, b, c) = part.Triangle(t);
            foreach (var plane in candidates)
            {
                var n = normals[plane];
                if (Vector3.Dot(normal, n) <= close) continue;
                var offset = offsets[plane];
                if (offset - Vector3.Dot(n, a) > ContactTolerance || offset - Vector3.Dot(n, b) > ContactTolerance || offset - Vector3.Dot(n, c) > ContactTolerance) continue;
                contact[plane].Add(t);
                area[plane] += Vector3.Cross(b - a, c - a).Length() / 2;
            }
        }

        return [.. Enumerable.Range(0, normals.Length).Where(plane => contact[plane].Count > 0)
            .Select(plane => new LayFace(normals[plane], (float)area[plane], [.. contact[plane]]))
            .OrderByDescending(face => face.ContactArea)];

        // Cells of a twentieth: normals a degree apart are in the same cell or next to it.
        static (int, int, int) Cell(Vector3 n) => ((int)MathF.Round(n.X * 20), (int)MathF.Round(n.Y * 20), (int)MathF.Round(n.Z * 20));
    }
}
