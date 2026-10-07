namespace GcodeFem.Core.Fem;

/// <summary>
/// The finite-element mesh of an <see cref="OctreeForest"/> as it stands: its leaves as hex
/// elements, their corner nodes, and the hanging nodes that 2:1 refinement leaves on the edges and
/// faces of a coarser neighbour. A hanging node is no unknown: it follows its coarse edge or face
/// by linear interpolation (u = T û), and assembly folds that into the matrix (K̂ = Tᵀ K T).
/// Loads and fixtures stay defined on the bead cells and are carried over by the same interpolation.
/// </summary>
public sealed class OctreeMesh
{
    const int CoordinateBits = 21;
    const long CoordinateMask = (1L << CoordinateBits) - 1;

    readonly Dictionary<long, int> leafIndex;
    readonly double[][] matrix;        // per leaf: stiffness = matrix × scale
    readonly double[] scale;
    readonly long[] nodeKeys;          // sorted by k, then j, then i
    readonly int[] expansionStart;     // every node as a weighted mix of free nodes; a free node is itself
    readonly int[] expansionFree;
    readonly double[] expansionWeight;

    OctreeMesh(OctreeForest forest)
    {
        Forest = forest;
        var leaves = forest.Leaves.ToArray();
        Array.Sort(leaves.Select(leaf => NodeKey(leaf.I, leaf.J, leaf.K)).ToArray(), leaves);
        Leaves = leaves;

        leafIndex = new Dictionary<long, int>(leaves.Length);
        matrix = new double[leaves.Length][];
        scale = new double[leaves.Length];
        for (var e = 0; e < leaves.Length; e++)
        {
            leafIndex[OctreeForest.Key(leaves[e])] = e;
            (matrix[e], scale[e]) = forest.Stiffness(leaves[e]);
        }

        // Nodes are the distinct leaf corners.
        var corners = new long[8 * leaves.Length];
        Parallel.For(0, leaves.Length, e =>
        {
            var leaf = leaves[e];
            for (var a = 0; a < 8; a++)
                corners[8 * e + a] = NodeKey(leaf.I + HexElement.CornerX[a] * leaf.Size, leaf.J + HexElement.CornerY[a] * leaf.Size, leaf.K + HexElement.CornerZ[a] * leaf.Size);
        });
        var sorted = (long[])corners.Clone();
        Array.Sort(sorted);
        var nodes = 0;
        for (var n = 0; n < sorted.Length; n++)
            if (n == 0 || sorted[n] != sorted[n - 1]) sorted[nodes++] = sorted[n];
        nodeKeys = sorted[..nodes];
        var leafNodes = LeafNodes = new int[corners.Length];
        Parallel.For(0, corners.Length, c => leafNodes[c] = Array.BinarySearch(nodeKeys, corners[c]));

        // A node in the middle of a coarser leaf's edge or face hangs on that edge's or face's corners.
        var masterCount = new byte[nodes];
        var masters = new int[4 * nodes];
        var weights = new double[4 * nodes];
        Span<int> found = stackalloc int[4];
        Span<double> foundWeight = stackalloc double[4];
        for (var e = 0; e < leaves.Length; e++)
        {
            var leaf = leaves[e];
            if (leaf.Level == 0) continue;
            var half = leaf.Size / 2;
            var middle = (forest.Z(leaf.K + half) - forest.Z(leaf.K)) / (forest.Z(leaf.K + leaf.Size) - forest.Z(leaf.K));
            for (var c = 0; c <= 2; c++)
            for (var b = 0; b <= 2; b++)
            for (var a = 0; a <= 2; a++)
            {
                var halves = (a == 1 ? 1 : 0) + (b == 1 ? 1 : 0) + (c == 1 ? 1 : 0);
                if (halves is 0 or 3) continue; // a corner, or the leaf's own centre
                var node = Array.BinarySearch(nodeKeys, NodeKey(leaf.I + a * half, leaf.J + b * half, leaf.K + c * half));
                if (node < 0) continue;

                var count = 0;
                for (var corner = 0; corner < 8; corner++)
                {
                    if ((a != 1 && 2 * HexElement.CornerX[corner] != a) || (b != 1 && 2 * HexElement.CornerY[corner] != b) || (c != 1 && 2 * HexElement.CornerZ[corner] != c))
                        continue;
                    found[count] = leafNodes[8 * e + corner];
                    foundWeight[count] = (a == 1 ? 0.5 : 1) * (b == 1 ? 0.5 : 1) * (c != 1 ? 1 : HexElement.CornerZ[corner] == 1 ? middle : 1 - middle);
                    count++;
                }
                if (masterCount[node] == 0)
                {
                    masterCount[node] = (byte)count;
                    for (var m = 0; m < count; m++) (masters[4 * node + m], weights[4 * node + m]) = (found[m], foundWeight[m]);
                }
                else if (masterCount[node] != count) throw new InvalidOperationException("The octree is not 2:1 balanced.");
            }
        }

        FreeIndex = new int[nodes];
        for (var n = 0; n < nodes; n++) FreeIndex[n] = masterCount[n] == 0 ? FreeCount++ : -1;

        expansionStart = new int[nodes + 1];
        for (var n = 0; n < nodes; n++) expansionStart[n + 1] = expansionStart[n] + Math.Max(1, (int)masterCount[n]);
        expansionFree = new int[expansionStart[nodes]];
        expansionWeight = new double[expansionStart[nodes]];
        for (var n = 0; n < nodes; n++)
        {
            var at = expansionStart[n];
            if (masterCount[n] == 0)
            {
                (expansionFree[at], expansionWeight[at]) = (FreeIndex[n], 1);
                continue;
            }
            for (var m = 0; m < masterCount[n]; m++)
            {
                var master = FreeIndex[masters[4 * n + m]];
                if (master < 0) throw new InvalidOperationException("A hanging node depends on another one: the octree is not 2:1 balanced.");
                (expansionFree[at + m], expansionWeight[at + m]) = (master, weights[4 * n + m]);
            }
        }

        var cells = forest.Mesh.Cells;
        var cellLeaf = CellLeaf = new int[cells.Length];
        Parallel.For(0, cells.Length, e =>
        {
            if (!TryFindLeaf(cells[e].I, cells[e].J, cells[e].K, out cellLeaf[e]))
                throw new InvalidOperationException("A bead cell lies outside the octree.");
        });
    }

