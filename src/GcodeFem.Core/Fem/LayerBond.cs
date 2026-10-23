namespace GcodeFem.Core.Fem;

/// <summary>
/// What the bond across a layer line is measured against: how the data sheet's specimens were
/// printed. Their strength across the layers is the material's, so a layer line printed like
/// theirs has a bond factor of 1.
/// </summary>
/// <param name="NozzleTemperature">°C.</param>
/// <param name="GlassTransition">°C. Below it the plastic no longer heals across a layer line.</param>
/// <param name="LayerTime">Seconds between two layers of a specimen.</param>
/// <param name="Fan">The part-cooling fan while the specimens were printed, 0 to 1.</param>
/// <param name="Assumed">What of this is not the material's own numbers, in words.</param>
public sealed record BondReference(double NozzleTemperature, double GlassTransition, double LayerTime, double Fan, IReadOnlyList<string> Assumed);

/// <summary>Per cell: its bond to the cell below as a share of the specimens', and how long after that cell it was printed (s; NaN where nothing is below).</summary>
public sealed record LayerBonds(float[] Factor, float[] Gap);

/// <summary>
/// How well a cell has bonded to the one below it, as a share of the data sheet's specimens. Not
/// calibrated yet (M10); every parameter is an assumption with a physical meaning:
/// <list type="bullet">
/// <item>Since it was printed, the cell below has cooled towards the air of the chamber
/// (<paramref name="AmbientTemperature"/>, °C), by Newton's law with a time constant of
/// <paramref name="CoolingTime"/> seconds in still air, 1 + <paramref name="FanCooling"/> times
/// shorter at full fan.</item>
/// <item>When the new bead lands, the layer line takes the mean of the two temperatures, as two
/// bodies of one plastic in contact do.</item>
/// <item>The bond grows with how far that is above the glass transition, where the plastic stops
/// healing; the factor is that excess over the specimens', capped at 1: the sheet's numbers are the
/// best the plastic can do.</item>
/// </list>
/// So a hotter nozzle bonds better, a long wait for the next layer or a strong fan worse, and the
/// bond stops falling once the layer below has cooled to the chamber.
/// </summary>
public sealed record BondModel(double AmbientTemperature = 35, double CoolingTime = 20, double FanCooling = 6)
{
    /// <summary>The temperature of plastic printed at <paramref name="printedAt"/> °C, <paramref name="gap"/> seconds later.</summary>
    public double SurfaceTemperature(double printedAt, double gap, double fan) =>
        AmbientTemperature + (printedAt - AmbientTemperature) * Math.Exp(-Math.Max(0, gap) * (1 + FanCooling * Math.Clamp(fan, 0, 1)) / CoolingTime);

    /// <summary>The layer line when a bead at <paramref name="nozzle"/> °C lands on plastic printed at <paramref name="below"/> °C <paramref name="gap"/> seconds before.</summary>
    public double InterfaceTemperature(double nozzle, double below, double gap, double fan) =>
        (nozzle + SurfaceTemperature(below, gap, fan)) / 2;

    public double Factor(double nozzle, double below, double gap, double fan, BondReference reference)
    {
        var specimens = InterfaceTemperature(reference.NozzleTemperature, reference.NozzleTemperature, reference.LayerTime, reference.Fan) - reference.GlassTransition;
        if (!(specimens > 0)) return 1;
        return Math.Clamp((InterfaceTemperature(nozzle, below, gap, fan) - reference.GlassTransition) / specimens, 0, 1);
    }

    /// <summary>
    /// The bond of each of <paramref name="cells"/> to the cell below it. A cell with nothing below
    /// (the first layer, or over air) has no layer line to weaken: its factor is 1. So has a cell
    /// whose plastic does not say how it was printed.
    /// </summary>
    public LayerBonds Cells(CellGrid grid, IReadOnlyList<CellIndex> cells, BondReference reference, float minFill)
    {
        var factor = new float[cells.Count];
        var gap = new float[cells.Count];
        Parallel.For(0, cells.Count, e =>
        {
            var (i, j, k) = (cells[e].I, cells[e].J, cells[e].K);
            var time = grid.DepositionTime(i, j, k);
            var before = k > 0 && grid.Fill(i, j, k - 1) >= minFill ? grid.DepositionTime(i, j, k - 1) : double.NaN;
            if (double.IsNaN(time) || double.IsNaN(before))
            {
                (factor[e], gap[e]) = (1, float.NaN);
                return;
            }
            var wait = Math.Max(0, time - before);
            gap[e] = (float)wait;
            factor[e] = (float)Factor(grid.NozzleTemperature(i, j, k), grid.NozzleTemperature(i, j, k - 1), wait, grid.Fan(i, j, k), reference);
        });
        return new LayerBonds(factor, gap);
    }
}
