using System.Numerics;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Gcode;

namespace GcodeFem.Tests.Fem;

public class OrthotropicMaterialTests
{
    static readonly OrthotropicMaterial Printed = new(3000, 2000, 1500, 0.35);

    /// <summary>The compliance, by Gauss–Jordan elimination of the 6×6 stiffness.</summary>
    static double[] Inverse(double[] c)
    {
        var a = (double[])c.Clone();
        var inverse = new double[36];
        for (var n = 0; n < 6; n++) inverse[7 * n] = 1;
        for (var col = 0; col < 6; col++)
        {
            var pivot = Enumerable.Range(col, 6 - col).MaxBy(r => Math.Abs(a[6 * r + col]));
            for (var k = 0; k < 6; k++)
            {
                (a[6 * col + k], a[6 * pivot + k]) = (a[6 * pivot + k], a[6 * col + k]);
                (inverse[6 * col + k], inverse[6 * pivot + k]) = (inverse[6 * pivot + k], inverse[6 * col + k]);
            }
            var scale = a[7 * col];
            for (var k = 0; k < 6; k++)
            {
                a[6 * col + k] /= scale;
                inverse[6 * col + k] /= scale;
            }
            for (var r = 0; r < 6; r++)
            {
                if (r == col) continue;
                var factor = a[6 * r + col];
                for (var k = 0; k < 6; k++)
                {
                    a[6 * r + k] -= factor * a[6 * col + k];
                    inverse[6 * r + k] -= factor * inverse[6 * col + k];
                }
            }
        }
        return inverse;
    }

    static bool PositiveDefinite(double[] c)
    {
        var l = new double[36];
        for (var i = 0; i < 6; i++)
        for (var j = 0; j <= i; j++)
        {
            var sum = c[6 * i + j];
            for (var k = 0; k < j; k++) sum -= l[6 * i + k] * l[6 * j + k];
            if (i == j)
            {
                if (sum <= 0) return false;
                l[7 * i] = Math.Sqrt(sum);
            }
            else l[6 * i + j] = sum / l[7 * j];
        }
        return true;
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.7, 0)]
    [InlineData(2.1, 0.5)]
    public void With_one_modulus_it_is_the_isotropic_material(double angle, double spread)
    {
        var isotropic = new IsotropicMaterial(2500, 0.35);

        var c = ((OrthotropicMaterial)isotropic).Constitutive(angle, spread);

        var expected = isotropic.Constitutive();
        for (var n = 0; n < 36; n++) Assert.Equal(expected[n], c[n], 8);
    }

    [Fact]
    public void Pulled_along_each_of_its_directions_it_has_that_modulus()
    {
        var s = Inverse(Printed.Constitutive());

        Assert.Equal(1 / Printed.AlongLine, s[0], 12);
        Assert.Equal(1 / Printed.AcrossLine, s[7], 12);
        Assert.Equal(1 / Printed.AcrossLayers, s[14], 12);
        // Huber's rule: ν_13 = ν √(E_1 / E_3), and the compliance stays symmetric.
        Assert.Equal(0.35 * Math.Sqrt(3000.0 / 1500), -s[2] / s[0], 10);
        Assert.Equal(s[2], s[12], 14);
        // G_12 = √(E_1 E_2) / 2(1 + ν)
        Assert.Equal(2 * 1.35 / Math.Sqrt(3000.0 * 2000), s[21], 12);
    }

    [Fact]
    public void Lines_turned_a_quarter_round_swap_the_moduli_in_the_plane()
    {
        var s = Inverse(Printed.Constitutive(Math.PI / 2));

        Assert.Equal(1 / Printed.AcrossLine, s[0], 12);
        Assert.Equal(1 / Printed.AlongLine, s[7], 12);
        Assert.Equal(1 / Printed.AcrossLayers, s[14], 12);
    }

    [Fact]
    public void At_45_degrees_the_modulus_along_x_lies_between()
    {
        var s = Inverse(Printed.Constitutive(Math.PI / 4));

        // 1/E(45°) = (1/E1 + 1/E2 + 1/G12 − 2 ν12 / E1) / 4
        var g12 = Math.Sqrt(3000.0 * 2000) / 2.7;
        var expected = (1 / 3000.0 + 1 / 2000.0 + 1 / g12 - 2 * 0.35 / Math.Sqrt(3000.0 * 2000)) / 4;
        Assert.Equal(expected, s[0], 12);
        Assert.NotEqual(0, s[3], 9); // pulling along x shears a cell whose lines run at 45°
    }

    [Theory]
    [InlineData(0.3, 0)]
    [InlineData(1.2, 0.25)]
    [InlineData(2.9, 0.5)]
    public void It_stays_positive_definite_however_it_is_turned(double angle, double spread) =>
        Assert.True(PositiveDefinite(new OrthotropicMaterial(4000, 900, 600, 0.45).Constitutive(angle, spread)));

    [Fact]
    public void Lines_crossing_in_equal_parts_are_the_same_both_ways_in_the_plane()
    {
        var crossed = Printed.Constitutive(0, 0.5);
        var turned = Printed.Constitutive(1.1, 0.5);

        Assert.Equal(crossed[0], crossed[7], 9);
        for (var n = 0; n < 36; n++) Assert.Equal(crossed[n], turned[n], 8);
    }

    [Fact]
    public void Cells_share_a_matrix_by_layer_height_direction_and_spread()
    {
        // Three columns of lines: along X, along Y, along X again; one layer.
        var grid = new CellGrid(Vector2.Zero, 0.42f, [0, 0.2f], 3, 1);
        var cellVolume = grid.CellVolume(0);
        grid.Deposit(0, 0, 0, cellVolume, new Deposition(Vector2.UnitX, 0, 220, 1, FeatureClass.InnerWall));
        grid.Deposit(1, 0, 0, cellVolume, new Deposition(Vector2.UnitY, 0, 220, 1, FeatureClass.InnerWall));
        grid.Deposit(2, 0, 0, cellVolume, new Deposition(-Vector2.UnitX, 0, 220, 1, FeatureClass.InnerWall));
        var mesh = FemMesh.Build(grid, 0.05f, [new Fixture(_ => true)]);

        var orthotropic = new CellStiffness(mesh, Printed);
        var inPlane = new CellStiffness(mesh, new OrthotropicMaterial(3000, 3000, 1500, 0.35));

        Assert.Equal(2, orthotropic.KindCount); // a line and its reverse are one direction
        Assert.Equal(1, inPlane.KindCount);
        var along = Array.FindIndex(mesh.Cells, c => c.I == 0);
        Assert.Equal(1 / 3000.0, Inverse(orthotropic.Constitutive(orthotropic.Kind[along]))[0], 12);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(0.06, 0.06, 1, 0)]
    [InlineData(Math.PI - 0.01, 0.2, 0, 2)]
    [InlineData(Math.PI / 2, 0.5, 18, 4)]
    public void Directions_fall_in_steps_of_5_degrees_and_spreads_in_steps_of_an_eighth(double angle, double spread, int angleStep, int spreadStep) =>
        Assert.Equal((angleStep, spreadStep), CellStiffness.Bins(new LineDirection((float)angle, (float)spread)));
}
