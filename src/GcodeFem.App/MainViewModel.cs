using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Slicing;
using GcodeFem.Core.Study;
using GcodeFem.Core.Visual;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Color = System.Windows.Media.Color;
using PerspectiveCamera = HelixToolkit.Wpf.SharpDX.PerspectiveCamera;
using Point3D = System.Windows.Media.Media3D.Point3D;
using Vector3D = System.Windows.Media.Media3D.Vector3D;

namespace GcodeFem.App;

/// <summary>An entry of a drop-down list.</summary>
public sealed record Choice<T>(T Value, string Label);

/// <summary>A legend row; a line type can also be switched off.</summary>
public sealed partial class LegendItem(int key, string label, Vector4 colour) : ObservableObject
{
    public int Key => key;
    public string Label => label;
    public Brush Swatch { get; } = Swatches.Frozen(colour);

    [ObservableProperty]
    public partial bool IsShown { get; set; } = true;
}

/// <summary>A label beside the colour scale, <paramref name="Top"/> pixels down.</summary>
public sealed record LegendTick(string Text, double Top);

/// <summary>A row of the pass table.</summary>
public sealed record PassRow(int Pass, string Elements, string Unknowns, int Iterations, string MaxVonMises, string PeakIn, string Seconds);

static class Swatches
{
    public static Color Colour(Vector4 c) => Color.FromRgb((byte)(c.X * 255 + 0.5f), (byte)(c.Y * 255 + 0.5f), (byte)(c.Z * 255 + 0.5f));

