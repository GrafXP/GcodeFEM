using System.Numerics;
using System.Runtime.InteropServices;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Materials;
using GcodeFem.Core.Slicing;
using GcodeFem.Core.Study;
using GcodeFem.Core.Visual;
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;

namespace GcodeFem.App;

public enum ViewMode { Model, Toolpaths, Cells, Results, Compare }

public enum ResultQuantity { SafetyFactor, FailureMode, VonMises, Displacement, InterlayerStress, ElementSize }

/// <summary>
/// What the cells view colours by: how full each cell is, which way its lines run, their line
/// type, how well the cell has bonded to the one below and how long after it it was printed, or
/// which interface its faces belong to.
/// </summary>
public enum CellColouring { Fill, Direction, Feature, Bond, Gap, Interfaces }

/// <summary>What a result is drawn on: the bead cells, or the print's lines, each piece of a line in the colour of its cell.</summary>
public enum ResultDrawing { Cells, Lines }

/// <summary>What the viewport should show; a snapshot of the view settings taken when a redraw starts.</summary>
/// <param name="LayerLow">First layer shown, and <paramref name="LayerHigh"/> the one after the last.</param>
/// <param name="HiddenGroups">Bit per feature group of <see cref="ToolpathColours.Group"/> that is switched off.</param>
/// <param name="ThinLines">Draw every bead as a plain line instead of a tube of its real size: far less to render.</param>
/// <param name="ScaleTop">The colour scale ends at this fraction of the largest value, so a single sharp peak does not darken everything else.</param>
/// <param name="Deformation">Factor on the displacements for the deformed shape; 0 draws the part as printed.</param>
/// <param name="Marks">The interfaces, with their values in the load case shown.</param>
/// <param name="ProbedCell">The cell of the result that was clicked, −1 for none.</param>
/// <param name="Copies">The orientations of the compare view, and of them <paramref name="SelectedCopy"/> the one selected, −1 for none.</param>
sealed record ViewState(
    ViewMode Mode,
    int LayerLow,
    int LayerHigh,
    ToolpathColouring Colouring,
    int HiddenGroups,
    bool ThinLines,
    ResultQuantity Quantity,
    ResultDrawing Drawing,
    int Pass,
    float ScaleTop,
    float Deformation,
    bool ShowElements,
    CellColouring CellColouring,
    IReadOnlyList<InterfaceMark> Marks,
    int ProbedCell,
    IReadOnlyList<CompareCopy> Copies,
    int SelectedCopy)
{
    /// <summary>Whether lines of this feature are drawn: its group is not switched off.</summary>
    public bool Shows(FeatureType feature) => (HiddenGroups >> ToolpathColours.Group(feature) & 1) == 0;
}

/// <summary>One sliced orientation of the part with everything derived from it. The next slice replaces it as a whole.</summary>
sealed class PrintJob
{
    public const float MinFill = PrintedPart.MinFill;

    public required PrintedPart Part { get; init; }

    public SliceResult Slice => Part.Slice;
    public Toolpath Toolpath => Part.Toolpath;

    /// <summary>When each line was printed, on the slicer's times: the clock the cells were filled by.</summary>
    public PrintClock Clock => Part.Clock;

    public CellGrid Grid => Part.Grid;

    /// <summary>The cells holding enough plastic to count, whether or not a load case reaches them.</summary>
    public required CellIndex[] Cells { get; init; }

    /// <summary>Per cell of <see cref="Cells"/>: its bond to the cell below, against the specimens of its material's data sheet.</summary>
    public LayerBonds? Bonds { get; set; }

    public SolvedJob? Solved { get; set; }

    // Built on first use by the scene builder, which runs one build at a time.
    public ToolpathGeometry? Beads;
    public readonly Dictionary<ToolpathColouring, (Vector4[] BySegment, Legend Legend)> BeadColours = [];
    public (object Cells, CellRange Range, CellSurface Surface)? Surface;
}

/// <summary>A solve of a <see cref="PrintJob"/> and the fields the viewer colours and deforms with.</summary>
sealed class SolvedJob
{
    public SolvedJob(PartSolution solution)
    {
        Solution = solution;
        Displacement = ResultFields.DisplacementMagnitude(Result.Mesh, NodeDisplacements);
        InterlayerStress = ResultFields.InterlayerStress(Result);
        Diagonal = Result.Mesh.Grid.OccupiedBounds(PrintJob.MinFill).Size.Length();
    }

    public PartSolution Solution { get; }
    public AdaptiveResult Result => Solution.Result;

    /// <summary>The name of the load case that was solved.</summary>
    public string LoadCaseName => Solution.LoadCase;

    /// <summary>What each interface got hold of on the cells.</summary>
    public IReadOnlyList<InterfaceReach> Reach => Solution.Reach;

    /// <summary>The material it was solved with, and how the filament came by it.</summary>
    public MaterialMatch Material => Solution.Material;

    /// <summary>Per cell of the result's mesh: its bond to the cell below.</summary>
    public LayerBonds Bonds => Solution.Bonds;

    /// <summary>What the cells were checked against; null if the material gives no strength, and why in <see cref="NoStrength"/>.</summary>
    public CellStrength? Strength => Solution.Strength;

