using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Slicing;
using GcodeFem.Core.Study;

namespace GcodeFem.App;

/// <summary>A row of the comparison: an orientation, a candidate of the sweep or one solved by hand, and what has come of it.</summary>
public sealed partial class CompareRow : ObservableObject
{
    /// <summary>A candidate of the sweep, not tried yet.</summary>
    /// <param name="part">The model, to stand it as the candidate lies.</param>
    public CompareRow(OrientationCandidate candidate, TriangleMesh part)
    {
        Candidate = candidate;
        Label = $"#{candidate.Number}";
        Rotation = candidate.Rotation;
        Standing = part.Transformed(candidate.Matrix);
        Contact = candidate.Face.ContactArea.ToString("0.0", CultureInfo.InvariantCulture);
        Height = candidate.Height.ToString("0.0", CultureInfo.InvariantCulture);
        Overhang = candidate.OverhangArea.ToString("0", CultureInfo.InvariantCulture);
    }

    /// <summary>An orientation solved by hand: one load case, on the slice it was solved on.</summary>
    internal CompareRow(int number, PrintJob job, SolvedJob solved)
    {
        Label = $"by hand {number}";
        Rotation = Orientation.ToEulerDegrees(job.Slice.Placement.Rotation);
        Standing = job.Slice.PrintMesh;
        Height = Standing.Bounds.Size.Z.ToString("0.0", CultureInfo.InvariantCulture);
        Overhang = OrientationCandidates.OverhangArea(Standing).ToString("0", CultureInfo.InvariantCulture);
        Hand = (job, solved);
        IsTicked = false;
        State = $"solved by hand, '{solved.LoadCaseName}' only";
        SafetyFactor = Number(solved.Solution.SafetyFactor, "0.00");
        Displacement = Number(solved.MaxDisplacement, "0.000");
        (PrintTime, Filament, Support) = Print(job.Part);
    }

    /// <summary>Null for an orientation solved by hand.</summary>
    public OrientationCandidate? Candidate { get; }

    public string Label { get; }

    /// <summary>Degrees about X, then Y, then Z, as the rotation fields take them.</summary>
    public Vector3 Rotation { get; }

    public string RotationText => $"{Tidy(Rotation.X)}, {Tidy(Rotation.Y)}, {Tidy(Rotation.Z)}";

    /// <summary>Two decimals, and never a minus zero.</summary>
    static string Tidy(float degrees) => (MathF.Round(degrees, 2) + 0f).ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>The part turned as it would print (print frame).</summary>
    public TriangleMesh Standing { get; }

    public string Contact { get; } = "";
    public string Height { get; }
    public string Overhang { get; }

    /// <summary>Only the sweep's candidates can be ticked off.</summary>
    public bool CanTick => Candidate is not null;

    [ObservableProperty]
    public partial bool IsTicked { get; set; } = true;

    [ObservableProperty]
    public partial string Rank { get; set; } = "";

    [ObservableProperty]
    public partial string State { get; set; } = "to try";

    /// <summary>Of the full solve: the lowest of the load cases.</summary>
    [ObservableProperty]
    public partial string SafetyFactor { get; set; } = "";

    [ObservableProperty]
    public partial string ScreenSafetyFactor { get; set; } = "";

    [ObservableProperty]
    public partial string Displacement { get; set; } = "";

    [ObservableProperty]
    public partial string PrintTime { get; set; } = "";

    [ObservableProperty]
    public partial string Filament { get; set; } = "";

    [ObservableProperty]
    public partial string Support { get; set; } = "";

    /// <summary>The sweep's record of this candidate, from the last sweep that tried it.</summary>
    public SweepEntry? Entry { get; private set; }

    internal (PrintJob Job, SolvedJob Solved)? Hand { get; }

    public bool HasResult => Hand is not null || Entry?.Governing is not null;

    // Made when first shown or opened, and kept: the views cache what they build on them.
    PrintJob? job;
    SolvedJob? solved;

