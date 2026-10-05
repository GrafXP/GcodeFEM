using System.Numerics;

namespace GcodeFem.Core.Fem;

public readonly record struct CellIndex(int I, int J, int K);

/// <summary>
/// Bead cells in the print frame: square in XY (one line width), one layer tall, so a cell is
/// roughly one piece of one line. Only blocks of 8×8×8 cells that received material are stored;
/// in M3 these blocks become the octree roots.
/// </summary>
public sealed class CellGrid
{
    public const int BlockSize = 8;
    const int BlockShift = 3;
    const int BlockMask = BlockSize - 1;

    readonly Dictionary<int, float[]> blocks = [];
    readonly float[] zBoundaries;

    /// <param name="origin">Print-frame XY of the low corner of cell (0, 0).</param>
    /// <param name="zBoundaries">Layer boundaries in the print frame: layer k spans zBoundaries[k]..zBoundaries[k + 1].</param>
    public CellGrid(Vector2 origin, float pitch, float[] zBoundaries, int sizeX, int sizeY)
    {
        if (pitch <= 0) throw new ArgumentOutOfRangeException(nameof(pitch));
        if (zBoundaries.Length < 2) throw new ArgumentException("Need at least one layer.", nameof(zBoundaries));
        for (var k = 1; k < zBoundaries.Length; k++)
            if (zBoundaries[k] <= zBoundaries[k - 1])
                throw new ArgumentException($"Layer boundaries must increase (layer {k - 1}).", nameof(zBoundaries));
        if ((sizeX >> BlockShift) >= 1024 || (sizeY >> BlockShift) >= 1024 || ((zBoundaries.Length - 1) >> BlockShift) >= 1024)
            throw new ArgumentException("Grid too large for 10-bit block coordinates.");

        Origin = origin;
        Pitch = pitch;
        this.zBoundaries = zBoundaries;
        SizeX = sizeX;
        SizeY = sizeY;
    }

    public Vector2 Origin { get; }
    public float Pitch { get; }
    public int SizeX { get; }
    public int SizeY { get; }
    public int SizeZ => zBoundaries.Length - 1;
    public ReadOnlySpan<float> ZBoundaries => zBoundaries;
    public int BlockCount => blocks.Count;

    /// <summary>Material that landed outside the grid (should stay near zero).</summary>
    public double LostVolume { get; private set; }

    public float CellHeight(int k) => zBoundaries[k + 1] - zBoundaries[k];
    public float CellVolume(int k) => Pitch * Pitch * CellHeight(k);

    /// <summary>Print-frame position of grid node (i, j, k), the low corner of cell (i, j, k).</summary>
    public Vector3 NodePosition(int i, int j, int k) => new(Origin.X + i * Pitch, Origin.Y + j * Pitch, zBoundaries[k]);

    public bool Contains(int i, int j, int k) =>
        (uint)i < (uint)SizeX && (uint)j < (uint)SizeY && (uint)k < (uint)SizeZ;

    public void Deposit(int i, int j, int k, float volume)
    {
        if (!Contains(i, j, k))
        {
            LostVolume += volume;
            return;
        }
        var key = BlockKey(i >> BlockShift, j >> BlockShift, k >> BlockShift);
        if (!blocks.TryGetValue(key, out var block))
            blocks[key] = block = new float[BlockSize * BlockSize * BlockSize];
        block[Local(i, j, k)] += volume;
    }

    /// <summary>Deposited plastic in mm³ (can exceed the cell volume where beads overlap).</summary>
    public float Volume(int i, int j, int k) =>
        Contains(i, j, k) && blocks.TryGetValue(BlockKey(i >> BlockShift, j >> BlockShift, k >> BlockShift), out var block)
            ? block[Local(i, j, k)]
            : 0;

    /// <summary>Fill fraction φ, capped at 1.</summary>
    public float Fill(int i, int j, int k) => Contains(i, j, k) ? MathF.Min(1, Volume(i, j, k) / CellVolume(k)) : 0;

    public double TotalVolume => blocks.Values.Sum(b => b.Sum(v => (double)v));

    /// <summary>Cells with φ ≥ <paramref name="minFill"/>, block by block.</summary>
    public IEnumerable<CellIndex> Occupied(float minFill)
    {
        foreach (var (key, block) in blocks)
        {
            var (bi, bj, bk) = (key & 1023, (key >> 10) & 1023, key >> 20);
            for (var local = 0; local < block.Length; local++)
            {
                if (block[local] <= 0) continue;
                var i = (bi << BlockShift) | (local & BlockMask);
                var j = (bj << BlockShift) | ((local >> BlockShift) & BlockMask);
                var k = (bk << BlockShift) | (local >> (2 * BlockShift));
                if (Contains(i, j, k) && block[local] / CellVolume(k) >= minFill)
                    yield return new CellIndex(i, j, k);
            }
        }
    }

    /// <summary>A completely filled box of cells, for tests and benchmarks.</summary>
    public static CellGrid Solid(int sizeX, int sizeY, int sizeZ, float pitch, float layerHeight)
    {
        var z = Enumerable.Range(0, sizeZ + 1).Select(k => k * layerHeight).ToArray();
        var grid = new CellGrid(Vector2.Zero, pitch, z, sizeX, sizeY);
        for (var k = 0; k < sizeZ; k++)
        for (var j = 0; j < sizeY; j++)
        for (var i = 0; i < sizeX; i++)
            grid.Deposit(i, j, k, grid.CellVolume(k));
        return grid;
    }

    static int BlockKey(int bi, int bj, int bk) => bi | (bj << 10) | (bk << 20);

    static int Local(int i, int j, int k) =>
        (i & BlockMask) | ((j & BlockMask) << BlockShift) | ((k & BlockMask) << (2 * BlockShift));
}
