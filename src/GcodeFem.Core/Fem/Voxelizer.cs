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
    /// cells of diagonal lines face-connected; the deposited volume is conserved exactly.
    /// </summary>
    public static CellGrid Voxelize(Toolpath toolpath, Placement placement, Box3 printBounds)
    {
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

        foreach (var segment in toolpath.Segments)
            if (segment.Feature.IsPartMaterial())
                Deposit(grid, placement.BedToPrint(segment.Start), placement.BedToPrint(segment.End), segment.Width, segment.Volume, segment.Layer);

        return grid;
    }

    static void Deposit(CellGrid grid, Vector3 start, Vector3 end, float width, float volume, int k)
    {
        var along = new Vector2(end.X - start.X, end.Y - start.Y);
        var length = along.Length();
        if (length <= 0) return;
        var normal = new Vector2(-along.Y, along.X) / length;

        var steps = Math.Max(1, (int)MathF.Ceiling(length / grid.Pitch * SamplesPerPitch));
        var share = volume / (steps * LateralSamples);
        var inverse = 1 / grid.Pitch;
        for (var s = 0; s < steps; s++)
        {
            var t = (s + 0.5f) / steps;
            var centre = new Vector2(start.X, start.Y) + along * t - grid.Origin;
            for (var l = 0; l < LateralSamples; l++)
            {
                var offset = ((l + 0.5f) / LateralSamples - 0.5f) * width;
                var p = centre + normal * offset;
                grid.Deposit((int)MathF.Floor(p.X * inverse), (int)MathF.Floor(p.Y * inverse), k, share);
            }
        }
    }
}