    public static OctreeMesh Build(OctreeForest forest) => new(forest);

    public OctreeForest Forest { get; }
    public OctreeLeaf[] Leaves { get; }

    /// <summary>8 node indices per leaf, in <see cref="HexElement"/> order.</summary>
    public int[] LeafNodes { get; }

    /// <summary>All nodes, hanging ones included.</summary>
    public int NodeCount => nodeKeys.Length;

    /// <summary>Nodes that carry unknowns.</summary>
    public int FreeCount { get; }

    public int HangingCount => NodeCount - FreeCount;
    public int Dofs => 3 * FreeCount;

    /// <summary>Per node: which free node it is, or −1 if it hangs.</summary>
    public int[] FreeIndex { get; }

    /// <summary>Per bead cell of the forest's mesh: the leaf it lies in.</summary>
    public int[] CellLeaf { get; }

    /// <summary>Number of leaves at each level, bead cells first.</summary>
    public int[] LeavesByLevel()
    {
        var counts = new int[Forest.RootLevel + 1];
        foreach (var leaf in Leaves) counts[leaf.Level]++;
        return counts;
    }

    /// <summary>Per bead cell: the level of the leaf it lies in (0 = the cell is its own element).</summary>
    public byte[] CellLevels()
    {
        var levels = new byte[CellLeaf.Length];
        for (var e = 0; e < levels.Length; e++) levels[e] = (byte)Leaves[CellLeaf[e]].Level;
        return levels;
    }

