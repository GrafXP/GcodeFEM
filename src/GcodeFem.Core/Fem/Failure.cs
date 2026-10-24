namespace GcodeFem.Core.Fem;

/// <summary>How a printed cell gives way, in the frame of its lines.</summary>
public enum FailureMode : byte
{
    /// <summary>The layer line below the cell opens or slides: tension across the layers, with shear along them.</summary>
    LayerSeparation,

    /// <summary>Neighbouring beads of one layer come apart: tension across the line in the plane, with shear along it.</summary>
    BeadSeparation,

    /// <summary>The line itself tears: tension along it.</summary>
    LineBreak,

    /// <summary>Pressed together harder than the plastic takes, in any of the three directions.</summary>
    Crushing,
}

public static class FailureModes
{
    public const int Count = 4;

    public static string DisplayName(this FailureMode mode) => mode switch
    {
        FailureMode.LayerSeparation => "Layers pull apart",
        FailureMode.BeadSeparation => "Beads of a layer pull apart",
        FailureMode.LineBreak => "Lines break",
        _ => "Crushed",
    };
}

/// <summary>
/// How strong printed plastic is in the frame of its lines (MPa): 1 along a line, 2 across it in the
/// plane of the layer (bead to bead), 3 across the layers. <paramref name="AcrossLayers"/> is the data
/// sheet's, for a layer line bonded as well as its specimens'; each cell multiplies it by its own bond
/// factor. No sheet gives a shear strength; a plane takes the shear its tensile strength gives by von
/// Mises' rule, τ = σ / √3.
/// </summary>
/// <param name="Compression">The same in all three directions: pressed together, the layer lines and the joints between beads close.</param>
/// <param name="Assumed">What of this is not the material's own numbers, in words.</param>
public sealed record PrintedStrength(double AlongLine, double AcrossLine, double AcrossLayers, double Compression, IReadOnlyList<string> Assumed)
{
    public static readonly double ShearRatio = 1 / Math.Sqrt(3);

    /// <summary>Along a joint between two beads of a layer.</summary>
    public double InPlaneShear => ShearRatio * AcrossLine;

    /// <summary>Along a layer line bonded as well as the specimens'.</summary>
    public double LayerShear => ShearRatio * AcrossLayers;
}

/// <summary>
/// The failure check of every cell of a mesh. A cell's stress is turned into the frame of its lines
/// and divided by its fill (at least <see cref="FillFloor"/>), which gives the stress in its plastic,
/// and then held against each way it can give way. Each check is written as a failure index that grows in
/// proportion to the load, so that the safety factor 1 / index is the factor on the load at which the
/// cell breaks. Three planes of a printed cell can give way, each opening under tension across it and
/// sliding under the shear along it (Hashin's modes, with the shear strength of a plane its tensile
/// strength / √3):
/// <list type="bullet">
/// <item>layers pull apart, the layer line below: √((⟨σ₃₃⟩ / Z)² + (τ₃₁² + τ₂₃²) / S_z²), with Z and S_z × the cell's bond to the layer below</item>
/// <item>beads pull apart, the joint between two beads of a layer: √((⟨σ₂₂⟩ / Y)² + (τ₁₂² + τ₂₃²) / S_y²)</item>
/// <item>lines break, across a line: √((⟨σ₁₁⟩ / X)² + (τ₁₂² + τ₃₁²) / S_x²)</item>
/// <item>crushed: the largest compression of σ₁₁, σ₂₂, σ₃₃ / the compressive strength</item>
/// </list>
/// ⟨ ⟩ keeps tension only. A plastic as strong every way, pulled at any angle to its lines, then breaks
/// at 1 to 1.06 × its tensile strength; without the shear in every plane it would hold up to 1.27 ×.
/// Where lines cross, the strengths along and across them are mixed by their shares, as their
/// stiffnesses are. Tsai–Wu with the same strengths is worked out alongside, as a cross-check that
/// takes all the stresses together.
/// </summary>
public sealed class CellStrength
{
    readonly float[] angle, spread, bond;

