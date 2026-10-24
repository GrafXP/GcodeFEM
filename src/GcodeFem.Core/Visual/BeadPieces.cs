using System.Numerics;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Slicing;

namespace GcodeFem.Core.Visual;

/// <summary>
/// A toolpath's beads in pieces along their centre lines, for drawing. Whole, each bead is one piece;
/// cut, a bead is cut wherever it passes from one bead cell into the next (<see cref="Voxelizer.Walk"/>),
/// so that each piece can take the result of its cell. A piece is then about one line width long, and
/// two beads that share a cell show the same value: the solve says nothing finer. Lines that are not
/// part of the model (skirt, support, prime tower) stay whole, and they and pieces whose cell is not in
/// the solve (an island no mount holds) have no cell. Pieces keep the order of the G-code.
/// </summary>
public sealed class BeadPieces
{
    /// <summary>Bits of <see cref="Tips"/>: the bead starts, or ends, at this end of the piece.</summary>
    public const byte StartTip = 1, EndTip = 2;

    /// <summary>Per piece: the segment of <see cref="Toolpath.Segments"/> it is part of, ascending.</summary>
    public required int[] Segments { get; init; }

    /// <summary>Two per piece, where it starts and ends: on the bead's centre line, half a bead below the nozzle (print frame).</summary>
    public required Vector3[] Ends { get; init; }

    /// <summary>Per piece: the index of its cell in the mesh it was cut for, −1 for none.</summary>
    public required int[] Cells { get; init; }

    /// <summary>Per piece: which of its ends are an end of its bead, where the plastic reaches on beyond the nozzle's path.</summary>
    public required byte[] Tips { get; init; }

    public int Count => Segments.Length;

    /// <summary>Every bead of some length as one piece, in no cell: what the toolpath view draws.</summary>
    public static BeadPieces Whole(Toolpath toolpath, Placement placement)
    {
        var segments = new List<int>(toolpath.Segments.Length);
        for (var s = 0; s < toolpath.Segments.Length; s++)
            if (toolpath.Segments[s].Length > 0) segments.Add(s);

        var ends = new Vector3[2 * segments.Count];
        Parallel.For(0, segments.Count, p => (ends[2 * p], ends[2 * p + 1]) = CentreLine(toolpath.Segments[segments[p]], placement));
        var cells = new int[segments.Count];
        Array.Fill(cells, -1);
        var tips = new byte[segments.Count];
        Array.Fill(tips, (byte)(StartTip | EndTip));
        return new BeadPieces { Segments = [.. segments], Ends = ends, Cells = cells, Tips = tips };
    }

    /// <summary>The part's beads cut at the cells of <paramref name="mesh"/>, each piece in its cell; the rest whole.</summary>
    public static BeadPieces Cut(Toolpath toolpath, Placement placement, FemMesh mesh)
    {
        var cellOf = new Dictionary<CellIndex, int>(mesh.Cells.Length);
        for (var e = 0; e < mesh.Cells.Length; e++) cellOf[mesh.Cells[e]] = e;

        var count = toolpath.Segments.Length;
        var (segments, ends, cells, tips) = (new List<int>(2 * count), new List<Vector3>(4 * count), new List<int>(2 * count), new List<byte>(2 * count));
        var walk = new List<(int I, int J, float From, float To)>();
        var inCell = new List<(int Cell, float From, float To)>();
        for (var s = 0; s < count; s++)
        {
            var segment = toolpath.Segments[s];
            if (segment.Length <= 0) continue;
            var (start, end) = CentreLine(segment, placement);
            walk.Clear();
            inCell.Clear();
            if (segment.Feature.IsPartMaterial()) Voxelizer.Walk(mesh.Grid, start, end, walk);
            else walk.Add((-1, -1, 0, 1));
            foreach (var (i, j, from, to) in walk)
                inCell.Add((i >= 0 && cellOf.TryGetValue(new CellIndex(i, j, segment.Layer), out var e) ? e : -1, from, to));

            // Where a line clips the corner of a cell, the voxelizer's samples may have left none of its
            // plastic there: a stretch that short outside the solve is part of the piece next to it.
            var shortest = mesh.Grid.Pitch / 2 / segment.Length;
            for (var n = 0; n < inCell.Count; n++)
            {
                if (inCell[n].Cell >= 0 || inCell[n].To - inCell[n].From >= shortest) continue;
                if (n > 0 && inCell[n - 1].Cell >= 0) inCell[n] = inCell[n] with { Cell = inCell[n - 1].Cell };
                else if (n + 1 < inCell.Count && inCell[n + 1].Cell >= 0) inCell[n] = inCell[n] with { Cell = inCell[n + 1].Cell };
            }

            var first = segments.Count;
            foreach (var (cell, from, to) in inCell)
            {
                // Neighbouring stretches in the same cell, or outside the solve, make one piece.
                if (segments.Count > first && cells[^1] == cell)
                {
                    ends[^1] = Vector3.Lerp(start, end, to);
                    continue;
                }
                segments.Add(s);
                ends.Add(Vector3.Lerp(start, end, from));
                ends.Add(Vector3.Lerp(start, end, to));
                cells.Add(cell);
                tips.Add(0);
            }
            tips[first] |= StartTip;
            tips[^1] |= EndTip;
        }
        return new BeadPieces { Segments = [.. segments], Ends = [.. ends], Cells = [.. cells], Tips = [.. tips] };
    }

