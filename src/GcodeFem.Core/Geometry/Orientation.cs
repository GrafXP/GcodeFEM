using System.Numerics;

namespace GcodeFem.Core.Geometry;

public static class Orientation
{
    /// <summary>
    /// Rotation about the fixed X axis, then Y, then Z, in degrees. Multiples of 90° come out exact,
    /// so a reoriented part's STL (and its slice cache key) doesn't depend on float noise.
    /// </summary>
    public static Matrix4x4 FromEulerDegrees(float x, float y, float z) =>
        Snap(Matrix4x4.CreateRotationX(Radians(x)) * Matrix4x4.CreateRotationY(Radians(y)) * Matrix4x4.CreateRotationZ(Radians(z)));

    /// <summary>
    /// The angles that <see cref="FromEulerDegrees"/> makes <paramref name="rotation"/> from, with Y
    /// between −90° and 90°. At Y = ±90° X and Z turn about the same line; Z is then given as 0.
    /// </summary>
    public static Vector3 ToEulerDegrees(Matrix4x4 rotation)
    {
        // Row by row the matrix is [cy cz, cy sz, −sy], [·, ·, sx cy], [·, ·, cx cy].
        var cosY = MathF.Sqrt(rotation.M11 * rotation.M11 + rotation.M12 * rotation.M12);
        var y = Degrees(MathF.Atan2(-rotation.M13, cosY));
        if (cosY < 1e-5f)
            // With cy = 0 the second row is [sin(x ∓ z), cos(x ∓ z), 0].
            return new Vector3(Degrees(MathF.Atan2(rotation.M13 < 0 ? rotation.M21 : -rotation.M21, rotation.M22)), y, 0);
        return new Vector3(Degrees(MathF.Atan2(rotation.M23, rotation.M33)), y, Degrees(MathF.Atan2(rotation.M12, rotation.M11)));
    }

    /// <summary>
    /// The angles for <see cref="FromEulerDegrees"/> that lay a face of the part on the bed: its
    /// outward normal, <paramref name="partNormal"/> in the part frame, then points straight down.
    /// Of all the ways to do that, the part ends up turned the least from where
    /// <paramref name="current"/> has it, as in a slicer's "lay on face". The angles are rounded to
    /// a ten-thousandth of a degree, which is a fifth of a micron over 100 mm.
    /// </summary>
    public static Vector3 LayFlat(Matrix4x4 current, Vector3 partNormal)
    {
        var normal = Vector3.Normalize(partNormal);
        var now = Vector3.Normalize(Vector3.TransformNormal(normal, current));

        // The shortest turn from where the face points now to straight down.
        var axis = Vector3.Cross(now, -Vector3.UnitZ);
        var turn = axis.Length() > 1e-6f
            ? Matrix4x4.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.Atan2(axis.Length(), -now.Z))
            : now.Z < 0 ? Matrix4x4.Identity : Matrix4x4.CreateRotationX(MathF.PI); // already down, or straight up: over onto its back
        var angles = ToEulerDegrees(Snap(current * turn));

        // X and Y alone decide whether the face lies flat, and follow from the normal directly; Z
        // only spins the part on the bed. Taken from the normal, flat is exact, whatever was lost
        // in multiplying and taking apart the matrices.
        var across = MathF.Sqrt(normal.Y * normal.Y + normal.Z * normal.Z);
        if (across < 1e-5f) angles.Y = normal.X > 0 ? 90 : -90;
        else (angles.X, angles.Y) = (Degrees(MathF.Atan2(-normal.Y, -normal.Z)), Degrees(MathF.Atan2(normal.X, across)));
        return new Vector3(Tidy(angles.X), Tidy(angles.Y), Tidy(angles.Z));

        // Rounded, half a turn always as +180, and never a minus zero.
        static float Tidy(float degrees)
        {
            var rounded = MathF.Round(degrees, 4);
            return rounded <= -180 ? 180 : rounded + 0f;
        }
    }

    static float Radians(float degrees) => degrees * MathF.PI / 180f;

    static float Degrees(float radians) => radians * 180f / MathF.PI;

    static Matrix4x4 Snap(Matrix4x4 m)
    {
        static float S(float v) => MathF.Abs(v) < 1e-6f ? 0f : MathF.Abs(MathF.Abs(v) - 1f) < 1e-6f ? MathF.Sign(v) : v;
        return new Matrix4x4(
            S(m.M11), S(m.M12), S(m.M13), m.M14,
            S(m.M21), S(m.M22), S(m.M23), m.M24,
            S(m.M31), S(m.M32), S(m.M33), m.M34,
            m.M41, m.M42, m.M43, m.M44);
    }
}