    /// <summary>Its print, for the views; null until it has been sliced.</summary>
    internal PrintJob? Job(Core.Materials.MaterialLibrary materials) =>
        Hand?.Job ?? (Entry?.Part is { } part ? job ??= MainViewModel.Job(part, materials) : null);

    /// <summary>Its result as the views show it: of the load case that governs, of the full solve where there is one.</summary>
    internal SolvedJob? Solved() =>
        Hand?.Solved ?? (Entry?.Governing is { } governing ? solved is { } kept && kept.Solution == governing ? kept : solved = new SolvedJob(governing) : null);

    /// <summary>A sweep is to try it.</summary>
    public void Start(SweepEntry entry)
    {
        (Entry, job, solved) = (entry, null, null);
        Refresh();
    }

    /// <summary>The results of <paramref name="old"/>, a row of the same orientation from before the list was made again.</summary>
    public void TakeOver(CompareRow old)
    {
        (IsTicked, Entry, job, solved) = (old.IsTicked, old.Entry, old.job, old.solved);
        Refresh();
    }

    /// <summary>Brings the numbers in line with what the sweep has found so far.</summary>
    public void Refresh()
    {
        if (Entry is not { } e) return;
        State = e.Outcomes is null
            ? e.Error is { } error ? $"failed: {error}" : "waiting"
            : e.Full is not null ? "solved in full" : e.Error is { } late ? $"screened; in full it failed: {late}" : "screened";
        // Such as a part that hangs in the air without support: the slicer's own word on the orientation.
        if (e.Part?.Slice.Report.Warning is { Length: > 0 } warning) State += $"; the slicer warns: {warning}";
        SafetyFactor = e.Full is null ? "" : Number(e.SafetyFactor, "0.00");
        ScreenSafetyFactor = e.Screened is null ? "" : Number(e.Screened.Min(o => o.SafetyFactor), "0.00");
        Displacement = e.Outcomes is null ? "" : Number(e.MaxDisplacement, "0.000");
        (PrintTime, Filament, Support) = e.Part is null ? ("", "", "") : Print(e.Part);
    }

    static (string Time, string Grams, string Support) Print(PrintedPart part) =>
        (TimeSpan.FromSeconds(part.Slice.Report.PrintSeconds).ToString(@"h\:mm", CultureInfo.InvariantCulture),
         part.Slice.Report.FilamentGrams.ToString("0.0", CultureInfo.InvariantCulture),
         part.SupportVolume.ToString("0", CultureInfo.InvariantCulture));

    static string Number(double value, string format) => double.IsFinite(value) ? value.ToString(format, CultureInfo.InvariantCulture) : "";
}

/// <summary>
/// The part of the view model that compares orientations: the faces the part can be laid on, the
/// sweep that tries them, and the table and the compare view that set them side by side with what
/// has been solved by hand.
/// </summary>
public sealed partial class MainViewModel
{
    List<LayFace>? layFaces; // of Part, found when first needed
    Matrix4x4 sweepBasis = Matrix4x4.Identity; // how the part lay when the candidates were listed: each is the shortest turn from there
    int solvedByHand;
    CancellationTokenSource? sweepCancel;

