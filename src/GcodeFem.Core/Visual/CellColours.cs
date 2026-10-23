using System.Numerics;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Gcode;

namespace GcodeFem.Core.Visual;

/// <summary>The cells view's colours for what a cell is made of: which way its lines run, and what line type they are.</summary>
public static class CellColours
{
    /// <summary>The direction group of cells where lines run more than one way.</summary>
    public const int Crossing = 4;

    /// <summary>From this spread on, a cell counts as crossed: at least a quarter of its plastic runs across the rest.</summary>
    public const float CrossingSpread = 0.25f;

    static readonly string[] DirectionNames = ["Along X", "Diagonal, X and Y together", "Along Y", "Diagonal, X against Y", "Lines cross"];

    /// <summary>
    /// Four sectors of 45° about X, the two diagonals and Y, or <see cref="Crossing"/>. Lines run
    /// either way along themselves, so a direction and its opposite are one.
    /// </summary>
    public static int DirectionGroup(LineDirection direction) =>
        direction.Spread >= CrossingSpread ? Crossing : (int)Math.Round(direction.Angle / (Math.PI / 4)) % 4;

    public static Vector4 DirectionColour(int group) => Palette.Categorical[group];

    public static Legend DirectionLegend() =>
        new("Which way the lines run", "", [.. DirectionNames.Select((name, group) => new LegendEntry(group, name, DirectionColour(group)))]);

    /// <summary>The same colour as the line type has in the toolpaths view.</summary>
    public static Vector4 FeatureColour(FeatureClass feature) => ToolpathColours.GroupColour((int)feature);

    /// <summary>Entries for the line types that occur, in their fixed order.</summary>
    public static Legend FeatureLegend(IEnumerable<FeatureClass> present) =>
        new("Line type holding most of the cell", "", [.. present.Distinct().Order().Select(f => new LegendEntry((int)f, f.DisplayName(), FeatureColour(f)))]);
}