    public string? NoStrength => Solution.NoStrength;

    /// <summary>Every cell checked for failure in the last pass; null without strengths.</summary>
    public CellFailure? Failure => Result.Failure;

    public Vector3[] NodeDisplacements => Solution.NodeDisplacements;
    public float[] Displacement { get; }
    public float[] InterlayerStress { get; }
    public float MaxDisplacement => Solution.MaxDisplacement;

    /// <summary>Size of the printed part, corner to corner (mm).</summary>
    public float Diagonal { get; }

    /// <summary>The result's cells, to look up a neighbour.</summary>
    public IReadOnlySet<CellIndex> CellSet => cellSet ??= new HashSet<CellIndex>(Result.Mesh.Cells);

    HashSet<CellIndex>? cellSet;

    // Built on first use by the scene builder, which runs one build at a time: the print's lines cut at
    // the result's cells, and drawn as printed; and the faces of its cells, for the compare view.
    public BeadPieces? Pieces;
    public ToolpathGeometry? Beads;
    public CellSurface? Whole;
}

/// <summary>An orientation in the compare view: the part as it would stand, what of it touches the bed, and its result once there is one.</summary>
/// <param name="Label">Its number, as in the table.</param>
/// <param name="Standing">The part turned as it would print, in the print frame (not moved onto the bed); its triangles are numbered as the model's.</param>
/// <param name="Contact">Its triangles that stand on the bed; none for an orientation solved by hand.</param>
/// <param name="Rotation">Part frame → print frame, for the markers of the interfaces.</param>
/// <param name="IsTicked">Whether the sweep is to try it.</param>
/// <param name="Solved">Its result: shown in its place once there is one.</param>
/// <param name="Result">What its label says of the result: the safety factor, and how it was reached.</param>
sealed record CompareCopy(string Label, TriangleMesh Standing, int[] Contact, Matrix4x4 Rotation, bool IsTicked, SolvedJob? Solved, string Result = "");

/// <summary>Where the copies of the compare view stand: each on a patch of bed of its own, in rows, read left to right and back to front.</summary>
static class CompareLayout
{
    /// <summary>Between two patches (mm).</summary>
    const float Gap = 12;

    /// <returns>Per copy the shift that stands it on its patch and the patch; and the box around them all.</returns>
    public static (Vector3[] Shifts, Box3[] Patches, Box3 Extent) Of(IReadOnlyList<TriangleMesh> standing)
    {
        if (standing.Count == 0) return ([], [], new Box3(Vector3.Zero, Vector3.Zero));
        var sizeX = standing.Max(m => m.Bounds.Size.X) + Gap;
        var sizeY = standing.Max(m => m.Bounds.Size.Y) + Gap;
        var columns = (int)Math.Ceiling(Math.Sqrt(standing.Count));
        var rows = (standing.Count + columns - 1) / columns;
        var shifts = new Vector3[standing.Count];
        var patches = new Box3[standing.Count];
        var top = 0f;
        for (var n = 0; n < standing.Count; n++)
        {
            // The first row farthest back, seen from the front as the camera starts.
            var centre = new Vector2(n % columns * sizeX, (rows - 1 - n / columns) * sizeY);
            var bounds = standing[n].Bounds;
            shifts[n] = new Vector3(centre - new Vector2(bounds.Center.X, bounds.Center.Y), -bounds.Min.Z);
            var half = new Vector2(sizeX - Gap / 2, sizeY - Gap / 2) / 2;
            patches[n] = new Box3(new Vector3(centre - half, 0), new Vector3(centre + half, 0));
            top = MathF.Max(top, bounds.Size.Z);
        }
        var low = patches.Aggregate(patches[0].Min, (m, p) => Vector3.Min(m, p.Min));
        var high = patches.Aggregate(patches[0].Max, (m, p) => Vector3.Max(m, p.Max));
        return (shifts, patches, new Box3(low, high with { Z = top }));
    }
}

/// <summary>Pieces of the print's lines as drawn: at <paramref name="Ends"/> (two per piece), and of them only <paramref name="Shown"/>.</summary>
sealed record DrawnLines(BeadPieces Pieces, Vector3[] Ends, int[] Shown);

/// <summary>What one redraw produced; the parts that are not in the current view stay null.</summary>
/// <param name="Marks">Lines that point something out: the weakest layer line.</param>
/// <param name="Surface">The cell faces drawn, and <paramref name="SurfacePositions"/> where they were drawn, for a click to find its cell.</param>
/// <param name="Lines">The pieces of line a result is drawn on, for a click to find its line and cell.</param>
/// <param name="Labels">Text in the scene: the numbers of the compare view's copies.</param>
/// <param name="Contact">Outlines of what stands on the bed, in the compare view: it faces down, out of sight.</param>
/// <param name="Extent">What the bed must reach under, where that is not the model or the print.</param>
sealed record Scene(
    MeshGeometry3D? Model = null,
    MeshGeometry3D? Glyphs = null,
    MeshGeometry3D? Beads = null,
    LineGeometry3D? BeadLines = null,
    MeshGeometry3D? Cells = null,
    LineGeometry3D? Elements = null,
    LineGeometry3D? Marks = null,
    LineGeometry3D? Bed = null,
    Legend? Legend = null,
    string Caption = "",
    CellSurface? Surface = null,
    Vector3[]? SurfacePositions = null,
    DrawnLines? Lines = null,
    BillboardText3D? Labels = null,
    LineGeometry3D? Contact = null,
    Box3? Extent = null);

