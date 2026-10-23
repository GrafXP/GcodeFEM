using System.Numerics;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Geometry;

namespace GcodeFem.Core.Fem;

public readonly record struct CellIndex(int I, int J, int K);

/// <summary>How a piece of bead that lands in a cell was printed.</summary>
/// <param name="Direction">Unit vector along the line, in the plane of the layer (print frame).</param>
/// <param name="Time">Seconds since the start of the file.</param>
/// <param name="Fan">Part-cooling fan, 0 to 1.</param>
public readonly record struct Deposition(Vector2 Direction, double Time, float NozzleTemperature, float Fan, FeatureClass Feature);

/// <summary>
/// Which way the lines in a cell run. <paramref name="Angle"/> is the main direction in the plane of
/// the layer, 0 to π from +X towards +Y; <paramref name="Spread"/> is the share of the plastic that
/// runs across it, from 0 (all one way) to ½ (two directions crossing in equal parts, or no lines at all).
/// </summary>
public readonly record struct LineDirection(float Angle, float Spread);

/// <summary>
/// Bead cells in the print frame: square in XY (one line width), one layer tall, so a cell is
/// roughly one piece of one line. Only blocks of 8×8×8 cells that received material are stored;
/// in M3 these blocks become the octree roots. Each cell holds its plastic, and where the deposits
/// said how they were printed, also its line direction, line type, time, temperature and fan.
/// </summary>
public sealed class CellGrid
{
    public const int BlockSize = 8;
    const int BlockShift = 3;
    const int BlockMask = BlockSize - 1;
    const int BlockCells = BlockSize * BlockSize * BlockSize;

    /// <summary>
    /// Sums over a block's cells, each weighted by the volume deposited: the in-plane orientation
    /// tensor (xx and xy; yy is the volume less xx), time, temperature and fan, and the volume per
    /// line type. Divided by the volume they are means.
    /// </summary>
    sealed class PrintSums
    {
        public readonly float[] Volume = new float[BlockCells];
        public readonly float[] Xx = new float[BlockCells];
        public readonly float[] Xy = new float[BlockCells];
        public readonly double[] Time = new double[BlockCells];
        public readonly float[] Temperature = new float[BlockCells];
        public readonly float[] Fan = new float[BlockCells];
        public readonly float[] Feature = new float[FeatureTypes.ClassCount * BlockCells];
    }

    sealed class Block
    {
        public readonly float[] Volume = new float[BlockCells];
        public PrintSums? Print;
    }

    readonly Dictionary<int, Block> blocks = [];
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

    /// <summary>Plastic that says nothing of how it was printed: solid blocks for tests, or taking some away again.</summary>
    public void Deposit(int i, int j, int k, float volume)
    {
        if (BlockAt(i, j, k, volume) is { } block) block.Volume[Local(i, j, k)] += volume;
    }

    public void Deposit(int i, int j, int k, float volume, in Deposition how)
    {
        if (BlockAt(i, j, k, volume) is not { } block) return;
        var local = Local(i, j, k);
        block.Volume[local] += volume;
        var print = block.Print ??= new PrintSums();
        print.Volume[local] += volume;
        print.Xx[local] += volume * how.Direction.X * how.Direction.X;
        print.Xy[local] += volume * how.Direction.X * how.Direction.Y;
        print.Time[local] += volume * how.Time;
        print.Temperature[local] += volume * how.NozzleTemperature;
        print.Fan[local] += volume * how.Fan;
        print.Feature[(int)how.Feature * BlockCells + local] += volume;
    }

    /// <summary>Deposited plastic in mm³ (can exceed the cell volume where beads overlap).</summary>
    public float Volume(int i, int j, int k) => TryBlock(i, j, k, out var block) ? block.Volume[Local(i, j, k)] : 0;

    /// <summary>Fill fraction φ, capped at 1.</summary>
    public float Fill(int i, int j, int k) => Contains(i, j, k) ? MathF.Min(1, Volume(i, j, k) / CellVolume(k)) : 0;

    public double TotalVolume => blocks.Values.Sum(b => b.Volume.Sum(v => (double)v));

    /// <summary>
    /// The main direction of the lines in the cell and how much they spread about it, from the
    /// eigenvectors of their orientation tensor. A cell whose plastic says nothing of its lines
    /// spreads fully: it is the same every way in the plane.
    /// </summary>
    public LineDirection Direction(int i, int j, int k)
    {
        if (!TryPrint(i, j, k, out var print, out var local)) return new LineDirection(0, 0.5f);
        var volume = print.Volume[local];
        double xx = print.Xx[local] / volume - 0.5, xy = print.Xy[local] / volume;
        var angle = 0.5 * Math.Atan2(2 * xy, 2 * xx);
        if (angle < 0) angle += Math.PI;
        var spread = 0.5 - Math.Sqrt(xx * xx + xy * xy);
        return new LineDirection((float)Math.Min(angle, Math.PI - 1e-9), (float)Math.Clamp(spread, 0, 0.5));
    }