    /// <summary>
    /// The ends moved by <paramref name="scale"/> times the displacement where they lie in their cell,
    /// interpolated between its corners as across the cell's faces: the deformed shape. Pieces that meet
    /// at a cell boundary stay joined. <paramref name="nodeDisplacements"/> holds one vector per node of
    /// <paramref name="mesh"/>, which the pieces must have been cut for; pieces in no cell stay where they are.
    /// </summary>
    public Vector3[] Displaced(FemMesh mesh, Vector3[] nodeDisplacements, float scale)
    {
        var moved = new Vector3[Ends.Length];
        Parallel.For(0, Count, p =>
        {
            for (var end = 2 * p; end < 2 * p + 2; end++)
                moved[end] = Cells[p] < 0 ? Ends[end] : Ends[end] + scale * DisplacementIn(mesh, nodeDisplacements, Cells[p], Ends[end]);
        });
        return moved;
    }

    /// <summary>The pieces of beads in layers <paramref name="layerLow"/> up to but not including <paramref name="layerHigh"/> whose feature <paramref name="shows"/> accepts.</summary>
    public int[] Shown(Toolpath toolpath, int layerLow, int layerHigh, Func<FeatureType, bool> shows)
    {
        var shown = new List<int>();
        for (var p = 0; p < Count; p++)
        {
            var segment = toolpath.Segments[Segments[p]];
            if (segment.Layer >= layerLow && segment.Layer < layerHigh && shows(segment.Feature)) shown.Add(p);
        }
        return [.. shown];
    }

    /// <summary>
    /// Of the <paramref name="shown"/> pieces, the one a ray meets first, as drawn at <paramref name="ends"/>
    /// (<see cref="Ends"/>, or the deformed shape); −1 if it passes them all. A ray meets a piece if, where
    /// it comes closest, it is within the bead's cross-section, taken as an ellipse of its width and height:
    /// beads of neighbouring layers lie only a bead's height apart.
    /// </summary>
    public int Pick(Toolpath toolpath, Vector3[] ends, ReadOnlySpan<int> shown, Vector3 origin, Vector3 direction)
    {
        direction = Vector3.Normalize(direction);
        var (best, nearest) = (-1, float.PositiveInfinity);
        foreach (var p in shown)
        {
            var (a, b) = (ends[2 * p], ends[2 * p + 1]);
            if (Approach(a, b - a, origin, direction) is not var (along, offset) || along >= nearest) continue;
            var segment = toolpath.Segments[Segments[p]];
            // Past an end of the piece the ray is on the next piece, unless the bead ends there and its plastic reaches on.
            var way = Vector3.Normalize(b - a);
            var beyond = Vector3.Dot(offset, way);
            var reach = (Tips[p] & (beyond > 0 ? EndTip : StartTip)) != 0 ? segment.Width / 2 : 0;
            if (MathF.Abs(beyond) > reach + 1e-4f) continue;
            offset -= beyond * way;
            var across = new Vector2(offset.X, offset.Y).Length() / (segment.Width / 2);
            var up = offset.Z / (segment.Height / 2);
            if (across * across + up * up <= 1) (best, nearest) = (p, along);
        }
        return best;
    }

