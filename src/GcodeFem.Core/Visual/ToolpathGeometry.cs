using System.Numerics;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Slicing;

namespace GcodeFem.Core.Visual;

/// <summary>
/// A toolpath's beads as drawable geometry in the print frame. Each bead is a tube with a diamond
/// cross-section of its own width and height, shaded round, so neighbouring lines stay apart to
/// the eye. Its ends come to a point half a width beyond the nozzle's path, as far as the plastic
/// reaches, which also closes the corners between one line and the next. A toolpath too big for
/// that is drawn as plain lines. Beads keep the order of the G-code.
/// </summary>
public sealed class ToolpathGeometry
{
    /// <summary>A ring of four at each end of the nozzle's path, then the two tips.</summary>
    const int TubeVertices = 10, TubeIndices = 48;

    /// <summary>Above this many beads, tubes cost too much memory (10 vertices each) and lines are drawn instead.</summary>
    public const int MaxTubes = 400_000;

    public required bool Tubes { get; init; }
    public required Vector3[] Positions { get; init; }

    /// <summary>Per vertex, for tubes; lines have none.</summary>
    public required Vector3[]? Normals { get; init; }

    /// <summary>Per bead drawn: its index in <see cref="Toolpath.Segments"/>, ascending.</summary>
    public required int[] Segments { get; init; }

    public int VerticesPerBead => Tubes ? TubeVertices : 2;
    public int IndicesPerBead => Tubes ? TubeIndices : 2;

    /// <param name="tubes">Null chooses tubes unless there are more than <see cref="MaxTubes"/> beads.</param>
    public static ToolpathGeometry Build(Toolpath toolpath, Placement placement, bool? tubes = null)
    {
        var segments = new List<int>(toolpath.Segments.Length);
        for (var s = 0; s < toolpath.Segments.Length; s++)
            if (toolpath.Segments[s].Length > 0) segments.Add(s);
        var asTubes = tubes ?? segments.Count <= MaxTubes;

        var perBead = asTubes ? TubeVertices : 2;
        var positions = new Vector3[perBead * segments.Count];
        var normals = asTubes ? new Vector3[positions.Length] : null;
        Parallel.For(0, segments.Count, b =>
        {
            var segment = toolpath.Segments[segments[b]];
            // The G-code's Z is the nozzle, at the top of the bead.
            var down = new Vector3(0, 0, segment.Height / 2);
            var start = placement.BedToPrint(segment.Start) - down;
            var end = placement.BedToPrint(segment.End) - down;
            if (normals is null)
            {
                (positions[2 * b], positions[2 * b + 1]) = (start, end);
                return;
            }

            var along = Vector3.Normalize(end - start);
            var side = Vector3.Cross(along, Vector3.UnitZ);
            side = side.LengthSquared() > 1e-6f ? Vector3.Normalize(side) : Vector3.UnitX;
            var up = Vector3.Cross(side, along);
            Span<Vector3> ring = [up, side, -up, -side];
            var first = TubeVertices * b;
            for (var m = 0; m < 4; m++)
            {
                var offset = ring[m] * ((m & 1) == 0 ? segment.Height : segment.Width) / 2;
                (positions[first + m], positions[first + 4 + m]) = (start + offset, end + offset);
                (normals[first + m], normals[first + 4 + m]) = (ring[m], ring[m]);
            }
            var tip = along * segment.Width / 2;
            (positions[first + 8], positions[first + 9]) = (start - tip, end + tip);
            (normals[first + 8], normals[first + 9]) = (-along, along);
        });
        return new ToolpathGeometry { Tubes = asTubes, Positions = positions, Normals = normals, Segments = [.. segments] };
    }

    /// <summary>
    /// The index buffer for the beads of layers <paramref name="layerLow"/> up to but not including
    /// <paramref name="layerHigh"/> whose feature <paramref name="shows"/> accepts: triangles for tubes, point pairs for lines.
    /// </summary>
    public int[] Indices(Toolpath toolpath, int layerLow, int layerHigh, Func<FeatureType, bool> shows)
    {
        var visible = new List<int>();
        for (var b = 0; b < Segments.Length; b++)
        {
            var segment = toolpath.Segments[Segments[b]];
            if (segment.Layer >= layerLow && segment.Layer < layerHigh && shows(segment.Feature)) visible.Add(b);
        }

        if (!Tubes)
        {
            var pairs = new int[2 * visible.Count];
            for (var n = 0; n < visible.Count; n++) (pairs[2 * n], pairs[2 * n + 1]) = (2 * visible[n], 2 * visible[n] + 1);
            return pairs;
        }

        var indices = new int[TubeIndices * visible.Count];
        Parallel.For(0, visible.Count, n =>
        {
            int first = TubeVertices * visible[n], at = TubeIndices * n;
            for (var m = 0; m < 4; m++)
            {
                // The side between ring corners m and m + 1, start ring first, then the faces of the
                // two tips on that side; all counter-clockwise from outside.
                int a = first + m, b = first + (m + 1) % 4, c = b + 4, d = a + 4;
                (indices[at], indices[at + 1], indices[at + 2]) = (a, b, c);
                (indices[at + 3], indices[at + 4], indices[at + 5]) = (a, c, d);
                (indices[at + 6], indices[at + 7], indices[at + 8]) = (first + 8, b, a);
                (indices[at + 9], indices[at + 10], indices[at + 11]) = (first + 9, d, c);
                at += 12;
            }
        });
        return indices;
    }

