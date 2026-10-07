using System.Numerics;
using GcodeFem.Core.Geometry;

namespace GcodeFem.Core.Fem;

/// <summary>What a named mount or load got hold of on a mesh.</summary>
/// <param name="Area">Of the cell faces it reaches (mm²). On a slanted or round face this is the area of the steps, more than the face's own.</param>
/// <param name="Force">What a load applies in all (N, print frame); zero for a mount.</param>
public sealed record InterfaceReach(string Name, bool IsLoad, int Faces, double Area, Vector3 Force);

/// <summary>Builds the global stiffness matrix and load vector for a <see cref="FemMesh"/>.</summary>
public static class Assembler
{
    /// <summary>
    /// Each node couples to at most its 27 grid neighbours (itself included); a 27-bit mask per node
    /// records which. Neighbour code c = (dx+1) + 3(dy+1) + 9(dz+1) increases with the neighbour's
    /// grid index, so a row's columns come out sorted and an entry's slot is a popcount.
    /// Cells are assembled in 8 parity colours: cells of one colour share no nodes, so each colour
    /// runs in parallel without locks.
    /// </summary>
    public static LinearSystem Assemble(FemMesh mesh, IsotropicMaterial material, LoadCase loadCase)
    {
        var nodes = mesh.NodeCount;
        var colours = ColourCells(mesh);

        // Neighbour masks.
        var mask = new int[nodes];
        foreach (var colour in colours)
            Parallel.ForEach(colour, e =>
            {
                for (var a = 0; a < 8; a++)
                {
                    var na = mesh.CellNodes[e * 8 + a];
                    for (var b = 0; b < 8; b++) mask[na] |= 1 << Code(a, b);
                }
            });

        // Row layout: node n owns rows 3n..3n+2, each with 3 × degree(n) entries.
        var rowPointers = new int[3 * nodes + 1];
        long running = 0;
        for (var n = 0; n < nodes; n++)
        {
            var width = 3 * BitOperations.PopCount((uint)mask[n]);
            for (var p = 0; p < 3; p++)
            {
                rowPointers[3 * n + p] = checked((int)running);
                running += width;
            }
        }
        rowPointers[3 * nodes] = checked((int)running);

        var columns = new int[running];
        Parallel.For(0, nodes, n =>
        {
            var dense = mesh.DenseOfNode[n];
            var (i, j, k) = mesh.Undense(dense);
            var rank = 0;
            for (var c = 0; c < 27; c++)
            {
                if ((mask[n] & (1 << c)) == 0) continue;
                var m = mesh.NodeAt(i + c % 3 - 1, j + c / 3 % 3 - 1, k + c / 9 - 1);
                for (var p = 0; p < 3; p++)
                for (var q = 0; q < 3; q++)
                    columns[rowPointers[3 * n + p] + 3 * rank + q] = 3 * m + q;
                rank++;
            }
        });

        // Values: one element matrix per layer height, scaled by E × φ per cell.
        var unit = new IsotropicMaterial(1, material.PoissonRatio).Constitutive();
        var byHeight = new Dictionary<float, double[]>();
        foreach (var height in mesh.Cells.Select(c => mesh.Grid.CellHeight(c.K)).Distinct())
            byHeight[height] = HexElement.Stiffness(mesh.Grid.Pitch, mesh.Grid.Pitch, height, unit);

        var values = new double[running];
        foreach (var colour in colours)
            Parallel.ForEach(colour, e =>
            {
                var ke = byHeight[mesh.Grid.CellHeight(mesh.Cells[e].K)];
                var scale = material.YoungsModulus * mesh.Fill[e];
                for (var a = 0; a < 8; a++)
                {
                    var na = mesh.CellNodes[e * 8 + a];
                    for (var b = 0; b < 8; b++)
                    {
                        var rank = BitOperations.PopCount((uint)(mask[na] & ((1 << Code(a, b)) - 1)));
                        for (var p = 0; p < 3; p++)
                        {
                            var row = rowPointers[3 * na + p] + 3 * rank;
                            for (var q = 0; q < 3; q++)
                                values[row + q] += ke[(3 * a + p) * 24 + 3 * b + q] * scale;
                        }
                    }
                }
            });

        var system = new LinearSystem
        {
            Size = 3 * nodes,
            RowPointers = rowPointers,
            Columns = columns,
            Values = values,
            RightHandSide = Loads(mesh, loadCase),
            Coordinates = mesh.Coordinates,
            Fixed = FixedDofs(mesh, loadCase),
        };
        ApplyFixtures(system);
        return system;
    }