    public (int I, int J, int K) NodeGrid(int node)
    {
        var key = nodeKeys[node];
        return ((int)(key & CoordinateMask), (int)((key >> CoordinateBits) & CoordinateMask), (int)(key >> (2 * CoordinateBits)));
    }

    public bool TryLeafIndex(OctreeLeaf leaf, out int index)
    {
        index = -1;
        return leaf is { I: >= 0, J: >= 0, K: >= 0 } && leafIndex.TryGetValue(OctreeForest.Key(leaf), out index);
    }

    /// <summary>The leaf whose box contains cell (i, j, k), if there is one.</summary>
    public bool TryFindLeaf(int i, int j, int k, out int index)
    {
        index = -1;
        if (i < 0 || j < 0 || k < 0) return false;
        for (var level = 0; level <= Forest.RootLevel; level++)
            if (leafIndex.TryGetValue(OctreeForest.Key(level, i >> level, j >> level, k >> level), out index))
                return true;
        return false;
    }

    /// <summary>
    /// K̂ û = f̂ over the free nodes. Each row is built by the node that owns it, from the leaf
    /// corners that lean on that node, so rows are assembled in parallel without locks.
    /// </summary>
    public LinearSystem Assemble(double[] rightHandSide, bool[] fixedDofs)
    {
        // Per free node: the leaf corners that are this node or hang on it, with their weight.
        var start = new int[FreeCount + 1];
        foreach (var node in LeafNodes)
            for (var x = expansionStart[node]; x < expansionStart[node + 1]; x++)
                start[expansionFree[x] + 1]++;
        for (var a = 0; a < FreeCount; a++) start[a + 1] += start[a];
        var corner = new int[start[FreeCount]]; // 8 × leaf + local corner
        var weight = new double[start[FreeCount]];
        var next = (int[])start.Clone();
        for (var c = 0; c < LeafNodes.Length; c++)
            for (var x = expansionStart[LeafNodes[c]]; x < expansionStart[LeafNodes[c] + 1]; x++)
            {
                var at = next[expansionFree[x]]++;
                (corner[at], weight[at]) = (c, expansionWeight[x]);
            }

        var capacity = 0;
        for (var a = 0; a < FreeCount; a++) capacity = Math.Max(capacity, 32 * (start[a + 1] - start[a]));

        // The free nodes that node a couples to: sorted, each once.
        int Neighbours(int a, int[] buffer)
        {
            var n = 0;
            for (var p = start[a]; p < start[a + 1]; p++)
            {
                var first = corner[p] & ~7;
                for (var d = 0; d < 8; d++)
                {
                    var node = LeafNodes[first + d];
                    for (var x = expansionStart[node]; x < expansionStart[node + 1]; x++) buffer[n++] = expansionFree[x];
                }
            }
            Array.Sort(buffer, 0, n);
            var unique = 0;
            for (var m = 0; m < n; m++)
                if (m == 0 || buffer[m] != buffer[m - 1]) buffer[unique++] = buffer[m];
            return unique;
        }

        var width = new int[FreeCount];
        Parallel.For(0, FreeCount, () => new int[capacity], (a, _, buffer) =>
        {
            width[a] = Neighbours(a, buffer);
            return buffer;
        }, _ => { });

        var rowPointers = new int[Dofs + 1];
        long running = 0;
        for (var a = 0; a < FreeCount; a++)
        for (var r = 0; r < 3; r++)
        {
            rowPointers[3 * a + r] = checked((int)running);
            running += 3 * width[a];
        }
        rowPointers[Dofs] = checked((int)running);

        var columns = new int[running];
        var values = new double[running];
        Parallel.For(0, FreeCount, () => (Nodes: new int[capacity], Blocks: new double[9 * capacity]), (a, _, work) =>
        {
            var count = Neighbours(a, work.Nodes);
            Array.Clear(work.Blocks, 0, 9 * count);
            for (var p = start[a]; p < start[a + 1]; p++)
            {
                int leaf = corner[p] >> 3, c = corner[p] & 7;
                var ke = matrix[leaf];
                var rowWeight = weight[p] * scale[leaf];
                for (var d = 0; d < 8; d++)
                {
                    var node = LeafNodes[8 * leaf + d];
                    for (var x = expansionStart[node]; x < expansionStart[node + 1]; x++)
                    {
                        var block = 9 * Array.BinarySearch(work.Nodes, 0, count, expansionFree[x]);
                        var factor = rowWeight * expansionWeight[x];
                        for (var r = 0; r < 3; r++)
                        for (var q = 0; q < 3; q++)
                            work.Blocks[block + 3 * r + q] += factor * ke[(3 * c + r) * HexElement.Dofs + 3 * d + q];
                    }
                }
            }
            for (var r = 0; r < 3; r++)
            {
                var row = rowPointers[3 * a + r];
                for (var m = 0; m < count; m++)
                for (var q = 0; q < 3; q++)
                {
                    columns[row + 3 * m + q] = 3 * work.Nodes[m] + q;
                    values[row + 3 * m + q] = work.Blocks[9 * m + 3 * r + q];
                }
            }
            return work;
        }, _ => { });

        var coordinates = new double[Dofs];
        for (var n = 0; n < NodeCount; n++)
        {
            if (FreeIndex[n] < 0) continue;
            var (i, j, k) = NodeGrid(n);
            (coordinates[3 * FreeIndex[n]], coordinates[3 * FreeIndex[n] + 1], coordinates[3 * FreeIndex[n] + 2]) = (Forest.X(i), Forest.Y(j), Forest.Z(k));
        }

        var system = new LinearSystem
        {
            Size = Dofs,
            RowPointers = rowPointers,
            Columns = columns,
            Values = values,
            RightHandSide = rightHandSide,
            Coordinates = coordinates,
            Fixed = fixedDofs,
        };
        Assembler.ApplyFixtures(system);
        return system;
    }