    public static Brush Frozen(Vector4 colour)
    {
        var brush = new SolidColorBrush(Colour(colour));
        brush.Freeze();
        return brush;
    }
}

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    public const double LegendBarHeight = 180;

    BambuInstallation? bambu;
    PresetLibrary? presets;
    string machine = "";
    PrintJob? job;
    Task redraw = Task.CompletedTask;
    bool redrawWanted;

    public MainViewModel()
    {
        ColouredMaterial = new PhongMaterial
        {
            // Vertex colours only: any ambient or specular share would shift them away from the legend.
            DiffuseColor = new Color4(1, 1, 1, 1),
            AmbientColor = new Color4(0, 0, 0, 1),
            SpecularColor = new Color4(0, 0, 0, 1),
            VertexColorBlendingFactor = 1,
        };
        Level = Levels[0];
        ResetStudy();
    }

    public DefaultEffectsManager EffectsManager { get; } = new();

    public PerspectiveCamera Camera { get; } = new()
    {
        Position = new Point3D(60, -60, 50),
        LookDirection = new Vector3D(-50, 50, -40),
        UpDirection = new Vector3D(0, 0, 1),
        NearPlaneDistance = 0.1,
        FarPlaneDistance = 10000,
    };

    public PhongMaterial ColouredMaterial { get; }

    public TriangleMesh? Part { get; private set; }

    /// <summary>Raised with the bounds to frame when a new model or a new slice arrives.</summary>
    public event Action<Box3>? FitRequested;

    /// <summary>Raised with a title and a message when something the user asked for failed.</summary>
    public event Action<string, string>? Failed;

    /// <summary>Completes when the viewport shows the current settings.</summary>
    public Task SceneReady => redraw;

    // ---- model and print settings ----

    [ObservableProperty]
    public partial string ModelName { get; set; } = "No model";

    [ObservableProperty]
    public partial double RotationX { get; set; }

    [ObservableProperty]
    public partial double RotationY { get; set; }

    [ObservableProperty]
    public partial double RotationZ { get; set; }

    public ObservableCollection<string> Processes { get; } = [];
    public ObservableCollection<string> Filaments { get; } = [];

    [ObservableProperty]
    public partial string? Process { get; set; }

    [ObservableProperty]
    public partial string? Filament { get; set; }

    [ObservableProperty]
    public partial string Printer { get; set; } = "Looking for Bambu Studio…";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SliceCommand))]
    public partial bool CanSlice { get; set; }

    // ---- mesh ----

    public IReadOnlyList<Choice<int?>> Levels { get; } =
    [
        new(null, "Automatic"),
        new(0, "Every bead cell (no octree)"),
        new(2, "Accurate: start at 4 x 4 x 4 cells"),
        new(3, "Balanced: start at 8 x 8 x 8 cells"),
        new(4, "Fast: start at 16 x 16 x 16 cells"),
        new(5, "Big parts: start at 32 x 32 x 32 cells"),
    ];

    [ObservableProperty]
    public partial Choice<int?> Level { get; set; }

    // ---- view ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsToolpathView), nameof(IsCellView), nameof(IsResultView), nameof(HasLayers))]
    public partial ViewMode View { get; set; }

    public bool IsToolpathView => View == ViewMode.Toolpaths;
    public bool IsCellView => View == ViewMode.Cells;
    public bool IsResultView => View == ViewMode.Results;
    public bool HasLayers => View != ViewMode.Model && HasPrint;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLayers))]
    [NotifyCanExecuteChangedFor(nameof(SolveCommand))]
    public partial bool HasPrint { get; set; }

    [ObservableProperty]
    public partial bool HasResult { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LayerText))]
    public partial int LayerCount { get; set; } = 1;

    /// <summary>First and last layer shown, counted from 1.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LayerText))]
    public partial int LayerLow { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LayerText))]
    public partial int LayerHigh { get; set; } = 1;

    public string LayerText
    {
        get
        {
            if (job is null) return "";
            var z = job.Grid.ZBoundaries;
            var bed = z[0];
            int low = Math.Clamp(LayerLow, 1, z.Length - 1), high = Math.Clamp(LayerHigh, low, z.Length - 1);
            return $"Layers {low}–{high} of {LayerCount}, {z[low - 1] - bed:0.##}–{z[high] - bed:0.##} mm above the bed";
        }
    }

    public IReadOnlyList<Choice<ToolpathColouring>> Colourings { get; } =
    [
        new(ToolpathColouring.Feature, "Line type"),
        new(ToolpathColouring.NozzleTemperature, "Nozzle temperature"),
        new(ToolpathColouring.Fan, "Part-cooling fan"),
        new(ToolpathColouring.Speed, "Speed"),
        new(ToolpathColouring.Time, "Time into the print"),
        new(ToolpathColouring.Width, "Line width"),
    ];

    [ObservableProperty]
    public partial ToolpathColouring Colouring { get; set; }

    /// <summary>Plain lines instead of beads of their real size; for big parts on a weak graphics card.</summary>
    [ObservableProperty]
    public partial bool ThinLines { get; set; }

    /// <summary>The line types in the current toolpath; unticking one hides its lines.</summary>
    public ObservableCollection<LegendItem> LineTypes { get; } = [];

    public IReadOnlyList<Choice<ResultQuantity>> Quantities { get; } =
    [
        new(ResultQuantity.VonMises, "von Mises stress"),
        new(ResultQuantity.Displacement, "Displacement"),
        new(ResultQuantity.InterlayerStress, "Stress across the layers"),
        new(ResultQuantity.ElementSize, "Element size (where the solver refined)"),
    ];

    [ObservableProperty]
    public partial ResultQuantity Quantity { get; set; }

    /// <summary>The pass shown; the pass table's selected row.</summary>
    [ObservableProperty]
    public partial int Pass { get; set; }

    public ObservableCollection<PassRow> Passes { get; } = [];

    /// <summary>Per cent of the largest value at which the colour scale ends.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScaleTopText))]
    public partial double ScaleTop { get; set; } = 100;

    public string ScaleTopText => ScaleTop >= 99.5 ? "Colour scale up to the peak" : $"Colour scale up to {ScaleTop:0} % of the peak";

    /// <summary>0 to 1: the largest displacement drawn as this share of a tenth of the part's size.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeformationText))]
    public partial double Deformation { get; set; } = 0.5;

    public string DeformationText => DeformationFactor > 0 ? $"Deformed shape, displacements x {DeformationFactor:0.#}" : "Deformed shape: off";

    [ObservableProperty]
    public partial bool ShowElements { get; set; } = true;

    [ObservableProperty]
    public partial string ResultSummary { get; set; } = "";

    /// <summary>Set when the result must not be trusted as it stands.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResultWarning))]
    public partial string ResultWarning { get; set; } = "";

    public bool HasResultWarning => ResultWarning.Length > 0;

    // ---- legend ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLegend))]
    public partial string LegendTitle { get; set; } = "";

    public bool HasLegend => LegendTitle.Length > 0;

    public ObservableCollection<LegendItem> LegendEntries { get; } = [];
    public ObservableCollection<LegendTick> LegendTicks { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLegendScale))]
    public partial Brush? LegendScale { get; set; }

    public bool HasLegendScale => LegendScale is not null;

    // ---- viewport content ----

    [ObservableProperty]
    public partial MeshGeometry3D? ModelGeometry { get; set; }

    /// <summary>The markers of the interfaces: cones on mounts, arrows for loads.</summary>
    [ObservableProperty]
    public partial MeshGeometry3D? GlyphGeometry { get; set; }

    [ObservableProperty]
    public partial MeshGeometry3D? BeadGeometry { get; set; }

    [ObservableProperty]
    public partial LineGeometry3D? BeadLines { get; set; }

    [ObservableProperty]
    public partial MeshGeometry3D? CellGeometry { get; set; }

    [ObservableProperty]
    public partial LineGeometry3D? ElementLines { get; set; }

    [ObservableProperty]
    public partial LineGeometry3D? BedGrid { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = "Open an STL to start.";

    [ObservableProperty]
    public partial string Caption { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SliceCommand), nameof(SolveCommand))]
    public partial bool IsBusy { get; set; }

    partial void OnRotationXChanged(double value) => OnRotationChanged();
    partial void OnRotationYChanged(double value) => OnRotationChanged();
    partial void OnRotationZChanged(double value) => OnRotationChanged();
    partial void OnViewChanged(ViewMode value)
    {
        if (value != ViewMode.Model) IsPicking = false; // faces are picked on the model only
        Redraw();
    }

    partial void OnColouringChanged(ToolpathColouring value) => Redraw();
    partial void OnThinLinesChanged(bool value) => Redraw();
    partial void OnQuantityChanged(ResultQuantity value) => Redraw();
    partial void OnPassChanged(int value) => Redraw();
    partial void OnScaleTopChanged(double value) => Redraw();
    partial void OnDeformationChanged(double value) => Redraw();
    partial void OnShowElementsChanged(bool value) => Redraw();

    partial void OnLayerLowChanged(int value)
    {
        if (LayerHigh < value) LayerHigh = value;
        Redraw();
    }

    partial void OnLayerHighChanged(int value)
    {
        if (LayerLow > value) LayerLow = value;
        Redraw();
    }

    Matrix4x4 Rotation => Orientation.FromEulerDegrees((float)RotationX, (float)RotationY, (float)RotationZ);

    /// <summary>The model view previews the rotation as it is typed; the other views keep showing the last slice.</summary>
    void OnRotationChanged()
    {
        if (View == ViewMode.Model && Part is { } part) FitRequested?.Invoke(part.Transformed(Rotation).Bounds);
        Redraw();
    }

    /// <summary>Displacements are scaled so that the largest one spans up to a tenth of the part's diagonal.</summary>
    float DeformationFactor =>
        job?.Solved is { MaxDisplacement: > 0 } solved
            ? (float)(Deformation * 0.1 * solved.Diagonal / solved.MaxDisplacement)
            : 0;

    // ---- actions ----

    /// <summary>Finds Bambu Studio and lists the processes and filaments that fit its selected printer.</summary>
    public async Task LoadPresetsAsync()
    {
        try
        {
            var found = await Task.Run(() =>
            {
                if (BambuInstallation.TryLocate() is not { } installation) return null;
                var selection = BambuSelection.Read(installation);
                var library = PresetLibrary.Load(installation, selection.UserPresetFolder);
                static string[] Names(IEnumerable<PresetInfo> infos) => [.. infos.OrderBy(p => !p.IsUser).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Select(p => p.Name)];
                return new
                {
                    Installation = installation,
                    Selection = selection,
                    Library = library,
                    Processes = Names(library.CompatibleWith(selection.Machine, PresetKind.Process)),
                    Filaments = Names(library.CompatibleWith(selection.Machine, PresetKind.Filament)),
                };
            });
            if (found is null)
            {
                Printer = "Bambu Studio not found: slicing is unavailable.";
                return;
            }

            (bambu, presets, machine) = (found.Installation, found.Library, found.Selection.Machine);
            foreach (var name in found.Processes) Processes.Add(name);
            foreach (var name in found.Filaments) Filaments.Add(name);
            Process ??= found.Selection.Process;
            Filament ??= found.Selection.Filament;
            Printer = $"{machine} (Bambu Studio {bambu.Version})";
            CanSlice = true;
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            Printer = "Bambu Studio's presets could not be read: " + ex.Message;
        }
    }

    public void LoadModel(string path)
    {
        var mesh = Stl.Read(path);
        Part = mesh;
        ModelPath = Path.GetFullPath(path);
        job = null;
        HasPrint = HasResult = false;
        Passes.Clear();
        LineTypes.Clear();
        ResultSummary = ResultWarning = "";
        ResetStudy(); // interfaces are faces of the model that was open
        View = ViewMode.Model;

        var size = mesh.Bounds.Size;
        ModelName = $"{Path.GetFileName(path)}: {mesh.TriangleCount:N0} triangles, {size.X:0.##} x {size.Y:0.##} x {size.Z:0.##} mm";
        Status = "Model loaded. Add its mounts and loads, and slice it to see the toolpaths.";
        FitRequested?.Invoke(mesh.Transformed(Rotation).Bounds);
        Redraw();
    }

    bool CanStartSlice() => CanSlice && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanStartSlice))]
    public async Task SliceAsync()
    {
        if (Part is not { } part || bambu is null || presets is null || Process is null || Filament is null) return;
        IsBusy = true;
        Status = "Slicing with Bambu Studio…";
        try
        {
            var slicer = new BambuSlicer(bambu, presets, BambuSlicer.DefaultCacheDirectory);
            var request = new SliceRequest(part, Rotation, machine, Process, Filament);
            var sliced = await Task.Run(async () =>
            {
                var slice = await slicer.SliceAsync(request);
                var toolpath = GcodeParser.Parse(slice.GcodePath);
                var grid = Voxelizer.Voxelize(toolpath, slice.Placement, slice.PrintMesh.Bounds);
                return new PrintJob { Slice = slice, Toolpath = toolpath, Grid = grid, Cells = [.. grid.Occupied(PrintJob.MinFill)] };
            });

            if (!ReferenceEquals(Part, part)) return; // another model was opened meanwhile

            job = sliced;
            HasResult = false;
            Passes.Clear();
            ResultSummary = ResultWarning = "";
            LineTypes.Clear();
            foreach (var entry in ToolpathColours.Colour(sliced.Toolpath, ToolpathColouring.Feature).Legend.Entries)
            {
                var item = new LegendItem(entry.Key, entry.Label, entry.Colour);
                item.PropertyChanged += OnLineTypeChanged;
                LineTypes.Add(item);
            }

            LayerCount = sliced.Grid.SizeZ;
            (LayerLow, LayerHigh) = (1, LayerCount);
            HasPrint = true;
            OnPropertyChanged(nameof(LayerText));
            FitRequested?.Invoke(sliced.Slice.PrintMesh.Bounds);
            if (View is ViewMode.Model or ViewMode.Results) View = ViewMode.Toolpaths;

            var report = sliced.Slice.Report;
            Status = $"Sliced in {sliced.Slice.Elapsed.TotalSeconds:0.0} s{(sliced.Slice.FromCache ? " (from the cache)" : "")}: {sliced.Toolpath.Layers.Length} layers, " +
                     $"{sliced.Toolpath.Segments.Length:N0} lines, {report.FilamentGrams:0.0} g, {TimeSpan.FromSeconds(report.PrintSeconds):h\\:mm} h to print; {sliced.Cells.Length:N0} cells.";
            Redraw();
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            Status = "Slicing failed.";
            Failed?.Invoke("Slicing failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    bool CanStartSolve() => HasPrint && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanStartSolve))]
    public async Task SolveAsync()
    {
        if (job is not { } current || SelectedLoadCase is not { } shown) return;
        IsBusy = true;
        try
        {
            if (Interfaces.FirstOrDefault(item => item.HasValueError) is { } unread)
                throw new FormatException($"The value of '{unread.Name}' cannot be read: {unread.ValueError}");
            var study = ToStudy();
            var chosen = study.LoadCases[LoadCases.IndexOf(shown)];
            var options = new AdaptiveOptions(RootLevel: Level.Value, MinFill: PrintJob.MinFill, KeepPassFields: true);
            Passes.Clear();
            Status = "Solving: building the mesh…";
            var progress = new Progress<AdaptivePass>(pass =>
            {
                Passes.Add(Row(pass));
                Status = $"Solving: pass {pass.Index + 1} done ({pass.Dofs:N0} unknowns, {pass.Marked:N0} elements to refine)…";
            });

            var solved = await Task.Run(() =>
            {
                // The interfaces are faces of the model; the slice says how it was turned for this print.
                var loadCase = InterfaceMapper.Map(current.Slice.PrintMesh, current.Slice.Placement.Rotation, current.Grid.Pitch, study.Interfaces, chosen);
                var mesh = FemMesh.Build(current.Grid, PrintJob.MinFill, loadCase);
                var reach = Assembler.Reach(mesh, loadCase);
                var result = AdaptiveAnalysis.Run(mesh, IsotropicMaterial.Pla, loadCase, LinearSolvers.Best(), options, ((IProgress<AdaptivePass>)progress).Report);
                return new SolvedJob(result, chosen.Name, reach);
            });

            if (!ReferenceEquals(job, current)) return; // sliced again or another model opened meanwhile

            current.Solved = solved;
            current.Surface = null;
            Passes.Clear();
            foreach (var pass in solved.Result.Passes) Passes.Add(Row(pass));
            Describe(solved);
            HasResult = true;
            OnPropertyChanged(nameof(DeformationText));
            View = ViewMode.Results;
            Pass = solved.Result.Passes.Count - 1;
            Redraw();
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            Status = "The solve failed.";
            Failed?.Invoke("The solve failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    static PassRow Row(AdaptivePass pass) => new(
        pass.Index + 1,
        string.Join(" / ", pass.LeavesByLevel.Select(n => n.ToString("N0"))),
        pass.Dofs.ToString("N0"),
        pass.Solve.Iterations,
        pass.MaxVonMises.ToString("0.00"),
        pass.PeakLevel == 0 ? "bead cell" : $"{1 << pass.PeakLevel}³ cells",
        pass.Time.TotalSeconds.ToString("0.00"));

    void Describe(SolvedJob solved)
    {
        var result = solved.Result;
        var final = result.Final;
        var mesh = result.Mesh;
        var cell = mesh.Cells[final.PeakCell];
        var at = mesh.Grid.NodePosition(cell.I, cell.J, cell.K) + new Vector3(mesh.Grid.Pitch / 2, mesh.Grid.Pitch / 2, mesh.Grid.CellHeight(cell.K) / 2);
        var dropped = mesh.DroppedCells > 0 ? $" {mesh.DroppedCells:N0} cells are not connected to a mount and were left out." : "";
        // What the solver took each interface to be: a check on the picking. A force is given as it acts on the print.
        var reach = string.Join("; ", solved.Reach.Select(r => r.IsLoad
            ? $"{r.Name} loads {r.Faces:N0} cell faces with ({r.Force.X:0.##}, {r.Force.Y:0.##}, {r.Force.Z:0.##}) N"
            : $"{r.Name} holds {r.Faces:N0} cell faces"));
        ResultSummary = $"{solved.LoadCaseName}: max von Mises {final.MaxVonMises:0.00} MPa at ({at.X:0.0}, {at.Y:0.0}, {at.Z:0.0}) mm, layer {cell.K + 1}.\n" +
                        $"Max displacement {solved.MaxDisplacement:0.0000} mm.\n" +
                        $"{reach}.\n" +
                        $"{result.Passes.Count} {(result.Passes.Count == 1 ? "pass" : "passes")}, {final.Dofs:N0} unknowns in the last, {result.Time.TotalSeconds:0.0} s. " +
                        $"Stopped because {result.StopReason}.{dropped}\n" +
                        $"Material: generic PLA, E {IsotropicMaterial.Pla.YoungsModulus:0} MPa, isotropic.";

        var unconverged = result.Passes.Where(p => !p.Solve.Converged).Select(p => p.Index + 1).ToList();
        var warnings = new List<string>();
        if (final.PeakLevel > 0)
            warnings.Add($"The peak still sits in an element of {1 << final.PeakLevel}³ cells. Coarse elements read too low (down to half the real stress), so the real peak is higher.");
        if (unconverged.Count > 0)
            warnings.Add($"The linear solver did not converge in pass {string.Join(", ", unconverged)}.");
        ResultWarning = string.Join("\n", warnings);
        Status = $"Solved in {result.Time.TotalSeconds:0.0} s.";
    }

    void OnLineTypeChanged(object? sender, PropertyChangedEventArgs e) => Redraw();

    ViewState CurrentState() => new(
        View,
        LayerLow - 1,
        LayerHigh,
        Colouring,
        LineTypes.Where(item => !item.IsShown).Aggregate(0, (mask, item) => mask | 1 << item.Key),
        ThinLines,
        Quantity,
        Pass,
        (float)(ScaleTop / 100),
        DeformationFactor,
        ShowElements,
        CellColouring,
        CurrentMarks());

    /// <summary>Brings the viewport up to date. Builds run one at a time, and the last request always wins.</summary>
    void Redraw()
    {
        redrawWanted = true;
        if (redraw.IsCompleted) redraw = RedrawLoop();
    }

    async Task RedrawLoop()
    {
        while (redrawWanted)
        {
            redrawWanted = false;
            var state = CurrentState();
            var (current, part, rotation) = (job, Part, Rotation);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var scene = await Task.Run(() => SceneBuilder.Build(state, state.Mode == ViewMode.Model ? part?.Transformed(rotation) : null, rotation, current));
                Apply(scene with { Caption = scene.Caption.Length > 0 ? $"{scene.Caption}; built in {watch.ElapsedMilliseconds:N0} ms" : "" });
            }
            catch (Exception ex)
            {
                // Nobody awaits a redraw, so a failure here would otherwise pass unseen.
                Status = "The view could not be drawn: " + ex.Message;
            }
        }
    }

    void Apply(Scene scene)
    {
        ModelGeometry = scene.Model;
        GlyphGeometry = scene.Glyphs;
        BeadGeometry = scene.Beads;
        BeadLines = scene.BeadLines;
        CellGeometry = scene.Cells;
        ElementLines = scene.Elements;
        BedGrid = scene.Bed;
        Caption = scene.Caption;
        OnPropertyChanged(nameof(DeformationText));

        LegendEntries.Clear();
        LegendTicks.Clear();
        LegendScale = null;
        if (scene.Legend is not { } legend)
        {
            LegendTitle = "";
            return;
        }

        LegendTitle = legend.Unit.Length > 0 ? $"{legend.Title} · {legend.Unit}" : legend.Title;
        foreach (var entry in legend.Entries) LegendEntries.Add(new LegendItem(entry.Key, entry.Label, entry.Colour));
        if (legend.Scale is not { } scale) return;

        // Drawn top to bottom, so the high end comes first.
        var stops = new GradientStopCollection();
        const int samples = 24;
        for (var n = 0; n <= samples; n++)
            stops.Add(new GradientStop(Swatches.Colour(scale.Ramp[(scale.Ramp.Count - 1) * (samples - n) / samples]), n / (double)samples));
        var brush = new LinearGradientBrush(stops, new System.Windows.Point(0, 0), new System.Windows.Point(0, 1));
        brush.Freeze();
        LegendScale = brush;

        const int ticks = 4;
        for (var n = 0; n <= ticks; n++)
        {
            var value = scale.Max + (scale.Min - scale.Max) * n / ticks;
            // A scale cut short of the peak shows everything above its top in the top colour.
            var cut = ScaleTop < 99.5 && IsResultView;
            var text = Format(value, scale.Max - scale.Min) + (cut && n == 0 ? " and above" : cut && n == ticks && scale.IsDiverging ? " and below" : "");
            LegendTicks.Add(new LegendTick(text, LegendBarHeight * n / ticks - 8));
        }
    }

    /// <summary>Enough decimals to tell the ticks of a scale of this <paramref name="span"/> apart.</summary>
    static string Format(float value, float span)
    {
        var decimals = span <= 0 ? 2 : Math.Clamp(2 - (int)Math.Floor(Math.Log10(span)), 0, 6);
        return value.ToString("F" + decimals);
    }

    public static Vector3 ParseTriple(string text)
    {
        var parts = text.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || !parts.All(p => float.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
            throw new FormatException($"Expected three numbers like 0, 0, -100 but got '{text}'.");
        return new Vector3(float.Parse(parts[0], CultureInfo.InvariantCulture), float.Parse(parts[1], CultureInfo.InvariantCulture), float.Parse(parts[2], CultureInfo.InvariantCulture));
    }

    /// <summary>Failures that are reported to the user instead of ending the program.</summary>
    public static bool IsExpected(Exception ex) =>
        ex is IOException or InvalidDataException or SlicerException or KeyNotFoundException or FormatException
            or ArgumentException or InvalidOperationException or DllNotFoundException or UnauthorizedAccessException or OutOfMemoryException;

    public void Dispose() => EffectsManager.Dispose();
}