/// <summary>Turns the current view settings into Helix geometry. Runs off the UI thread, one build at a time.</summary>
static class SceneBuilder
{
    /// <summary>Cell faces that belong to no interface: darker than the model's grey, so that those which do stand out.</summary>
    static readonly Vector4 Unreached = Palette.Hex("#5a5a57");

    static readonly LegendEntry[] InterfaceEntries = [new(0, "Held: a mount", InterfaceGlyphs.Mount), new(1, "Loaded", InterfaceGlyphs.Load)];

    static readonly LegendEntry NothingBelow = new(0, "Nothing below: no layer line", Unreached);

    /// <summary>Lines with no result, in the grey the toolpath view gives what is not part of the model.</summary>
    static readonly Vector4 NotSolved = Palette.Neutral;

    static readonly LegendEntry NotSolvedEntry = new(-1, "Not in the solve: not part of the model, or held by no mount", NotSolved);

    /// <param name="model">The part turned as the rotation fields say, for the model view.</param>
    /// <param name="rotation">Part frame → print frame as the rotation fields say; forces are given in the part frame.</param>
    public static Scene Build(ViewState state, TriangleMesh? model, Matrix4x4 rotation, PrintJob? job)
    {
        var scene = state.Mode switch
        {
            ViewMode.Toolpaths when job is not null => Toolpaths(state, job),
            ViewMode.Cells when job is not null => Cells(state, job),
            ViewMode.Results when job?.Solved is not null => Results(state, job, job.Solved),
            ViewMode.Compare => Compare(state),
            _ => model is null ? new Scene() : Model(state, model, rotation),
        };
        var shown = scene.Extent ?? (scene.Model is not null ? model?.Bounds : job?.Slice.PrintMesh.Bounds);
        return shown is { } bounds ? scene with { Bed = BedGrid(bounds) } : scene;
    }

    /// <summary>The model with the faces of its interfaces tinted, and their markers.</summary>
    static Scene Model(ViewState state, TriangleMesh model, Matrix4x4 rotation)
    {
        var glyphs = InterfaceGlyphs.Build(model, rotation, state.Marks);
        return new Scene(
            Model: FlatShaded(model, InterfaceGlyphs.TriangleColours(model.TriangleCount, state.Marks)),
            Glyphs: glyphs.TriangleCount == 0 ? null : Mesh([.. glyphs.Positions], [.. glyphs.Normals], [.. Enumerable.Range(0, glyphs.Positions.Count)], [.. glyphs.Colours]),
            Legend: state.Marks.Count == 0 ? null : new Legend("Interfaces", "", InterfaceEntries));
    }

    static Scene Toolpaths(ViewState state, PrintJob job)
    {
        var toolpath = job.Toolpath;
        // A toolpath too big for tubes comes out as lines whatever was asked for.
        var thin = state.ThinLines || toolpath.Segments.Length > ToolpathGeometry.MaxTubes;
        if (job.Beads is not { } beads || beads.Tubes == thin) job.Beads = beads = ToolpathGeometry.Build(toolpath, job.Slice.Placement, tubes: !thin);
        if (!job.BeadColours.TryGetValue(state.Colouring, out var colours))
            job.BeadColours[state.Colouring] = colours = ToolpathColours.Colour(toolpath, state.Colouring);

        var indices = beads.Indices(toolpath, state.LayerLow, state.LayerHigh, state.Shows);
        var vertexColours = beads.VertexColours(colours.BySegment);
        var shown = indices.Length / beads.IndicesPerPiece;
        var caption = $"{shown:N0} of {beads.Segments.Length:N0} lines{(beads.Tubes || state.ThinLines ? "" : ", drawn thin: too many for full beads")}";
        return beads.Tubes
            ? new Scene(Beads: Mesh(beads.Positions, beads.Normals!, indices, vertexColours), Legend: colours.Legend, Caption: caption)
            : new Scene(BeadLines: Lines(beads.Positions, indices, vertexColours), Legend: colours.Legend, Caption: caption);
    }

