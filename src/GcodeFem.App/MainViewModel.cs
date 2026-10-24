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
using GcodeFem.Core.Materials;
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
public sealed record PassRow(int Pass, string Elements, string Unknowns, int Iterations, string SafetyFactor, string MaxVonMises, string PeakIn, string Seconds);

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
    MaterialLibrary materials = MaterialLibrary.Load();
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
        workTimer.Tick += (_, _) => ShowWork();
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

    /// <summary>The material the selected filament is solved with, in a sentence or two.</summary>
    [ObservableProperty]
    public partial string MaterialText { get; set; } = "";

    /// <summary>The filament has no material of its own, or one the solver cannot use: shown in the colour of a caution.</summary>
    [ObservableProperty]
    public partial bool MaterialIsStandIn { get; set; }

    public MaterialLibrary Materials => materials;

    public PresetLibrary? Presets => presets;

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
        new(ResultQuantity.SafetyFactor, "Safety factor"),
        new(ResultQuantity.FailureMode, "What breaks first"),
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

    /// <summary>What there is to know about the cell clicked last in the results view.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProbe))]
    public partial string ProbeText { get; set; } = "";

    public bool HasProbe => ProbeText.Length > 0;

    /// <summary>The cell of the result clicked last, −1 for none.</summary>
    int probedCell = -1;

    /// <summary>The cell faces of the results view as drawn, for a click to find its cell.</summary>
    (CellSurface Surface, Vector3[] Positions)? shownSurface;

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

    /// <summary>The weakest layer line and the cell clicked last, outlined.</summary>
    [ObservableProperty]
    public partial LineGeometry3D? MarkLines { get; set; }

    [ObservableProperty]
    public partial LineGeometry3D? BedGrid { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = "Open an STL to start.";

    [ObservableProperty]
    public partial string Caption { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SliceCommand), nameof(SolveCommand))]
    public partial bool IsBusy { get; set; }

    partial void OnFilamentChanged(string? value) => RefreshMaterial();

    /// <summary>Reads the material files again, after the materials window changed them.</summary>
    public void ReloadMaterials()
    {
        materials = MaterialLibrary.Load(materials.Directory);
        RefreshMaterial();
        if (job is { } current)
        {
            current.Bonds = Bonds(current.Grid, current.Cells, current.Toolpath, materials);
            if (View == ViewMode.Cells) Redraw();
        }
    }

    /// <summary>How well each cell has bonded to the one below, against the specimens of the material of the filament the part was sliced with.</summary>
    static LayerBonds Bonds(CellGrid grid, CellIndex[] cells, Toolpath toolpath, MaterialLibrary library) =>
        new BondModel().Cells(grid, cells, library.Match(toolpath).Material.Bonding(toolpath), PrintJob.MinFill);

    /// <summary>Says which material the selected filament will be solved with. The solve itself goes by the filament of the slice.</summary>
    void RefreshMaterial()
    {
        if (Filament is not { } filament)
        {
            (MaterialText, MaterialIsStandIn) = ("", false);
            return;
        }
        // The type decides the stand-in; it is known once Bambu Studio's presets are read.
        var type = presets is not null && presets.Contains(filament) ? presets.SummarizeFilament(filament).Type : null;
        var match = materials.Match(filament, type);
        var material = match.Material;
        if (material.Lacking() is { Count: > 0 } lacking)
        {
            MaterialText = $"{material.Name} gives no {string.Join(" and no ", lacking.Select(p => p.Label()))}: it cannot be solved with until that is filled in.";
            MaterialIsStandIn = true;
            return;
        }

        var numbers = $"E {material.Number(MaterialProperty.YoungsModulusXY):0} MPa in the plane of the layers" +
                      (material.Number(MaterialProperty.YoungsModulusZ) is { } z ? $", {z:0} across them" : "") +
                      (material.Number(MaterialProperty.TensileStrengthXY) is { } strength
                          ? $"; tensile strength {strength:0.#}" + (material.Number(MaterialProperty.TensileStrengthZ) is { } weak ? $" and {weak:0.#} MPa" : " MPa")
                          : "");
        MaterialText = match.Kind switch
        {
            MatchKind.ByName => $"{material.Name}: {numbers}.",
            MatchKind.ByType => $"No data for this filament. {material.Name} stands in for it, as for every {match.Type} without data of its own: {numbers}.",
            _ => match.Describe(),
        };
        MaterialIsStandIn = match.IsStandIn;
    }

    partial void OnRotationXChanged(double value) => OnRotationChanged();
    partial void OnRotationYChanged(double value) => OnRotationChanged();
    partial void OnRotationZChanged(double value) => OnRotationChanged();
    partial void OnViewChanged(ViewMode value)
    {
        if (value != ViewMode.Model) IsPicking = IsPlacing = false; // faces are picked on the model only
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
        // Forces are fixed to the part but read in the directions on screen, so their numbers follow the turn.
        foreach (var item in Interfaces) item.Show(SelectedLoadCase, Rotation);
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
                RefreshMaterial();
                return;
            }

            (bambu, presets, machine) = (found.Installation, found.Library, found.Selection.Machine);
            foreach (var name in found.Processes) Processes.Add(name);
            foreach (var name in found.Filaments) Filaments.Add(name);
            Process ??= found.Selection.Process;
            Filament ??= found.Selection.Filament;
            Printer = $"{machine} (Bambu Studio {bambu.Version})";
            CanSlice = true;
            RefreshMaterial();
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
        ResultSummary = ResultWarning = ProbeText = "";
        probedCell = -1;
        ResetStudy(); // interfaces are faces of the model that was open
        View = ViewMode.Model;

        var size = mesh.Bounds.Size;
        ModelName = $"{Path.GetFileName(path)}: {mesh.TriangleCount:N0} triangles, {size.X:0.##} x {size.Y:0.##} x {size.Z:0.##} mm";
        Status = "Model loaded. Lay it on the face it is to be printed on, add its mounts and loads, and slice it.";
        FitRequested?.Invoke(mesh.Transformed(Rotation).Bounds);
        Redraw();
    }

    bool CanStartSlice() => CanSlice && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanStartSlice))]
    public async Task SliceAsync()
    {
        if (Part is not { } part || bambu is null || presets is null || Process is null || Filament is null) return;
        IsBusy = true;
        // Bambu Studio says nothing while it slices, so the bar can only show the time it takes.
        var steps = new StepClock("Bambu Studio at work");
        BeginWork("Slicing", steps.Now);
        try
        {
            var slicer = new BambuSlicer(bambu, presets, BambuSlicer.DefaultCacheDirectory);
            var request = new SliceRequest(part, Rotation, machine, Process, Filament);
            var library = materials;
            var sliced = await Task.Run(async () =>
            {
                var slice = await slicer.SliceAsync(request);
                steps.Step = "reading the toolpath";
                var toolpath = GcodeParser.Parse(slice.GcodePath);
                // The slicer's own times per line type include acceleration, which the bond of the layer lines depends on.
                steps.Step = "turning the toolpath into cells";
                var grid = Voxelizer.Voxelize(toolpath, slice.Placement, slice.PrintMesh.Bounds, PrintClock.Calibrated(toolpath, slice.Report.FeatureSeconds));
                CellIndex[] cells = [.. grid.Occupied(PrintJob.MinFill)];
                steps.Step = "how well the layer lines bonded";
                return new PrintJob { Slice = slice, Toolpath = toolpath, Grid = grid, Cells = cells, Bonds = Bonds(grid, cells, toolpath, library) };
            });

            if (!ReferenceEquals(Part, part)) return; // another model was opened meanwhile

            job = sliced;
            HasResult = false;
            Passes.Clear();
            ResultSummary = ResultWarning = ProbeText = "";
            probedCell = -1;
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
            EndWork();
            IsBusy = false;
        }
    }

    bool CanStartSolve() => HasPrint && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanStartSolve))]
    public async Task SolveAsync()
    {
        if (job is not { } current || SelectedLoadCase is not { } shown) return;
        IsBusy = true;
        var estimate = new SolveEstimate();
        BeginWork("Solving", estimate.Now);
        try
        {
            if (Interfaces.FirstOrDefault(item => item.HasValueError) is { } unread)
                throw new FormatException($"The value of '{unread.Name}' cannot be read: {unread.ValueError}");
            var study = ToStudy();
            var chosen = study.LoadCases[LoadCases.IndexOf(shown)];
            // The material of the filament the part was sliced with, which need not be the one selected now.
            var match = materials.Match(current.Toolpath);
            var material = match.Material.Stiffness();
            var strength = match.Material.Strength(out var noStrength);
            var options = new AdaptiveOptions(RootLevel: Level.Value, MinFill: PrintJob.MinFill, KeepPassFields: true);
            Passes.Clear();
            var progress = new Progress<AdaptivePass>(pass => Passes.Add(Row(pass)));

            var solved = await Task.Run(() =>
            {
                // The interfaces are faces of the model; the slice says how it was turned for this print.
                var loadCase = InterfaceMapper.Map(current.Slice.PrintMesh, current.Slice.Placement.Rotation, current.Grid.Pitch, study.Interfaces, chosen);
                var mesh = FemMesh.Build(current.Grid, PrintJob.MinFill, loadCase);
                var reach = Assembler.Reach(mesh, loadCase);
                // The layer lines are as strong as they have bonded: the refinement goes where the part is nearest to breaking.
                var bonds = new BondModel().Cells(mesh.Grid, mesh.Cells, match.Material.Bonding(current.Toolpath), PrintJob.MinFill);
                var cells = strength is null ? null : new CellStrength(mesh, strength, bonds.Factor);
                // The estimate is fed here, on the solving thread, for its clock to read the passes as they end.
                var result = AdaptiveAnalysis.Run(mesh, material, loadCase, LinearSolvers.Best(), options, pass =>
                {
                    estimate.Passed(pass);
                    ((IProgress<AdaptivePass>)progress).Report(pass);
                }, cells, estimate.Solving);
                estimate.Finished();
                return new SolvedJob(result, chosen.Name, reach, match, bonds, cells, noStrength);
            });

            if (!ReferenceEquals(job, current)) return; // sliced again or another model opened meanwhile

            current.Solved = solved;
            current.Surface = null;
            (probedCell, ProbeText) = (-1, "");
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
            EndWork();
            IsBusy = false;
        }
    }

    static PassRow Row(AdaptivePass pass) => new(
        pass.Index + 1,
        string.Join(" / ", pass.LeavesByLevel.Select(n => n.ToString("N0"))),
        pass.Dofs.ToString("N0"),
        pass.Solve.Iterations,
        double.IsNaN(pass.SafetyFactor) ? "" : pass.SafetyFactor.ToString("0.00"),
        pass.MaxVonMises.ToString("0.00"),
        pass.PeakLevel == 0 ? "bead cell" : $"{1 << pass.PeakLevel}³ cells",
        pass.Time.TotalSeconds.ToString("0.00"));

    void Describe(SolvedJob solved)
    {
        var result = solved.Result;
        var final = result.Final;
        var mesh = result.Mesh;
        var dropped = mesh.DroppedCells > 0 ? $" {mesh.DroppedCells:N0} cells are not connected to a mount and were left out." : "";
        // A known fault of the native solver, not of the result: noted so that it can be traced (HANDOFF.md).
        if (result.Passes.Where(p => p.Solve.Retried).Select(p => p.Index + 1).ToList() is { Count: > 0 } retried)
            dropped += $" The linear solver had to be run again in pass {string.Join(", ", retried)}: its first try gave NaN.";
        // What the solver took each interface to be: a check on the picking. A force is given as it acts on the print.
        var reach = string.Join("; ", solved.Reach.Select(r => r.IsLoad
            ? $"{r.Name} loads {r.Faces:N0} cell faces with ({r.Force.X:0.##}, {r.Force.Y:0.##}, {r.Force.Z:0.##}) N"
            : $"{r.Name} holds {r.Faces:N0} cell faces"));
        ResultSummary = $"{solved.LoadCaseName}: {FailureLines(solved)}\n" +
                        $"Max von Mises {final.MaxVonMises:0.00} MPa; max displacement {solved.MaxDisplacement:0.0000} mm.\n" +
                        $"{reach}.\n" +
                        $"{result.Passes.Count} {(result.Passes.Count == 1 ? "pass" : "passes")}, {final.Dofs:N0} unknowns in the last, {result.Time.TotalSeconds:0.0} s. " +
                        $"Stopped because {result.StopReason}.{dropped}\n" +
                        MaterialLine(solved.Material) +
                        "\n" + BondLine(mesh, solved.Bonds);

        var unconverged = result.Passes.Where(p => !p.Solve.Converged).Select(p => p.Index + 1).ToList();
        var warnings = new List<string>();
        if (solved.Material.Kind == MatchKind.None) warnings.Add(solved.Material.Describe());
        if (final.PeakLevel > 0)
            warnings.Add(solved.Failure is null
                ? $"The peak still sits in an element of {1 << final.PeakLevel}³ cells. Coarse elements read too low (down to half the real stress), so the real peak is higher."
                : $"The weakest spot still sits in an element of {1 << final.PeakLevel}³ cells. Coarse elements read the stress too low (down to half), so the real safety factor is lower.");
        if (unconverged.Count > 0)
            warnings.Add($"The linear solver did not converge in pass {string.Join(", ", unconverged)}.");
        ResultWarning = string.Join("\n", warnings);
        Status = $"Solved in {result.Time.TotalSeconds:0.0} s.";
    }

    /// <summary>What the result was solved with, and whose numbers those are.</summary>
    static string MaterialLine(MaterialMatch match)
    {
        var material = match.Material;
        var stiffness = material.Stiffness();
        var moduli = stiffness.IsIsotropic
            ? $"E {stiffness.AlongLine:0} MPa in every direction"
            : $"E {stiffness.AlongLine:0} MPa in the plane of the layers and {stiffness.AcrossLayers:0} across them";
        var poisson = material.Of(MaterialProperty.PoissonRatio) is { IsAssumed: true } ? ", assumed" : "";
        var whose = match.Kind switch
        {
            MatchKind.ByName => "",
            MatchKind.ByType => $" It stands in for {match.Filament}, which has no data of its own.",
            _ => " No data for this filament.",
        };
        return $"Material: {material.Name}, {moduli}, Poisson's ratio {stiffness.PoissonRatio:0.##}{poisson}.{whose}";
    }

    /// <summary>How well the layer lines of the solved cells have bonded; the strength across them is the data sheet's × that.</summary>
    static string BondLine(FemMesh mesh, LayerBonds bonds)
    {
        var lined = Enumerable.Range(0, mesh.Cells.Length).Where(e => !float.IsNaN(bonds.Gap[e])).ToList();
        if (lined.Count == 0) return "No cell lies on another, so there are no layer lines.";
        var weakest = lined.GroupBy(e => mesh.Cells[e].K).Select(g => (Layer: g.Key, Factor: g.Average(e => bonds.Factor[e]), Gap: g.Average(e => bonds.Gap[e]))).MinBy(l => l.Factor);
        return $"Layer lines bonded {lined.Min(e => bonds.Factor[e]):0%} to {lined.Max(e => bonds.Factor[e]):0%} as well as the data sheet's specimens; the weakest layer is {weakest.Layer + 1} " +
               $"({weakest.Factor:0%}, printed {weakest.Gap:0} s after the one below). The strength across a layer line is the sheet's × its bond; the stiffness does not depend on it.";
    }

    /// <summary>
    /// The safety factor and what it means for the loads, how and where the part breaks first, its
    /// weakest layer line, and what the strengths rest on; or why there is no safety factor.
    /// </summary>
    static string FailureLines(SolvedJob solved)
    {
        if (solved.Failure is not { } failure || solved.Strength is not { } cells)
            return $"no safety factor: {solved.NoStrength}";
        var mesh = solved.Result.Mesh;
        var grid = mesh.Grid;
        var bed = grid.ZBoundaries[0];
        var factor = failure.SafetyFactor;
        var weakest = mesh.Cells[failure.Weakest];
        var at = ResultMarks.Centre(grid, weakest);
        var feature = grid.Feature(weakest.I, weakest.J, weakest.K) is { } f ? $", {f.DisplayName().ToLowerInvariant()}" : "";
        var loads = solved.Reach.Where(r => r.IsLoad).Select(r => $"{r.Name} {factor * r.Force.Length():0.#} N").ToList();
        var breaks = factor < 1
            ? $"safety factor {factor:0.00}: it breaks under these loads, and would hold {factor:0%} of them"
            : $"safety factor {factor:0.00}: it starts to break at {factor:0.##} x these loads";
        var line = mesh.Cells[failure.WeakestLayerLine];
        var lineBond = float.IsNaN(solved.Bonds.Gap[failure.WeakestLayerLine])
            ? "nothing below it there"
            : $"bonded {solved.Bonds.Factor[failure.WeakestLayerLine]:0%} as well as the specimens', printed {solved.Bonds.Gap[failure.WeakestLayerLine]:0} s after the layer below";
        var strength = cells.Strength;
        return $"{breaks}{(loads.Count > 0 ? $" ({string.Join(", ", loads)})" : "")}. " +
               $"{failure.Mode[failure.Weakest].DisplayName()} first, in layer {weakest.K + 1} at ({at.X:0.0}, {at.Y:0.0}, {at.Z:0.0}) mm{feature}.\n" +
               $"Weakest layer line: under layer {line.K + 1}, {grid.ZBoundaries[line.K] - bed:0.##} mm above the bed, safety factor {failure.LayerLineSafetyFactor:0.00} against it pulling apart ({lineBond}). " +
               $"Tsai–Wu, as a cross-check: {failure.TsaiWu.Min():0.00}.\n" +
               $"Strength along the lines {strength.AlongLine:0.#} MPa, across them and across the layers {strength.AcrossLayers:0.#} × the bond, in compression {strength.Compression:0.#}; " +
               "the shear strengths and the compression are assumed. These are the data sheet's best and not yet checked against broken prints: read the safety factor as a ranking more than as a margin.";
    }

    /// <summary>
    /// The cell of the results view under a ray: what it is, how it was printed, its stress in the
    /// frame of its lines and how near each mode is to breaking it.
    /// </summary>
    void Probe(Vector3 origin, Vector3 direction)
    {
        if (job?.Solved is not { } solved || shownSurface is not { } shown) return;
        var face = shown.Surface.Pick(shown.Positions, origin, direction);
        if (face < 0) return;
        probedCell = shown.Surface.FaceCells[face];
        ProbeText = ProbeDescription(solved, probedCell);
        Redraw();
    }

    static string ProbeDescription(SolvedJob solved, int e)
    {
        var mesh = solved.Result.Mesh;
        var grid = mesh.Grid;
        var c = mesh.Cells[e];
        var at = ResultMarks.Centre(grid, c);
        var direction = grid.Direction(c.I, c.J, c.K);
        var lines = direction.Spread >= CellColours.CrossingSpread ? "lines crossing" : $"lines at {direction.Angle * 180 / MathF.PI:0}° to X";
        var feature = grid.Feature(c.I, c.J, c.K) is { } f ? f.DisplayName().ToLowerInvariant() : "plastic";
        var bond = float.IsNaN(solved.Bonds.Gap[e]) ? "nothing below" : $"bonded {solved.Bonds.Factor[e]:0%} to the layer below, {solved.Bonds.Gap[e]:0} s after it";
        var moved = Enumerable.Range(0, HexElement.Nodes).Aggregate(Vector3.Zero, (sum, a) => sum + solved.NodeDisplacements[mesh.CellNodes[8 * e + a]]) / HexElement.Nodes;
        var text = $"Cell in layer {c.K + 1} at ({at.X:0.0}, {at.Y:0.0}, {at.Z:0.0}) mm: {feature}, {lines}, {mesh.Fill[e]:0%} full, {bond}.\n" +
                   $"von Mises {solved.Result.VonMises[e]:0.00} MPa; moved {moved.Length():0.0000} mm.";
        if (solved.Strength is not { } strength || solved.Failure is not { } failure) return text;

        var stress = solved.Result.Stress.AsSpan(6 * e, 6);
        Span<double> local = stackalloc double[6];
        strength.LocalStress(e, stress, local);
        var modes = new double[FailureModes.Count];
        var tsaiWu = strength.Evaluate(e, stress, modes);
        var against = string.Join(", ", Enum.GetValues<FailureMode>().Select(m => $"{m.DisplayName().ToLowerInvariant()} {SafetyText(1 / modes[(int)m])}"));
        return text + "\n" +
               $"Stress in its plastic along the line, across it, across the layers: {local[0]:0.00}, {local[1]:0.00}, {local[2]:0.00} MPa; shear {local[3]:0.00} in the layer, {local[5]:0.00} and {local[4]:0.00} across it.\n" +
               $"Safety factor {SafetyText(failure.SafetyFactorOf(e))}: {against}. Tsai–Wu {SafetyText(tsaiWu)}.";
    }

    static string SafetyText(double factor) => double.IsFinite(factor) && factor < 1000 ? factor.ToString("0.00") : "over 1000";

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
        CurrentMarks(),
        probedCell);

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
        MarkLines = scene.Marks;
        BedGrid = scene.Bed;
        shownSurface = scene.Surface is { } surface && scene.SurfacePositions is { } positions ? (surface, positions) : null;
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
