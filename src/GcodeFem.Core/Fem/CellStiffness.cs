namespace GcodeFem.Core.Fem;

/// <summary>
/// Linear elastic printed plastic, orthotropic in the frame of its lines: 1 along a line, 2 across
/// it in the plane of the layer (bead to bead), 3 across the layers. Units: MPa. A data sheet gives
/// one Poisson's ratio at most, so the others and the shear moduli follow Huber's rule from it,
/// ν_ij = ν √(E_i / E_j) and G_ij = √(E_i E_j) / 2(1 + ν): the stiffness is the isotropic one with
/// each direction scaled, which keeps it positive definite, and with one modulus it is the
/// isotropic material.
/// </summary>
public readonly record struct OrthotropicMaterial(double AlongLine, double AcrossLine, double AcrossLayers, double PoissonRatio)
{
    public bool IsIsotropic => AlongLine == AcrossLine && AcrossLine == AcrossLayers;

    /// <summary>The same every way in the plane of the layers, so that which way the lines run makes no difference.</summary>
    public bool IsInPlaneIsotropic => AlongLine == AcrossLine;

    public static implicit operator OrthotropicMaterial(IsotropicMaterial material) =>
        new(material.YoungsModulus, material.YoungsModulus, material.YoungsModulus, material.PoissonRatio);

    /// <summary>
    /// 6×6 row-major Voigt matrix in the print frame (xx, yy, zz, xy, yz, zx, engineering shear) for
    /// lines running at <paramref name="angle"/> (radians from +X towards +Y) with
    /// <paramref name="spread"/> of the plastic across them (<see cref="LineDirection"/>). Where lines
    /// cross, the cell's moduli in its two in-plane directions are mixed by their shares.
    /// </summary>
    public double[] Constitutive(double angle = 0, double spread = 0)
    {
        double e1 = (1 - spread) * AlongLine + spread * AcrossLine, e2 = spread * AlongLine + (1 - spread) * AcrossLine, e3 = AcrossLayers;
        ReadOnlySpan<double> scale = [Math.Sqrt(e1), Math.Sqrt(e2), Math.Sqrt(e3), Math.Pow(e1 * e2, 0.25), Math.Pow(e2 * e3, 0.25), Math.Pow(e3 * e1, 0.25)];
        var local = new IsotropicMaterial(1, PoissonRatio).Constitutive();
        for (var r = 0; r < 6; r++)
        for (var c = 0; c < 6; c++)
            local[6 * r + c] *= scale[r] * scale[c];
        return angle == 0 ? local : Rotated(local, angle);
    }

    /// <summary>Voigt index → the tensor indices it stands for.</summary>
    static readonly (int I, int J)[] Pairs = [(0, 0), (1, 1), (2, 2), (0, 1), (1, 2), (2, 0)];

    /// <summary>C' = M C Mᵀ, M turning stresses about Z by <paramref name="angle"/>, from the line's frame into the print's.</summary>
    static double[] Rotated(double[] local, double angle)
    {
        double cos = Math.Cos(angle), sin = Math.Sin(angle);
        double[,] r = { { cos, -sin, 0 }, { sin, cos, 0 }, { 0, 0, 1 } };
        var m = new double[36];
        for (var a = 0; a < 6; a++)
        for (var b = 0; b < 6; b++)
        {
            var ((i, j), (k, l)) = (Pairs[a], Pairs[b]);
            m[6 * a + b] = k == l ? r[i, k] * r[j, l] : r[i, k] * r[j, l] + r[i, l] * r[j, k];
        }
        var mc = new double[36];
        for (var a = 0; a < 6; a++)
        for (var b = 0; b < 6; b++)
        {
            double sum = 0;
            for (var n = 0; n < 6; n++) sum += m[6 * a + n] * local[6 * n + b];
            mc[6 * a + b] = sum;
        }
        var result = new double[36];
        for (var a = 0; a < 6; a++)
        for (var b = 0; b < 6; b++)
        {
            double sum = 0;
            for (var n = 0; n < 6; n++) sum += mc[6 * a + n] * m[6 * b + n];
            result[6 * a + b] = sum;
        }
        return result;
    }
}

/// <summary>
/// The stiffness of every cell of a mesh. Cells share an element matrix when they share a layer
/// height, a line direction (in steps of 5°) and a spread (in steps of 1/8): a few hundred
/// matrices for a whole part. A cell's stiffness is its matrix × its fill φ.
/// </summary>
public sealed class CellStiffness
{
    public const int AngleSteps = 36;

    /// <summary>Spread runs from 0 to ½ in this many steps.</summary>
    public const int SpreadSteps = 4;

    readonly List<double[]> matrices = [];
    readonly List<double[]> constitutive = [];

    public CellStiffness(FemMesh mesh, OrthotropicMaterial material)
    {
        Material = material;
        var grid = mesh.Grid;
        var kinds = new Dictionary<(float Height, int Angle, int Spread), ushort>();
        Kind = new ushort[mesh.Cells.Length];
        for (var e = 0; e < Kind.Length; e++)
        {
            var c = mesh.Cells[e];
            // Which way the lines run is all the same to a material that is the same every way in the plane.
            var (angle, spread) = material.IsInPlaneIsotropic ? (0, 0) : Bins(grid.Direction(c.I, c.J, c.K));
            var height = grid.CellHeight(c.K);
            if (!kinds.TryGetValue((height, angle, spread), out var kind))
            {
                kinds[(height, angle, spread)] = kind = checked((ushort)matrices.Count);
                var cell = material.Constitutive(angle * Math.PI / AngleSteps, spread * 0.5 / SpreadSteps);
                constitutive.Add(cell);
                matrices.Add(HexElement.Stiffness(grid.Pitch, grid.Pitch, height, cell));
            }
            Kind[e] = kind;
        }
    }

    public OrthotropicMaterial Material { get; }

    /// <summary>Per cell of the mesh: which matrix it takes.</summary>
    public ushort[] Kind { get; }

    public int KindCount => matrices.Count;

    /// <summary>24×24 element stiffness of a full cell of this kind (multiply by φ).</summary>
    public double[] Matrix(int kind) => matrices[kind];

    /// <summary>6×6 constitutive matrix of a full cell of this kind, print frame (multiply by φ).</summary>
    public double[] Constitutive(int kind) => constitutive[kind];

    /// <summary>The steps of angle and spread a direction falls in.</summary>
    public static (int Angle, int Spread) Bins(LineDirection direction) =>
        ((int)Math.Round(direction.Angle / Math.PI * AngleSteps) % AngleSteps, Math.Clamp((int)Math.Round(direction.Spread * 2 * SpreadSteps), 0, SpreadSteps));
}
