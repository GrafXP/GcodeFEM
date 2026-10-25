using System.Diagnostics;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Materials;
using GcodeFem.Core.Slicing;

namespace GcodeFem.Core.Study;

/// <summary>What one load case came to on one candidate.</summary>
/// <param name="SafetyFactor">NaN if the material gives no strength.</param>
/// <param name="MaxDisplacement">mm, the most any point of the part moves.</param>
public sealed record CaseOutcome(string LoadCase, double SafetyFactor, float MaxDisplacement, double MaxVonMises)
{
    public static CaseOutcome Of(PartSolution solution) => new(solution.LoadCase, solution.SafetyFactor, solution.MaxDisplacement, solution.Result.Final.MaxVonMises);
}

/// <summary>A candidate of an orientation sweep and what has come of it, written by the sweep as it goes.</summary>
public sealed class SweepEntry(OrientationCandidate candidate)
{
    double? support;

    public OrientationCandidate Candidate { get; } = candidate;

    public PrintedPart? Part { get; set; }

    /// <summary>Per load case, from the screening: the adaptive loop stopped short of bead cells.</summary>
    public IReadOnlyList<CaseOutcome>? Screened { get; set; }

    /// <summary>Per load case, from the full adaptive loop; null for a candidate that was only screened.</summary>
    public IReadOnlyList<CaseOutcome>? Full { get; set; }

    /// <summary>The solution of the load case that governs, the lowest safety factor (without strengths the highest stress): of the full solve where there was one.</summary>
    public PartSolution? Governing { get; set; }

    /// <summary>Why the candidate could not be sliced or solved.</summary>
    public string? Error { get; set; }

    /// <summary>Its place in the ranking, 1 for the best; 0 until ranked, and for a candidate that came to nothing.</summary>
    public int Rank { get; set; }

    public IReadOnlyList<CaseOutcome>? Outcomes => Full ?? Screened;

    /// <summary>The lowest of the load cases; NaN until solved, and without strengths.</summary>
    public double SafetyFactor => Outcomes is { Count: > 0 } outcomes ? outcomes.Min(o => o.SafetyFactor) : double.NaN;

    /// <summary>The most of the load cases (mm).</summary>
    public float MaxDisplacement => Outcomes is { Count: > 0 } outcomes ? outcomes.Max(o => o.MaxDisplacement) : float.NaN;

    public double MaxVonMises => Outcomes is { Count: > 0 } outcomes ? outcomes.Max(o => o.MaxVonMises) : double.NaN;

    /// <summary>The slicer's estimate (s).</summary>
    public double PrintSeconds => Part?.Slice.Report.PrintSeconds ?? double.NaN;

    public double FilamentGrams => Part?.Slice.Report.FilamentGrams ?? double.NaN;

    /// <summary>mm³.</summary>
    public double SupportVolume => Part is null ? double.NaN : support ??= Part.SupportVolume;
}

/// <param name="Best">How many of the candidates, by the screening, are then solved in full.</param>
/// <param name="ScreenLevel">The screening refines down to elements of 2^level cells a side and leaves out the finer passes, the dearest. At 1 it ranked the bracket's six faces as the full solves do; the coarse pass alone (3) and one more (2) did not.</param>
public sealed record SweepOptions(int Best = 3, int ScreenLevel = 1);

/// <summary>
/// An orientation sweep (PLAN.md §11). Each candidate is sliced and then screened in every load case
/// with the adaptive loop stopped short of bead cells, which takes seconds. The best few by that are
/// solved in full, and all are ranked. The screening reads the safety factor high (up to 1.3 × on the
/// bracket), so it only ranks the candidates; the numbers that count are those of the full solves.
/// A candidate that cannot be sliced or solved is noted and the sweep goes on. It can be cancelled
/// between steps. <see cref="Now"/> says where it stands, from any thread.
/// </summary>
public sealed class OrientationSweep
{
    readonly PartStudy study;
    readonly Func<OrientationCandidate, Action<string>, CancellationToken, Task<PrintedPart>> slice;
    readonly MaterialLibrary materials;
    readonly ILinearSolver solver;
    readonly SweepOptions options;
    readonly Stopwatch watch = new();
    readonly Lock gate = new();

    // Where the sweep stands, for Now(): the candidate at hand, what it is doing, and how long the candidates done took.
    SweepEntry? current;
    string doing = "starting";
    bool inFull, sliced, finished;
    int screened, solvedInFull, fullPlanned, caseIndex;
    SolveEstimate? estimate;
    TimeSpan screenTime, fullTime;
    double lastDone;