    /// <summary>The line type holding most of the cell's plastic; null for plastic that does not say.</summary>
    public FeatureClass? Feature(int i, int j, int k)
    {
        if (!TryPrint(i, j, k, out var print, out var local)) return null;
        var best = 0;
        for (var f = 1; f < FeatureTypes.ClassCount; f++)
            if (print.Feature[f * BlockCells + local] > print.Feature[best * BlockCells + local]) best = f;
        return (FeatureClass)best;
    }

    /// <summary>When the cell's plastic was laid down, on average (s since the start of the file); NaN if it does not say.</summary>
    public double DepositionTime(int i, int j, int k) =>
        TryPrint(i, j, k, out var print, out var local) ? print.Time[local] / print.Volume[local] : double.NaN;

    /// <summary>The nozzle temperature the cell was printed at (°C); NaN if it does not say.</summary>
    public float NozzleTemperature(int i, int j, int k) =>
        TryPrint(i, j, k, out var print, out var local) ? print.Temperature[local] / print.Volume[local] : float.NaN;

    /// <summary>The part-cooling fan while the cell was printed, 0 to 1; NaN if it does not say.</summary>
    public float Fan(int i, int j, int k) =>
        TryPrint(i, j, k, out var print, out var local) ? print.Fan[local] / print.Volume[local] : float.NaN;

    /// <summary>Cells with φ ≥ <paramref name="minFill"/>, block by block.</summary>
    public IEnumerable<CellIndex> Occupied(float minFill)
    {
        foreach (var (key, block) in blocks)
        {
            var (bi, bj, bk) = (key & 1023, (key >> 10) & 1023, key >> 20);
            for (var local = 0; local < BlockCells; local++)
            {
                if (block.Volume[local] <= 0) continue;
                var i = (bi << BlockShift) | (local & BlockMask);
                var j = (bj << BlockShift) | ((local >> BlockShift) & BlockMask);
                var k = (bk << BlockShift) | (local >> (2 * BlockShift));
                if (Contains(i, j, k) && block.Volume[local] / CellVolume(k) >= minFill)
                    yield return new CellIndex(i, j, k);
            }
        }
    }

    /// <summary>
    /// Print-frame box around the cells with φ ≥ <paramref name="minFill"/>. Its faces lie on node
    /// planes, unlike the model's own bounds, whose far X and Y faces usually cut through a cell.
    /// </summary>
    public Box3 OccupiedBounds(float minFill)
    {
        CellIndex low = new(int.MaxValue, int.MaxValue, int.MaxValue), high = new(-1, -1, -1);
        foreach (var c in Occupied(minFill))
        {
            low = new CellIndex(Math.Min(low.I, c.I), Math.Min(low.J, c.J), Math.Min(low.K, c.K));
            high = new CellIndex(Math.Max(high.I, c.I), Math.Max(high.J, c.J), Math.Max(high.K, c.K));
        }
        if (high.I < 0) throw new InvalidOperationException("No cells reach the minimum fill.");
        return new Box3(NodePosition(low.I, low.J, low.K), NodePosition(high.I + 1, high.J + 1, high.K + 1));
    }

    /// <summary>A completely filled box of cells, for tests and benchmarks; <paramref name="how"/>, if given, says how every cell was printed.</summary>
    public static CellGrid Solid(int sizeX, int sizeY, int sizeZ, float pitch, float layerHeight, Deposition? how = null)
    {
        var z = Enumerable.Range(0, sizeZ + 1).Select(k => k * layerHeight).ToArray();
        var grid = new CellGrid(Vector2.Zero, pitch, z, sizeX, sizeY);
        for (var k = 0; k < sizeZ; k++)
        for (var j = 0; j < sizeY; j++)
        for (var i = 0; i < sizeX; i++)
            if (how is { } deposition) grid.Deposit(i, j, k, grid.CellVolume(k), deposition);
            else grid.Deposit(i, j, k, grid.CellVolume(k));
        return grid;
    }

    Block? BlockAt(int i, int j, int k, float volume)
    {
        if (!Contains(i, j, k))
        {
            LostVolume += volume;
            return null;
        }
        var key = BlockKey(i >> BlockShift, j >> BlockShift, k >> BlockShift);
        if (!blocks.TryGetValue(key, out var block)) blocks[key] = block = new Block();
        return block;
    }

    bool TryBlock(int i, int j, int k, out Block block)
    {
        block = null!;
        return Contains(i, j, k) && blocks.TryGetValue(BlockKey(i >> BlockShift, j >> BlockShift, k >> BlockShift), out block!);
    }

    bool TryPrint(int i, int j, int k, out PrintSums print, out int local)
    {
        local = Local(i, j, k);
        print = null!;
        if (!TryBlock(i, j, k, out var block) || block.Print is null || !(block.Print.Volume[local] > 0)) return false;
        print = block.Print;
        return true;
    }

    static int BlockKey(int bi, int bj, int bk) => bi | (bj << 10) | (bk << 20);

    static int Local(int i, int j, int k) =>
        (i & BlockMask) | ((j & BlockMask) << BlockShift) | ((k & BlockMask) << (2 * BlockShift));
}