    /// <summary>
    /// Of the <paramref name="shown"/> pieces in a cell, the one whose cell has the largest <paramref name="measure"/>
    /// (a failure index, a stress); −1 if none of them is in a cell.
    /// </summary>
    public int Highest(ReadOnlySpan<float> measure, ReadOnlySpan<int> shown)
    {
        var best = -1;
        foreach (var p in shown)
            if (Cells[p] >= 0 && (best < 0 || measure[Cells[p]] > measure[Cells[best]])) best = p;
        return best;
    }

    /// <summary>The middle of a piece as drawn at <paramref name="ends"/>.</summary>
    public static Vector3 Middle(Vector3[] ends, int piece) => (ends[2 * piece] + ends[2 * piece + 1]) / 2;

    /// <summary>The nozzle's path of a segment, moved down to the middle of the bead: the G-code's Z is the nozzle, at the top of the bead.</summary>
    static (Vector3 Start, Vector3 End) CentreLine(in ExtrusionSegment segment, Placement placement)
    {
        var down = new Vector3(0, 0, segment.Height / 2);
        return (placement.BedToPrint(segment.Start) - down, placement.BedToPrint(segment.End) - down);
    }

    /// <summary>Trilinear between the corners of cell <paramref name="e"/>, at <paramref name="at"/> (clamped into the cell).</summary>
    static Vector3 DisplacementIn(FemMesh mesh, Vector3[] nodeDisplacements, int e, Vector3 at)
    {
        var grid = mesh.Grid;
        var c = mesh.Cells[e];
        var low = grid.NodePosition(c.I, c.J, c.K);
        var u = Math.Clamp((at.X - low.X) / grid.Pitch, 0, 1);
        var v = Math.Clamp((at.Y - low.Y) / grid.Pitch, 0, 1);
        var w = Math.Clamp((at.Z - low.Z) / grid.CellHeight(c.K), 0, 1);
        var sum = Vector3.Zero;
        for (var a = 0; a < HexElement.Nodes; a++)
        {
            var weight = (HexElement.CornerX[a] == 1 ? u : 1 - u) * (HexElement.CornerY[a] == 1 ? v : 1 - v) * (HexElement.CornerZ[a] == 1 ? w : 1 - w);
            sum += weight * nodeDisplacements[mesh.CellNodes[8 * e + a]];
        }
        return sum;
    }

    /// <summary>
    /// How far along a ray (unit <paramref name="direction"/>, from <paramref name="origin"/>) it comes
    /// closest to the stretch from <paramref name="start"/> along <paramref name="span"/>, and the way from
    /// the stretch to the ray there; null if that point lies behind the origin.
    /// </summary>
    static (float Along, Vector3 Offset)? Approach(Vector3 start, Vector3 span, Vector3 origin, Vector3 direction)
    {
        var length = span.LengthSquared();
        var toStart = start - origin;
        float s;
        if (length < 1e-12f) s = 0;
        else
        {
            var cross = Vector3.Dot(span, direction);
            var denominator = length - cross * cross;
            s = denominator < 1e-9f * length
                ? 0 // parallel to the ray
                : (cross * Vector3.Dot(direction, toStart) - Vector3.Dot(span, toStart)) / denominator;
            s = Math.Clamp(s, 0, 1);
        }
        var along = Vector3.Dot(direction, toStart + s * span);
        // With the point on the ray fixed, the nearest point of the stretch to it.
        if (length >= 1e-12f) s = Math.Clamp(Vector3.Dot(origin + along * direction - start, span) / length, 0, 1);
        var point = start + s * span;
        along = Vector3.Dot(direction, point - origin);
        if (along < 0) return null;
        return (along, origin + along * direction - point);
    }
}
