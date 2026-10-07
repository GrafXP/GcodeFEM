using System.Numerics;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Study;

namespace GcodeFem.Core.Visual;

/// <summary>An interface as the viewer needs it: what it is, its faces, its value in the load case shown, and whether it is the one being edited.</summary>
public sealed record InterfaceMark(InterfaceKind Kind, int[] Triangles, LoadValue Value, bool IsSelected);

/// <summary>Solid shapes with a colour per vertex; every triangle has its own corners, so the faces are flat-shaded.</summary>
public sealed class GlyphMesh
{
    readonly List<Vector3> positions = [], normals = [];
    readonly List<Vector4> colours = [];

    /// <summary>Flat pieces around a cone or rod.</summary>
    const int Sides = 16;

    public IReadOnlyList<Vector3> Positions => positions;
    public IReadOnlyList<Vector3> Normals => normals;
    public IReadOnlyList<Vector4> Colours => colours;
    public int TriangleCount => positions.Count / 3;

    /// <summary>A cone with its point at <paramref name="tip"/> and its round end at <paramref name="foot"/>.</summary>
    public void AddCone(Vector3 tip, Vector3 foot, float radius, Vector4 colour)
    {
        var (u, v) = Across(tip - foot);
        for (var n = 0; n < Sides; n++)
        {
            Vector3 a = foot + radius * Around(u, v, n), b = foot + radius * Around(u, v, n + 1);
            Add(a, b, tip, colour);
            Add(b, a, foot, colour);
        }
    }

    /// <summary>A round rod from <paramref name="from"/> to <paramref name="to"/>, closed at both ends.</summary>
    public void AddRod(Vector3 from, Vector3 to, float radius, Vector4 colour)
    {
        var (u, v) = Across(to - from);
        for (var n = 0; n < Sides; n++)
        {
            Vector3 a = radius * Around(u, v, n), b = radius * Around(u, v, n + 1);
            Add(from + a, from + b, to + b, colour);
            Add(from + a, to + b, to + a, colour);
            Add(from + b, from + a, from, colour);
            Add(to + a, to + b, to, colour);
        }
    }

    /// <summary>An arrow from <paramref name="from"/> with its point at <paramref name="to"/>; the head takes up to a third of it.</summary>
    public void AddArrow(Vector3 from, Vector3 to, float radius, Vector4 colour)
    {
        var length = Vector3.Distance(from, to);
        if (!(length > 0)) return;
        var neck = to - (to - from) / length * MathF.Min(6 * radius, length / 3);
        AddRod(from, neck, radius, colour);
        AddCone(to, neck, 2.5f * radius, colour);
    }

    /// <summary>A triangle, counter-clockwise seen from outside.</summary>
    void Add(Vector3 a, Vector3 b, Vector3 c, Vector4 colour)
    {
        var normal = Vector3.Cross(b - a, c - a);
        if (!(normal.Length() > 0)) return;
        normal = Vector3.Normalize(normal);
        positions.AddRange([a, b, c]);
        normals.AddRange([normal, normal, normal]);
        colours.AddRange([colour, colour, colour]);
    }

    /// <summary>Two unit vectors square to <paramref name="axis"/> and to each other, right-handed with it.</summary>
    static (Vector3 U, Vector3 V) Across(Vector3 axis)
    {
        axis = Vector3.Normalize(axis);
        var u = Vector3.Normalize(Vector3.Cross(axis, MathF.Abs(axis.Z) < 0.9f ? Vector3.UnitZ : Vector3.UnitX));
        return (u, Vector3.Cross(axis, u));
    }

    static Vector3 Around(Vector3 u, Vector3 v, int step)
    {
        var angle = 2 * MathF.PI * step / Sides;
        return MathF.Cos(angle) * u + MathF.Sin(angle) * v;
    }
}

/// <summary>How interfaces show on the model: their faces tinted, mounts marked with cones, loads with arrows.</summary>
public static class InterfaceGlyphs
{
    /// <summary>Faces that hold the part, and their markers.</summary>
    public static readonly Vector4 Mount = Palette.Categorical[0];

    /// <summary>Faces that are loaded, and their arrows.</summary>
    public static readonly Vector4 Load = Palette.Categorical[1];

    /// <summary>The model itself, and cell faces that belong to no interface.</summary>
    public static readonly Vector4 Plain = Palette.Hex("#a3a29b");

    /// <summary>An interface that is not the one being edited keeps its hue but steps back towards the model's grey.</summary>
    const float Unselected = 0.45f;