    /// <param name="slice">Slices the part as a candidate lies, telling what it does.</param>
    public OrientationSweep(PartStudy study, IEnumerable<OrientationCandidate> candidates, Func<OrientationCandidate, Action<string>, CancellationToken, Task<PrintedPart>> slice,
        MaterialLibrary materials, ILinearSolver solver, SweepOptions? options = null)
    {
        (this.study, this.slice, this.materials, this.solver, this.options) = (study, slice, materials, solver, options ?? new SweepOptions());
        Entries = [.. candidates.Select(candidate => new SweepEntry(candidate))];
        fullPlanned = Math.Min(this.options.Best, Entries.Count);
    }

    /// <summary>The slicer's part of a sweep: the part turned as the candidate lies, with the study's presets.</summary>
    public static Func<OrientationCandidate, Action<string>, CancellationToken, Task<PrintedPart>> Slicing(BambuSlicer slicer, Geometry.TriangleMesh part, string machine, string process, string filament) =>
        (candidate, step, cancellationToken) => PrintedPart.SliceAsync(slicer, new SliceRequest(part, candidate.Matrix, machine, process, filament), step, cancellationToken);

    public IReadOnlyList<SweepEntry> Entries { get; }

    /// <param name="onChanged">Called when a candidate has been screened or solved in full, from the sweep's thread.</param>
    public async Task RunAsync(Action<SweepEntry>? onChanged = null, CancellationToken cancellationToken = default)
    {
        if (study.LoadCases.Count == 0) throw new InvalidOperationException("The study has no load case to solve.");
        watch.Start();
        // A study that solves every bead cell (level 0) still screens on the octree, or the screening would be a full solve.
        var screen = new AdaptiveOptions(RootLevel: study.MeshLevel is > 0 ? study.MeshLevel : null, MinFill: PrintedPart.MinFill, KeepPassFields: true, FinestLevel: options.ScreenLevel);
        var full = new AdaptiveOptions(RootLevel: study.MeshLevel, MinFill: PrintedPart.MinFill, KeepPassFields: true);

        foreach (var entry in Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var started = watch.Elapsed;
            lock (gate) (current, doing, sliced, caseIndex) = (entry, "slicing", false, 0);
            try
            {
                entry.Part = await slice(entry.Candidate, step => Doing($"slicing: {step}"), cancellationToken).ConfigureAwait(false);
                lock (gate) sliced = true;
                (entry.Screened, entry.Governing) = await SolveAll(entry, screen, "screening", cancellationToken).ConfigureAwait(false);
                // A part small enough to be solved directly, every bead cell an element, was screened in full already.
                if (entry.Governing.Result.RootLevel == 0) entry.Full = entry.Screened;
            }
            catch (Exception ex) when (Expected(ex))
            {
                entry.Error = ex.Message;
            }
            lock (gate) (screened, screenTime) = (screened + 1, screenTime + watch.Elapsed - started);
            onChanged?.Invoke(entry);
        }

        // The best by the screening; those screened in full already need nothing more.
        var best = Order(Entries.Where(entry => entry.Screened is not null), entry => entry.Screened, fullFirst: false).Take(options.Best).Where(entry => entry.Full is null).ToList();
        lock (gate) (inFull, fullPlanned) = (true, best.Count);
        foreach (var entry in best)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var started = watch.Elapsed;
            lock (gate) (current, caseIndex) = (entry, 0);
            try
            {
                (entry.Full, entry.Governing) = await SolveAll(entry, full, "in full", cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (Expected(ex))
            {
                // The screening stands; the candidate ranks by it.
                entry.Error = ex.Message;
            }
            lock (gate) (solvedInFull, fullTime) = (solvedInFull + 1, fullTime + watch.Elapsed - started);
            onChanged?.Invoke(entry);
        }

        var ranked = Ranked(Entries);
        for (var n = 0; n < ranked.Count; n++) ranked[n].Rank = ranked[n].Outcomes is null ? 0 : n + 1;
        lock (gate) finished = true;
    }

    /// <summary>Every load case on the candidate's part; and of them, the solution that governs.</summary>
    async Task<(IReadOnlyList<CaseOutcome>, PartSolution)> SolveAll(SweepEntry entry, AdaptiveOptions adaptive, string how, CancellationToken cancellationToken)
    {
        var part = entry.Part!;
        var material = materials.Match(part.Toolpath);
        var outcomes = new List<CaseOutcome>();
        PartSolution? governing = null;
        for (var n = 0; n < study.LoadCases.Count; n++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var loadCase = study.LoadCases[n];
            var solving = new SolveEstimate();
            lock (gate) (estimate, caseIndex, doing) = (solving, n, $"{how}, '{loadCase.Name}'");
            var solution = await Task.Run(() => PartSolution.Solve(part, study, loadCase, material, solver, adaptive, solving.Passed, solving.Solving), cancellationToken).ConfigureAwait(false);
            solving.Finished();
            outcomes.Add(CaseOutcome.Of(solution));
            if (governing is null || Governs(solution, governing)) governing = solution;
        }
        lock (gate) estimate = null;
        return (outcomes, governing!);
    }

    static bool Governs(PartSolution solution, PartSolution than) =>
        double.IsNaN(solution.SafetyFactor) || double.IsNaN(than.SafetyFactor)
            ? solution.Result.Final.MaxVonMises > than.Result.Final.MaxVonMises
            : solution.SafetyFactor < than.SafetyFactor;

    void Doing(string what)
    {
        lock (gate) doing = what;
    }

    /// <summary>
    /// Where the sweep stands. A candidate's share comes from its steps: its slice, which tells only its
    /// time, then a step per load case, each as far as its own <see cref="SolveEstimate"/> says. The time
    /// left follows from the time per candidate so far, screened and solved in full apart.
    /// </summary>
    public WorkProgress Now()
    {
        lock (gate)
        {
            var elapsed = watch.Elapsed;
            if (finished) return new WorkProgress("ranking", elapsed, 1, TimeSpan.Zero);
            var cases = study.LoadCases.Count;
            var solving = estimate?.Now().Done ?? 0;
            var share = inFull ? (caseIndex + solving) / cases : sliced ? (1 + caseIndex + solving) / (1 + cases) : 0;

            // Until a candidate is done, guesses: a slice from the cache and a coarse pass take seconds, a full solve longer.
            var screenEach = screened > 0 ? screenTime.TotalSeconds / screened : 10;
            var fullEach = solvedInFull > 0 ? fullTime.TotalSeconds / solvedInFull : Math.Max(2 * screenEach, 5.0 * cases);
            var left = inFull
                ? (fullPlanned - solvedInFull - share) * fullEach
                : (Entries.Count - screened - share) * screenEach + fullPlanned * fullEach;
            left = Math.Max(0, left);
            lastDone = Math.Max(lastDone, Math.Min(elapsed.TotalSeconds / (elapsed.TotalSeconds + left), 0.99));

            var at = current is null ? "" : inFull
                ? $"in full {solvedInFull + 1} of {fullPlanned}, candidate {current.Candidate.Number}"
                : $"candidate {current.Candidate.Number} of {Entries.Count}";
            var step = estimate is { } now ? $"{at}: {doing}, {now.Now().Step}" : $"{at}: {doing}";
            return new WorkProgress(step, elapsed, lastDone, TimeSpan.FromSeconds(left));
        }
    }

    /// <summary>
    /// Best first: those solved in full, by their own numbers; then those only screened, by theirs; then
    /// those that came to nothing. Within each, by the lowest safety factor of the load cases to two
    /// decimals, as the table shows it (without strengths, by the highest stress), then by the largest
    /// displacement, the print time, the filament and the support.
    /// </summary>
    public static List<SweepEntry> Ranked(IEnumerable<SweepEntry> entries) => Order(entries, e => e.Outcomes, fullFirst: true);

    /// <summary>The same order by the numbers <paramref name="outcomes"/> picks; with <paramref name="fullFirst"/>, those solved in full before the rest.</summary>
    static List<SweepEntry> Order(IEnumerable<SweepEntry> entries, Func<SweepEntry, IReadOnlyList<CaseOutcome>?> outcomes, bool fullFirst)
    {
        double Safety(SweepEntry e) => outcomes(e) is { Count: > 0 } o ? o.Min(c => c.SafetyFactor) : double.NaN;
        double Most(SweepEntry e, Func<CaseOutcome, double> value) => outcomes(e) is { Count: > 0 } o ? o.Max(value) : double.NaN;
        return
        [
            .. entries
                .OrderBy(e => outcomes(e) is null)
                .ThenBy(e => fullFirst && e.Full is null)
                .ThenByDescending(e => double.IsNaN(Safety(e)) ? double.NegativeInfinity : Math.Round(Safety(e), 2))
                .ThenBy(e => double.IsNaN(Safety(e)) ? Most(e, c => c.MaxVonMises) : 0)
                .ThenBy(e => Most(e, c => c.MaxDisplacement))
                .ThenBy(e => e.PrintSeconds)
                .ThenBy(e => e.FilamentGrams)
                .ThenBy(e => e.SupportVolume)
                .ThenBy(e => e.Candidate.Number),
        ];
    }

    /// <summary>What can go wrong with one candidate and leave the others to be tried: the slicer refusing it, no mount reaching its cells.</summary>
    static bool Expected(Exception ex) =>
        ex is SlicerException or InvalidOperationException or InvalidDataException or IOException or ArgumentException or KeyNotFoundException or FormatException
        && ex is not OperationCanceledException;
}
