using System.Numerics;

namespace GcodeFem.Core.Fem;

/// <summary>One side of a cell that has no active neighbour.</summary>
/// <param name="Nodes">The face's four corner nodes (compact indices).</param>
public readonly record struct BoundaryFace(int Cell, Vector3 Centre, Vector3 Normal, float Area, int N0, int N1, int N2, int N3);

/// <summary>
/// The cells that take part in the analysis and their corner nodes. Built from the grid's
/// occupied cells, keeping only face-connected groups that touch a fixture: a group that hangs
/// on by an edge or corner, or floats free, would make the stiffness matrix singular.
/// </summary>
public sealed class FemMesh
{
    static readonly (int Di, int Dj, int Dk)[] FaceNeighbours = [(-1, 0, 0), (1, 0, 0), (0, -1, 0), (0, 1, 0), (0, 0, -1), (0, 0, 1)];

    readonly int[] denseNode;

    FemMesh(CellGrid grid, CellIndex[] cells, float[] fill, int droppedCells, double droppedVolume)
    {
        Grid = grid;
        Cells = cells;
        Fill = fill;
        DroppedCells = droppedCells;
        DroppedVolume = droppedVolume;
        NodesX = grid.SizeX + 1;
        NodesY = grid.SizeY + 1;

        denseNode = new int[NodesX * NodesY * (grid.SizeZ + 1)];
        Array.Fill(denseNode, -1);
        foreach (var c in cells)
            for (var a = 0; a < HexElement.Nodes; a++)
                denseNode[Dense(c.I + HexElement.CornerX[a], c.J + HexElement.CornerY[a], c.K + HexElement.CornerZ[a])] = 0;

        var nodeCount = 0;
        var denseOfNode = new List<int>();
        for (var d = 0; d < denseNode.Length; d++)
            if (denseNode[d] == 0)
            {
                denseNode[d] = nodeCount++;
                denseOfNode.Add(d);
            }
        NodeCount = nodeCount;
        DenseOfNode = [.. denseOfNode];

        CellNodes = new int[cells.Length * HexElement.Nodes];
        for (var e = 0; e < cells.Length; e++)
        for (var a = 0; a < HexElement.Nodes; a++)
            CellNodes[e * 8 + a] = denseNode[Dense(cells[e].I + HexElement.CornerX[a], cells[e].J + HexElement.CornerY[a], cells[e].K + HexElement.CornerZ[a])];

        Coordinates = new double[3 * nodeCount];
        for (var n = 0; n < nodeCount; n++)
        {
            var (i, j, k) = Undense(DenseOfNode[n]);
            var p = grid.NodePosition(i, j, k);
            Coordinates[3 * n] = p.X;
            Coordinates[3 * n + 1] = p.Y;
            Coordinates[3 * n + 2] = p.Z;
        }
    }

    public CellGrid Grid { get; }
    public CellIndex[] Cells { get; }
    public float[] Fill { get; }

    /// <summary>8 compact node indices per cell, in <see cref="HexElement"/> order.</summary>
    public int[] CellNodes { get; }

    public int NodeCount { get; }

    /// <summary>x, y, z per node, print frame (mm).</summary>
    public double[] Coordinates { get; }

    /// <summary>Dense grid-node index per compact node; ascending.</summary>
    public int[] DenseOfNode { get; }

    public int NodesX { get; }
    public int NodesY { get; }

    /// <summary>Occupied cells left out because their group doesn't reach a fixture.</summary>
    public int DroppedCells { get; }
    public double DroppedVolume { get; }

    public Vector3 NodePosition(int node) =>
        new((float)Coordinates[3 * node], (float)Coordinates[3 * node + 1], (float)Coordinates[3 * node + 2]);

    public int Dense(int i, int j, int k) => i + NodesX * (j + NodesY * k);

    public (int I, int J, int K) Undense(int dense) =>
        (dense % NodesX, dense / NodesX % NodesY, dense / (NodesX * NodesY));

    /// <summary>Compact index of grid node (i, j, k), or −1 if no active cell uses it.</summary>
    public int NodeAt(int i, int j, int k) =>
        i < 0 || j < 0 || k < 0 || i >= NodesX || j >= NodesY || k > Grid.SizeZ ? -1 : denseNode[Dense(i, j, k)];

    public static FemMesh Build(CellGrid grid, float minFill, IReadOnlyList<Fixture> fixtures) => Build(grid, minFill, new LoadCase(fixtures, []));