    /// <summary>The candidates of the sweep, then the orientations solved by hand.</summary>
    public ObservableCollection<CompareRow> CompareRows { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenCompareRowCommand))]
    public partial CompareRow? SelectedCompareRow { get; set; }

    /// <summary>The smallest contact with the bed that a face must have to be tried (mm²), as typed; empty until it is given.</summary>
    [ObservableProperty]
    public partial string MinContactText { get; set; } = "";

    /// <summary>How many of the largest faces are tried, as typed; empty for all of them.</summary>
    [ObservableProperty]
    public partial string CountText { get; set; } = "";

    /// <summary>Each face also spun on the bed by 45° and 90°.</summary>
    [ObservableProperty]
    public partial bool Spins { get; set; }

    /// <summary>What the sweep will try and roughly what that costs; or why it cannot start.</summary>
    [ObservableProperty]
    public partial string SweepText { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartSweepCommand), nameof(CancelSweepCommand))]
    public partial bool IsSweeping { get; set; }

    public bool IsCompareView => View == ViewMode.Compare;

    partial void OnMinContactTextChanged(string value) => RefreshCandidates();
    partial void OnCountTextChanged(string value) => RefreshCandidates();
    partial void OnSpinsChanged(bool value) => RefreshCandidates();
    partial void OnSelectedCompareRowChanged(CompareRow? value) => RedrawCompare();

    /// <summary>The sweep's settings as typed; what cannot be read counts as not given, and says why in <paramref name="error"/>.</summary>
    SweepSettings ReadSweepSettings(out string? error)
    {
        error = null;
        float? least = null;
        int? count = null;
        if (MinContactText.Trim() is { Length: > 0 } area)
        {
            if (float.TryParse(area, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value >= 0) least = value;
            else error = $"'{area}' is not an area in mm².";
        }
        if (CountText.Trim() is { Length: > 0 } number)
        {
            if (int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0) count = value;
            else error = $"'{number}' is not a number of faces.";
        }
        return new SweepSettings(least, count, Spins);
    }

    /// <summary>Puts the study's sweep settings into the fields.</summary>
    void ShowSweepSettings(SweepSettings settings)
    {
        MinContactText = settings.MinContact?.ToString("0.##", CultureInfo.InvariantCulture) ?? "";
        CountText = settings.Count?.ToString(CultureInfo.InvariantCulture) ?? "";
        Spins = settings.Spins;
    }

    /// <summary>
    /// Lists the candidates as the settings say, from how the part lay when they were first listed.
    /// An orientation listed before keeps its tick and its results; those solved by hand stay.
    /// </summary>
    void RefreshCandidates()
    {
        if (Part is not { } part || IsSweeping) return;
        if (!CompareRows.Any(row => row.CanTick) && View != ViewMode.Compare) return; // nothing to keep up to date yet
        var settings = ReadSweepSettings(out var error);
        layFaces ??= LayFaces.Find(part);
        var candidates = OrientationCandidates.Find(part, sweepBasis, settings, OverhangDegrees(), layFaces);

        var before = CompareRows.Where(row => row.CanTick).GroupBy(row => row.Rotation).ToDictionary(group => group.Key, group => group.First());
        var byHand = CompareRows.Where(row => !row.CanTick).ToList();
        foreach (var row in CompareRows) row.PropertyChanged -= OnCompareRowChanged;
        CompareRows.Clear();
        foreach (var candidate in candidates)
        {
            var row = new CompareRow(candidate, part);
            if (before.TryGetValue(candidate.Rotation, out var old)) row.TakeOver(old);
            row.PropertyChanged += OnCompareRowChanged;
            CompareRows.Add(row);
        }
        foreach (var row in byHand) CompareRows.Add(row);
        RankRows();

        SweepText = error ?? $"{layFaces.Count} faces the part can lie on (planes of its convex hull that it touches with area); {candidates.Count} to try" +
            (settings.MinContact is null
                ? ". Type the smallest contact with the bed that counts, in mm², to start the sweep."
                : $". Each is a slice and a quick solve in each of the {LoadCases.Count} load cases, seconds each (slices come from the cache if made before); the best 3 are then solved in full, which takes longer.");
        StartSweepCommand.NotifyCanExecuteChanged();
        RedrawCompare();
    }

    void OnCompareRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CompareRow.IsTicked)) return;
        StartSweepCommand.NotifyCanExecuteChanged();
        RedrawCompare();
    }

    /// <summary>The process's support threshold, for the overhangs; Bambu Studio's default where it gives none.</summary>
    float OverhangDegrees() =>
        presets is not null && Process is not null && presets.Contains(Process)
        && PresetLibrary.First(presets.Resolve(Process), "support_threshold_angle") is { } text
        && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees) && degrees > 0
            ? degrees
            : OrientationCandidates.DefaultOverhangDegrees;

    /// <summary>Lists the faces as the part lies now, and shows them side by side.</summary>
    [RelayCommand]
    public void PreviewFaces()
    {
        if (Part is null) return;
        sweepBasis = Rotation;
        View = ViewMode.Compare; // lists them if there are none yet
        RefreshCandidates();
        FitCompare();
    }

    void FitCompare()
    {
        if (CompareRows.Count > 0) FitRequested?.Invoke(CompareLayout.Of([.. CompareRows.Select(row => row.Standing)]).Extent);
    }

    bool CanStartSweep() =>
        CanSlice && !IsBusy && ReadSweepSettings(out var error) is { CanStart: true } && error is null && CompareRows.Any(row => row.CanTick && row.IsTicked);

    /// <summary>Tries the ticked candidates: each sliced and screened, the best 3 solved in full, all ranked in the table as they come.</summary>
    [RelayCommand(CanExecute = nameof(CanStartSweep))]
    public async Task StartSweepAsync()
    {
        if (Part is not { } part || bambu is null || presets is null || Process is null || Filament is null) return;
        var rows = CompareRows.Where(row => row.CanTick && row.IsTicked).ToList();
        if (rows.Count == 0) return;
        IsBusy = IsSweeping = true;
        using var cancel = sweepCancel = new CancellationTokenSource();
        View = ViewMode.Compare;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            if (Interfaces.FirstOrDefault(item => item.HasValueError) is { } unread)
                throw new FormatException($"The value of '{unread.Name}' cannot be read: {unread.ValueError}");
            var study = ToStudy();
            var slicer = new BambuSlicer(bambu, presets, BambuSlicer.DefaultCacheDirectory);
            var sweep = new OrientationSweep(study, rows.Select(row => row.Candidate!), OrientationSweep.Slicing(slicer, part, machine, Process, Filament), materials, LinearSolvers.Best());
            for (var n = 0; n < rows.Count; n++) rows[n].Start(sweep.Entries[n]);
            RankRows();
            // Told on the sweep's thread, shown on this one.
            var progress = new Progress<SweepEntry>(entry =>
            {
                rows.FirstOrDefault(row => row.Entry == entry)?.Refresh();
                RankRows();
                RedrawCompare();
            });
            BeginWork("Sweeping", sweep.Now);
            await Task.Run(() => sweep.RunAsync(((IProgress<SweepEntry>)progress).Report, cancel.Token));
            if (!ReferenceEquals(Part, part)) return;

            var best = OrientationSweep.Ranked(sweep.Entries).FirstOrDefault(e => e.Outcomes is not null);
            var bestRow = rows.FirstOrDefault(row => row.Entry == best);
            Status = bestRow is null
                ? "None of the orientations could be sliced and solved; the table says why."
                : $"Swept {rows.Count} orientations in {watch.Elapsed.TotalSeconds:0} s. The best is {bestRow.Label}, lying at {bestRow.RotationText}: safety factor {bestRow.SafetyFactor}. Select a row and open it to see its result.";
        }
        catch (OperationCanceledException)
        {
            Status = "The sweep was stopped. What it had done is in the table.";
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            Status = "The sweep failed.";
            Failed?.Invoke("The sweep failed", ex.Message);
        }
        finally
        {
            EndWork();
            sweepCancel = null;
            IsBusy = IsSweeping = false;
            foreach (var row in rows) row.Refresh();
            RankRows();
            OpenCompareRowCommand.NotifyCanExecuteChanged();
            RedrawCompare();
        }
    }

    /// <summary>Stops the sweep after the step it is on.</summary>
    [RelayCommand(CanExecute = nameof(IsSweeping))]
    void CancelSweep() => sweepCancel?.Cancel();

    /// <summary>The sweep's ranking in the table, as far as it has come; orientations solved by hand are not ranked.</summary>
    void RankRows()
    {
        var ranked = OrientationSweep.Ranked(CompareRows.Where(row => row.Entry?.Outcomes is not null).Select(row => row.Entry!));
        foreach (var row in CompareRows) row.Rank = row.Entry is { } entry && ranked.IndexOf(entry) is >= 0 and var at ? (at + 1).ToString(CultureInfo.InvariantCulture) : "";
    }

    /// <summary>A solve by hand joins the comparison.</summary>
    void AddSolvedByHand(PrintJob current, SolvedJob solved)
    {
        CompareRows.Add(new CompareRow(++solvedByHand, current, solved));
        RedrawCompare();
    }

    bool CanOpenCompareRow() => SelectedCompareRow is { HasResult: true } && !IsBusy;

    /// <summary>Opens the selected orientation's result in the views: its print, its cells, the load case that governs it.</summary>
    [RelayCommand(CanExecute = nameof(CanOpenCompareRow))]
    public void OpenCompareRow()
    {
        if (SelectedCompareRow is not { } row || row.Job(materials) is not { } print || row.Solved() is not { } solved) return;
        (RotationX, RotationY, RotationZ) = (Math.Round(row.Rotation.X, 4), Math.Round(row.Rotation.Y, 4), Math.Round(row.Rotation.Z, 4));
        SelectedLoadCase = LoadCases.FirstOrDefault(c => string.Equals(c.Name.Trim(), solved.LoadCaseName, StringComparison.Ordinal)) ?? SelectedLoadCase;
        ShowPrint(print, (Process, Filament));
        ShowSolution(print, solved);
        var how = row.Hand is not null ? "solved by hand" : row.Entry?.Full is not null ? "solved in full" : "as the screening left it: refined only to elements of 2³ cells, so its safety factor reads high";
        Status = $"{row.Label}: the result of '{solved.LoadCaseName}', the load case that governs it, {how}.";
    }

    /// <summary>The compare view's copies, a row each.</summary>
    IReadOnlyList<CompareCopy> CompareCopies() =>
    [
        .. CompareRows.Select(row => new CompareCopy(
            row.Label,
            row.Standing,
            row.Candidate?.Face.Contact ?? [],
            row.Candidate?.Matrix ?? row.Hand!.Value.Job.Slice.Placement.Rotation,
            row.CanTick && row.IsTicked,
            row.Solved(),
            row.Hand is not null ? $"SF {row.SafetyFactor}"
            : row.Entry?.Full is not null ? $"SF {row.SafetyFactor}"
            : row.Entry?.Screened is not null ? $"screened {row.ScreenSafetyFactor}" : "")),
    ];

    /// <summary>A click on a copy in the compare view selects its row, and ticks it off or on again.</summary>
    void PickCopy(Vector3 origin, Vector3 direction)
    {
        var (shifts, _, _) = CompareLayout.Of([.. CompareRows.Select(row => row.Standing)]);
        var (best, nearest) = (-1, float.PositiveInfinity);
        for (var n = 0; n < CompareRows.Count; n++)
            if (MeshPicker.Pick(CompareRows[n].Standing, origin - shifts[n], direction) is { } hit && hit.Distance < nearest)
                (best, nearest) = (n, hit.Distance);
        if (best < 0) return;
        var row = CompareRows[best];
        SelectedCompareRow = row;
        if (row.CanTick && !IsSweeping) row.IsTicked = !row.IsTicked;
    }

    void RedrawCompare()
    {
        if (View == ViewMode.Compare) Redraw();
    }

    /// <summary>A new model: nothing to compare yet.</summary>
    void ResetComparison()
    {
        foreach (var row in CompareRows) row.PropertyChanged -= OnCompareRowChanged;
        CompareRows.Clear();
        SelectedCompareRow = null;
        layFaces = null;
        solvedByHand = 0;
        SweepText = "";
    }
}
