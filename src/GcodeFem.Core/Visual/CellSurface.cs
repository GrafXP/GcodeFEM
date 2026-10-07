using System.Numerics;
using GcodeFem.Core.Fem;

namespace GcodeFem.Core.Visual;

/// <summary>A box of cells, low bounds included and high bounds excluded: the part of the grid a view shows.</summary>
public readonly record struct CellRange(int ILow, int IHigh, int JLow, int JHigh, int KLow, int KHigh)
{
    public static CellRange All(CellGrid grid) => new(0, grid.SizeX, 0, grid.SizeY, 0, grid.SizeZ);

    /// <summary>The whole grid in X and Y, layers <paramref name="low"/> up to but not including <paramref name="high"/>.</summary>
    public static CellRange Layers(CellGrid grid, int low, int high) =>
        new(0, grid.SizeX, 0, grid.SizeY, Math.Clamp(low, 0, grid.SizeZ), Math.Clamp(high, 0, grid.SizeZ));

    public bool IsEmpty => IHigh <= ILow || JHigh <= JLow || KHigh <= KLow;

    public bool Contains(CellIndex c) => c.I >= ILow && c.I < IHigh && c.J >= JLow && c.J < JHigh && c.K >= KLow && c.K < KHigh;
}

/// <summary>
/// The faces of a set of cells that can be seen from outside: those next to air that connects to
/// the outside of the shown range. Sealed voids stay hidden, which leaves little more than the skin
/// of a whole part, while a range that cuts the part open shows the infill behind the cut.
/// Every face has its own four vertices, so it can carry its cell's colour.
/// </summary>
public sealed class CellSurface
{
    static readonly (int Di, int Dj, int Dk)[] Neighbours = [(-1, 0, 0), (1, 0, 0), (0, -1, 0), (0, 1, 0), (0, 0, -1), (0, 0, 1)];

    /// <summary>Per neighbour direction: the face's corners as offsets from the cell, counter-clockwise seen from outside.</summary>
    static readonly (int I, int J, int K)[][] Corners =
    [
        [(0, 0, 0), (0, 0, 1), (0, 1, 1), (0, 1, 0)],
        [(1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1)],
        [(0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)],
        [(0, 1, 0), (0, 1, 1), (1, 1, 1), (1, 1, 0)],
        [(0, 0, 0), (0, 1, 0), (1, 1, 0), (1, 0, 0)],
        [(0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)],
    ];

    /// <summary>Four per face, print frame.</summary>
    public required Vector3[] Positions { get; init; }

    public required Vector3[] Normals { get; init; }

    /// <summary>Two triangles per face.</summary>
    public required int[] Indices { get; init; }

    /// <summary>Per face: the index of its cell in the list the surface was built from.</summary>
    public required int[] FaceCells { get; init; }

    /// <summary>Per vertex: its grid node, numbered like <see cref="FemMesh.Dense"/>.</summary>
    public required int[] VertexNodes { get; init; }

    public int FaceCount => FaceCells.Length;

