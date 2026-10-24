using System.Numerics;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Visual;

namespace GcodeFem.Tests.Visual;

/// <summary>How the results view shows the failure check: the safety factor's bands, the weakest layer line, the weakest spot and a probed cell.</summary>
public class ResultMarksTests
{
    const float Pitch = 0.42f, Layer = 0.2f;

    [Fact]
    public void The_bands_of_the_safety_factor_get_lighter_towards_breaking()
    {
        var lightness = FailureColours.Bands.Select(band => Palette.Lightness(band.Colour)).ToList();

        for (var n = 1; n < lightness.Count; n++) Assert.True(lightness[n - 1] - lightness[n] > 0.05, $"band {n} is not darker enough than band {n - 1}");
        Assert.True(lightness[^1] - Palette.Lightness(Palette.Surface) > 0.1, "the safest band must still stand clear of the background");
    }

    [Theory]
    [InlineData(0.5, 0)]
    [InlineData(1.0, 1)]
    [InlineData(1.9, 1)]
    [InlineData(3, 2)]
    [InlineData(12, 3)]
    [InlineData(double.PositiveInfinity, 3)]
    public void A_safety_factor_falls_in_its_band(double safetyFactor, int band) =>
        Assert.Equal(FailureColours.Bands[band].Colour, FailureColours.SafetyColour(safetyFactor));

    [Fact]
    public void The_layer_line_is_outlined_where_cells_lie_on_cells()
    {
        // Three layers of 4 × 2 cells, the first one cell shorter: above it, that row has nothing below.
        var grid = CellGrid.Solid(4, 2, 3, Pitch, Layer);
        grid.Deposit(3, 0, 0, -grid.CellVolume(0));
        grid.Deposit(3, 1, 0, -grid.CellVolume(0));
        var mesh = FemMesh.Build(grid, 0.05f, [new Fixture(p => p.Z < 1e-3f)]);
        var cells = new HashSet<CellIndex>(mesh.Cells);

        var line = ResultMarks.LayerLine(mesh, cells, 1);

        // The outline of 3 × 2 cells: 10 cell edges, all on the plane between layers 0 and 1.
        Assert.Equal(20, line.Length);
        Assert.All(line, p => Assert.Equal(Layer, p.Z, 5));
        Assert.Equal(3 * Pitch, line.Max(p => p.X), 5);
    }

    [Fact]
    public void The_weakest_spot_is_pointed_at_from_outside_the_part()
    {
        var mesh = FemMesh.Build(CellGrid.Solid(3, 3, 3, Pitch, Layer), 0.05f, [new Fixture(p => p.Z < 1e-3f)]);
        var cells = new HashSet<CellIndex>(mesh.Cells);

        Assert.Equal(Vector3.UnitZ, ResultMarks.WayOut(cells, new CellIndex(1, 1, 2)));
        Assert.Equal(Vector3.UnitX, ResultMarks.WayOut(cells, new CellIndex(2, 1, 1)));
        Assert.Equal(-Vector3.UnitY, ResultMarks.WayOut(cells, new CellIndex(1, 0, 1)));
        Assert.Equal(Vector3.UnitZ, ResultMarks.WayOut(cells, new CellIndex(1, 1, 1))); // inside: from above
    }

    [Fact]
    public void A_click_finds_the_cell_under_it()
    {
        var grid = CellGrid.Solid(3, 3, 2, Pitch, Layer);
        var cells = grid.Occupied(0.05f).ToArray();
        var surface = CellSurface.Build(grid, cells, CellRange.All(grid));

        // Straight down onto the middle of the top: the top face of the upper middle cell.
        var face = surface.Pick(surface.Positions, new Vector3(1.5f * Pitch, 1.5f * Pitch, 5), -Vector3.UnitZ);

        Assert.Equal(new CellIndex(1, 1, 1), cells[surface.FaceCells[face]]);
        Assert.Equal(Vector3.UnitZ, surface.FaceNormal(face));
        Assert.Equal(-1, surface.Pick(surface.Positions, new Vector3(10, 10, 5), -Vector3.UnitZ));
    }
}