    /// <summary>
    /// f̂ = Tᵀ Pᵀ f: the load on each bead node goes to the corners of a leaf it lies in, by their
    /// shape functions. <paramref name="fineLoads"/> holds fx, fy, fz per node of the forest's mesh.
    /// </summary>
    public double[] RestrictLoads(double[] fineLoads)
    {
        var mesh = Forest.Mesh;
        var loads = new double[Dofs];
        Span<double> shape = stackalloc double[8];
        for (var n = 0; n < mesh.NodeCount; n++)
        {
            if (fineLoads[3 * n] == 0 && fineLoads[3 * n + 1] == 0 && fineLoads[3 * n + 2] == 0) continue;
            var (i, j, k) = mesh.Undense(mesh.DenseOfNode[n]);
            var leaf = LeafAtNode(i, j, k);
            ShapeAt(leaf, i, j, k, shape);
            for (var c = 0; c < 8; c++)
            {
                if (shape[c] == 0) continue;
                var node = LeafNodes[8 * leaf + c];
                for (var x = expansionStart[node]; x < expansionStart[node + 1]; x++)
                for (var d = 0; d < 3; d++)
                    loads[3 * expansionFree[x] + d] += shape[c] * expansionWeight[x] * fineLoads[3 * n + d];
            }
        }
        return loads;
    }

