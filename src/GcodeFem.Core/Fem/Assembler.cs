using System.Numerics;

namespace GcodeFem.Core.Fem;

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
    public static double[] Loads(FemMesh mesh, LoadCase loadCase, ISet<int>? loadedCells = null)
    {
        var rhs = new double[3 * mesh.NodeCount];
        if (loadCase.Loads.Count == 0) return rhs;
        var faces = mesh.BoundaryFaces().ToList();
        foreach (var load in loadCase.Loads)
        {
            var selected = faces.Where(f => load.SelectsFace(f.Centre, f.Normal)).ToList();
            var area = selected.Sum(f => (double)f.Area);
            if (area <= 0) throw new InvalidOperationException("A load selects no boundary faces.");
            foreach (var f in selected)
            {
                loadedCells?.Add(f.Cell);
                var share = load.TotalForce * (float)(f.Area / area / 4);
                foreach (var node in new[] { f.N0, f.N1, f.N2, f.N3 })
                {
                    rhs[3 * node] += share.X;
                    rhs[3 * node + 1] += share.Y;
                    rhs[3 * node + 2] += share.Z;
                }
            }
        }
        return rhs;
    }

    public static bool[] FixedDofs(FemMesh mesh, LoadCase loadCase)
    {
        var fixedDofs = new bool[3 * mesh.NodeCount];
        for (var n = 0; n < mesh.NodeCount; n++)
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
        if (!fixedDofs.Any(f => f)) throw new InvalidOperationException("No degree of freedom is fixed.");
        return fixedDofs;
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
