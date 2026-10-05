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

    static float Radians(float degrees) => degrees * MathF.PI / 180f;

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
