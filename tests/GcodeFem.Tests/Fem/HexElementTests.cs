using GcodeFem.Core.Fem;

namespace GcodeFem.Tests.Fem;

public class HexElementTests
{
    const double Dx = 0.42, Dy = 0.42, Dz = 0.2;
    static readonly IsotropicMaterial Material = new(2500, 0.35);
    static readonly double[] K = HexElement.Stiffness(Dx, Dy, Dz, Material.Constitutive());

    static double[] NodalField(Func<double, double, double, (double, double, double)> u)
    {
        var field = new double[24];
        for (var a = 0; a < 8; a++)
        {
            var (ux, uy, uz) = u(HexElement.CornerX[a] * Dx, HexElement.CornerY[a] * Dy, HexElement.CornerZ[a] * Dz);
            field[3 * a] = ux;
            field[3 * a + 1] = uy;
            field[3 * a + 2] = uz;
        }
        return field;
    }

    static double[] Times(double[] u)
    {
        var f = new double[24];
        for (var r = 0; r < 24; r++)
        for (var c = 0; c < 24; c++)
            f[r] += K[r * 24 + c] * u[c];
        return f;
    }

    [Fact]
    public void Is_symmetric()
    {
        for (var r = 0; r < 24; r++)
        for (var c = 0; c < 24; c++)
            Assert.Equal(K[r * 24 + c], K[c * 24 + r], 9);
    }

    [Fact]
    public void Rigid_body_motion_costs_no_force()
    {
        var modes = new Func<double, double, double, (double, double, double)>[]
        {
            (_, _, _) => (1, 0, 0), (_, _, _) => (0, 1, 0), (_, _, _) => (0, 0, 1),
            (_, y, z) => (0, -z, y), (x, _, z) => (z, 0, -x), (x, y, _) => (-y, x, 0),
        };
        foreach (var mode in modes)
            Assert.All(Times(NodalField(mode)), f => Assert.Equal(0, f, 8));
    }

    [Fact]
    public void Stores_the_exact_energy_of_a_uniform_strain()
    {
        // u = ε·x with εxx = 1e-3, εyy = −2e-4, γxy = 5e-4 (as ux = εxx x + γ/2 y, uy = γ/2 x + εyy y).
        double exx = 1e-3, eyy = -2e-4, gxy = 5e-4;
        var u = NodalField((x, y, _) => (exx * x + gxy / 2 * y, gxy / 2 * x + eyy * y, 0));
        var energy = 0.5 * u.Zip(Times(u), (a, b) => a * b).Sum();

        var c = Material.Constitutive();
        double[] strain = [exx, eyy, 0, gxy, 0, 0];
        var density = 0.0;
        for (var r = 0; r < 6; r++)
        for (var m = 0; m < 6; m++)
            density += 0.5 * strain[r] * c[r * 6 + m] * strain[m];

        Assert.Equal(density * Dx * Dy * Dz, energy, 12);
    }
}
