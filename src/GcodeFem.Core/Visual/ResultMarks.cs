using System.Numerics;
using GcodeFem.Core.Fem;

namespace GcodeFem.Core.Visual;

/// <summary>What the results view marks on the part: the weakest spot, with an arrow, and the weakest layer line, outlined.</summary>
public static class ResultMarks
{
    static readonly (int Di, int Dj, int Dk)[] Sides = [(0, 0, 1), (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, -1)];

    /// <summary>The middle of a cell, print frame.</summary>
    public static Vector3 Centre(CellGrid grid, CellIndex c) =>
        grid.NodePosition(c.I, c.J, c.K) + new Vector3(grid.Pitch / 2, grid.Pitch / 2, grid.CellHeight(c.K) / 2);

    /// <summary>
    /// The way out of the part from a cell: the first of its sides with no cell of <paramref name="cells"/>
    /// next to it, up first, then the four in the plane, then down; straight up for a cell inside the part.
    /// </summary>
    public static Vector3 WayOut(IReadOnlySet<CellIndex> cells, CellIndex cell)
    {
        foreach (var (di, dj, dk) in Sides)
            if (!cells.Contains(new CellIndex(cell.I + di, cell.J + dj, cell.K + dk))) return new Vector3(di, dj, dk);
        return Vector3.UnitZ;
    }

    /// <summary>An arrow from outside the part to <paramref name="target"/>, coming in along <paramref name="wayOut"/>.</summary>
    public static void AddPointer(GlyphMesh glyphs, Vector3 target, Vector3 wayOut, float length, Vector4 colour) =>
        glyphs.AddArrow(target + length * Vector3.Normalize(wayOut), target, length / 14, colour);

    /// <summary>
    /// The outline of the layer line under layer <paramref name="layer"/>: of the cells of that layer that
    /// lie on a cell below, the bottom edges of the sides that are not next to another such cell. Point
    /// pairs in the print frame, moved by <paramref name="displacement"/> for the deformed shape.
    /// </summary>
    /// <param name="cells">The cells of <paramref name="mesh"/>, to look up the one below.</param>
    public static Vector3[] LayerLine(FemMesh mesh, IReadOnlySet<CellIndex> cells, int layer, Func<int, int, int, Vector3>? displacement = null)
    {
        var bonded = new HashSet<(int I, int J)>();
        foreach (var c in mesh.Cells)
            if (c.K == layer && cells.Contains(c with { K = layer - 1 })) bonded.Add((c.I, c.J));

        var points = new List<Vector3>();
        foreach (var (i, j) in bonded)
        {
            // Each side's edge on the layer line, from one corner to the other.
            if (!bonded.Contains((i - 1, j))) Edge(i, j, i, j + 1);
            if (!bonded.Contains((i + 1, j))) Edge(i + 1, j, i + 1, j + 1);
            if (!bonded.Contains((i, j - 1))) Edge(i, j, i + 1, j);
            if (!bonded.Contains((i, j + 1))) Edge(i, j + 1, i + 1, j + 1);
        }
        return [.. points];

        void Edge(int i0, int j0, int i1, int j1)
        {
            points.Add(Point(i0, j0));
            points.Add(Point(i1, j1));
        }

        Vector3 Point(int i, int j)
        {
            var position = mesh.Grid.NodePosition(i, j, layer);
            return displacement is null ? position : position + displacement(i, j, layer);
        }
    }
}
