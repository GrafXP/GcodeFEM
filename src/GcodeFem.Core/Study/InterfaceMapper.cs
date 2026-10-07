using System.Numerics;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Geometry;

namespace GcodeFem.Core.Study;

/// <summary>
/// Carries interfaces from the model's faces to the printed cells. The model's surface runs
/// through or alongside the outermost cells, which follow it in steps: an interface takes the free
/// cell faces that lie within reach of its triangles and face the same way as the triangle next to them.
/// </summary>
public static class InterfaceMapper
{
    /// <summary>
    /// How far from a picked face a cell face may lie, in line widths. The outermost cell is there
    /// once a twentieth of it is filled, so its free face can sit most of a line width off the model's surface.
    /// </summary>
    public const float Reach = 1.25f;

    /// <summary>
    /// A cell face belongs to a triangle if it faces along the triangle's normal at all: on a slanted
    /// face that is both kinds of step, the treads and the risers. Faces square to it are another face's.
    /// </summary>
    const float MinFacing = 0.1f;

    /// <summary>An axis within about 10° of a print direction counts as running along it.</summary>
    const float Aligned = 0.985f;

    /// <summary>
    /// The mounts and loads of one load case for the bead cells, print frame.
    /// </summary>
    /// <param name="printMesh">The model turned into the print frame; its triangles are numbered like the model's.</param>
    /// <param name="rotation">Part frame → print frame, for the forces.</param>
    /// <param name="pitch">The cells' size in X and Y (mm).</param>
    public static LoadCase Map(TriangleMesh printMesh, Matrix4x4 rotation, float pitch, IReadOnlyList<PartInterface> interfaces, StudyLoadCase loadCase)
    {
        var fixtures = new List<FaceFixture>();
        var tractions = new List<TractionLoad>();
        foreach (var item in interfaces)
        {
            if (item.Triangles.Length == 0) throw new InvalidOperationException($"'{item.Name}' has no faces picked yet.");
            var patch = new SurfacePatch(printMesh, item.Triangles);
            var locator = patch.Locator(Reach * pitch);
            int Covering(Vector3 centre, Vector3 normal) => locator.Nearest(centre, normal, MinFacing);
            var value = loadCase.Of(item);

            switch (item.Kind)
            {
                case InterfaceKind.Fixed:
                    fixtures.Add(new FaceFixture(item.Name, (centre, normal) => Covering(centre, normal) < 0 ? Axes.None : Axes.All));
                    break;

                case InterfaceKind.Sliding:
                    // Each cell face is held along its own normal. On a face that is slanted in the print
                    // frame the steps hold both ways, so it cannot slide there: see the plan, §10.
                    fixtures.Add(new FaceFixture(item.Name, (centre, normal) => Covering(centre, normal) < 0 ? Axes.None : LoadCase.Along(normal)));
                    break;

                case InterfaceKind.BoltHole:
                    var alongHole = AlongHole(patch, item);
                    fixtures.Add(new FaceFixture(item.Name, (centre, normal) =>
                    {
                        var triangle = Covering(centre, normal);
                        return triangle < 0 ? Axes.None : LoadCase.Along(normal) | alongHole[triangle];
                    }));
                    break;

                case InterfaceKind.Force:
                    var force = value.NormalForce is { } push ? -push * Facing(patch, item) : Vector3.TransformNormal(value.Force, rotation);
                    if (force == Vector3.Zero) break;
                    var direction = Vector3.Normalize(force);
                    // Weighted by how squarely a step faces the model's surface, the steps add up to the face's own area.
                    tractions.Add(new TractionLoad(item.Name, (centre, normal) =>
                    {
                        var triangle = Covering(centre, normal);
                        return triangle < 0 ? Vector3.Zero : direction * Vector3.Dot(normal, patch.Normals[triangle]);
                    }, force));
                    break;

                case InterfaceKind.Pressure:
                    if (value.Pressure == 0) break;
                    // Onto every step along its own normal: the steps' forces add up to exactly the pressure's force on the true face.
                    tractions.Add(new TractionLoad(item.Name, (centre, normal) => Covering(centre, normal) < 0 ? Vector3.Zero : -value.Pressure * normal));
                    break;

                case InterfaceKind.Bearing:
                    var bearing = Vector3.TransformNormal(value.Force, rotation);
                    if (bearing == Vector3.Zero) break;
                    var towards = Vector3.Normalize(bearing);
                    // Pressure on the wall the pin pushes against, falling off as the cosine to nothing at the sides.
                    tractions.Add(new TractionLoad(item.Name, (centre, normal) =>
                    {
                        var triangle = Covering(centre, normal);
                        if (triangle < 0) return Vector3.Zero;
                        var wall = patch.Normals[triangle];
                        var press = -Vector3.Dot(wall, towards);
                        return press <= 0 ? Vector3.Zero : -wall * (press * Vector3.Dot(normal, wall));
                    }, bearing));
                    break;
            }
        }

        if (fixtures.Count == 0) throw new InvalidOperationException("Nothing holds the part: add a mount and pick its faces.");
        if (tractions.Count == 0) throw new InvalidOperationException($"Nothing loads the part in '{loadCase.Name}': add a load and give it a value.");
        return new LoadCase([], []) { FaceFixtures = fixtures, Tractions = tractions };
    }

    /// <summary>
    /// Says whether a cell face (centre and outward normal, print frame) belongs to the triangles
    /// picked for an interface: for showing what the solver will take the interface to be.
    /// </summary>
    public static Func<Vector3, Vector3, bool> Covers(TriangleMesh printMesh, IReadOnlyCollection<int> triangles, float pitch)
    {
        if (triangles.Count == 0) return (_, _) => false;
        var locator = new SurfacePatch(printMesh, triangles).Locator(Reach * pitch);
        return (centre, normal) => locator.Nearest(centre, normal, MinFacing) >= 0;
    }

    /// <summary>The one direction a patch faces, for a force along the normal.</summary>
    static Vector3 Facing(SurfacePatch patch, PartInterface item) =>
        patch.MeanNormal.Length() > 0.2f
            ? Vector3.Normalize(patch.MeanNormal)
            : throw new InvalidOperationException($"The faces of '{item.Name}' point every way, so a force along their normal has no direction. Give it as X, Y, Z.");

    /// <summary>
    /// Per triangle of the patch: the direction to hold on top of each cell face's own normal so that
    /// the hole it belongs to is held along its axis, if the bolt is to do that. Only a hole along a
    /// print direction can be held across and left free along; a slanted one is held every way.
    /// </summary>
    static Axes[] AlongHole(SurfacePatch patch, PartInterface item)
    {
        var hold = new Axes[patch.Triangles.Length];
        foreach (var part in patch.Components())
        {
            var hole = Cylinder.Fit(patch.Mesh, part)
                       ?? throw new InvalidOperationException($"'{item.Name}' is a bolt hole, but its faces are not the wall of a round hole. Pick the inside of the hole, or make it a fixed mount.");
            var axis = Vector3.Abs(hole.Axis);
            var along = MathF.Max(axis.X, MathF.Max(axis.Y, axis.Z)) < Aligned ? Axes.All : item.Axial ? LoadCase.Along(hole.Axis) : Axes.None;
            foreach (var triangle in part) hold[Array.BinarySearch(patch.Triangles, triangle)] = along;
        }
        return hold;
    }
}
