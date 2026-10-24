using System.Numerics;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Slicing;

namespace GcodeFem.Core.Visual;

/// <summary>
/// A toolpath's beads as drawable geometry in the print frame, whole or in the pieces of
/// <see cref="BeadPieces"/>. Each piece is a tube with a diamond cross-section of its bead's width and
/// height, shaded round, so neighbouring lines stay apart to the eye. Where a bead ends, its tube comes
/// to a point half a width beyond the nozzle's path, as far as the plastic reaches, which also closes
/// the corners between one line and the next; where it is only cut, the tube ends flat against the
/// next piece. A toolpath too big for that is drawn as plain lines. Pieces keep the order of the G-code.
/// </summary>
public sealed class ToolpathGeometry
{
    /// <summary>A ring of four at each end of the piece, then the two tips.</summary>
    const int TubeVertices = 10, TubeIndices = 48;

    /// <summary>Above this many pieces, tubes cost too much memory (10 vertices each) and lines are drawn instead.</summary>
    public const int MaxTubes = 400_000;

    public required BeadPieces Pieces { get; init; }
    public required bool Tubes { get; init; }
    public required Vector3[] Positions { get; init; }

    /// <summary>Per vertex, for tubes; lines have none.</summary>
    public required Vector3[]? Normals { get; init; }

    /// <summary>Per piece: its bead's index in <see cref="Toolpath.Segments"/>, ascending.</summary>
    public int[] Segments => Pieces.Segments;

    public int VerticesPerPiece => Tubes ? TubeVertices : 2;
    public int IndicesPerPiece => Tubes ? TubeIndices : 2;

    /// <summary>Every bead whole.</summary>
    /// <param name="tubes">Null chooses tubes unless there are more than <see cref="MaxTubes"/> beads.</param>
    public static ToolpathGeometry Build(Toolpath toolpath, Placement placement, bool? tubes = null) =>
        Build(toolpath, BeadPieces.Whole(toolpath, placement), tubes);

    /// <param name="tubes">Null chooses tubes unless there are more than <see cref="MaxTubes"/> pieces.</param>
    /// <param name="ends">Where the pieces are drawn, two per piece: <see cref="BeadPieces.Ends"/> by default, or the deformed shape. Each keeps the cross-section of its bead as printed.</param>
    public static ToolpathGeometry Build(Toolpath toolpath, BeadPieces pieces, bool? tubes = null, Vector3[]? ends = null)
    {
        var asTubes = tubes ?? pieces.Count <= MaxTubes;
        ends ??= pieces.Ends;

        var perPiece = asTubes ? TubeVertices : 2;
        var positions = new Vector3[perPiece * pieces.Count];
        var normals = asTubes ? new Vector3[positions.Length] : null;
        Parallel.For(0, pieces.Count, p =>
        {
            var (start, end) = (ends[2 * p], ends[2 * p + 1]);
            if (normals is null)
            {
                (positions[2 * p], positions[2 * p + 1]) = (start, end);
                return;
            }

            // The bead's own direction as printed, so that the pieces of one bead line up even where they are moved.
            var segment = toolpath.Segments[pieces.Segments[p]];
            var along = Vector3.Normalize(segment.End - segment.Start);
            var side = Vector3.Cross(along, Vector3.UnitZ);
            side = side.LengthSquared() > 1e-6f ? Vector3.Normalize(side) : Vector3.UnitX;
            var up = Vector3.Cross(side, along);
            Span<Vector3> ring = [up, side, -up, -side];
            var first = TubeVertices * p;
            for (var m = 0; m < 4; m++)
            {
                var offset = ring[m] * ((m & 1) == 0 ? segment.Height : segment.Width) / 2;
                (positions[first + m], positions[first + 4 + m]) = (start + offset, end + offset);
                (normals[first + m], normals[first + 4 + m]) = (ring[m], ring[m]);
            }
            // A cut end's tip lies in its ring, which closes the tube flat.
            var tip = along * segment.Width / 2;
            var tips = pieces.Tips[p];
            (positions[first + 8], positions[first + 9]) = (start - ((tips & BeadPieces.StartTip) != 0 ? tip : Vector3.Zero), end + ((tips & BeadPieces.EndTip) != 0 ? tip : Vector3.Zero));
            (normals[first + 8], normals[first + 9]) = (-along, along);
        });
        return new ToolpathGeometry { Pieces = pieces, Tubes = asTubes, Positions = positions, Normals = normals };
    }

    /// <summary>
    /// The index buffer for the pieces of layers <paramref name="layerLow"/> up to but not including
    /// <paramref name="layerHigh"/> whose feature <paramref name="shows"/> accepts: triangles for tubes, point pairs for lines.
    /// </summary>
    public int[] Indices(Toolpath toolpath, int layerLow, int layerHigh, Func<FeatureType, bool> shows) =>
        Indices(Pieces.Shown(toolpath, layerLow, layerHigh, shows));

    /// <summary>The index buffer for the pieces <paramref name="visible"/>, as <see cref="BeadPieces.Shown"/> lists them.</summary>
    public int[] Indices(int[] visible)
    {
        if (!Tubes)
        {
            var pairs = new int[2 * visible.Length];
            for (var n = 0; n < visible.Length; n++) (pairs[2 * n], pairs[2 * n + 1]) = (2 * visible[n], 2 * visible[n] + 1);
            return pairs;
        }

        var indices = new int[TubeIndices * visible.Length];
        Parallel.For(0, visible.Length, n =>
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
    public Vector4[] VertexColours(Vector4[] segmentColours) => VertexColours(p => segmentColours[Segments[p]]);

    /// <summary>Per-vertex colours from one colour per piece.</summary>
    public Vector4[] VertexColours(Func<int, Vector4> pieceColour)
    {
        var perPiece = VerticesPerPiece;
        var colours = new Vector4[Positions.Length];
        Parallel.For(0, Pieces.Count, p => colours.AsSpan(perPiece * p, perPiece).Fill(pieceColour(p)));
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
    public static int Group(FeatureType feature) => feature.IsPartMaterial() ? (int)feature.Class() : NotPart;

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