    /// <param name="bond">Per cell of <paramref name="mesh"/>: its bond to the cell below as a share of the specimens'; null for 1 everywhere.</param>
    public CellStrength(FemMesh mesh, PrintedStrength strength, float[]? bond = null)
    {
        if (bond is not null && bond.Length != mesh.Cells.Length) throw new ArgumentException("One bond factor per cell of the mesh.", nameof(bond));
        Mesh = mesh;
        Strength = strength;
        angle = new float[mesh.Cells.Length];
        spread = new float[mesh.Cells.Length];
        Parallel.For(0, mesh.Cells.Length, e =>
        {
            var c = mesh.Cells[e];
            (angle[e], spread[e]) = mesh.Grid.Direction(c.I, c.J, c.K);
        });
        this.bond = bond ?? [.. Enumerable.Repeat(1f, mesh.Cells.Length)];
    }

    /// <summary>
    /// A cell less full than this holds the edge of a bead whose middle lies in the next cell. The mesh
    /// makes it a soft link between its neighbours, and its strain is not that of the plastic in it, so
    /// its stress is taken over this share of the cell at least. On the bracket, 0.25 and 0.5 give the
    /// same safety factor at the same place; without it a cell a tenth full governs (PLAN.md §8).
    /// </summary>
    public const float FillFloor = 0.5f;

    public FemMesh Mesh { get; }
    public PrintedStrength Strength { get; }

    /// <summary>The cell's bond to the layer below, as a share of the specimens'.</summary>
    public float Bond(int cell) => bond[cell];

    /// <summary>
    /// The stress in the plastic of a cell in the frame of its lines: σ₁₁, σ₂₂, σ₃₃, τ₁₂, τ₂₃, τ₃₁.
    /// <paramref name="stress"/> is the cell's own, as the analysis gives it: σxx, σyy, σzz, τxy, τyz, τzx in
    /// the print frame, averaged over the whole cell.
    /// </summary>
    public void LocalStress(int cell, ReadOnlySpan<float> stress, Span<double> local)
    {
        var plastic = 1.0 / Math.Max(Mesh.Fill[cell], FillFloor);
        double c = Math.Cos(angle[cell]), s = Math.Sin(angle[cell]);
        double xx = stress[0] * plastic, yy = stress[1] * plastic, zz = stress[2] * plastic;
        double xy = stress[3] * plastic, yz = stress[4] * plastic, zx = stress[5] * plastic;
        local[0] = c * c * xx + s * s * yy + 2 * c * s * xy;
        local[1] = s * s * xx + c * c * yy - 2 * c * s * xy;
        local[2] = zz;
        local[3] = c * s * (yy - xx) + (c * c - s * s) * xy;
        local[4] = c * yz - s * zx;
        local[5] = c * zx + s * yz;
    }

    /// <summary>
    /// The failure index of each mode for one cell, into <paramref name="modes"/> in the order of
    /// <see cref="FailureMode"/>; returns the safety factor by Tsai–Wu.
    /// </summary>
    public double Evaluate(int cell, ReadOnlySpan<float> stress, Span<double> modes)
    {
        Span<double> l = stackalloc double[6];
        LocalStress(cell, stress, l);
        var (along, across, layers, crush) = Strengths(cell);
        // The shear a plane takes along itself: of the line's cross-section, the joint between beads, the layer line.
        double s1 = PrintedStrength.ShearRatio * along, s2 = PrintedStrength.ShearRatio * across, s3 = PrintedStrength.ShearRatio * layers;
        double t12 = l[3], t23 = l[4], t31 = l[5];

        modes[(int)FailureMode.LayerSeparation] = Math.Sqrt(Square(Math.Max(l[2], 0) / layers) + (t31 * t31 + t23 * t23) / (s3 * s3));
        modes[(int)FailureMode.BeadSeparation] = Math.Sqrt(Square(Math.Max(l[1], 0) / across) + (t12 * t12 + t23 * t23) / (s2 * s2));
        modes[(int)FailureMode.LineBreak] = Math.Sqrt(Square(Math.Max(l[0], 0) / along) + (t12 * t12 + t31 * t31) / (s1 * s1));
        modes[(int)FailureMode.Crushing] = Math.Max(0, -Math.Min(l[0], Math.Min(l[1], l[2]))) / crush;

        // Tsai–Wu: F_i σ_i + F_ij σ_i σ_j = 1, with F_ij = −½ √(F_ii F_jj) between the normal stresses, and each
        // shear against the weaker of the two planes it runs along. Its linear terms do not grow with the square
        // of the load, so the factor on the load is the root of λ² a + λ b = 1.
        double f11 = 1 / (along * crush), f22 = 1 / (across * crush), f33 = 1 / (layers * crush);
        var a = f11 * l[0] * l[0] + f22 * l[1] * l[1] + f33 * l[2] * l[2]
                - Math.Sqrt(f11 * f22) * l[0] * l[1] - Math.Sqrt(f11 * f33) * l[0] * l[2] - Math.Sqrt(f22 * f33) * l[1] * l[2]
                + Square(t12 / Math.Min(s1, s2)) + Square(t23 / Math.Min(s2, s3)) + Square(t31 / Math.Min(s3, s1));
        var b = (1 / along - 1 / crush) * l[0] + (1 / across - 1 / crush) * l[1] + (1 / layers - 1 / crush) * l[2];
        var root = b + Math.Sqrt(b * b + 4 * Math.Max(a, 0));
        return root > 0 ? 2 / root : double.PositiveInfinity;
    }

