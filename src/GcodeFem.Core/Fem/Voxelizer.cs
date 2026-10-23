using System.Numerics;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Slicing;

namespace GcodeFem.Core.Fem;

/// <summary>Turns the part's extrusion segments into bead cells.</summary>
public static class Voxelizer
{
    /// <summary>Samples across a bead. Even, so a bead centred on a cell boundary splits 50/50.</summary>
    const int LateralSamples = 4;

    /// <summary>Samples along a bead per cell pitch.</summary>
    const int SamplesPerPitch = 4;

    /// <summary>
    /// Grid: XY pitch = the profile's line width, origin at the model's low corner (so outer-wall
    /// beads, centred half a line width inside, fill exactly one cell column); one cell per layer.
    /// Each bead is spread over the cells under its footprint (width × length), which keeps the
    /// cells of diagonal lines face-connected; the deposited volume is conserved exactly. Every
    /// piece also tells its cell which way the line runs, its line type, when it was printed
    /// (by <paramref name="clock"/>, else by feed rates), at what nozzle temperature and fan.
    /// </summary>
    public static CellGrid Voxelize(Toolpath toolpath, Placement placement, Box3 printBounds, PrintClock? clock = null)
    {
        clock ??= PrintClock.FromFeedRates(toolpath);
        var pitch = toolpath.SettingNumber("line_width")
                    ?? throw new InvalidDataException("The G-code has no line_width setting.");
        if (toolpath.Layers.Length == 0) throw new InvalidDataException("The G-code has no layers.");

        var z = new float[toolpath.Layers.Length + 1];
        var first = toolpath.Layers[0];
        z[0] = first.Z - first.Height - placement.BedOffset.Z;
        for (var k = 0; k < toolpath.Layers.Length; k++)
        {
            var layer = toolpath.Layers[k];
            if (float.IsNaN(layer.Z) || layer.Index != k)
                throw new InvalidDataException($"Layer {k} has no Z_HEIGHT marker or is out of order.");
            z[k + 1] = layer.Z - placement.BedOffset.Z;
        }

        var origin = new Vector2(printBounds.Min.X, printBounds.Min.Y);
        var grid = new CellGrid(origin, pitch, z,
            (int)MathF.Ceiling(printBounds.Size.X / pitch - 1e-4f),
            (int)MathF.Ceiling(printBounds.Size.Y / pitch - 1e-4f));

        var segments = toolpath.Segments;
        for (var s = 0; s < segments.Length; s++)
            if (segments[s].Feature.IsPartMaterial())
                Deposit(grid, segments[s], placement.BedToPrint(segments[s].Start), placement.BedToPrint(segments[s].End), clock.Start(s), clock.Duration(s));

        return grid;
    }

    static void Deposit(CellGrid grid, in ExtrusionSegment segment, Vector3 start, Vector3 end, double startTime, double duration)
    {
        var along = new Vector2(end.X - start.X, end.Y - start.Y);
        var length = along.Length();
        if (length <= 0) return;
        var direction = along / length;
        var normal = new Vector2(-direction.Y, direction.X);

        var steps = Math.Max(1, (int)MathF.Ceiling(length / grid.Pitch * SamplesPerPitch));
        var share = segment.Volume / (steps * LateralSamples);
        var inverse = 1 / grid.Pitch;
        var (temperature, fan, feature) = ((float)segment.NozzleTemperature, segment.Fan / 255f, segment.Feature.Class());
        for (var s = 0; s < steps; s++)
        {
            var t = (s + 0.5f) / steps;
            var centre = new Vector2(start.X, start.Y) + along * t - grid.Origin;
            var how = new Deposition(direction, startTime + t * duration, temperature, fan, feature);
            for (var l = 0; l < LateralSamples; l++)
            {
                var offset = ((l + 0.5f) / LateralSamples - 0.5f) * segment.Width;
                var p = centre + normal * offset;
                grid.Deposit((int)MathF.Floor(p.X * inverse), (int)MathF.Floor(p.Y * inverse), segment.Layer, share, how);
            }
        }
    }
}
