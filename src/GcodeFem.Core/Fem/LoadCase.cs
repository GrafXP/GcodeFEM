using System.Numerics;
using GcodeFem.Core.Geometry;

namespace GcodeFem.Core.Fem;

public enum Axis { X, Y, Z }

/// <summary>A set of the print frame's directions, such as those a mount holds.</summary>
[Flags]
public enum Axes { None = 0, X = 1, Y = 2, Z = 4, All = X | Y | Z }

/// <summary>Holds the nodes it selects (by print-frame position) fixed in the chosen directions.</summary>
public sealed record Fixture(Func<Vector3, bool> SelectsNode, bool FixX = true, bool FixY = true, bool FixZ = true);

/// <summary>
/// A total force spread evenly (as a uniform traction) over the boundary faces it selects.
/// The selector gets each face's centre and outward normal, print frame.
/// </summary>
public sealed record SurfaceLoad(Func<Vector3, Vector3, bool> SelectsFace, Vector3 TotalForce);

/// <summary>
/// Holds the corners of the boundary faces it reaches. <paramref name="Holds"/> gets a face's centre
/// and outward normal (print frame) and returns the directions held there: none where it does not reach.
/// </summary>
public sealed record FaceFixture(string Name, Func<Vector3, Vector3, Axes> Holds);

/// <summary>
/// A load given face by face. <paramref name="Traction"/> gets a boundary face's centre and outward
/// normal (print frame) and returns the traction on it (MPa), zero where the load does not reach.
/// With a <paramref name="Resultant"/> only the directions and relative sizes of the tractions
/// count: they are scaled together until they add up to that force, or to as much of it as their
/// sum's direction can carry (a pin in a hole cannot push along the hole).
/// </summary>
public sealed record TractionLoad(string Name, Func<Vector3, Vector3, Vector3> Traction, Vector3? Resultant = null);

public sealed record LoadCase(IReadOnlyList<Fixture> Fixtures, IReadOnlyList<SurfaceLoad> Loads)
{
    /// <summary>Mounts given by the cell faces they cover: this is how interfaces picked on the model arrive.</summary>
    public IReadOnlyList<FaceFixture> FaceFixtures { get; init; } = [];

    /// <summary>Loads given face by face, likewise.</summary>
    public IReadOnlyList<TractionLoad> Tractions { get; init; } = [];

    /// <summary>
    /// Clamps one face of <paramref name="bounds"/> (the max face along <paramref name="axis"/> if
    /// <paramref name="fixMax"/>, else the min face) and spreads <paramref name="force"/> over the
    /// opposite face: a load case for benchmarks and tests that needs no model to pick faces on.
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

    /// <summary>The print axis that <paramref name="direction"/> runs along most.</summary>
    public static Axes Along(Vector3 direction)
    {
        var size = Vector3.Abs(direction);
        return size.X >= size.Y && size.X >= size.Z ? Axes.X : size.Y >= size.Z ? Axes.Y : Axes.Z;
    }

    static bool Near(float a, float b) => MathF.Abs(a - b) < 1e-3f;
}