    static Scene Cells(ViewState state, PrintJob job)
    {
        var surface = Surface(job, job.Cells, state);
        var (grid, cells) = (job.Grid, job.Cells);
        var caption = $"{cells.Length:N0} cells of {grid.Pitch:0.##} x {grid.Pitch:0.##} mm, one per layer; {surface.FaceCount:N0} faces drawn";
        if (state.CellColouring == CellColouring.Interfaces)
        {
            // The same test the solver uses, so this is what it will take each interface to be. The one being edited goes first.
            var print = job.Slice.PrintMesh;
            var interfaces = state.Marks.OrderByDescending(mark => mark.IsSelected)
                .Select(mark => (Colour: InterfaceGlyphs.Colour(mark), Covers: InterfaceMapper.Covers(print, [.. mark.Triangles.Where(t => (uint)t < (uint)print.TriangleCount)], grid.Pitch)))
                .ToList();
            var held = surface.FaceColours(f =>
            {
                var (centre, normal) = (surface.FaceCentre(f), surface.FaceNormal(f));
                foreach (var (colour, covers) in interfaces)
                    if (covers(centre, normal)) return colour;
                return Unreached;
            });
            return new Scene(
                Cells: Mesh(surface.Positions, surface.Normals, surface.Indices, held),
                Legend: new Legend("Cell faces the solver takes for each interface", "", InterfaceEntries),
                Caption: caption);
        }

        Vector4[] colours;
        Legend legend;
        switch (state.CellColouring)
        {
            case CellColouring.Direction:
                colours = surface.VertexColours(e => CellColours.DirectionColour(CellColours.DirectionGroup(grid.Direction(cells[e].I, cells[e].J, cells[e].K))));
                legend = CellColours.DirectionLegend();
                break;
            case CellColouring.Feature:
                var features = cells.Select(c => grid.Feature(c.I, c.J, c.K) ?? FeatureClass.Other).ToArray();
                colours = surface.VertexColours(e => CellColours.FeatureColour(features[e]));
                legend = CellColours.FeatureLegend(features);
                break;
            case CellColouring.Bond when job.Bonds is { } bonds:
                // From the weakest bond up to that of the specimens; a cell with nothing below has no layer line.
                var weakest = Enumerable.Range(0, cells.Length).Where(e => !float.IsNaN(bonds.Gap[e])).Select(e => bonds.Factor[e]).DefaultIfEmpty(1).Min();
                var bond = ColourScale.Sequential(MathF.Min(weakest, 0.99f), 1);
                colours = surface.VertexColours(e => float.IsNaN(bonds.Gap[e]) ? Unreached : bond.Colour(bonds.Factor[e]));
                legend = new Legend("Bond to the layer below, of the data sheet's specimens", "", [NothingBelow], bond);
                break;
            case CellColouring.Gap when job.Bonds is { } bonds:
                var longest = Enumerable.Range(0, cells.Length).Where(e => !float.IsNaN(bonds.Gap[e])).Select(e => bonds.Gap[e]).DefaultIfEmpty(1).Max();
                var gap = ColourScale.Sequential(0, longest);
                colours = surface.VertexColours(e => float.IsNaN(bonds.Gap[e]) ? Unreached : gap.Colour(bonds.Gap[e]));
                legend = new Legend("Time since the layer below was printed there", "s", [NothingBelow], gap);
                break;
            default:
                var fill = ColourScale.Sequential(0, 1);
                colours = surface.VertexColours(e => fill.Colour(grid.Fill(cells[e].I, cells[e].J, cells[e].K)));
                legend = new Legend("Fill: share of the cell holding plastic", "", [], fill);
                break;
        }
        return new Scene(Cells: Mesh(surface.Positions, surface.Normals, surface.Indices, colours), Legend: legend, Caption: caption);
    }

