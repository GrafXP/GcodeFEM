namespace GcodeFem.Core.Fem;

/// <summary>An octree element: 2^Level cells per side, with cell (I, J, K) at its low corner.</summary>
public readonly record struct OctreeLeaf(int Level, int I, int J, int K)
{
    public int Size => 1 << Level;
}

/// <summary>
/// Sparse forest of octrees over a mesh's active bead cells. Bead cells are level 0 and the roots
/// are boxes of 2^RootLevel cells per side; a box exists only if it holds material. Every leaf is
/// one hex element whose stiffness is the Galerkin sum of its own cells, K = Σ Pᵀ K_cell P with P
/// the trilinear interpolation from the leaf's corners. A coarse element therefore already carries
/// its walls, infill lines and gaps, and comes out stiffer than the cells it replaces.
/// </summary>
public sealed class OctreeForest
{
    public const int MaxRootLevel = 5;
    const int BlockShift = 3;
    const int BlockMask = (1 << BlockShift) - 1;
    const int MatrixLength = HexElement.Dofs * HexElement.Dofs;

    readonly Dictionary<int, float[]> fill = [];       // active-cell fill per 8³ block; 0 = no cell
    readonly HashSet<long>[] occupied;                 // [level] boxes holding at least one active cell, levels 1..RootLevel
    readonly Dictionary<long, double[]?> leaves = [];  // leaf → its Galerkin stiffness (none at level 0)
    readonly double[][] unitStiffness;                 // per layer: one full cell with E = 1
    readonly double[] z;                               // layer boundaries, continued above the part
    readonly double youngsModulus, originX, originY, pitch;

    public OctreeForest(FemMesh mesh, IsotropicMaterial material, int rootLevel)
    {
        if (rootLevel is < 0 or > MaxRootLevel)
            throw new ArgumentOutOfRangeException(nameof(rootLevel), $"Root level must be 0..{MaxRootLevel}.");
        Mesh = mesh;
        RootLevel = rootLevel;
        youngsModulus = material.YoungsModulus;
        var grid = mesh.Grid;
        (originX, originY, pitch) = (grid.Origin.X, grid.Origin.Y, grid.Pitch);

        // Root boxes may stick out above the part; the layers they need there repeat the top one.
        var paddedZ = (((grid.SizeZ - 1) >> rootLevel) + 1) << rootLevel;
        z = new double[paddedZ + 1];
        for (var k = 0; k <= paddedZ; k++)
            z[k] = k <= grid.SizeZ
                ? grid.ZBoundaries[k]
                : grid.ZBoundaries[grid.SizeZ] + (k - grid.SizeZ) * (double)grid.CellHeight(grid.SizeZ - 1);

        var unit = new IsotropicMaterial(1, material.PoissonRatio).Constitutive();
        var byHeight = new Dictionary<float, double[]>();
        unitStiffness = new double[grid.SizeZ][];
        for (var k = 0; k < grid.SizeZ; k++)
        {
            var height = grid.CellHeight(k);
            if (!byHeight.TryGetValue(height, out var ke))
                byHeight[height] = ke = HexElement.Stiffness(grid.Pitch, grid.Pitch, height, unit);
            unitStiffness[k] = ke;
        }

        for (var e = 0; e < mesh.Cells.Length; e++)
        {
            var c = mesh.Cells[e];
            var key = BlockKey(c.I >> BlockShift, c.J >> BlockShift, c.K >> BlockShift);
            if (!fill.TryGetValue(key, out var block))
                fill[key] = block = new float[1 << (3 * BlockShift)];
            block[Local(c.I, c.J, c.K)] = mesh.Fill[e];
        }

        occupied = new HashSet<long>[rootLevel + 1];
        for (var level = 1; level <= rootLevel; level++)
        {
            var boxes = occupied[level] = [];
            if (level == 1)
                foreach (var c in mesh.Cells) boxes.Add(Key(1, c.I >> 1, c.J >> 1, c.K >> 1));
            else
                foreach (var child in occupied[level - 1])
                {
                    var box = Decode(child);
                    boxes.Add(Key(level, box.I >> level, box.J >> level, box.K >> level));
                }
        }

        if (rootLevel == 0)
            foreach (var c in mesh.Cells) leaves[Key(0, c.I, c.J, c.K)] = null;
        else
            foreach (var root in occupied[rootLevel]) leaves[root] = null;
        BuildStiffness();
    }

    public FemMesh Mesh { get; }
    public int RootLevel { get; }
    public int LeafCount => leaves.Count;
    public IEnumerable<OctreeLeaf> Leaves => leaves.Keys.Select(Decode);

    /// <summary>Print-frame coordinates of grid node planes; valid for the padding above the part too.</summary>
    public double X(int i) => originX + i * pitch;
    public double Y(int j) => originY + j * pitch;
    public double Z(int k) => z[k];

