using System.Collections.ObjectModel;
using System.IO;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Visual;

namespace GcodeFem.App;

/// <summary>
/// The steps of the work, left to right along the top bar. Each has a panel of its own and the view
/// that goes with it: the model for the first two, then the print, the result and the comparison.
/// </summary>
public enum Stage { Setup, Loads, Preview, Results, Compare }

/// <summary>A number worth reading at a glance, and what it is.</summary>
public sealed record Fact(string Label, string Value);

/// <summary>
/// The part of the view model that leads through the work: the step on screen, what a click on the
/// model does just now, whether the print on screen is still the part as it lies, and the result
/// told in a few numbers before the full report.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>Which of the print's views the preview shows: the toolpath or the cells.</summary>
    ViewMode previewView = ViewMode.Toolpaths;

    /// <summary>The process and filament the print on screen was sliced with.</summary>
    (string? Process, string? Filament) slicedWith;

    [ObservableProperty]
    public partial Stage Stage { get; set; }

    partial void OnStageChanged(Stage value)
    {
        if (value != Stage.Loads) IsPicking = false;
        if (value != Stage.Setup) IsPlacing = false;
        View = value switch
        {
            Stage.Setup or Stage.Loads => ViewMode.Model,
            Stage.Preview => previewView,
            Stage.Results => ViewMode.Results,
            _ => ViewMode.Compare,
        };
    }

    /// <summary>Keeps the step in line with a view the program chose: after a slice or a solve, or from the command line.</summary>
    void FollowView(ViewMode view)
    {
        if (view is ViewMode.Toolpaths or ViewMode.Cells) previewView = view;
        Stage = view switch
        {
            ViewMode.Model => Stage is Stage.Setup or Stage.Loads ? Stage : Stage.Setup,
            ViewMode.Toolpaths or ViewMode.Cells => Stage.Preview,
            ViewMode.Results => Stage.Results,
            _ => Stage.Compare,
        };
    }

    [RelayCommand]
    void GoTo(Stage stage) => Stage = stage;

    /// <summary>The study's file, or the model's while there is no study: what the top bar names.</summary>
    public string DocumentPath => StudyPath ?? ModelPath ?? "";

    public string DocumentName => Path.GetFileName(DocumentPath) is { Length: > 0 } name ? name : "No part open";

    /// <summary>How the mouse moves the view, and what a click does in the view on screen.</summary>
    public string MouseHint =>
        "Right-drag to turn · middle-drag to pan · wheel to zoom" + View switch
        {
            ViewMode.Results => " · click a line or cell to probe it",
            ViewMode.Compare => " · click a copy to leave it out or take it back",
            _ => "",
        };

    [RelayCommand]
    void ResetRotation() => (RotationX, RotationY, RotationZ) = (0, 0, 0);

    [RelayCommand]
    void ClearProbe()
    {
        (probedCell, ProbeText) = (-1, "");
        Redraw();
    }

    // ---- what a click on the model does ----

    /// <summary>A click on the model picks a face or lays one on the bed: the viewport says so, and Esc ends it.</summary>
    public bool IsClicking => IsPicking || IsPlacing;

    public string ClickText =>
        IsPlacing ? "Click the face of the model that is to lie on the bed."
        : IsPicking && SelectedInterface is { } item ? $"Click faces of the model to add them to '{item.Name}'. Click a picked face again to take it off."
        : "";

    [RelayCommand]
    public void EndClicking() => IsPicking = IsPlacing = false;

    // ---- the print on screen ----

    /// <summary>Layers, lines, filament and time of the print on screen.</summary>
    public ObservableCollection<Fact> PrintFacts { get; } = [];

    /// <summary>What the slicer warned of, such as regions that float without support.</summary>
    [ObservableProperty]
    public partial string PrintWarning { get; set; } = "";

    /// <summary>The part has been turned, or the process or filament changed, since the print on screen was sliced.</summary>
    public bool PrintIsStale => StaleText.Length > 0;

    public string StaleText =>
        job is not { } current ? ""
        : !SameTurn(current.Slice.Placement.Rotation, Rotation) ? "The part has been turned since this was sliced. Solve or slice again to print it as it lies now."
        : slicedWith != (Process, Filament) ? "The process or the filament has changed since this was sliced. Solve or slice again to print with them."
        : "";

    static bool SameTurn(Matrix4x4 a, Matrix4x4 b)
    {
        for (var row = 0; row < 4; row++)
            for (var column = 0; column < 4; column++)
                if (MathF.Abs(a[row, column] - b[row, column]) > 1e-4f) return false;
        return true;
    }

    void RefreshStale()
    {
        OnPropertyChanged(nameof(StaleText));
        OnPropertyChanged(nameof(PrintIsStale));
    }

    void DescribePrint(PrintJob print)
    {
        var report = print.Slice.Report;
        PrintFacts.Clear();
        PrintFacts.Add(new("Layers", $"{print.Toolpath.Layers.Length:N0}"));
        PrintFacts.Add(new("Print time", $"{TimeSpan.FromSeconds(report.PrintSeconds):h\\:mm} h"));
        PrintFacts.Add(new("Filament", $"{report.FilamentGrams:0.0} g"));
        PrintFacts.Add(new("Lines", $"{print.Toolpath.Segments.Length:N0}"));
        PrintFacts.Add(new("Bead cells", $"{print.Cells.Length:N0}"));
        if (print.Part.SupportVolume > 0) PrintFacts.Add(new("Support", $"{print.Part.SupportVolume:N0} mm³"));
        PrintWarning = report.Warning ?? "";
    }

    // ---- the result at a glance ----

    /// <summary>The safety factor as the headline shows it, or a dash where there is none.</summary>
    [ObservableProperty]
    public partial string HeadlineValue { get; set; } = "";

    /// <summary>"breaks" under 1, "marginal" from 1 to 2, "holds" from 2, "none" without a safety factor: the headline's colour.</summary>
    [ObservableProperty]
    public partial string HeadlineRating { get; set; } = "none";

    [ObservableProperty]
    public partial string HeadlineVerdict { get; set; } = "";

    /// <summary>How and where the part breaks first.</summary>
    [ObservableProperty]
    public partial string HeadlineDetail { get; set; } = "";

    [ObservableProperty]
    public partial string HeadlineLoadCase { get; set; } = "";

    public ObservableCollection<Fact> ResultFacts { get; } = [];

    void DescribeHeadline(SolvedJob solved)
    {
        var result = solved.Result;
        var final = result.Final;
        HeadlineLoadCase = solved.LoadCaseName;
        ResultFacts.Clear();
        if (solved.Failure is { } failure && solved.Strength is not null)
        {
            var factor = failure.SafetyFactor;
            var mesh = result.Mesh;
            var grid = mesh.Grid;
            var weakest = mesh.Cells[failure.Weakest];
            var at = ResultMarks.Centre(grid, weakest);
            var feature = grid.Feature(weakest.I, weakest.J, weakest.K) is { } f ? $", {f.DisplayName().ToLowerInvariant()}" : "";
            HeadlineValue = factor.ToString("0.00");
            HeadlineRating = factor < 1 ? "breaks" : factor < 2 ? "marginal" : "holds";
            HeadlineVerdict = factor < 1
                ? $"Breaks under these loads: it holds {factor:0%} of them."
                : $"Holds these loads, and starts to break at {factor:0.##} × them.";
            HeadlineDetail = $"{failure.Mode[failure.Weakest].DisplayName()} first, in layer {weakest.K + 1}{feature}, at ({at.X:0.0}, {at.Y:0.0}, {at.Z:0.0}) mm.";
            if (solved.Reach.Where(r => r.IsLoad).Select(r => $"{r.Name} {factor * r.Force.Length():0.#} N").ToList() is { Count: > 0 } loads)
                ResultFacts.Add(new("Breaks at", string.Join(", ", loads)));
            var line = mesh.Cells[failure.WeakestLayerLine];
            ResultFacts.Add(new("Weakest layer line", $"SF {failure.LayerLineSafetyFactor:0.00}, under layer {line.K + 1}"));
        }
        else
        {
            HeadlineValue = "–";
            HeadlineRating = "none";
            HeadlineVerdict = $"No safety factor: {solved.NoStrength}";
            HeadlineDetail = "";
        }
        ResultFacts.Add(new("Max von Mises", $"{final.MaxVonMises:0.00} MPa"));
        ResultFacts.Add(new("Max displacement", $"{solved.MaxDisplacement:0.000} mm"));
        ResultFacts.Add(new("Material", solved.Material.Material.Name));
        ResultFacts.Add(new("Solve", $"{result.Passes.Count} {(result.Passes.Count == 1 ? "pass" : "passes")}, {result.Time.TotalSeconds:0.0} s"));
    }
}