    static Scene Results(ViewState state, PrintJob job, SolvedJob solved)
    {
        var result = solved.Result;
        var mesh = result.Mesh;
        var pass = result.Passes[Math.Clamp(state.Pass, 0, result.Passes.Count - 1)];
        var isFinal = ReferenceEquals(pass, result.Final);
        var levels = pass.CellLevels ?? result.Octree.CellLevels();
        var failure = solved.Failure;

        // Only the last pass kept its displacements, so earlier passes are drawn undeformed.
        var deformation = isFinal ? state.Deformation : 0;
        Func<int, int, int, Vector3>? move = deformation > 0
            ? (i, j, k) =>
            {
                var (x, y, z) = result.Octree.DisplacementAt(result.NodeDisplacements, i, j, k);
                return deformation * new Vector3((float)x, (float)y, (float)z);
            }
            : null;

        var (colour, legend) = CellColour(state, solved, pass, levels);
        var caption = $"pass {pass.Index + 1} of {result.Passes.Count}: {pass.Leaves:N0} elements, {pass.Dofs:N0} unknowns" +
                      (deformation > 0 ? $"; deformation x {deformation:0.#}" : "");
        var range = Range(job, state);
        var glyphs = new GlyphMesh();
        Scene scene;
        if (state.Drawing == ResultDrawing.Lines)
        {
            var toolpath = job.Toolpath;
            var pieces = solved.Pieces ??= BeadPieces.Cut(toolpath, job.Slice.Placement, mesh);
            // Too many pieces for tubes come out as lines whatever was asked for, as in the toolpath view.
            var thin = state.ThinLines || pieces.Count > ToolpathGeometry.MaxTubes;
            var ends = deformation > 0 ? pieces.Displaced(mesh, solved.NodeDisplacements, deformation) : pieces.Ends;
            ToolpathGeometry beads;
            if (deformation > 0) beads = ToolpathGeometry.Build(toolpath, pieces, !thin, ends);
            else if (solved.Beads is { } built && built.Tubes != thin) beads = built;
            else solved.Beads = beads = ToolpathGeometry.Build(toolpath, pieces, !thin);

            var shown = pieces.Shown(toolpath, state.LayerLow, state.LayerHigh, state.Shows);
            var colours = beads.VertexColours(p => pieces.Cells[p] >= 0 ? colour(pieces.Cells[p]) : NotSolved);
            if (shown.Any(p => pieces.Cells[p] < 0)) legend = legend with { Entries = [.. legend.Entries, NotSolvedEntry] };
            var indices = beads.Indices(shown);
            caption += $"; {shown.Length:N0} of {pieces.Count:N0} pieces of line, one per cell{(beads.Tubes || state.ThinLines ? "" : ", drawn thin: too many for full beads")}";

            // The weakest of the lines shown: with some line types switched off, the weakest of the rest.
            var weakest = pieces.Highest(Governing(solved, pass), shown);
            if (weakest >= 0)
            {
                var segment = toolpath.Segments[pieces.Segments[weakest]];
                var wayOut = ResultMarks.WayOut(solved.CellSet, mesh.Cells[pieces.Cells[weakest]]);
                var surface = wayOut.Z != 0 ? segment.Height / 2 : segment.Width / 2;
                ResultMarks.AddPointer(glyphs, BeadPieces.Middle(ends, weakest) + wayOut * surface, wayOut, 0.15f * solved.Diagonal, FailureColours.Marker);
                caption += $"; weakest line shown: {segment.Feature.DisplayName().ToLowerInvariant()} in layer {segment.Layer + 1}, {GoverningText(solved, pass, pieces.Cells[weakest])}";
            }
            scene = (beads.Tubes
                ? new Scene(Beads: Mesh(beads.Positions, beads.Normals!, indices, colours))
                : new Scene(BeadLines: Lines(beads.Positions, indices, colours))) with { Lines = new DrawnLines(pieces, ends, shown) };
        }
        else
        {
            var surface = Surface(job, mesh.Cells, state);
            var positions = deformation > 0 ? surface.Displaced(mesh, solved.NodeDisplacements, deformation) : surface.Positions;
            // The weakest spot of the pass shown.
            var weakest = mesh.Cells[pass.PeakCell];
            if (range.Contains(weakest))
            {
                var wayOut = ResultMarks.WayOut(solved.CellSet, weakest);
                var size = new Vector3(mesh.Grid.Pitch, mesh.Grid.Pitch, mesh.Grid.CellHeight(weakest.K));
                ResultMarks.AddPointer(glyphs, ResultMarks.Centre(mesh.Grid, weakest) + wayOut * size / 2 + Moved(pass.PeakCell), wayOut, 0.15f * solved.Diagonal, FailureColours.Marker);
            }
            scene = new Scene(Cells: Mesh(positions, surface.Normals, surface.Indices, surface.VertexColours(colour)), Surface: surface, SurfacePositions: positions);
        }

        // The outlines lie on cell faces, which the beads do not fill: on the lines they would show between them as dots.
        LineGeometry3D? elements = null;
        if (state.ShowElements && state.Drawing == ResultDrawing.Cells)
        {
            var points = OctreeWireframe.Build(mesh, levels, range, move);
            if (points.Length > 0) elements = Lines(points);
        }

        // The weakest layer line, and the cell clicked last.
        var marks = new List<Vector3>();
        if (isFinal && failure is not null && mesh.Cells[failure.WeakestLayerLine].K is var line && line >= range.KLow && line < range.KHigh)
            marks.AddRange(ResultMarks.LayerLine(mesh, solved.CellSet, line, move));
        if (state.ProbedCell >= 0 && state.ProbedCell < mesh.Cells.Length && range.Contains(mesh.Cells[state.ProbedCell]))
            marks.AddRange(CellBox(mesh, state.ProbedCell, move));

        return scene with
        {
            Glyphs = glyphs.TriangleCount == 0 ? null : Mesh([.. glyphs.Positions], [.. glyphs.Normals], [.. Enumerable.Range(0, glyphs.Positions.Count)], [.. glyphs.Colours]),
            Elements = elements,
            Marks = marks.Count == 0 ? null : Lines([.. marks]),
            Legend = legend,
            Caption = caption,
        };

        // How far the middle of a cell moves in the deformed shape: the mean of its corners.
        Vector3 Moved(int cell)
        {
            if (deformation == 0) return Vector3.Zero;
            var sum = Vector3.Zero;
            for (var a = 0; a < HexElement.Nodes; a++) sum += solved.NodeDisplacements[mesh.CellNodes[8 * cell + a]];
            return deformation * sum / HexElement.Nodes;
        }
    }

