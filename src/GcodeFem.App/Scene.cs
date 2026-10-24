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

public enum ViewMode { Model, Toolpaths, Cells, Results }

public enum ResultQuantity { SafetyFactor, FailureMode, VonMises, Displacement, InterlayerStress, ElementSize }

/// <summary>
/// What the cells view colours by: how full each cell is, which way its lines run, their line
/// type, how well the cell has bonded to the one below and how long after it it was printed, or
/// which interface its faces belong to.
/// </summary>
public enum CellColouring { Fill, Direction, Feature, Bond, Gap, Interfaces }

/// <summary>What the viewport should show; a snapshot of the view settings taken when a redraw starts.</summary>
/// <param name="LayerLow">First layer shown, and <paramref name="LayerHigh"/> the one after the last.</param>
/// <param name="HiddenGroups">Bit per feature group of <see cref="ToolpathColours.Group"/> that is switched off.</param>
/// <param name="ThinLines">Draw every bead as a plain line instead of a tube of its real size: far less to render.</param>
/// <param name="ScaleTop">The colour scale ends at this fraction of the largest value, so a single sharp peak does not darken everything else.</param>
/// <param name="Deformation">Factor on the displacements for the deformed shape; 0 draws the part as printed.</param>
/// <param name="Marks">The interfaces, with their values in the load case shown.</param>
/// <param name="ProbedCell">The cell of the result that was clicked, −1 for none.</param>
sealed record ViewState(
    ViewMode Mode,
    int LayerLow,
    int LayerHigh,
    ToolpathColouring Colouring,
    int HiddenGroups,
    bool ThinLines,
    ResultQuantity Quantity,
    int Pass,
    float ScaleTop,
    float Deformation,
    bool ShowElements,
    CellColouring CellColouring,
    IReadOnlyList<InterfaceMark> Marks,
    int ProbedCell);

/// <summary>One sliced orientation of the part with everything derived from it. The next slice replaces it as a whole.</summary>
sealed class PrintJob
{
    public const float MinFill = 0.05f;

    public required SliceResult Slice { get; init; }
    public required Toolpath Toolpath { get; init; }
    public required CellGrid Grid { get; init; }

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
    /// <param name="loadCase">The name of the load case that was solved.</param>
    /// <param name="reach">What each interface got hold of on the cells.</param>
    /// <param name="material">The material it was solved with, and how the filament came by it.</param>
    /// <param name="bonds">Per cell of the result's mesh: its bond to the cell below.</param>
    /// <param name="strength">What the cells were checked against; null if the material gives no strength, and why in <paramref name="noStrength"/>.</param>
    public SolvedJob(AdaptiveResult result, string loadCase, IReadOnlyList<InterfaceReach> reach, MaterialMatch material, LayerBonds bonds, CellStrength? strength, string? noStrength)
    {
        (LoadCaseName, Reach, Material, Bonds, Strength, NoStrength) = (loadCase, reach, material, bonds, strength, noStrength);
        Result = result;
        NodeDisplacements = ResultFields.NodeDisplacements(result);
        Displacement = ResultFields.DisplacementMagnitude(result.Mesh, NodeDisplacements);
        InterlayerStress = ResultFields.InterlayerStress(result);
        MaxDisplacement = NodeDisplacements.Max(u => u.Length());
        Diagonal = result.Mesh.Grid.OccupiedBounds(PrintJob.MinFill).Size.Length();
    }

    public AdaptiveResult Result { get; }
    public string LoadCaseName { get; }
    public IReadOnlyList<InterfaceReach> Reach { get; }
    public MaterialMatch Material { get; }
    public LayerBonds Bonds { get; }
    public CellStrength? Strength { get; }
    public string? NoStrength { get; }

    /// <summary>Every cell checked for failure in the last pass; null without strengths.</summary>
    public CellFailure? Failure => Result.Failure;

    public Vector3[] NodeDisplacements { get; }
    public float[] Displacement { get; }
    public float[] InterlayerStress { get; }
    public float MaxDisplacement { get; }

