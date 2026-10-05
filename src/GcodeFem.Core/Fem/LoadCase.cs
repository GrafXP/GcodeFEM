using System.Numerics;
using GcodeFem.Core.Geometry;

namespace GcodeFem.Core.Fem;

public enum Axis { X, Y, Z }

/// <summary>Holds the nodes it selects (by print-frame position) fixed in the chosen directions.</summary>
public sealed record Fixture(Func<Vector3, bool> SelectsNode, bool FixX = true, bool FixY = true, bool FixZ = true);

/// <summary>
/// A total force spread evenly (as a uniform traction) over the boundary faces it selects.
/// The selector gets each face's centre and outward normal, print frame.
/// </summary>
public sealed record SurfaceLoad(Func<Vector3, Vector3, bool> SelectsFace, Vector3 TotalForce);

public sealed record LoadCase(IReadOnlyList<Fixture> Fixtures, IReadOnlyList<SurfaceLoad> Loads)
{
    /// <summary>
    /// Clamps one face of <paramref name="bounds"/> (the max face along <paramref name="axis"/> if
    /// <paramref name="fixMax"/>, else the min face) and spreads <paramref name="force"/> over the
    /// opposite face. Until interfaces arrive in M5 this is how the CLI and tests load a part.
    /// </summary>
    public static LoadCase ClampAndPush(Box3 bounds, Axis axis, bool fixMax, Vector3 force)
    {
        var fixedValue = Component(fixMax ? bounds.Max : bounds.Min, axis);
        var loadedValue = Component(fixMax ? bounds.Min : bounds.Max, axis);
        var outward = (fixMax ? -1 : 1) * Unit(axis);
        return new LoadCase(
            [new Fixture(p => Near(Component(p, axis), fixedValue))],
            [new SurfaceLoad((centre, normal) => Near(Component(centre, axis), loadedValue) && Vector3.Dot(normal, outward) > 0.99f, force)]);
    }

    public static float Component(Vector3 v, Axis axis) => axis switch { Axis.X => v.X, Axis.Y => v.Y, _ => v.Z };

    public static Vector3 Unit(Axis axis) => axis switch { Axis.X => Vector3.UnitX, Axis.Y => Vector3.UnitY, _ => Vector3.UnitZ };

    static bool Near(float a, float b) => MathF.Abs(a - b) < 1e-3f;
}