    /// <summary>The colour of each cell of the result in the quantity shown, and the legend that explains it.</summary>
    static (Func<int, Vector4> Colour, Legend Legend) CellColour(ViewState state, SolvedJob solved, AdaptivePass pass, byte[] levels)
    {
        var result = solved.Result;
        var mesh = result.Mesh;
        var failure = solved.Failure;
        switch (state.Quantity)
        {
            case ResultQuantity.SafetyFactor when failure is not null:
                var index = pass.FailureIndex ?? failure.Index;
                return (e => FailureColours.SafetyColour(1.0 / index[e]), FailureColours.SafetyLegend());
            case ResultQuantity.FailureMode when failure is not null:
                // How a cell would break matters near the weakest spot only: the same cells the refinement looks at.
                var limit = failure.Index[failure.Weakest] / NearBreaking;
                return (e => failure.Index[e] >= limit ? FailureColours.ModeColour(failure.Mode[e]) : FailureColours.FarFromBreaking,
                        FailureColours.ModeLegend(Enumerable.Range(0, mesh.Cells.Length).Where(e => failure.Index[e] >= limit).Select(e => failure.Mode[e]), NearBreaking));
            case ResultQuantity.ElementSize:
                var steps = Palette.Ordinal(result.RootLevel + 1);
                return (e => steps[levels[e]], new Legend("Element size", "", [.. steps.Select((colour, level) => new LegendEntry(level, ElementName(mesh.Grid, level), colour))]));
            case ResultQuantity.Displacement:
                return Scaled(solved.Displacement, ColourScale.Sequential(0, state.ScaleTop * solved.MaxDisplacement), "Displacement", "mm");
            case ResultQuantity.InterlayerStress:
                var extent = solved.InterlayerStress.Max(MathF.Abs);
                return Scaled(solved.InterlayerStress, ColourScale.Diverging(state.ScaleTop * extent), "Stress across the layers (+ pulls them apart)", "MPa");
            default:
                // The last pass's scale for every pass, so stepping through them shows the peak coming out.
                return Scaled(pass.VonMises ?? result.VonMises, ColourScale.Sequential(0, state.ScaleTop * (float)result.Final.MaxVonMises), "von Mises stress", "MPa");
        }
    }

    /// <summary>What the refinement of a pass went by, per cell: the failure index where there is a check, else the von Mises stress.</summary>
    static float[] Governing(SolvedJob solved, AdaptivePass pass) =>
        solved.Failure is { } failure ? pass.FailureIndex ?? failure.Index : pass.VonMises ?? solved.Result.VonMises;

    static string GoverningText(SolvedJob solved, AdaptivePass pass, int cell) =>
        solved.Failure is not null ? $"safety factor {1 / Governing(solved, pass)[cell]:0.00}" : $"von Mises {Governing(solved, pass)[cell]:0.00} MPa";

    /// <summary>What stands on the bed, in the compare view.</summary>
    static readonly Vector4 ContactColour = Palette.Categorical[2];

    /// <summary>
    /// The orientations side by side, each copy on its own patch of bed with its number: before its
    /// result the part as it would stand, with what touches the bed and its interfaces tinted (dimmed
    /// if left out of the sweep); with a result, its cells coloured by safety factor and an arrow at its
    /// weakest spot. The selected one's patch is outlined in white.
    /// </summary>
    static Scene Compare(ViewState state)
    {
        var copies = state.Copies;
        if (copies.Count == 0) return new Scene(Caption: "no orientations to compare yet");
        var (shifts, patches, extent) = CompareLayout.Of([.. copies.Select(c => c.Standing)]);
        var (models, cells) = (new FlatMesh(), new FlatMesh());
        var glyphs = new GlyphMesh();
        var labels = new BillboardText3D();
        var (outlines, selected, contact) = (new List<Vector3>(), new List<Vector3>(), new List<Vector3>());
        // Without strengths, one stress scale for all, so that the copies can be held against each other.
        var stressTop = copies.Where(c => c.Solved is { Failure: null }).Select(c => (float)c.Solved!.Result.Final.MaxVonMises).DefaultIfEmpty(1).Max();
        var stress = ColourScale.Sequential(0, stressTop);

        for (var n = 0; n < copies.Count; n++)
        {
            var (copy, shift) = (copies[n], shifts[n]);
            string label;
            if (copy.Solved is { } solved)
            {
                var mesh = solved.Result.Mesh;
                var surface = solved.Whole ??= CellSurface.Build(mesh.Grid, mesh.Cells, CellRange.All(mesh.Grid));
                var failure = solved.Failure;
                var colours = surface.VertexColours(e => failure is null ? stress.Colour(solved.Result.VonMises[e]) : FailureColours.SafetyColour(1.0 / failure.Index[e]));
                cells.Add(surface.Positions, surface.Normals, surface.Indices, colours, shift);
                var weakest = mesh.Cells[solved.Result.Final.PeakCell];
                var wayOut = ResultMarks.WayOut(solved.CellSet, weakest);
                var size = new Vector3(mesh.Grid.Pitch, mesh.Grid.Pitch, mesh.Grid.CellHeight(weakest.K));
                ResultMarks.AddPointer(glyphs, ResultMarks.Centre(mesh.Grid, weakest) + wayOut * size / 2 + shift, wayOut, 0.15f * solved.Diagonal, FailureColours.Marker);
                label = copy.Result.Length > 0 ? $"{copy.Label}: {copy.Result}" : copy.Label;
            }
            else
            {
                var colours = InterfaceGlyphs.TriangleColours(copy.Standing.TriangleCount, state.Marks);
                foreach (var t in copy.Contact) colours[t] = ContactColour;
                if (!copy.IsTicked)
                    for (var t = 0; t < colours.Length; t++) colours[t] = new Vector4(0.35f * new Vector3(colours[t].X, colours[t].Y, colours[t].Z), 1);
                models.Add(copy.Standing, colours, shift);
                if (copy.IsTicked) glyphs.Append(InterfaceGlyphs.Build(copy.Standing, copy.Rotation, state.Marks), shift);
                label = copy.IsTicked ? copy.Label : $"{copy.Label}: left out";
            }

            var patch = patches[n];
            // On the bed at the front of its patch, where no copy stands in the way.
            labels.TextInfo.Add(new TextInfo(label, new Vector3(patch.Center.X, patch.Min.Y + 3, 0)) { Foreground = new Color4(1, 1, 1, 1), Background = new Color4(0.1f, 0.1f, 0.1f, 0.85f), Scale = 0.6f });
            if (copy.Solved is null && copy.IsTicked) contact.AddRange(ContactOutline(copy.Standing, copy.Contact).Select(p => p + shift));
            Vector3[] corners = [patch.Min, patch.Min with { X = patch.Max.X }, patch.Max, patch.Min with { Y = patch.Max.Y }];
            for (var c = 0; c < 4; c++) (n == state.SelectedCopy ? selected : outlines).AddRange([corners[c], corners[(c + 1) % 4]]);
        }

        var solvedCount = copies.Count(c => c.Solved is not null);
        var legend = solvedCount == 0
            ? new Legend("Orientations", "", [new(0, "Stands on the bed", ContactColour), .. state.Marks.Count == 0 ? [] : InterfaceEntries])
            : copies.Any(c => c.Solved?.Failure is not null)
                ? FailureColours.SafetyLegend() with { Entries = [.. FailureColours.SafetyLegend().Entries, .. solvedCount < copies.Count ? [new LegendEntry(9, "Not solved: stands on the bed", ContactColour)] : Array.Empty<LegendEntry>()] }
                : new Legend("von Mises stress", "MPa", [], stress);
        return new Scene(
            Model: models.IsEmpty ? null : models.Geometry(),
            Cells: cells.IsEmpty ? null : cells.Geometry(),
            Glyphs: glyphs.TriangleCount == 0 ? null : Mesh([.. glyphs.Positions], [.. glyphs.Normals], [.. Enumerable.Range(0, glyphs.Positions.Count)], [.. glyphs.Colours]),
            Elements: outlines.Count == 0 ? null : Lines([.. outlines]),
            Marks: selected.Count == 0 ? null : Lines([.. selected]),
            Contact: contact.Count == 0 ? null : Lines([.. contact]),
            Legend: legend,
            Caption: $"{copies.Count} orientations, {copies.Count(c => c.IsTicked)} ticked for the sweep" + (solvedCount > 0 ? $", {solvedCount} with a result" : ""),
            Labels: labels,
            Extent: extent);
    }