    /// <summary>The leaf whose box contains cell (i, j, k), if there is one.</summary>
    public bool TryFindLeaf(int i, int j, int k, out OctreeLeaf leaf)
    {
        if (i >= 0 && j >= 0 && k >= 0)
            for (var level = 0; level <= RootLevel; level++)
                if (leaves.ContainsKey(Key(level, i >> level, j >> level, k >> level)))
                {
                    leaf = new OctreeLeaf(level, i >> level << level, j >> level << level, k >> level << level);
                    return true;
                }
        leaf = default;
        return false;
    }

    /// <summary>The leaf's 24×24 stiffness as matrix × scale; bead cells share one matrix per layer height.</summary>
    public (double[] Matrix, double Scale) Stiffness(OctreeLeaf leaf) =>
        leaf.Level == 0
            ? (unitStiffness[leaf.K], youngsModulus * Fill(leaf.I, leaf.J, leaf.K))
            : (leaves[Key(leaf)]!, 1);

    /// <summary>
    /// Splits the given leaves into their occupied children and keeps the forest 2:1 balanced:
    /// leaves that touch by a face, an edge or a corner never differ by more than one level, so a
    /// split can force coarser neighbours to split too. Returns the number of leaves split.
    /// </summary>
    public int Refine(IEnumerable<OctreeLeaf> marked)
    {
        var queue = new Queue<OctreeLeaf>(marked.Where(leaf => leaf.Level > 0));
        var split = 0;
        while (queue.TryDequeue(out var leaf))
        {
            if (!leaves.Remove(Key(leaf))) continue; // already split
            split++;

            var half = leaf.Size / 2;
            for (var octant = 0; octant < 8; octant++)
            {
                var child = new OctreeLeaf(leaf.Level - 1, leaf.I + (octant & 1) * half, leaf.J + ((octant >> 1) & 1) * half, leaf.K + (octant >> 2) * half);
                if (Occupied(child)) leaves[Key(child)] = null;
            }

            if (leaf.Level == RootLevel) continue;
            var coarser = leaf.Level + 1;
            for (var dk = -1; dk <= 1; dk++)
            for (var dj = -1; dj <= 1; dj++)
            for (var di = -1; di <= 1; di++)
            {
                int i = leaf.I + di * leaf.Size, j = leaf.J + dj * leaf.Size, k = leaf.K + dk * leaf.Size;
                if (i < 0 || j < 0 || k < 0) continue;
                var neighbour = new OctreeLeaf(coarser, i >> coarser << coarser, j >> coarser << coarser, k >> coarser << coarser);
                if (leaves.ContainsKey(Key(neighbour))) queue.Enqueue(neighbour);
            }
        }
        BuildStiffness();
        return split;
    }

    internal static long Key(int level, int boxI, int boxJ, int boxK) =>
        (uint)level | ((long)boxI << 4) | ((long)boxJ << 24) | ((long)boxK << 44);

    internal static long Key(OctreeLeaf leaf) =>
        Key(leaf.Level, leaf.I >> leaf.Level, leaf.J >> leaf.Level, leaf.K >> leaf.Level);

    static OctreeLeaf Decode(long key)
    {
        var level = (int)(key & 15);
        return new OctreeLeaf(level, (int)((key >> 4) & 0xFFFFF) << level, (int)((key >> 24) & 0xFFFFF) << level, (int)((key >> 44) & 0xFFFFF) << level);
    }

    static int BlockKey(int bi, int bj, int bk) => bi | (bj << 10) | (bk << 20);

    static int Local(int i, int j, int k) =>
        (i & BlockMask) | ((j & BlockMask) << BlockShift) | ((k & BlockMask) << (2 * BlockShift));

    float Fill(int i, int j, int k) =>
        fill.TryGetValue(BlockKey(i >> BlockShift, j >> BlockShift, k >> BlockShift), out var block) ? block[Local(i, j, k)] : 0;

    bool Occupied(OctreeLeaf box) =>
        box.Level == 0 ? Fill(box.I, box.J, box.K) > 0 : occupied[box.Level].Contains(Key(box));

    /// <summary>Scratch space for one thread's Galerkin sums.</summary>
    sealed class Workspace(int levels)
    {
        public readonly double[][] Child = [.. Enumerable.Range(0, levels).Select(_ => new double[MatrixLength])];
        public readonly double[] Product = new double[MatrixLength];
        public readonly int[] Count = new int[8];
        public readonly int[] Index = new int[64];
        public readonly double[] Weight = new double[64];
    }