    /// <summary>The mesh for a load case: the cells that hang together with something one of its mounts holds.</summary>
    public static FemMesh Build(CellGrid grid, float minFill, LoadCase loadCase)
    {
        var (fixtures, faceFixtures) = (loadCase.Fixtures, loadCase.FaceFixtures);
        var occupied = grid.Occupied(minFill).ToArray();
        if (occupied.Length == 0) throw new InvalidOperationException("No cells reach the minimum fill.");

        // Face-connected groups (breadth-first over a dense cell index).
        long cellSlots = (long)grid.SizeX * grid.SizeY * grid.SizeZ;
        if (cellSlots > int.MaxValue) throw new InvalidOperationException("Cell grid too large for a dense index.");
        var slot = new int[cellSlots];
        Array.Fill(slot, -1);
        int Slot(int i, int j, int k) => i + grid.SizeX * (j + grid.SizeY * k);
        for (var c = 0; c < occupied.Length; c++) slot[Slot(occupied[c].I, occupied[c].J, occupied[c].K)] = c;

        var group = new int[occupied.Length];
        Array.Fill(group, -1);
        var groups = 0;
        var queue = new Queue<int>();
        for (var seed = 0; seed < occupied.Length; seed++)
        {
            if (group[seed] >= 0) continue;
            group[seed] = groups;
            queue.Enqueue(seed);
            while (queue.Count > 0)
            {
                var c = occupied[queue.Dequeue()];
                foreach (var (di, dj, dk) in FaceNeighbours)
                {
                    int i = c.I + di, j = c.J + dj, k = c.K + dk;
                    if (!grid.Contains(i, j, k)) continue;
                    var n = slot[Slot(i, j, k)];
                    if (n >= 0 && group[n] < 0)
                    {
                        group[n] = groups;
                        queue.Enqueue(n);
                    }
                }
            }
            groups++;
        }

        // Keep the groups that have at least one fixed corner, or a free side that a mount holds.
        var anchored = new bool[groups];
        for (var c = 0; c < occupied.Length; c++)
        {
            if (anchored[group[c]]) continue;
            var cell = occupied[c];
            for (var a = 0; a < HexElement.Nodes && !anchored[group[c]]; a++)
            {
                var p = grid.NodePosition(cell.I + HexElement.CornerX[a], cell.J + HexElement.CornerY[a], cell.K + HexElement.CornerZ[a]);
                if (fixtures.Any(f => f.SelectsNode(p))) anchored[group[c]] = true;
            }
            if (faceFixtures.Count == 0) continue;
            foreach (var (di, dj, dk) in FaceNeighbours)
            {
                if (anchored[group[c]]) break;
                int i = cell.I + di, j = cell.J + dj, k = cell.K + dk;
                if (grid.Contains(i, j, k) && slot[Slot(i, j, k)] >= 0) continue;
                var (centre, normal) = (FaceCentre(grid, cell, di, dj, dk), new Vector3(di, dj, dk));
                if (faceFixtures.Any(f => f.Holds(centre, normal) != Axes.None)) anchored[group[c]] = true;
            }
        }
        if (!anchored.Any(a => a)) throw new InvalidOperationException("No fixture touches the part.");

        var kept = new List<CellIndex>(occupied.Length);
        var fill = new List<float>(occupied.Length);
        var droppedVolume = 0.0;
        for (var c = 0; c < occupied.Length; c++)
        {
            var cell = occupied[c];
            if (anchored[group[c]])
            {
                kept.Add(cell);
                fill.Add(grid.Fill(cell.I, cell.J, cell.K));
            }
            else droppedVolume += grid.Volume(cell.I, cell.J, cell.K);
        }

        return new FemMesh(grid, [.. kept], [.. fill], occupied.Length - kept.Count, droppedVolume);
    }

    /// <summary>Faces of active cells that border no active cell, with outward normals.</summary>
    public IEnumerable<BoundaryFace> BoundaryFaces()
    {
        var active = new HashSet<CellIndex>(Cells);
        float p = Grid.Pitch;
        for (var e = 0; e < Cells.Length; e++)
        {
            var c = Cells[e];
            var h = Grid.CellHeight(c.K);
            foreach (var (di, dj, dk) in FaceNeighbours)
            {
                if (active.Contains(new CellIndex(c.I + di, c.J + dj, c.K + dk))) continue;
                var normal = new Vector3(di, dj, dk);
                var area = dk != 0 ? p * p : p * h;
                var nodes = FaceCorners(e, di, dj, dk);
                yield return new BoundaryFace(e, FaceCentre(Grid, c, di, dj, dk), normal, area, nodes[0], nodes[1], nodes[2], nodes[3]);
            }
        }
    }

    /// <summary>The middle of the side of <paramref name="cell"/> that faces (di, dj, dk).</summary>
    static Vector3 FaceCentre(CellGrid grid, CellIndex cell, int di, int dj, int dk)
    {
        float p = grid.Pitch, h = grid.CellHeight(cell.K);
        return grid.NodePosition(cell.I, cell.J, cell.K) + new Vector3((1 + di) * p / 2, (1 + dj) * p / 2, (1 + dk) * h / 2);
    }

    int[] FaceCorners(int cell, int di, int dj, int dk)
    {
        var corners = new int[4];
        var n = 0;
        for (var a = 0; a < HexElement.Nodes; a++)
        {
            var onFace = (di != 0 && HexElement.CornerX[a] == (di + 1) / 2)
                         || (dj != 0 && HexElement.CornerY[a] == (dj + 1) / 2)
                         || (dk != 0 && HexElement.CornerZ[a] == (dk + 1) / 2);
            if (onFace) corners[n++] = CellNodes[cell * 8 + a];
        }
        return corners;
    }
}
