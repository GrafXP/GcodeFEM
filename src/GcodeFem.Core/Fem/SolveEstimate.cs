using System.Diagnostics;

namespace GcodeFem.Core.Fem;

/// <summary>
/// How far an adaptive solve has come and how long it has left, from what it has done so far.
/// Fed from the solving thread (<see cref="Solving"/>, <see cref="Passed"/>) and read from any
/// other (<see cref="Now"/>), as often as a progress bar likes.
/// <list type="bullet">
/// <item>A solve takes about root level + 1 passes: each refines one level until the hotspots
/// are bead cells (4 for the bracket and 5 for the cube at level 3, one at level 0). If it goes
/// on, one more is expected.</item>
/// <item>A pass takes a fixed part (refining, building the mesh, assembling) plus a time per
/// unknown. That time is read off the last pass big enough to show it; before one has run it
/// is a guess, and the estimate settles as the passes come in.</item>
/// <item>Passes to come grow by the factor the last two grew by (at most 5), less and less
/// with each one, as the refined zone stops spreading.</item>
/// <item>The native solver says nothing until it is done, so within a pass the bar runs on the
/// clock towards that pass's predicted end, and slows down rather than passing it. The share
/// done never goes back.</item>
/// </list>
/// </summary>
public sealed class SolveEstimate
{
    /// <summary>Seconds every pass takes besides its solve, measured at 0.3–0.6 s on the desktop.</summary>
    const double Overhead = 0.3;

    /// <summary>Seconds per unknown until a pass has shown it: 17–30 µs measured on the desktop.</summary>
    const double GuessedRate = 25e-6;

    /// <summary>A pass smaller than this is mostly overhead and says little about the time per unknown.</summary>
    const int RateFromDofs = 20_000;

    /// <summary>Within a pass, the bar stops short of its predicted end by this much.</summary>
    const double Margin = 0.95;

    readonly Func<TimeSpan> clock;
    readonly Lock gate = new();
    readonly List<(int Dofs, double Seconds)> passes = [];
    AdaptiveSolving? solving;
    int rootLevel = -1, beadDofs;
    double passesEnd, lastDone;
    bool finished;

    /// <param name="clock">Time since the solve began; a stopwatch started now if null.</param>
    public SolveEstimate(Func<TimeSpan>? clock = null)
    {
        var watch = Stopwatch.StartNew();
        this.clock = clock ?? (() => watch.Elapsed);
    }

    /// <summary>A pass's system is built and goes to the solver.</summary>
    public void Solving(AdaptiveSolving pass)
    {
        lock (gate)
        {
            (solving, rootLevel, beadDofs) = (pass, pass.RootLevel, pass.BeadDofs);
        }
    }

    /// <summary>A pass is solved and its stresses read.</summary>
    public void Passed(AdaptivePass pass)
    {
        lock (gate)
        {
            var now = clock().TotalSeconds;
            passes.Add((pass.Dofs, now - passesEnd));
            passesEnd = now;
            solving = null;
        }
    }

    /// <summary>The passes are over; what is left is reading the result.</summary>
    public void Finished()
    {
        lock (gate)
        {
            finished = true;
        }
    }

    public WorkProgress Now()
    {
        lock (gate)
        {
            var elapsed = clock();
            if (finished) return new WorkProgress("reading the result", elapsed, 1, TimeSpan.Zero);
            if (rootLevel < 0) return new WorkProgress("building the mesh", elapsed);

            var current = passes.Count;
            var expected = Math.Max(rootLevel + 1, current + 1);
            var growth = passes.Count >= 2 ? Math.Clamp((double)passes[^1].Dofs / passes[^2].Dofs, 1.5, 5) : 4;
            var dofs = solving is { } now ? now.Dofs : Math.Min(beadDofs, (int)(passes[^1].Dofs * growth));

            // This pass: as long as predicted, or longer if it already is.
            var t = elapsed.TotalSeconds;
            var total = passesEnd + Math.Max(Predict(dofs), (t - passesEnd) / Margin);
            for (var pass = current + 1; pass < expected; pass++)
            {
                growth = 1 + 0.7 * (growth - 1);
                dofs = Math.Min(beadDofs, (int)(dofs * growth));
                total += Predict(dofs);
            }

            lastDone = Math.Max(lastDone, Math.Min(t / total, 0.99));
            var step = solving is { } solved ? $"pass {current + 1} of about {expected}, {solved.Dofs:N0} unknowns" : $"refining after pass {current}";
            return new WorkProgress(step, elapsed, lastDone, TimeSpan.FromSeconds(Math.Max(0, total - t)));
        }
    }

    double Predict(int dofs) => Overhead + Rate() * dofs;

    /// <summary>Seconds per unknown, from the last pass big enough to tell.</summary>
    double Rate()
    {
        for (var n = passes.Count - 1; n >= 0; n--)
            if (passes[n].Dofs >= RateFromDofs) return Math.Max(0, passes[n].Seconds - Overhead) / passes[n].Dofs;
        return GuessedRate;
    }
}