    /// <summary>Per-vertex colours from one colour per toolpath segment.</summary>
    public Vector4[] VertexColours(Vector4[] segmentColours)
    {
        var perBead = VerticesPerBead;
        var colours = new Vector4[Positions.Length];
        Parallel.For(0, Segments.Length, b => colours.AsSpan(perBead * b, perBead).Fill(segmentColours[Segments[b]]));
        return colours;
    }
}

public enum ToolpathColouring { Feature, NozzleTemperature, Fan, Speed, Time, Width }

/// <summary>What colour each bead gets, and the legend that explains it.</summary>
public static class ToolpathColours
{
    /// <summary>The last group: skirt, brim, support, prime tower and custom G-code.</summary>
    public const int NotPart = 8;

    /// <summary>
    /// Features share the palette's eight identity colours, in order of how much of a part they
    /// usually make up. Both bridge types share one, the rare part features share the last, and
    /// whatever is not part of the model is grey.
    /// </summary>
    public static int Group(FeatureType feature) => feature switch
    {
        FeatureType.OuterWall => 0,
        FeatureType.InnerWall => 1,
        FeatureType.SparseInfill => 2,
        FeatureType.InternalSolidInfill => 3,
        FeatureType.TopSurface => 4,
        FeatureType.BottomSurface => 5,
        FeatureType.Bridge or FeatureType.InternalBridge => 6,
        _ when feature.IsPartMaterial() => 7,
        _ => NotPart,
    };

    public static Vector4 GroupColour(int group) => group < Palette.Categorical.Length ? Palette.Categorical[group] : Palette.Neutral;

    /// <summary>One colour per segment of <paramref name="toolpath"/>. A scale spans the part's own lines.</summary>
    public static (Vector4[] BySegment, Legend Legend) Colour(Toolpath toolpath, ToolpathColouring by)
    {
        var segments = toolpath.Segments;
        var colours = new Vector4[segments.Length];
        if (by == ToolpathColouring.Feature)
        {
            var present = new SortedSet<FeatureType>[NotPart + 1];
            for (var s = 0; s < segments.Length; s++)
            {
                var group = Group(segments[s].Feature);
                colours[s] = GroupColour(group);
                (present[group] ??= []).Add(segments[s].Feature);
            }
            var entries = new List<LegendEntry>();
            for (var group = 0; group <= NotPart; group++)
                if (present[group] is { } features)
                    entries.Add(new LegendEntry(group, string.Join(", ", features.Select(f => f.DisplayName())), GroupColour(group)));
            return (colours, new Legend("Line type", "", entries));
        }

        var (title, unit, value) = Quantity(by);
        var (min, max) = ToolpathStatistics.PartRange(toolpath, value) ?? (0, 0);
        var scale = ColourScale.Sequential(min, max);
        Parallel.For(0, segments.Length, s => colours[s] = scale.Colour(value(segments[s])));
        return (colours, new Legend(title, unit, [], scale));
    }

    static (string Title, string Unit, Func<ExtrusionSegment, float> Value) Quantity(ToolpathColouring by) => by switch
    {
        ToolpathColouring.NozzleTemperature => ("Nozzle temperature", "°C", s => s.NozzleTemperature),
        ToolpathColouring.Fan => ("Part-cooling fan", "%", s => s.Fan / 2.55f),
        ToolpathColouring.Speed => ("Speed", "mm/s", s => s.Duration > 0 ? s.Length / s.Duration : 0),
        ToolpathColouring.Time => ("Time into the print, from feed rates", "min", s => s.StartTime / 60),
        ToolpathColouring.Width => ("Line width", "mm", s => s.Width),
        _ => throw new ArgumentOutOfRangeException(nameof(by)),
    };
}