    public static Vector4 Colour(InterfaceMark mark)
    {
        var colour = mark.Kind.IsLoad() ? Load : Mount;
        return mark.IsSelected ? colour : Vector4.Lerp(colour, Plain, Unselected);
    }

    /// <summary>A colour per triangle of the model: the interface it belongs to (the one being edited wins), or plain grey.</summary>
    public static Vector4[] TriangleColours(int triangleCount, IReadOnlyList<InterfaceMark> marks)
    {
        var colours = new Vector4[triangleCount];
        Array.Fill(colours, Plain);
        foreach (var mark in marks.OrderBy(mark => mark.IsSelected))
        foreach (var triangle in mark.Triangles)
            if ((uint)triangle < (uint)triangleCount)
                colours[triangle] = Colour(mark);
        return colours;
    }

    /// <summary>
    /// The markers for all interfaces, in the print frame, sized to the model:
    /// a mount gets cones standing on its face, or a rod along the axis if it is a hole;
    /// a force gets one arrow, a pressure several small ones, a bearing load one at each mouth of its hole.
    /// </summary>
    /// <param name="printMesh">The model turned into the print frame.</param>
    /// <param name="rotation">Part frame → print frame, for the forces.</param>
    public static GlyphMesh Build(TriangleMesh printMesh, Matrix4x4 rotation, IReadOnlyList<InterfaceMark> marks)
    {
        var glyphs = new GlyphMesh();
        var size = printMesh.Bounds.Size.Length();
        float arrow = 0.2f * size, shaft = 0.006f * size, cone = 0.035f * size;
        foreach (var mark in marks)
        {
            var triangles = mark.Triangles.Where(t => (uint)t < (uint)printMesh.TriangleCount).ToArray();
            if (triangles.Length == 0) continue;
            var patch = new SurfacePatch(printMesh, triangles);
            var colour = Colour(mark);
            var holes = patch.Components().Select(part => Cylinder.Fit(printMesh, part)).ToList();
            var allHoles = holes.All(hole => hole is { IsHole: true });

            switch (mark.Kind)
            {
                case InterfaceKind.Fixed or InterfaceKind.Sliding or InterfaceKind.BoltHole when allHoles:
                    foreach (var hole in holes)
                    {
                        var reach = (hole!.Length / 2 + cone) * hole.Axis;
                        glyphs.AddRod(hole.Centre - reach, hole.Centre + reach, shaft, colour);
                    }
                    break;

                case InterfaceKind.Fixed or InterfaceKind.Sliding or InterfaceKind.BoltHole:
                    foreach (var (point, normal) in patch.Spread(12))
                        glyphs.AddCone(point, point + cone * normal, 0.4f * cone, colour);
                    break;

                case InterfaceKind.Force:
                    var force = mark.Value.NormalForce is { } push
                        ? patch.MeanNormal.Length() > 0.2f ? -push * Vector3.Normalize(patch.MeanNormal) : Vector3.Zero
                        : Vector3.TransformNormal(mark.Value.Force, rotation);
                    if (force == Vector3.Zero) break;
                    var along = Vector3.Normalize(force);
                    // Outside the part either way: a push ends on the face, a pull starts there.
                    if (Vector3.Dot(along, patch.MeanNormal) <= 0) glyphs.AddArrow(patch.Centroid - arrow * along, patch.Centroid, shaft, colour);
                    else glyphs.AddArrow(patch.Centroid, patch.Centroid + arrow * along, shaft, colour);
                    break;

                case InterfaceKind.Pressure:
                    if (mark.Value.Pressure == 0) break;
                    foreach (var (point, normal) in patch.Spread(9))
                    {
                        var away = point + 0.4f * arrow * normal;
                        if (mark.Value.Pressure > 0) glyphs.AddArrow(away, point, 0.7f * shaft, colour);
                        else glyphs.AddArrow(point, away, 0.7f * shaft, colour);
                    }
                    break;

                case InterfaceKind.Bearing:
                    var bearing = Vector3.TransformNormal(mark.Value.Force, rotation);
                    if (bearing == Vector3.Zero) break;
                    var towards = Vector3.Normalize(bearing);
                    if (!allHoles)
                    {
                        glyphs.AddArrow(patch.Centroid - arrow * towards, patch.Centroid, shaft, colour);
                        break;
                    }
                    foreach (var hole in holes)
                    foreach (var end in (ReadOnlySpan<float>)[-1, 1])
                    {
                        var mouth = hole!.Centre + end * (hole.Length / 2 + 2 * shaft) * hole.Axis;
                        glyphs.AddArrow(mouth, mouth + arrow * towards, shaft, colour);
                    }
                    break;
            }
        }
        return glyphs;
    }
}