    /// <summary>
    /// Uniform traction per load: each selected face carries force × (face area / total area), a quarter per corner.
    /// <paramref name="loadedCells"/>, if given, collects the cells those faces belong to.
    /// </summary>
    /// <remarks>A <see cref="TractionLoad"/> puts its own force on each face it reaches, likewise a quarter per corner.</remarks>
    public static double[] Loads(FemMesh mesh, LoadCase loadCase, ISet<int>? loadedCells = null)
    {
        var rhs = new double[3 * mesh.NodeCount];
        if (loadCase.Loads.Count == 0 && loadCase.Tractions.Count == 0) return rhs;
        var faces = mesh.BoundaryFaces().ToList();

        void Add(BoundaryFace f, Vector3 force)
        {
            loadedCells?.Add(f.Cell);
            foreach (var node in (ReadOnlySpan<int>)[f.N0, f.N1, f.N2, f.N3])
            {
                rhs[3 * node] += force.X / 4;
                rhs[3 * node + 1] += force.Y / 4;
                rhs[3 * node + 2] += force.Z / 4;
            }
        }

        foreach (var load in loadCase.Loads)
        {
            var selected = faces.Where(f => load.SelectsFace(f.Centre, f.Normal)).ToList();
            var area = selected.Sum(f => (double)f.Area);
            if (area <= 0) throw new InvalidOperationException("A load selects no boundary faces.");
            foreach (var f in selected) Add(f, load.TotalForce * (float)(f.Area / area));
        }
        foreach (var load in loadCase.Tractions)
            foreach (var (face, force) in FaceForces(faces, load).Forces)
                Add(face, force);
        return rhs;
    }

    /// <summary>The force on each face that a traction load reaches, scaled to its resultant if it has one, and their sum.</summary>
    static (List<(BoundaryFace Face, Vector3 Force)> Forces, Vector3 Sum) FaceForces(List<BoundaryFace> faces, TractionLoad load)
    {
        var forces = new List<(BoundaryFace Face, Vector3 Force)>();
        double x = 0, y = 0, z = 0;
        foreach (var face in faces)
        {
            var force = load.Traction(face.Centre, face.Normal) * face.Area;
            if (force == Vector3.Zero) continue;
            forces.Add((face, force));
            (x, y, z) = (x + force.X, y + force.Y, z + force.Z);
        }
        if (forces.Count == 0) throw new InvalidOperationException($"The load '{load.Name}' reaches no face of the printed part.");
        if (load.Resultant is not { } wanted) return (forces, new Vector3((float)x, (float)y, (float)z));

        // Scaled so that the sum is the wanted force as far as it lies along the sum's own direction.
        var along = wanted.X * x + wanted.Y * y + wanted.Z * z;
        var size = x * x + y * y + z * z;
        if (!(along > 1e-9 * Math.Sqrt(size) * wanted.Length()))
            throw new InvalidOperationException($"The faces of the load '{load.Name}' cannot take a force in that direction.");
        var scale = (float)(along / size);
        for (var n = 0; n < forces.Count; n++) forces[n] = (forces[n].Face, forces[n].Force * scale);
        return (forces, new Vector3((float)(x * scale), (float)(y * scale), (float)(z * scale)));
    }

    public static bool[] FixedDofs(FemMesh mesh, LoadCase loadCase)
    {
        var fixedDofs = new bool[3 * mesh.NodeCount];
        for (var n = 0; n < mesh.NodeCount && loadCase.Fixtures.Count > 0; n++)
        {
            var p = mesh.NodePosition(n);
            foreach (var fixture in loadCase.Fixtures)
            {
                if (!fixture.SelectsNode(p)) continue;
                fixedDofs[3 * n] |= fixture.FixX;
                fixedDofs[3 * n + 1] |= fixture.FixY;
                fixedDofs[3 * n + 2] |= fixture.FixZ;
            }
        }
        if (loadCase.FaceFixtures.Count > 0)
            foreach (var face in mesh.BoundaryFaces())
            foreach (var fixture in loadCase.FaceFixtures)
            {
                var held = fixture.Holds(face.Centre, face.Normal);
                if (held == Axes.None) continue;
                foreach (var node in (ReadOnlySpan<int>)[face.N0, face.N1, face.N2, face.N3])
                {
                    fixedDofs[3 * node] |= held.HasFlag(Axes.X);
                    fixedDofs[3 * node + 1] |= held.HasFlag(Axes.Y);
                    fixedDofs[3 * node + 2] |= held.HasFlag(Axes.Z);
                }
            }
        if (!fixedDofs.Any(f => f)) throw new InvalidOperationException("No degree of freedom is fixed.");
        if (FreeMotion(mesh, fixedDofs) is { } motion)
            throw new InvalidOperationException($"The mounts leave the part free to {motion} (in the print's directions). Hold it that way as well.");
        return fixedDofs;
    }

