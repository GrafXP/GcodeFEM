using System.Numerics;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Slicing;
using static GcodeFem.Tests.Fem.PrintedCellTests;

namespace GcodeFem.Tests.Fem;

public class LayerBondTests
{
    /// <summary>PLA Basic's specimens: 220 °C, glass transition at 60 °C, layers 4 s apart at full fan.</summary>
    static readonly BondReference Specimens = new(220, 60, 4, 1, []);

    static readonly BondModel Model = new();

    static double Bond(double gap, double nozzle = 220, double fan = 1) => Model.Factor(nozzle, nozzle, gap, fan, Specimens);

    [Fact]
    public void Printed_like_the_specimens_a_layer_line_bonds_like_theirs() =>
        Assert.Equal(1, Bond(4), 12);

    [Fact]
    public void A_bead_landing_at_once_meets_plastic_as_hot_as_itself() =>
        Assert.Equal(220, Model.InterfaceTemperature(220, 220, 0, 1), 12);

    [Fact]
    public void The_longer_the_layer_below_waits_the_weaker_the_bond_until_it_has_cooled_to_the_chamber()
    {
        Assert.True(Bond(8) < Bond(4));
        Assert.True(Bond(20) < Bond(8));
        // Cold below: the layer line lies half way between the nozzle and the chamber, and waiting longer changes nothing.
        var halfway = (220 + Model.AmbientTemperature) / 2;
        Assert.Equal((halfway - 60) / (Model.InterfaceTemperature(220, 220, 4, 1) - 60), Bond(600), 6);
        Assert.Equal(Bond(600), Bond(6000), 9);
    }

    [Fact]
    public void A_hotter_nozzle_bonds_better_but_never_better_than_the_specimens()
    {
        Assert.True(Bond(20, nozzle: 240) > Bond(20));
        Assert.True(Bond(20, nozzle: 200) < Bond(20));
        Assert.Equal(1, Bond(1, nozzle: 250));
    }

    [Fact]
    public void Less_fan_lets_the_layer_below_stay_warm_for_longer() =>
        Assert.True(Bond(8, fan: 0.3) > Bond(8, fan: 1));

    [Fact]
    public void Each_cell_bonds_to_the_one_below_by_how_long_ago_that_was_printed()
    {
        // The same row twice: the second layer 30 s after the first, at the same speed.
        const float width = 0.42f;
        var from = new Vector2(0, 3.5f * width);
        var to = new Vector2(10 * width, 3.5f * width);
        var toolpath = Print(Bead(from, to, layer: 0, start: 0, duration: 5), Bead(from, to, layer: 1, start: 30, duration: 5));
        var grid = Voxelizer.Voxelize(toolpath, new Placement(Matrix4x4.Identity, Vector3.Zero), new Box3(Vector3.Zero, new Vector3(20 * width, 20 * width, 0.4f)));
        var cells = grid.Occupied(0.05f).ToArray();

        var bonds = Model.Cells(grid, cells, Specimens, 0.05f);

        for (var e = 0; e < cells.Length; e++)
            if (cells[e].K == 0) Assert.Equal((1f, float.NaN), (bonds.Factor[e], bonds.Gap[e]));
            else
            {
                Assert.Equal(30, bonds.Gap[e], 3);
                Assert.Equal(Bond(30), bonds.Factor[e], 5);
            }
    }
}
