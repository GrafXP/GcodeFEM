namespace GcodeFem.Core.Fem;

/// <summary>
/// 8-node trilinear brick on an axis-aligned box, 2×2×2 Gauss integration.
/// Local nodes: 0 (0,0,0), 1 (1,0,0), 2 (1,1,0), 3 (0,1,0), 4 (0,0,1), 5 (1,0,1), 6 (1,1,1), 7 (0,1,1);
/// DOFs (ux, uy, uz) per node. Strain order (Voigt): xx, yy, zz, xy, yz, zx with engineering shear.
/// </summary>
public static class HexElement
{
    public const int Nodes = 8;
    public const int Dofs = 24;

    public static readonly int[] CornerX = [0, 1, 1, 0, 0, 1, 1, 0];
    public static readonly int[] CornerY = [0, 0, 1, 1, 0, 0, 1, 1];
    public static readonly int[] CornerZ = [0, 0, 0, 0, 1, 1, 1, 1];

    /// <summary>24×24 row-major stiffness for a dx × dy × dz box with constitutive matrix <paramref name="c"/> (6×6 row-major).</summary>
    public static double[] Stiffness(double dx, double dy, double dz, double[] c)
    {
        var k = new double[Dofs * Dofs];
        var b = new double[6 * Dofs];
        var cb = new double[6 * Dofs];
        var g = 1 / Math.Sqrt(3);
        var weight = dx * dy * dz / 8; // det(J) × Gauss weight 1

        foreach (var xi in new[] { -g, g })
        foreach (var eta in new[] { -g, g })
        foreach (var zeta in new[] { -g, g })
        {
            StrainMatrix(dx, dy, dz, xi, eta, zeta, b);
            for (var r = 0; r < 6; r++)
            for (var col = 0; col < Dofs; col++)
            {
                double sum = 0;
                for (var m = 0; m < 6; m++) sum += c[r * 6 + m] * b[m * Dofs + col];
                cb[r * Dofs + col] = sum;
            }
            for (var row = 0; row < Dofs; row++)
            for (var col = 0; col < Dofs; col++)
            {
                double sum = 0;
                for (var m = 0; m < 6; m++) sum += b[m * Dofs + row] * cb[m * Dofs + col];
                k[row * Dofs + col] += sum * weight;
            }
        }
        return k;
    }

    /// <summary>Strain-displacement matrix B (6×24, row-major) at natural coordinates ξ, η, ζ ∈ [−1, 1].</summary>
    public static void StrainMatrix(double dx, double dy, double dz, double xi, double eta, double zeta, double[] b)
    {
        Array.Clear(b);
        for (var a = 0; a < Nodes; a++)
        {
            double sx = 2 * CornerX[a] - 1, sy = 2 * CornerY[a] - 1, sz = 2 * CornerZ[a] - 1;
            var dNdx = sx * (1 + sy * eta) * (1 + sz * zeta) / 8 * 2 / dx;
            var dNdy = sy * (1 + sx * xi) * (1 + sz * zeta) / 8 * 2 / dy;
            var dNdz = sz * (1 + sx * xi) * (1 + sy * eta) / 8 * 2 / dz;
            var col = 3 * a;
            b[0 * Dofs + col] = dNdx;
            b[1 * Dofs + col + 1] = dNdy;
            b[2 * Dofs + col + 2] = dNdz;
            b[3 * Dofs + col] = dNdy;
            b[3 * Dofs + col + 1] = dNdx;
            b[4 * Dofs + col + 1] = dNdz;
            b[4 * Dofs + col + 2] = dNdy;
            b[5 * Dofs + col] = dNdz;
            b[5 * Dofs + col + 2] = dNdx;
        }
    }
}

/// <summary>Linear elastic, isotropic. Units: MPa.</summary>
public readonly record struct IsotropicMaterial(double YoungsModulus, double PoissonRatio)
{
    /// <summary>6×6 row-major Voigt matrix (engineering shear strains).</summary>
    public double[] Constitutive()
    {
        double e = YoungsModulus, nu = PoissonRatio;
        var lambda = e * nu / ((1 + nu) * (1 - 2 * nu));
        var mu = e / (2 * (1 + nu));
        var c = new double[36];
        for (var r = 0; r < 3; r++)
        for (var col = 0; col < 3; col++)
            c[r * 6 + col] = r == col ? lambda + 2 * mu : lambda;
        c[3 * 6 + 3] = c[4 * 6 + 4] = c[5 * 6 + 5] = mu;
        return c;
    }

    public double ShearModulus => YoungsModulus / (2 * (1 + PoissonRatio));

    /// <summary>Generic PLA until M6 brings per-filament data from the TDS.</summary>
    public static IsotropicMaterial Pla => new(2500, 0.35);
}
