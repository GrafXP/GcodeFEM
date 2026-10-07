using System.Numerics;
using System.Runtime.InteropServices;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Slicing;
using GcodeFem.Core.Visual;
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;

namespace GcodeFem.App;

public enum ViewMode { Model, Toolpaths, Cells, Results }

public enum ResultQuantity { VonMises, Displacement, InterlayerStress, ElementSize }

/// <summary>What the viewport should show; a snapshot of the view settings taken when a redraw starts.</summary>
/// <param name="LayerLow">First layer shown, and <paramref name="LayerHigh"/> the one after the last.</param>
/// <param name="HiddenGroups">Bit per feature group of <see cref="ToolpathColours.Group"/> that is switched off.</param>
/// <param name="ThinLines">Draw every bead as a plain line instead of a tube of its real size: far less to render.</param>
/// <param name="ScaleTop">The colour scale ends at this fraction of the largest value, so a single sharp peak does not darken everything else.</param>
/// <param name="Deformation">Factor on the displacements for the deformed shape; 0 draws the part as printed.</param>
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
    bool ShowElements);

/// <summary>One sliced orientation of the part with everything derived from it. The next slice replaces it as a whole.</summary>
sealed class PrintJob
{
    public const float MinFill = 0.05f;

    public required SliceResult Slice { get; init; }
    public required Toolpath Toolpath { get; init; }
    public required CellGrid Grid { get; init; }

    /// <summary>The cells holding enough plastic to count, whether or not a load case reaches them.</summary>
    public required CellIndex[] Cells { get; init; }

    public SolvedJob? Solved { get; set; }

    // Built on first use by the scene builder, which runs one build at a time.
    public ToolpathGeometry? Beads;
    public readonly Dictionary<ToolpathColouring, (Vector4[] BySegment, Legend Legend)> BeadColours = [];
    public (object Cells, CellRange Range, CellSurface Surface)? Surface;
}

/// <summary>A solve of a <see cref="PrintJob"/> and the fields the viewer colours and deforms with.</summary>
sealed class SolvedJob
{
    public SolvedJob(AdaptiveResult result)
    {
        Result = result;
        NodeDisplacements = ResultFields.NodeDisplacements(result);
        Displacement = ResultFields.DisplacementMagnitude(result.Mesh, NodeDisplacements);
        InterlayerStress = ResultFields.InterlayerStress(result);
        MaxDisplacement = NodeDisplacements.Max(u => u.Length());
        Diagonal = result.Mesh.Grid.OccupiedBounds(PrintJob.MinFill).Size.Length();
    }

    public AdaptiveResult Result { get; }
    public Vector3[] NodeDisplacements { get; }
    public float[] Displacement { get; }
    public float[] InterlayerStress { get; }
    public float MaxDisplacement { get; }

    /// <summary>Size of the printed part, corner to corner (mm).</summary>
    public float Diagonal { get; }
}

/// <summary>What one redraw produced; the parts that are not in the current view stay null.</summary>
sealed record Scene(
    MeshGeometry3D? Model = null,
    MeshGeometry3D? Beads = null,
    LineGeometry3D? BeadLines = null,
    MeshGeometry3D? Cells = null,
    LineGeometry3D? Elements = null,
    LineGeometry3D? Bed = null,
    Legend? Legend = null,
    string Caption = "");

/// <summary>Turns the current view settings into Helix geometry. Runs off the UI thread, one build at a time.</summary>
static class SceneBuilder
{
    /// <param name="model">The part turned as the rotation fields say, for the model view.</param>
    public static Scene Build(ViewState state, TriangleMesh? model, PrintJob? job)
    {
        var scene = state.Mode switch
        {
            ViewMode.Toolpaths when job is not null => Toolpaths(state, job),
            ViewMode.Cells when job is not null => Cells(state, job),
            ViewMode.Results when job?.Solved is not null => Results(state, job, job.Solved),
            _ => model is null ? new Scene() : new Scene(Model: FlatShaded(model)),
        };
        var shown = scene.Model is not null ? model?.Bounds : job?.Slice.PrintMesh.Bounds;
        return shown is { } bounds ? scene with { Bed = BedGrid(bounds) } : scene;
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
        var scale = ColourScale.Sequential(0, 1);
        var (grid, cells) = (job.Grid, job.Cells);
        var colours = surface.VertexColours(e => scale.Colour(grid.Fill(cells[e].I, cells[e].J, cells[e].K)));
        return new Scene(
            Cells: Mesh(surface.Positions, surface.Normals, surface.Indices, colours),
            Legend: new Legend("Fill: share of the cell holding plastic", "", [], scale),
            Caption: $"{cells.Length:N0} cells of {grid.Pitch:0.##} x {grid.Pitch:0.##} mm, one per layer; {surface.FaceCount:N0} faces drawn");
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

        Legend legend;
        Vector4[] colours;
        switch (state.Quantity)
        {
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

        LineGeometry3D? elements = null;
        if (state.ShowElements)
        {
            Func<int, int, int, Vector3>? move = deformation > 0
                ? (i, j, k) =>
                {
                    var (x, y, z) = result.Octree.DisplacementAt(result.NodeDisplacements, i, j, k);
                    return deformation * new Vector3((float)x, (float)y, (float)z);
                }
                : null;
            var points = OctreeWireframe.Build(mesh, levels, Range(job, state), move);
            if (points.Length > 0) elements = Lines(points);
        }

        return new Scene(
            Cells: Mesh(positions, surface.Normals, surface.Indices, colours),
            Elements: elements,
            Legend: legend,
            Caption: $"pass {pass.Index + 1} of {result.Passes.Count}: {pass.Leaves:N0} elements, {pass.Dofs:N0} unknowns" +
                     (deformation > 0 ? $"; deformation x {deformation:0.#}" : ""));
    }

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

    /// <summary>STL faces are flat, so every triangle gets its own corners and face normal.</summary>
    static MeshGeometry3D FlatShaded(TriangleMesh mesh)
    {
        var positions = new Vector3[mesh.TriangleCount * 3];
        var normals = new Vector3[positions.Length];
        for (var t = 0; t < mesh.TriangleCount; t++)
        {
            (positions[3 * t], positions[3 * t + 1], positions[3 * t + 2]) = mesh.Triangle(t);
            normals[3 * t] = normals[3 * t + 1] = normals[3 * t + 2] = mesh.Normal(t);
        }
        return Mesh(positions, normals, [.. Enumerable.Range(0, positions.Length)], null);
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
