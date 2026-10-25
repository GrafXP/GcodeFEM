using System.Numerics;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Materials;
using GcodeFem.Core.Slicing;
using GcodeFem.Core.Study;

namespace GcodeFem.Tests.Study;

/// <summary>
/// The sweep's own work on the printed cube of the samples, which needs no slicer: every candidate gets
/// that slice, but for one the slicer refuses. Screening, solving the best in full, ranking, progress, cancelling.
/// </summary>
public class OrientationSweepTests
{
    static readonly TriangleMesh Cube = Stl.Read(TestPaths.Sample("cube20.stl"));

    static readonly Lazy<PrintedPart> Printed = new(() =>
    {
        var report = SlicerReport.Read(TestPaths.Sample("cube20_X1C_PLA.result.json"));
        var placement = Placement.FromSlice(Matrix4x4.Identity, Cube.Bounds, report.ObjectBounds);
        return PrintedPart.Read(new SliceResult(TestPaths.Root, TestPaths.Sample("cube20_X1C_PLA.gcode"), report, placement, Cube, true, TimeSpan.Zero));
    });

    /// <summary>Held at its bottom face and pushed at its top, sideways or forwards; or only sideways.</summary>
    static PartStudy Study(bool bothWays = true)
    {
        int[] Facing(Vector3 way) => [.. Enumerable.Range(0, Cube.TriangleCount).Where(t => Vector3.Dot(Cube.Normal(t), way) > 0.99f)];
        var study = new PartStudy { ModelPath = TestPaths.Sample("cube20.stl"), GeometryHash = Cube.GeometryHash() };
        var bottom = new PartInterface { Name = "Bottom", Kind = InterfaceKind.Fixed, Triangles = Facing(-Vector3.UnitZ) };
        var top = new PartInterface { Name = "Top", Kind = InterfaceKind.Force, Triangles = Facing(Vector3.UnitZ) };
        study.Interfaces.AddRange([bottom, top]);
        var forwards = new StudyLoadCase { Name = "Forwards" };
        forwards.Loads[top] = new LoadValue(new Vector3(0, 150, 0));
        var sideways = new StudyLoadCase { Name = "Sideways" };
        sideways.Loads[top] = new LoadValue(new Vector3(100, 0, 0));
        study.LoadCases.AddRange(bothWays ? [sideways, forwards] : [sideways]);
        return study;
    }

    static OrientationSweep Sweep(int count, int best, bool bothWays = true) => new(
        Study(bothWays),
        OrientationCandidates.Find(Cube, Matrix4x4.Identity, new SweepSettings(MinContact: 100, Count: count)),
        (candidate, step, _) =>
        {
            step("from the samples");
            return candidate.Number == 2 ? throw new SlicerException("The part does not fit on the plate.") : Task.FromResult(Printed.Value);
        },
        MaterialLibrary.Load(TestPaths.Materials),
        LinearSolvers.Best(),
        new SweepOptions(Best: best));

    [Fact]
    public async Task Every_candidate_is_screened_the_best_solved_in_full_and_all_ranked()
    {
        var sweep = Sweep(count: 3, best: 1);
        var seen = new List<int>();
        var progress = new List<GcodeFem.Core.WorkProgress>();
        using var timer = new Timer(_ =>
        {
            lock (progress) progress.Add(sweep.Now());
        }, null, 0, 20);

        await sweep.RunAsync(entry => seen.Add(entry.Candidate.Number));
        await timer.DisposeAsync();

        var (first, refused, third) = (sweep.Entries[0], sweep.Entries[1], sweep.Entries[2]);
        // Told of each candidate as it is screened, then of the one solved in full.
        Assert.Equal([1, 2, 3, 1], seen);
        Assert.Contains("does not fit", refused.Error);
        Assert.Null(refused.Outcomes);
        Assert.Equal(["Sideways", "Forwards"], first.Screened!.Select(o => o.LoadCase));
        Assert.NotNull(first.Full);
        Assert.Null(third.Full);
        // The same slice twice: the tie goes to the first, which alone is solved in full and so ranks before the other.
        Assert.Equal((1, 2, 0), (first.Rank, third.Rank, refused.Rank));
        Assert.Equal([first, third, refused], OrientationSweep.Ranked(sweep.Entries));
        // The governing load case is the one with the lowest safety factor, of the full solve.
        Assert.Equal(first.Full!.MinBy(o => o.SafetyFactor)!.LoadCase, first.Governing!.LoadCase);
        Assert.Equal(first.SafetyFactor, first.Governing.SafetyFactor, 9);
        Assert.Equal(first.Full!.Min(o => o.SafetyFactor), first.SafetyFactor);
        Assert.True(first.Governing.Result.Passes.Count > third.Governing!.Result.Passes.Count, "the full solve refines further than the screening");

        // The bar never goes back, names what it does, and ends full.
        lock (progress)
        {
            for (var n = 1; n < progress.Count; n++) Assert.True(progress[n].Done >= progress[n - 1].Done, $"went back at sample {n}");
            Assert.Contains(progress, p => p.Step.Contains("candidate 3 of 3"));
            Assert.Contains(progress, p => p.Step.Contains("in full 1 of 1"));
        }
        Assert.Equal((1.0, "ranking"), (sweep.Now().Done!.Value, sweep.Now().Step));
    }

    [Fact]
    public async Task A_cancelled_sweep_stops_before_the_next_candidate()
    {
        var sweep = Sweep(count: 3, best: 3, bothWays: false);
        using var cancel = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sweep.RunAsync(_ => cancel.Cancel(), cancel.Token));

        Assert.NotNull(sweep.Entries[0].Screened);
        Assert.All(sweep.Entries.Skip(1), e => Assert.True(e.Outcomes is null && e.Error is null));
    }

    [Fact]
    public void Those_solved_in_full_rank_first_then_the_screened_then_the_failed()
    {
        static SweepEntry Entry(int number, double safety, float displacement, bool full, double vonMises = 10)
        {
            var face = new LayFace(-Vector3.UnitZ, 100, []);
            var entry = new SweepEntry(new OrientationCandidate(number, face, 0, Vector3.Zero, 10, 0));
            IReadOnlyList<CaseOutcome> outcomes = [new("A", safety, displacement, vonMises), new("B", safety + 1, displacement / 2, vonMises / 2)];
            entry.Screened = outcomes;
            if (full) entry.Full = outcomes;
            return entry;
        }
        var failed = new SweepEntry(new OrientationCandidate(9, new LayFace(Vector3.UnitZ, 1, []), 0, Vector3.Zero, 1, 0)) { Error = "refused" };
        SweepEntry[] entries =
        [
            failed,
            Entry(1, 2.5, 1, full: false), // best screening, but only screened
            Entry(2, 1.2, 1, full: true),
            Entry(3, 1.5, 1, full: true),
            Entry(4, 1.0, 2, full: false),
            Entry(5, 1.004, 1, full: false), // 1.00 as the table shows it: the smaller displacement wins the tie
        ];

        Assert.Equal([3, 2, 1, 5, 4, 9], OrientationSweep.Ranked(entries).Select(e => e.Candidate.Number));
        Assert.Equal(1.2, entries[2].SafetyFactor);
        Assert.Equal(1, entries[2].MaxDisplacement);

        // Without strengths, the lowest stress first.
        SweepEntry[] weak = [Entry(1, double.NaN, 1, true, vonMises: 30), Entry(2, double.NaN, 1, true, vonMises: 20)];
        Assert.Equal([2, 1], OrientationSweep.Ranked(weak).Select(e => e.Candidate.Number));
    }
}
