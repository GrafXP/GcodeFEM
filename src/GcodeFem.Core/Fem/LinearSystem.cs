using System.Collections.Concurrent;

namespace GcodeFem.Core.Fem;

/// <summary>
/// K u = f in CSR form, three unknowns (ux, uy, uz) per node. Fixed DOFs keep their place in the
/// matrix as identity rows and columns (prescribed value 0), so the 3×3 block structure and the
/// rigid-body modes stay intact for AMG.
/// </summary>
public sealed class LinearSystem
{
    public required int Size { get; init; }
    public required int[] RowPointers { get; init; }
    public required int[] Columns { get; init; }
    public required double[] Values { get; init; }
    public required double[] RightHandSide { get; init; }

    /// <summary>x, y, z per node: one triple per three unknowns.</summary>
    public required double[] Coordinates { get; init; }

    public required bool[] Fixed { get; init; }

    public long NonZeros => Values.LongLength;

    /// <summary>Bytes held by the matrix and vectors.</summary>
    public long Bytes => RowPointers.LongLength * 4 + Columns.LongLength * 4 + Values.LongLength * 8 +
                         RightHandSide.LongLength * 8 + Coordinates.LongLength * 8 + Fixed.LongLength;

    /// <summary>y = K x, rows split across cores.</summary>
    public void Multiply(double[] x, double[] y)
    {
        Parallel.ForEach(Partitioner.Create(0, Size, Math.Max(4096, Size / (8 * Environment.ProcessorCount))), range =>
        {
            for (var row = range.Item1; row < range.Item2; row++)
            {
                double sum = 0;
                for (var p = RowPointers[row]; p < RowPointers[row + 1]; p++) sum += Values[p] * x[Columns[p]];
                y[row] = sum;
            }
        });
    }

    public double[] Diagonal()
    {
        var d = new double[Size];
        for (var row = 0; row < Size; row++)
            for (var p = RowPointers[row]; p < RowPointers[row + 1]; p++)
                if (Columns[p] == row) d[row] = Values[p];
        return d;
    }
}
