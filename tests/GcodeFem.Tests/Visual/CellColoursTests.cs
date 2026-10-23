using GcodeFem.Core.Fem;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Visual;

namespace GcodeFem.Tests.Visual;

public class CellColoursTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(20, 0, 0)]
    [InlineData(30, 0.1, 1)]
    [InlineData(95, 0, 2)]
    [InlineData(130, 0.2, 3)]
    [InlineData(170, 0, 0)]
    [InlineData(45, 0.3, CellColours.Crossing)]
    public void Directions_fall_in_four_sectors_unless_lines_cross(double degrees, double spread, int group) =>
        Assert.Equal(group, CellColours.DirectionGroup(new LineDirection((float)(degrees * Math.PI / 180), (float)spread)));

    [Fact]
    public void Every_direction_group_has_its_own_colour_and_name()
    {
        var legend = CellColours.DirectionLegend();

        Assert.Equal(5, legend.Entries.Count);
        Assert.Equal(5, legend.Entries.Select(e => e.Colour).Distinct().Count());
        Assert.Equal("Lines cross", legend.Entries[CellColours.Crossing].Label);
    }

    [Fact]
    public void Line_types_keep_the_colours_of_the_toolpaths_view()
    {
        var legend = CellColours.FeatureLegend([FeatureClass.SparseInfill, FeatureClass.OuterWall, FeatureClass.SparseInfill]);

        Assert.Equal(["Outer wall", "Sparse infill"], legend.Entries.Select(e => e.Label));
        Assert.Equal(ToolpathColours.GroupColour(ToolpathColours.Group(FeatureType.SparseInfill)), CellColours.FeatureColour(FeatureClass.SparseInfill));
    }
}