    /// <summary>Size of the printed part, corner to corner (mm).</summary>
    public float Diagonal { get; }

    /// <summary>The result's cells, to look up a neighbour.</summary>
    public IReadOnlySet<CellIndex> CellSet => cellSet ??= new HashSet<CellIndex>(Result.Mesh.Cells);

    HashSet<CellIndex>? cellSet;
}

/// <summary>What one redraw produced; the parts that are not in the current view stay null.</summary>
/// <param name="Marks">Lines that point something out: the weakest layer line.</param>
/// <param name="Surface">The cell faces drawn, and <paramref name="SurfacePositions"/> where they were drawn, for a click to find its cell.</param>
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
    Vector3[]? SurfacePositions = null);

/// <summary>Turns the current view settings into Helix geometry. Runs off the UI thread, one build at a time.</summary>
static class SceneBuilder
{
    /// <summary>Cell faces that belong to no interface: darker than the model's grey, so that those which do stand out.</summary>
    static readonly Vector4 Unreached = Palette.Hex("#5a5a57");

    static readonly LegendEntry[] InterfaceEntries = [new(0, "Held: a mount", InterfaceGlyphs.Mount), new(1, "Loaded", InterfaceGlyphs.Load)];

    static readonly LegendEntry NothingBelow = new(0, "Nothing below: no layer line", Unreached);

