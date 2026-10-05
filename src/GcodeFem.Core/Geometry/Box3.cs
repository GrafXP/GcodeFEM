using System.Numerics;

namespace GcodeFem.Core.Geometry;

/// <summary>Axis-aligned bounding box. Units: mm.</summary>
public readonly record struct Box3(Vector3 Min, Vector3 Max)
{
    public Vector3 Size => Max - Min;
    public Vector3 Center => (Min + Max) * 0.5f;

    public static Box3 Of(ReadOnlySpan<Vector3> points)
    {
        if (points.IsEmpty) throw new ArgumentException("No points.", nameof(points));
        var min = points[0];
        var max = points[0];
        foreach (var p in points)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return new Box3(min, max);
    }
}