    /// <summary>Along and across the cell's lines, across the layers (with its bond), and in compression.</summary>
    (double Along, double Across, double Layers, double Compression) Strengths(int cell)
    {
        var (s, share) = (Strength, (double)spread[cell]);
        return ((1 - share) * s.AlongLine + share * s.AcrossLine,
                share * s.AlongLine + (1 - share) * s.AcrossLine,
                s.AcrossLayers * Math.Max(bond[cell], 1e-6),
                s.Compression);
    }

    /// <summary>Every cell checked against the stresses of an analysis (6 per cell, as <see cref="LocalStress"/> takes them).</summary>
    public CellFailure Check(float[] stress)
    {
        var cells = Mesh.Cells.Length;
        if (stress.Length != 6 * cells) throw new ArgumentException("Six stresses per cell of the mesh.", nameof(stress));
        var index = new float[cells];
        var mode = new FailureMode[cells];
        var layer = new float[cells];
        var tsaiWu = new float[cells];
        Parallel.For(0, cells, () => new double[FailureModes.Count], (e, _, modes) =>
        {
            tsaiWu[e] = (float)Evaluate(e, stress.AsSpan(6 * e, 6), modes);
            var worst = 0;
            for (var m = 1; m < modes.Length; m++)
                if (modes[m] > modes[worst]) worst = m;
            (index[e], mode[e], layer[e]) = ((float)modes[worst], (FailureMode)worst, (float)modes[(int)FailureMode.LayerSeparation]);
            return modes;
        }, _ => { });
        return new CellFailure(index, mode, layer, tsaiWu);
    }

    static double Square(double x) => x * x;
}

/// <summary>The failure check of every cell of a mesh.</summary>
/// <param name="Index">Per cell: the largest failure index of its modes; 1 / it is the cell's safety factor.</param>
/// <param name="Mode">Per cell: the mode with that index, the way it would break first.</param>
/// <param name="LayerIndex">Per cell: the index of the layer line below it pulling apart, whether or not that governs.</param>
/// <param name="TsaiWu">Per cell: the safety factor by Tsai–Wu, the cross-check.</param>
public sealed record CellFailure(float[] Index, FailureMode[] Mode, float[] LayerIndex, float[] TsaiWu)
{
    /// <summary>The cell that breaks first: the highest failure index.</summary>
    public int Weakest { get; } = ArgMax(Index);

    /// <summary>The cell whose layer line below would open first.</summary>
    public int WeakestLayerLine { get; } = ArgMax(LayerIndex);

    /// <summary>The factor on the loads at which the part starts to break.</summary>
    public double SafetyFactor => 1.0 / Index[Weakest];

    public double LayerLineSafetyFactor => 1.0 / LayerIndex[WeakestLayerLine];

    public double SafetyFactorOf(int cell) => 1.0 / Index[cell];

    static int ArgMax(float[] values)
    {
        var best = 0;
        for (var e = 1; e < values.Length; e++)
            if (values[e] > values[best]) best = e;
        return best;
    }
}