    /// <summary>
    /// How the part can still move as a whole with these degrees of freedom held, or null if it cannot.
    /// A rigid motion is a shift t plus a turn ω about the middle of the held nodes; each held
    /// direction at a node rules out the motions that move the node that way. What is left over when
    /// all are counted is free, and would make the stiffness matrix singular.
    /// </summary>
    public static string? FreeMotion(FemMesh mesh, bool[] fixedDofs)
    {
        var held = Enumerable.Range(0, mesh.NodeCount).Where(n => fixedDofs[3 * n] || fixedDofs[3 * n + 1] || fixedDofs[3 * n + 2]).ToList();
        if (held.Count == 0) return "move any way";
        double cx = held.Average(n => mesh.Coordinates[3 * n]), cy = held.Average(n => mesh.Coordinates[3 * n + 1]), cz = held.Average(n => mesh.Coordinates[3 * n + 2]);
        double Radius(int n) => Math.Sqrt(Math.Pow(mesh.Coordinates[3 * n] - cx, 2) + Math.Pow(mesh.Coordinates[3 * n + 1] - cy, 2) + Math.Pow(mesh.Coordinates[3 * n + 2] - cz, 2));
        var scale = held.Max(Radius);
        if (scale <= 0) scale = 1;

        // Sum of rᵀ r over the held directions, r being the motion (t, ω) → the node's shift that way.
        var sum = new double[36];
        Span<double> row = stackalloc double[6];
        foreach (var n in held)
        {
            double qx = (mesh.Coordinates[3 * n] - cx) / scale, qy = (mesh.Coordinates[3 * n + 1] - cy) / scale, qz = (mesh.Coordinates[3 * n + 2] - cz) / scale;
            for (var d = 0; d < 3; d++)
            {
                if (!fixedDofs[3 * n + d]) continue;
                row.Clear();
                row[d] = 1;
                // (ω × q) along d
                if (d == 0) (row[4], row[5]) = (qz, -qy);
                else if (d == 1) (row[3], row[5]) = (-qz, qx);
                else (row[3], row[4]) = (qy, -qx);
                for (var r = 0; r < 6; r++)
                for (var c = 0; c < 6; c++)
                    sum[6 * r + c] += row[r] * row[c];
            }
        }
        var (values, vectors) = SymmetricEigen.Solve(sum, 6);
        if (values[0] > 1e-9 * values[5]) return null;

        var free = vectors[0];
        Vector3 shift = new((float)free[0], (float)free[1], (float)free[2]), turn = new((float)free[3], (float)free[4], (float)free[5]);
        return turn.Length() < 1e-3f * shift.Length() ? $"slide along {Name(shift)}" : $"turn about an axis along {Name(turn)}";

        static string Name(Vector3 direction)
        {
            direction = Vector3.Normalize(direction);
            var size = Vector3.Abs(direction);
            if (MathF.Max(size.X, MathF.Max(size.Y, size.Z)) > 0.999f) return LoadCase.Along(direction).ToString();
            return FormattableString.Invariant($"({direction.X:0.##}, {direction.Y:0.##}, {direction.Z:0.##})");
        }
    }

    /// <summary>
    /// What each named mount and load of a load case gets hold of on <paramref name="mesh"/>, to check
    /// the load case against what was meant.
    /// </summary>
    public static IReadOnlyList<InterfaceReach> Reach(FemMesh mesh, LoadCase loadCase)
    {
        var faces = mesh.BoundaryFaces().ToList();
        var reach = new List<InterfaceReach>();
        foreach (var fixture in loadCase.FaceFixtures)
        {
            var held = faces.Where(f => fixture.Holds(f.Centre, f.Normal) != Axes.None).ToList();
            reach.Add(new InterfaceReach(fixture.Name, false, held.Count, held.Sum(f => (double)f.Area), Vector3.Zero));
        }
        foreach (var load in loadCase.Tractions)
        {
            var (forces, sum) = FaceForces(faces, load);
            reach.Add(new InterfaceReach(load.Name, true, forces.Count, forces.Sum(f => (double)f.Face.Area), sum));
        }
        return reach;
    }

    /// <summary>Zero displacement: fixed rows and columns become identity, the load on them is dropped.</summary>
    internal static void ApplyFixtures(LinearSystem s)
    {
        Parallel.For(0, s.Size, row =>
        {
            if (s.Fixed[row])
            {
                s.RightHandSide[row] = 0;
                for (var p = s.RowPointers[row]; p < s.RowPointers[row + 1]; p++)
                    s.Values[p] = s.Columns[p] == row ? 1 : 0;
            }
            else
            {
                for (var p = s.RowPointers[row]; p < s.RowPointers[row + 1]; p++)
                    if (s.Fixed[s.Columns[p]]) s.Values[p] = 0;
            }
        });
    }

    static int Code(int a, int b) =>
        (HexElement.CornerX[b] - HexElement.CornerX[a] + 1) +
        3 * (HexElement.CornerY[b] - HexElement.CornerY[a] + 1) +
        9 * (HexElement.CornerZ[b] - HexElement.CornerZ[a] + 1);

    static List<int>[] ColourCells(FemMesh mesh)
    {
        var colours = Enumerable.Range(0, 8).Select(_ => new List<int>()).ToArray();
        for (var e = 0; e < mesh.Cells.Length; e++)
        {
            var c = mesh.Cells[e];
            colours[(c.I & 1) | ((c.J & 1) << 1) | ((c.K & 1) << 2)].Add(e);
        }
        return colours;
    }
}