    /// <summary>
    /// The unknowns to fix, from the bead-node DOFs a fixture holds. A held node sits on a corner,
    /// an edge, a face or the inside of its leaf, and every corner of that part of the leaf is
    /// fixed: exact once the leaf is a bead cell, and too stiff rather than too loose before that.
    /// A hanging corner passes the hold on to the nodes it depends on.
    /// </summary>
    public bool[] RestrictFixtures(bool[] fineFixed)
    {
        var mesh = Forest.Mesh;
        var held = new bool[Dofs];
        for (var e = 0; e < mesh.Cells.Length; e++)
        for (var a = 0; a < 8; a++)
        {
            var fine = 3 * mesh.CellNodes[8 * e + a];
            if (!fineFixed[fine] && !fineFixed[fine + 1] && !fineFixed[fine + 2]) continue;
            var cell = mesh.Cells[e];
            var leaf = Leaves[CellLeaf[e]];
            int di = cell.I + HexElement.CornerX[a] - leaf.I, dj = cell.J + HexElement.CornerY[a] - leaf.J, dk = cell.K + HexElement.CornerZ[a] - leaf.K;
            for (var c = 0; c < 8; c++)
            {
                if (!Reaches(di, HexElement.CornerX[c], leaf.Size) || !Reaches(dj, HexElement.CornerY[c], leaf.Size) || !Reaches(dk, HexElement.CornerZ[c], leaf.Size))
                    continue;
                var node = LeafNodes[8 * CellLeaf[e] + c];
                for (var x = expansionStart[node]; x < expansionStart[node + 1]; x++)
                for (var d = 0; d < 3; d++)
                    held[3 * expansionFree[x] + d] |= fineFixed[fine + d];
            }
        }
        return held;

        // Along one axis: a point on the leaf's low or high side belongs to that side's corners only.
        static bool Reaches(int offset, int side, int size) => offset == 0 ? side == 0 : offset != size || side == 1;
    }

    /// <summary>ux, uy, uz for every node, hanging ones included, from the solved unknowns.</summary>
    public double[] NodeDisplacements(double[] unknowns)
    {
        var u = new double[3 * NodeCount];
        Parallel.For(0, NodeCount, n =>
        {
            for (var x = expansionStart[n]; x < expansionStart[n + 1]; x++)
            for (var d = 0; d < 3; d++)
                u[3 * n + d] += expansionWeight[x] * unknowns[3 * expansionFree[x] + d];
        });
        return u;
    }

    /// <summary>The displacement field at grid node (i, j, k) of the bead-cell grid.</summary>
    public (double X, double Y, double Z) DisplacementAt(double[] nodeDisplacements, int i, int j, int k)
    {
        var leaf = LeafAtNode(i, j, k);
        Span<double> shape = stackalloc double[8];
        ShapeAt(leaf, i, j, k, shape);
        double x = 0, y = 0, z = 0;
        for (var c = 0; c < 8; c++)
        {
            var at = 3 * LeafNodes[8 * leaf + c];
            x += shape[c] * nodeDisplacements[at];
            y += shape[c] * nodeDisplacements[at + 1];
            z += shape[c] * nodeDisplacements[at + 2];
        }
        return (x, y, z);
    }

    /// <summary>Unknowns for this mesh read off an earlier mesh's solution: the start guess after refining.</summary>
    public double[] Interpolate(OctreeMesh earlier, double[] earlierNodeDisplacements)
    {
        var unknowns = new double[Dofs];
        Parallel.For(0, NodeCount, n =>
        {
            var free = FreeIndex[n];
            if (free < 0) return;
            var (i, j, k) = NodeGrid(n);
            (unknowns[3 * free], unknowns[3 * free + 1], unknowns[3 * free + 2]) = earlier.DisplacementAt(earlierNodeDisplacements, i, j, k);
        });
        return unknowns;
    }