    /// <param name="model">The part turned as the rotation fields say, for the model view.</param>
    /// <param name="rotation">Part frame → print frame as the rotation fields say; forces are given in the part frame.</param>
    public static Scene Build(ViewState state, TriangleMesh? model, Matrix4x4 rotation, PrintJob? job)
    {
        var scene = state.Mode switch
        {
            ViewMode.Toolpaths when job is not null => Toolpaths(state, job),
            ViewMode.Cells when job is not null => Cells(state, job),
            ViewMode.Results when job?.Solved is not null => Results(state, job, job.Solved),
            _ => model is null ? new Scene() : Model(state, model, rotation),
        };
        var shown = scene.Model is not null ? model?.Bounds : job?.Slice.PrintMesh.Bounds;
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

        var indices = beads.Indices(toolpath, state.LayerLow, state.LayerHigh, feature => (state.HiddenGroups >> ToolpathColours.Group(feature) & 1) == 0);
        var vertexColours = beads.VertexColours(colours.BySegment);
        var shown = indices.Length / beads.IndicesPerBead;
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
        var surface = Surface(job, mesh.Cells, state);

        // Only the last pass kept its displacements, so earlier passes are drawn undeformed.
        var deformation = isFinal ? state.Deformation : 0;
        var positions = deformation > 0 ? surface.Displaced(mesh, solved.NodeDisplacements, deformation) : surface.Positions;
        Func<int, int, int, Vector3>? move = deformation > 0
            ? (i, j, k) =>
            {
                var (x, y, z) = result.Octree.DisplacementAt(result.NodeDisplacements, i, j, k);
                return deformation * new Vector3((float)x, (float)y, (float)z);
            }
            : null;

        Legend legend;
        Vector4[] colours;
        var failure = solved.Failure;
        switch (state.Quantity)
        {
            case ResultQuantity.SafetyFactor when failure is not null:
                var index = pass.FailureIndex ?? failure.Index;
                colours = surface.VertexColours(e => FailureColours.SafetyColour(1.0 / index[e]));
                legend = FailureColours.SafetyLegend();
                break;
            case ResultQuantity.FailureMode when failure is not null:
                // How a cell would break matters near the weakest spot only: the same cells the refinement looks at.
                var limit = failure.Index[failure.Weakest] / NearBreaking;
                colours = surface.VertexColours(e => failure.Index[e] >= limit ? FailureColours.ModeColour(failure.Mode[e]) : FailureColours.FarFromBreaking);
                legend = FailureColours.ModeLegend(Enumerable.Range(0, mesh.Cells.Length).Where(e => failure.Index[e] >= limit).Select(e => failure.Mode[e]), NearBreaking);
                break;
            case ResultQuantity.ElementSize:
                var steps = Palette.Ordinal(result.RootLevel + 1);
                colours = surface.VertexColours(e => steps[levels[e]]);
                legend = new Legend("Element size", "", [.. steps.Select((colour, level) => new LegendEntry(level, ElementName(mesh.Grid, level), colour))]);
                break;
            case ResultQuantity.Displacement:
                (colours, legend) = Scaled(surface, solved.Displacement, ColourScale.Sequential(0, state.ScaleTop * solved.MaxDisplacement), "Displacement", "mm");
                break;
            case ResultQuantity.InterlayerStress:
                var extent = solved.InterlayerStress.Max(MathF.Abs);
                (colours, legend) = Scaled(surface, solved.InterlayerStress, ColourScale.Diverging(state.ScaleTop * extent), "Stress across the layers (+ pulls them apart)", "MPa");
                break;
            default:
                // The last pass's scale for every pass, so stepping through them shows the peak coming out.
                (colours, legend) = Scaled(surface, pass.VonMises ?? result.VonMises, ColourScale.Sequential(0, state.ScaleTop * (float)result.Final.MaxVonMises), "von Mises stress", "MPa");
                break;
        }

        var range = Range(job, state);
        LineGeometry3D? elements = null;
        if (state.ShowElements)
        {
            var points = OctreeWireframe.Build(mesh, levels, range, move);
            if (points.Length > 0) elements = Lines(points);
        }

        // The weakest spot of the pass shown, the weakest layer line, and the cell clicked last.
        var glyphs = new GlyphMesh();
        var marks = new List<Vector3>();
        var weakest = mesh.Cells[pass.PeakCell];
        if (range.Contains(weakest))
        {
            var wayOut = ResultMarks.WayOut(solved.CellSet, weakest);
            var size = new Vector3(mesh.Grid.Pitch, mesh.Grid.Pitch, mesh.Grid.CellHeight(weakest.K));
            ResultMarks.AddPointer(glyphs, ResultMarks.Centre(mesh.Grid, weakest) + wayOut * size / 2 + Moved(pass.PeakCell), wayOut, 0.15f * solved.Diagonal, FailureColours.Marker);
        }
        if (isFinal && failure is not null && mesh.Cells[failure.WeakestLayerLine].K is var line && line >= range.KLow && line < range.KHigh)
            marks.AddRange(ResultMarks.LayerLine(mesh, solved.CellSet, line, move));
        if (state.ProbedCell >= 0 && state.ProbedCell < mesh.Cells.Length && range.Contains(mesh.Cells[state.ProbedCell]))
            marks.AddRange(CellBox(mesh, state.ProbedCell, move));

        return new Scene(
            Cells: Mesh(positions, surface.Normals, surface.Indices, colours),
            Glyphs: glyphs.TriangleCount == 0 ? null : Mesh([.. glyphs.Positions], [.. glyphs.Normals], [.. Enumerable.Range(0, glyphs.Positions.Count)], [.. glyphs.Colours]),
            Elements: elements,
            Marks: marks.Count == 0 ? null : Lines([.. marks]),
            Legend: legend,
            Caption: $"pass {pass.Index + 1} of {result.Passes.Count}: {pass.Leaves:N0} elements, {pass.Dofs:N0} unknowns" +
                     (deformation > 0 ? $"; deformation x {deformation:0.#}" : ""),
            Surface: surface,
            SurfacePositions: positions);

        // How far the middle of a cell moves in the deformed shape: the mean of its corners.
        Vector3 Moved(int cell)
        {
            if (deformation == 0) return Vector3.Zero;
            var sum = Vector3.Zero;
            for (var a = 0; a < HexElement.Nodes; a++) sum += solved.NodeDisplacements[mesh.CellNodes[8 * cell + a]];
            return deformation * sum / HexElement.Nodes;
        }
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

    static (Vector4[] Colours, Legend Legend) Scaled(CellSurface surface, float[] values, ColourScale scale, string title, string unit) =>
        (surface.VertexColours(e => scale.Colour(values[e])), new Legend(title, unit, [], scale));

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
