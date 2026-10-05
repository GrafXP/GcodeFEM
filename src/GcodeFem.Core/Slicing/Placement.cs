using System.Numerics;
using GcodeFem.Core.Geometry;

namespace GcodeFem.Core.Slicing;

/// <summary>
/// Links the three frames: part (as imported), print (part rotated by <see cref="Rotation"/>, where
/// the FEM runs and layers are horizontal) and bed (print frame moved by <see cref="BedOffset"/>,
/// the coordinates in the G-code).
/// </summary>
public readonly record struct Placement(Matrix4x4 Rotation, Vector3 BedOffset)
{
    public Vector3 BedToPrint(Vector3 bed) => bed - BedOffset;

    public Vector3 PrintToPart(Vector3 print) =>
        Vector3.Transform(print, Matrix4x4.Invert(Rotation, out var inverse) ? inverse : throw new InvalidOperationException("Rotation is not invertible."));

    public Vector3 BedToPart(Vector3 bed) => PrintToPart(BedToPrint(bed));

    /// <summary>
    /// Derives the bed offset from where the slicer put the part. The slicer may only translate it:
    /// a different bounding-box size means it rotated or scaled the part, which would break every
    /// mapping back to the model, so that is rejected.
    /// </summary>
    public static Placement FromSlice(Matrix4x4 rotation, Box3 printBounds, Box3 bedBounds)
    {
        var expected = printBounds.Size;
        var actual = bedBounds.Size;
        var tolerance = Math.Max(0.05f, 0.002f * expected.Length());
        if (Vector3.Distance(expected, actual) > tolerance)
            throw new SlicerException(
                $"The slicer changed the part's size or orientation: expected {Format(expected)} mm, got {Format(actual)} mm.");

        return new Placement(rotation, bedBounds.Min - printBounds.Min);
    }

    static string Format(Vector3 v) => FormattableString.Invariant($"{v.X:0.###} x {v.Y:0.###} x {v.Z:0.###}");
}