    /// <summary>
    /// Downscaling: each bead cell's stress at its centre, from its leaf's displacement field and
    /// the cell's own stiffness E φ. Returns σxx, σyy, σzz, τxy, τyz, τzx per cell and von Mises.
    /// </summary>
    public (float[] Stress, float[] VonMises) CellStresses(double[] nodeDisplacements, IsotropicMaterial material)
    {
        var mesh = Forest.Mesh;
        var unit = new IsotropicMaterial(1, material.PoissonRatio).Constitutive();
        var stress = new float[6 * mesh.Cells.Length];
        var vonMises = new float[mesh.Cells.Length];
        Parallel.For(0, mesh.Cells.Length, () => new double[6 * HexElement.Dofs], (e, _, b) =>
        {
            var cell = mesh.Cells[e];
            var leaf = Leaves[CellLeaf[e]];
            var side = leaf.Size * (double)mesh.Grid.Pitch;
            double low = Forest.Z(leaf.K), height = Forest.Z(leaf.K + leaf.Size) - low;
            HexElement.StrainMatrix(side, side, height,
                2 * (cell.I + 0.5 - leaf.I) / leaf.Size - 1,
                2 * (cell.J + 0.5 - leaf.J) / leaf.Size - 1,
                (Forest.Z(cell.K) + Forest.Z(cell.K + 1) - 2 * low) / height - 1, b);

            Span<double> strain = stackalloc double[6];
            for (var r = 0; r < 6; r++)
            {
                double sum = 0;
                for (var a = 0; a < 8; a++)
                {
                    var node = LeafNodes[8 * CellLeaf[e] + a];
                    for (var d = 0; d < 3; d++) sum += b[r * HexElement.Dofs + 3 * a + d] * nodeDisplacements[3 * node + d];
                }
                strain[r] = sum;
            }
            var stiffness = material.YoungsModulus * mesh.Fill[e];
            Span<double> s = stackalloc double[6];
            for (var r = 0; r < 6; r++)
            {
                double sum = 0;
                for (var m = 0; m < 6; m++) sum += unit[r * 6 + m] * strain[m];
                s[r] = sum * stiffness;
                stress[6 * e + r] = (float)s[r];
            }
            vonMises[e] = (float)Math.Sqrt(0.5 * ((s[0] - s[1]) * (s[0] - s[1]) + (s[1] - s[2]) * (s[1] - s[2]) + (s[2] - s[0]) * (s[2] - s[0]))
                                           + 3 * (s[3] * s[3] + s[4] * s[4] + s[5] * s[5]));
            return b;
        }, _ => { });
        return (stress, vonMises);
    }

    /// <summary>Strain energy ½ uᵀ K u held by each leaf.</summary>
    public double[] LeafEnergies(double[] nodeDisplacements)
    {
        var energy = new double[Leaves.Length];
        Parallel.For(0, Leaves.Length, e =>
        {
            Span<double> u = stackalloc double[HexElement.Dofs];
            for (var a = 0; a < 8; a++)
            for (var d = 0; d < 3; d++)
                u[3 * a + d] = nodeDisplacements[3 * LeafNodes[8 * e + a] + d];
            var ke = matrix[e];
            double sum = 0;
            for (var r = 0; r < HexElement.Dofs; r++)
            for (var c = 0; c < HexElement.Dofs; c++)
                sum += u[r] * ke[r * HexElement.Dofs + c] * u[c];
            energy[e] = 0.5 * scale[e] * sum;
        });
        return energy;
    }

    static long NodeKey(int i, int j, int k) => (uint)i | ((long)j << CoordinateBits) | ((long)k << (2 * CoordinateBits));

    /// <summary>A leaf that has grid node (i, j, k) inside it or on its surface.</summary>
    int LeafAtNode(int i, int j, int k)
    {
        for (var corner = 0; corner < 8; corner++)
            if (TryFindLeaf(i - HexElement.CornerX[corner], j - HexElement.CornerY[corner], k - HexElement.CornerZ[corner], out var leaf))
                return leaf;
        throw new InvalidOperationException($"Grid node ({i}, {j}, {k}) lies outside the octree.");
    }

    /// <summary>The leaf's corner shape functions at grid node (i, j, k).</summary>
    void ShapeAt(int leaf, int i, int j, int k, Span<double> shape)
    {
        var box = Leaves[leaf];
        double size = box.Size, low = Forest.Z(box.K);
        OctreeForest.Shape((i - box.I) / size, (j - box.J) / size, (Forest.Z(k) - low) / (Forest.Z(box.K + box.Size) - low), shape);
    }
}