    void BuildStiffness()
    {
        var pending = leaves.Where(leaf => leaf.Value is null && (leaf.Key & 15) != 0).Select(leaf => leaf.Key).ToArray();
        var built = new double[pending.Length][];
        Parallel.For(0, pending.Length, () => new Workspace(RootLevel), (n, _, work) =>
        {
            var leaf = Decode(pending[n]);
            built[n] = new double[MatrixLength];
            if (!Accumulate(leaf.Level, leaf.I, leaf.J, leaf.K, null, built[n], work))
                throw new InvalidOperationException("An octree leaf holds no material.");
            return work;
        }, _ => { });
        for (var n = 0; n < pending.Length; n++) leaves[pending[n]] = built[n];
    }

    /// <summary>
    /// Stiffness of the box at (i0, j0, k0) into <paramref name="result"/>, summed bottom-up over its
    /// eight children. Trilinear interpolation composes exactly, so this equals summing every cell
    /// straight into the box's corners. Returns false when the box holds no active cell.
    /// </summary>
    bool Accumulate(int level, int i0, int j0, int k0, float[]? block, double[] result, Workspace work)
    {
        if (!occupied[level].Contains(Key(level, i0 >> level, j0 >> level, k0 >> level))) return false;
        if (level <= BlockShift) block ??= fill[BlockKey(i0 >> BlockShift, j0 >> BlockShift, k0 >> BlockShift)];

        Array.Clear(result);
        var half = 1 << (level - 1);
        // Layers can differ in height, so the children's shared plane is not always halfway up.
        var middle = (z[k0 + half] - z[k0]) / (z[k0 + 2 * half] - z[k0]);
        for (var octant = 0; octant < 8; octant++)
        {
            int ox = octant & 1, oy = (octant >> 1) & 1, oz = octant >> 2;
            int i = i0 + ox * half, j = j0 + oy * half, k = k0 + oz * half;
            double[] child;
            double scale;
            if (level == 1)
            {
                var cellFill = block![Local(i, j, k)];
                if (cellFill <= 0) continue;
                (child, scale) = (unitStiffness[k], youngsModulus * cellFill);
            }
            else
            {
                child = work.Child[level - 1];
                if (!Accumulate(level - 1, i, j, k, block, child, work)) continue;
                scale = 1;
            }
            Prolongation(ox, oy, oz, middle, work);
            AddProjected(child, scale, work, result);
        }
        return true;
    }

    /// <summary>P for one child: each of its corners as a weighted mix of 1, 2, 4 or 8 parent corners.</summary>
    static void Prolongation(int ox, int oy, int oz, double middle, Workspace work)
    {
        Span<double> shape = stackalloc double[8];
        for (var corner = 0; corner < 8; corner++)
        {
            var zFraction = (oz + HexElement.CornerZ[corner]) switch { 0 => 0, 1 => middle, _ => 1.0 };
            Shape((ox + HexElement.CornerX[corner]) * 0.5, (oy + HexElement.CornerY[corner]) * 0.5, zFraction, shape);
            var count = 0;
            for (var parent = 0; parent < 8; parent++)
            {
                if (shape[parent] == 0) continue;
                work.Index[corner * 8 + count] = parent;
                work.Weight[corner * 8 + count] = shape[parent];
                count++;
            }
            work.Count[corner] = count;
        }
    }

    /// <summary>result += Pᵀ (scale · child) P on 3×3 node blocks, using P's 27 non-zeros.</summary>
    static void AddProjected(double[] child, double scale, Workspace work, double[] result)
    {
        const int n = HexElement.Dofs;
        var product = work.Product; // child · P
        Array.Clear(product);
        for (var corner = 0; corner < 8; corner++)
        for (var m = 0; m < work.Count[corner]; m++)
        {
            var parent = 3 * work.Index[corner * 8 + m];
            var weight = work.Weight[corner * 8 + m] * scale;
            for (var row = 0; row < n; row++)
            {
                int from = row * n + 3 * corner, to = row * n + parent;
                product[to] += weight * child[from];
                product[to + 1] += weight * child[from + 1];
                product[to + 2] += weight * child[from + 2];
            }
        }
        for (var corner = 0; corner < 8; corner++)
        for (var m = 0; m < work.Count[corner]; m++)
        {
            var parent = 3 * work.Index[corner * 8 + m];
            var weight = work.Weight[corner * 8 + m];
            for (var p = 0; p < 3; p++)
            {
                int from = (3 * corner + p) * n, to = (parent + p) * n;
                for (var column = 0; column < n; column++) result[to + column] += weight * product[from + column];
            }
        }
    }

    /// <summary>Trilinear shape functions of a box's 8 corners at fractions x, y, z ∈ [0, 1] of its size.</summary>
    internal static void Shape(double x, double y, double z, Span<double> shape)
    {
        for (var corner = 0; corner < 8; corner++)
            shape[corner] = (HexElement.CornerX[corner] == 1 ? x : 1 - x)
                            * (HexElement.CornerY[corner] == 1 ? y : 1 - y)
                            * (HexElement.CornerZ[corner] == 1 ? z : 1 - z);
    }
}
