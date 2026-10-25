using System.Numerics;
using GcodeFem.Core.Geometry;

namespace GcodeFem.Core.Study;

/// <summary>
/// What an orientation sweep tries, saved with the study. Neither number has a built-in value: until
/// the smallest contact is set, every face with contact is shown and the sweep does not start; until
/// the count is set, all the faces left are taken.
/// </summary>
/// <param name="MinContact">mm²: a face that touches the bed with less is not tried.</param>
/// <param name="Count">Of the faces left, the largest this many are tried.</param>
/// <param name="Spins">Each face is also tried spun on the bed by 45° and by 90°: the walls follow the part, but the sparse infill and the skins follow the bed's axes.</param>
public sealed record SweepSettings(float? MinContact = null, int? Count = null, bool Spins = false)
{
    public bool CanStart => MinContact is not null;
}

/// <summary>One way to print the part in a sweep: a face laid on the bed, and spun on it.</summary>
/// <param name="Number">Counted from 1, as the preview numbers them.</param>
/// <param name="Spin">Degrees about the vertical, on top of the shortest turn that lays the face down.</param>
/// <param name="Rotation">Degrees about X, then Y, then Z, as the rotation fields take them.</param>
/// <param name="Height">mm, as it stands on the bed.</param>
/// <param name="OverhangArea">mm² of the part, other than what stands on the bed, facing down more steeply than the support threshold: what prints in the air, or on support.</param>
public sealed record OrientationCandidate(int Number, LayFace Face, float Spin, Vector3 Rotation, float Height, float OverhangArea)
{
    public Matrix4x4 Matrix => Orientation.FromEulerDegrees(Rotation.X, Rotation.Y, Rotation.Z);
}

public static class OrientationCandidates
{
    /// <summary>Bambu Studio's support threshold when the process gives none: a face less than this many degrees from the bed, facing down, needs support.</summary>
    public const float DefaultOverhangDegrees = 30;

    /// <summary>
    /// The candidates the settings ask for, largest contact first. Each face is laid down by the
    /// shortest turn from how the part lies now (<paramref name="current"/>), as "Lay a face on the bed" does.
    /// </summary>
    /// <param name="faces">The part's faces (<see cref="LayFaces.Find"/>), if they are known already.</param>
    public static List<OrientationCandidate> Find(TriangleMesh part, Matrix4x4 current, SweepSettings settings, float overhangDegrees = DefaultOverhangDegrees, IReadOnlyList<LayFace>? faces = null)
    {
        faces ??= LayFaces.Find(part);
        float[] spins = settings.Spins ? [0, 45, 90] : [0];
        var candidates = new List<OrientationCandidate>();
        foreach (var face in faces.Where(f => settings.MinContact is not { } least || f.ContactArea >= least).Take(settings.Count ?? int.MaxValue))
        {
            var flat = Orientation.LayFlat(current, face.Normal);
            foreach (var spin in spins)
            {
                var rotation = flat with { Z = Wrap(flat.Z + spin) };
                var standing = part.Transformed(Orientation.FromEulerDegrees(rotation.X, rotation.Y, rotation.Z));
                candidates.Add(new OrientationCandidate(candidates.Count + 1, face, spin, rotation, standing.Bounds.Size.Z, OverhangArea(standing, overhangDegrees)));
            }
        }
        return candidates;
    }

    /// <summary>
    /// mm² of a part as it stands (print frame) that faces down at less than <paramref name="degrees"/>
    /// from the bed, as Bambu Studio's support threshold counts it; what lies on the bed does not count.
    /// </summary>
    public static float OverhangArea(TriangleMesh standing, float degrees = DefaultOverhangDegrees)
    {
        var steep = MathF.Cos(degrees * MathF.PI / 180);
        var bed = standing.Bounds.Min.Z + LayFaces.ContactTolerance;
        var area = 0f;
        for (var t = 0; t < standing.TriangleCount; t++)
        {
            if (-standing.Normal(t).Z <= steep) continue;
            var (a, b, c) = standing.Triangle(t);
            if (a.Z <= bed && b.Z <= bed && c.Z <= bed) continue;
            area += Vector3.Cross(b - a, c - a).Length() / 2;
        }
        return area;
    }

    /// <summary>Into (−180°, 180°], as <see cref="Orientation.LayFlat"/> gives angles.</summary>
    static float Wrap(float degrees)
    {
        var wrapped = degrees % 360;
        return wrapped > 180 ? wrapped - 360 : wrapped <= -180 ? wrapped + 360 : wrapped;
    }
}