    /// <summary>The rim of the contact: the edges of its triangles that no other of them shares, as point pairs.</summary>
    static IEnumerable<Vector3> ContactOutline(TriangleMesh mesh, int[] contact)
    {
        var edges = new HashSet<(int, int)>();
        foreach (var t in contact)
            for (var c = 0; c < 3; c++)
                edges.Add((mesh.Indices[3 * t + c], mesh.Indices[3 * t + (c + 1) % 3]));
        foreach (var (a, b) in edges)
            if (!edges.Contains((b, a)))
            {
                yield return mesh.Positions[a];
                yield return mesh.Positions[b];
            }
    }

    /// <summary>Meshes gathered into one, each moved by a shift.</summary>
    sealed class FlatMesh
    {
        readonly List<Vector3> positions = [], normals = [];
        readonly List<Vector4> colours = [];
        readonly List<int> indices = [];

        public bool IsEmpty => indices.Count == 0;

        /// <summary>A model with a colour per triangle, flat shaded.</summary>
        public void Add(TriangleMesh mesh, Vector4[] triangleColours, Vector3 shift)
        {
            for (var t = 0; t < mesh.TriangleCount; t++)
            {
                var (a, b, c) = mesh.Triangle(t);
                var normal = mesh.Normal(t);
                indices.AddRange([positions.Count, positions.Count + 1, positions.Count + 2]);
                positions.AddRange([a + shift, b + shift, c + shift]);
                normals.AddRange([normal, normal, normal]);
                colours.AddRange([triangleColours[t], triangleColours[t], triangleColours[t]]);
            }
        }

        public void Add(Vector3[] meshPositions, Vector3[] meshNormals, int[] meshIndices, Vector4[] vertexColours, Vector3 shift)
        {
            var first = positions.Count;
            positions.AddRange(meshPositions.Select(p => p + shift));
            normals.AddRange(meshNormals);
            colours.AddRange(vertexColours);
            indices.AddRange(meshIndices.Select(i => i + first));
        }

        public MeshGeometry3D Geometry() => Mesh([.. positions], [.. normals], [.. indices], [.. colours]);
    }