    public static CellSurface Build(CellGrid grid, IReadOnlyList<CellIndex> cells, CellRange range)
    {
        if (range.IsEmpty) return new CellSurface { Positions = [], Normals = [], Indices = [], FaceCells = [], VertexNodes = [] };

        // One slot per cell of the range, plus a ring of air around it for the flood to travel in.
        int nx = range.IHigh - range.ILow + 2, ny = range.JHigh - range.JLow + 2, nz = range.KHigh - range.KLow + 2;
        long slots = (long)nx * ny * nz;
        if (slots > int.MaxValue) throw new InvalidOperationException("The cell range is too large to display.");
        const byte Material = 1, Outside = 2;
        var state = new byte[slots];
        int Slot(int i, int j, int k) => i - range.ILow + 1 + nx * (j - range.JLow + 1 + ny * (k - range.KLow + 1));
        foreach (var c in cells)
            if (range.Contains(c)) state[Slot(c.I, c.J, c.K)] = Material;

        var pending = new Stack<int>();
        state[0] = Outside;
        pending.Push(0);
        while (pending.TryPop(out var slot))
        {
            int i = slot % nx, j = slot / nx % ny, k = slot / (nx * ny);
            foreach (var (di, dj, dk) in Neighbours)
            {
                int ni = i + di, nj = j + dj, nk = k + dk;
                if ((uint)ni >= (uint)nx || (uint)nj >= (uint)ny || (uint)nk >= (uint)nz) continue;
                var next = ni + nx * (nj + ny * nk);
                if (state[next] != 0) continue;
                state[next] = Outside;
                pending.Push(next);
            }
        }

        var faceCells = new List<int>();
        var faceSides = new List<byte>();
        for (var e = 0; e < cells.Count; e++)
        {
            var c = cells[e];
            if (!range.Contains(c)) continue;
            for (var side = 0; side < 6; side++)
            {
                var (di, dj, dk) = Neighbours[side];
                if (state[Slot(c.I + di, c.J + dj, c.K + dk)] != Outside) continue;
                faceCells.Add(e);
                faceSides.Add((byte)side);
            }
        }

        var positions = new Vector3[4 * faceCells.Count];
        var normals = new Vector3[positions.Length];
        var nodes = new int[positions.Length];
        var indices = new int[6 * faceCells.Count];
        int nodesX = grid.SizeX + 1, nodesY = grid.SizeY + 1;
        Parallel.For(0, faceCells.Count, f =>
        {
            var c = cells[faceCells[f]];
            var (di, dj, dk) = Neighbours[faceSides[f]];
            var corners = Corners[faceSides[f]];
            for (var v = 0; v < 4; v++)
            {
                int i = c.I + corners[v].I, j = c.J + corners[v].J, k = c.K + corners[v].K;
                positions[4 * f + v] = grid.NodePosition(i, j, k);
                normals[4 * f + v] = new Vector3(di, dj, dk);
                nodes[4 * f + v] = i + nodesX * (j + nodesY * k);
            }
            int first = 4 * f, at = 6 * f;
            (indices[at], indices[at + 1], indices[at + 2]) = (first, first + 1, first + 2);
            (indices[at + 3], indices[at + 4], indices[at + 5]) = (first, first + 2, first + 3);
        });
        return new CellSurface { Positions = positions, Normals = normals, Indices = indices, FaceCells = [.. faceCells], VertexNodes = nodes };
    }

    /// <summary>Per-vertex colours from one colour per cell.</summary>
    public Vector4[] VertexColours(Func<int, Vector4> cellColour)
    {
        var colours = new Vector4[Positions.Length];
        Parallel.For(0, FaceCount, f => colours.AsSpan(4 * f, 4).Fill(cellColour(FaceCells[f])));
        return colours;
    }

    /// <summary>Per-vertex colours from one colour per face.</summary>
    public Vector4[] FaceColours(Func<int, Vector4> faceColour)
    {
        var colours = new Vector4[Positions.Length];
        Parallel.For(0, FaceCount, f => colours.AsSpan(4 * f, 4).Fill(faceColour(f)));
        return colours;
    }

    public Vector3 FaceCentre(int face) => (Positions[4 * face] + Positions[4 * face + 2]) / 2;

    /// <summary>Outward, along one of the grid's directions.</summary>
    public Vector3 FaceNormal(int face) => Normals[4 * face];

    /// <summary>
    /// The vertices moved by <paramref name="scale"/> times their node's displacement: the deformed
    /// shape. <paramref name="nodeDisplacements"/> holds one vector per node of <paramref name="mesh"/>,
    /// whose cells the surface must have been built from.
    /// </summary>
    public Vector3[] Displaced(FemMesh mesh, Vector3[] nodeDisplacements, float scale)
    {
        var moved = new Vector3[Positions.Length];
        Parallel.For(0, moved.Length, v =>
        {
            var (i, j, k) = mesh.Undense(VertexNodes[v]);
            moved[v] = Positions[v] + scale * nodeDisplacements[mesh.NodeAt(i, j, k)];
        });
        return moved;
    }
}
