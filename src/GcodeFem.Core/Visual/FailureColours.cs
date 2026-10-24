using System.Numerics;
using GcodeFem.Core.Fem;

namespace GcodeFem.Core.Visual;

/// <summary>
/// The results view's colours for how near the part is to breaking. The safety factor comes in four
/// bands, red where it breaks, amber under 2, green under 4, and a dark grey-green above; they get
/// darker in that order, so the eye goes to the danger first and the bands stay apart without colour
/// vision. How a cell would break takes the categorical hues in a fixed order.
/// </summary>
public static class FailureColours
{
    /// <summary>The upper end of each band of the safety factor, its name and its colour, the band that breaks first.</summary>
    public static readonly IReadOnlyList<(float Below, string Label, Vector4 Colour)> Bands =
    [
        (1, "Breaks: below 1", Palette.Hex("#f28b82")),
        (2, "1 to 2", Palette.Hex("#b9821a")),
        (4, "2 to 4", Palette.Hex("#24704d")),
        (float.PositiveInfinity, "4 and above", Palette.Hex("#333d38")),
    ];

    /// <summary>Cells far enough from breaking that how they would break does not matter.</summary>
    public static readonly Vector4 FarFromBreaking = Palette.Hex("#5a5a57");

    /// <summary>The weakest spot, the weakest layer line and a probed cell.</summary>
    public static readonly Vector4 Marker = Palette.Ink;

    public static Vector4 SafetyColour(double safetyFactor)
    {
        foreach (var (below, _, colour) in Bands)
            if (safetyFactor < below) return colour;
        return Bands[^1].Colour; // NaN
    }

    public static Legend SafetyLegend() =>
        new("Safety factor: the factor on the loads at which it breaks", "", [.. Bands.Select((band, n) => new LegendEntry(n, band.Label, band.Colour))]);

    public static Vector4 ModeColour(FailureMode mode) => Palette.Categorical[(int)mode];

    /// <param name="present">The modes that govern somewhere near the weakest spot.</param>
    /// <param name="near">How far from the weakest a cell may be and still show its mode: up to this × the lowest safety factor.</param>
    public static Legend ModeLegend(IEnumerable<FailureMode> present, double near) =>
        new("What breaks first", "",
            [
                .. present.Distinct().Order().Select(mode => new LegendEntry((int)mode, mode.DisplayName(), ModeColour(mode))),
                new LegendEntry(FailureModes.Count, $"Safety factor over {near:0.#} x the lowest", FarFromBreaking),
            ]);
}
