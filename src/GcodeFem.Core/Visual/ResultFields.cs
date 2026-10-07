using System.Numerics;
using GcodeFem.Core.Fem;

namespace GcodeFem.Core.Visual;

/// <summary>Per-cell and per-node values of an adaptive result, in the shape the viewer colours and deforms with.</summary>
public static class ResultFields
{
    /// <summary>The displacement of every node of the bead-cell mesh, read off the octree's solution.</summary>
    public static Vector3[] NodeDisplacements(AdaptiveResult result)
    {
        var displacements = new Vector3[result.Mesh.NodeCount];
        Parallel.For(0, displacements.Length, n => displacements[n] = result.Displacement(n));
        return displacements;
    }

    /// <summary>Per cell: how far its centre moved (mm).</summary>
    public static float[] DisplacementMagnitude(FemMesh mesh, Vector3[] nodeDisplacements)
    {
        var magnitude = new float[mesh.Cells.Length];
        Parallel.For(0, magnitude.Length, e =>
        {
            var sum = Vector3.Zero;
            for (var a = 0; a < HexElement.Nodes; a++) sum += nodeDisplacements[mesh.CellNodes[8 * e + a]];
            magnitude[e] = sum.Length() / HexElement.Nodes;
        });
        return magnitude;
    }

    /// <summary>Per cell: σzz, the stress across the layer lines (MPa); positive pulls the layers apart.</summary>
    public static float[] InterlayerStress(AdaptiveResult result)
    {
        var stress = new float[result.Mesh.Cells.Length];
        for (var e = 0; e < stress.Length; e++) stress[e] = result.Stress[6 * e + 2];
        return stress;
    }
}

/// <summary>The outlines of an octree's coarse elements, to draw over the cells.</summary>
public static class OctreeWireframe
{
    /// <summary>Elements of 4 × 4 × 4 cells and up. Outlines of smaller ones lie too close together to read and only grey out the part.</summary>
    public const int CoarseLevel = 2;

    /// <summary>
    /// The box edges of every element of at least <paramref name="minLevel"/>, cut off at
    /// <paramref name="range"/>, as point pairs in the print frame; an edge shared by several elements
    /// comes once. Only the stretches of an edge that run along shown material are kept, so a coarse
    /// element that spans infill voids or sticks out of the part leaves no lines hanging in the air.
    /// Where the lines stop, the mesh is fine.
    /// </summary>
    /// <param name="cellLevels">Per cell of <paramref name="mesh"/>: the level of its element.</param>
    /// <param name="displacement">Moves a grid node (i, j, k), for the deformed shape.</param>
    public static Vector3[] Build(FemMesh mesh, ReadOnlySpan<byte> cellLevels, CellRange range, Func<int, int, int, Vector3>? displacement = null, int minLevel = CoarseLevel)
    {
        var grid = mesh.Grid;
        var shown = new HashSet<CellIndex>();
        var boxes = new HashSet<(int Level, int I, int J, int K)>();
        for (var e = 0; e < mesh.Cells.Length; e++)
        {
            var (c, level) = (mesh.Cells[e], cellLevels[e]);
            if (!range.Contains(c)) continue;
            shown.Add(c);
            if (level >= minLevel) boxes.Add((level, c.I >> level, c.J >> level, c.K >> level));
        }

        var runs = new HashSet<(int From, int To)>();
        int nodesX = grid.SizeX + 1, nodesY = grid.SizeY + 1;
        Span<int> low = stackalloc int[3], high = stackalloc int[3], at = stackalloc int[3], cell = stackalloc int[3];
        foreach (var (level, bi, bj, bk) in boxes)
        {
            (low[0], high[0]) = (Math.Max(bi << level, range.ILow), Math.Min((bi + 1) << level, range.IHigh));
            (low[1], high[1]) = (Math.Max(bj << level, range.JLow), Math.Min((bj + 1) << level, range.JHigh));
            (low[2], high[2]) = (Math.Max(bk << level, range.KLow), Math.Min((bk + 1) << level, range.KHigh));

            // Four edges along each axis, walked one cell at a time.
            for (var axis = 0; axis < 3; axis++)
            for (var side = 0; side < 4; side++)
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                at[u] = (side & 1) == 0 ? low[u] : high[u];
                at[v] = (side & 2) == 0 ? low[v] : high[v];
                var start = -1;
                for (var t = low[axis]; t <= high[axis]; t++)
                {
                    at[axis] = t;
                    var alongMaterial = false;
                    for (var corner = 0; corner < 4 && t < high[axis] && !alongMaterial; corner++)
                    {
                        // The four cells that share this stretch of the edge.
                        at.CopyTo(cell);
                        cell[u] -= corner & 1;
                        cell[v] -= corner >> 1;
                        alongMaterial = shown.Contains(new CellIndex(cell[0], cell[1], cell[2]));
                    }
                    var node = at[0] + nodesX * (at[1] + nodesY * at[2]);
                    if (alongMaterial && start < 0) start = node;
                    else if (!alongMaterial && start >= 0)
                    {
                        runs.Add((start, node));
                        start = -1;
                    }
                }
            }
        }

        var points = new Vector3[2 * runs.Count];
        var n = 0;
        foreach (var (from, to) in runs)
        {
            points[n++] = Point(from);
            points[n++] = Point(to);
        }
        return points;

        Vector3 Point(int node)
        {
            var (i, j, k) = mesh.Undense(node);
            var position = grid.NodePosition(i, j, k);
            return displacement is null ? position : position + displacement(i, j, k);
        }
    }
}