    /// <summary>A cell's twelve edges, as point pairs.</summary>
    static IEnumerable<Vector3> CellBox(FemMesh mesh, int cell, Func<int, int, int, Vector3>? move)
    {
        var c = mesh.Cells[cell];
        Vector3 Corner(int a)
        {
            int i = c.I + HexElement.CornerX[a], j = c.J + HexElement.CornerY[a], k = c.K + HexElement.CornerZ[a];
            var position = mesh.Grid.NodePosition(i, j, k);
            return move is null ? position : position + move(i, j, k);
        }
        for (var a = 0; a < HexElement.Nodes; a++)
        for (var b = a + 1; b < HexElement.Nodes; b++)
        {
            // Corners one step apart along a single axis share an edge.
            var apart = Math.Abs(HexElement.CornerX[a] - HexElement.CornerX[b]) + Math.Abs(HexElement.CornerY[a] - HexElement.CornerY[b]) + Math.Abs(HexElement.CornerZ[a] - HexElement.CornerZ[b]);
            if (apart != 1) continue;
            yield return Corner(a);
            yield return Corner(b);
        }
    }

    /// <summary>The mode view shows how cells would break up to this factor × the lowest safety factor, as far as the refinement looks.</summary>
    const double NearBreaking = 2;

    static (Func<int, Vector4> Colour, Legend Legend) Scaled(float[] values, ColourScale scale, string title, string unit) =>
        (e => scale.Colour(values[e]), new Legend(title, unit, [], scale));

    static string ElementName(CellGrid grid, int level)
    {
        var side = (1 << level) * grid.Pitch;
        return level == 0 ? $"1 bead cell, {side:0.##} mm" : $"{1 << level} x {1 << level} x {1 << level} cells, {side:0.#} mm";
    }

    static CellRange Range(PrintJob job, ViewState state) => CellRange.Layers(job.Grid, state.LayerLow, state.LayerHigh);

    /// <summary>The visible faces of <paramref name="cells"/>; kept until the cells or the layer range change.</summary>
    static CellSurface Surface(PrintJob job, CellIndex[] cells, ViewState state)
    {
        var range = Range(job, state);
        if (job.Surface is { } cached && ReferenceEquals(cached.Cells, cells) && cached.Range == range) return cached.Surface;
        var surface = CellSurface.Build(job.Grid, cells, range);
        job.Surface = (cells, range, surface);
        return surface;
    }

    /// <summary>STL faces are flat, so every triangle gets its own corners, its face normal and its own colour.</summary>
    static MeshGeometry3D FlatShaded(TriangleMesh mesh, Vector4[] triangleColours)
    {
        var positions = new Vector3[mesh.TriangleCount * 3];
        var normals = new Vector3[positions.Length];
        var colours = new Vector4[positions.Length];
        for (var t = 0; t < mesh.TriangleCount; t++)
        {
            (positions[3 * t], positions[3 * t + 1], positions[3 * t + 2]) = mesh.Triangle(t);
            normals[3 * t] = normals[3 * t + 1] = normals[3 * t + 2] = mesh.Normal(t);
            colours[3 * t] = colours[3 * t + 1] = colours[3 * t + 2] = triangleColours[t];
        }
        return Mesh(positions, normals, [.. Enumerable.Range(0, positions.Length)], colours);
    }

    /// <summary>A grid of lines under the part, 10 mm apart, standing in for the bed.</summary>
    static LineGeometry3D BedGrid(Box3 bounds)
    {
        const float step = 10;
        float x0 = MathF.Floor(bounds.Min.X / step - 1) * step, x1 = MathF.Ceiling(bounds.Max.X / step + 1) * step;
        float y0 = MathF.Floor(bounds.Min.Y / step - 1) * step, y1 = MathF.Ceiling(bounds.Max.Y / step + 1) * step;
        var points = new List<Vector3>();
        for (var x = x0; x <= x1 + 1e-3f; x += step) points.AddRange([new Vector3(x, y0, bounds.Min.Z), new Vector3(x, y1, bounds.Min.Z)]);
        for (var y = y0; y <= y1 + 1e-3f; y += step) points.AddRange([new Vector3(x0, y, bounds.Min.Z), new Vector3(x1, y, bounds.Min.Z)]);
        return Lines([.. points]);
    }

    static MeshGeometry3D Mesh(Vector3[] positions, Vector3[] normals, int[] indices, Vector4[]? colours) => new()
    {
        Positions = Fill(new Vector3Collection(positions.Length), positions),
        Normals = Fill(new Vector3Collection(normals.Length), normals),
        Indices = Fill(new IntCollection(indices.Length), indices),
        Colors = colours is null ? null : Fill(new Color4Collection(colours.Length), MemoryMarshal.Cast<Vector4, Color4>(colours)),
    };

    /// <summary>Point pairs; without <paramref name="indices"/> every two points are one line.</summary>
    static LineGeometry3D Lines(Vector3[] points, int[]? indices = null, Vector4[]? colours = null) => new()
    {
        Positions = Fill(new Vector3Collection(points.Length), points),
        Indices = Fill(new IntCollection(indices?.Length ?? points.Length), indices ?? [.. Enumerable.Range(0, points.Length)]),
        Colors = colours is null ? null : Fill(new Color4Collection(colours.Length), MemoryMarshal.Cast<Vector4, Color4>(colours)),
    };

    static TList Fill<TList, T>(TList list, ReadOnlySpan<T> items) where TList : FastList<T>
    {
        list.Resize(items.Length, true);
        items.CopyTo(list.GetInternalArray());
        return list;
    }
}
