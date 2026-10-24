using GcodeFem.Core.Fem;

namespace GcodeFem.Tests.Fem;

/// <summary>The progress of an adaptive solve, replayed on a clock of its own.</summary>
public class SolveEstimateTests
{
    sealed class Clock
    {
        public double Seconds;
        public TimeSpan Now() => TimeSpan.FromSeconds(Seconds);
    }

    static AdaptivePass Pass(int index, int dofs) =>
        new(index, [], dofs, 0, 0, 0, 0, 0, 0, new SolveStatistics("", 0, 0, true, TimeSpan.Zero, TimeSpan.Zero), TimeSpan.Zero, TimeSpan.Zero);

    /// <summary>The flat bracket at level 3, as measured (PLAN.md §8): when each pass went to the solver and when it was done.</summary>
    static readonly (int Dofs, double Solving, double Passed)[] Bracket =
        [(1_584, 0.40, 0.55), (9_759, 0.80, 1.02), (57_135, 1.40, 2.26), (211_089, 3.00, 5.79)];

    /// <summary>Plays the bracket's passes to <paramref name="until"/> seconds, telling <paramref name="estimate"/> what happened.</summary>
    static void Play(SolveEstimate estimate, Clock clock, double until, ref int next, ref bool solving)
    {
        while (next < Bracket.Length)
        {
            var (dofs, start, end) = Bracket[next];
            if (!solving && start <= until)
            {
                clock.Seconds = start;
                estimate.Solving(new AdaptiveSolving(next, dofs, RootLevel: 3, BeadDofs: 385_449));
                solving = true;
            }
            else if (solving && end <= until)
            {
                clock.Seconds = end;
                estimate.Passed(Pass(next, dofs));
                (solving, next) = (false, next + 1);
            }
            else break;
        }
        clock.Seconds = until;
    }

    [Fact]
    public void Before_the_first_pass_it_cannot_tell_how_far_it_is()
    {
        var clock = new Clock { Seconds = 0.2 };

        var progress = new SolveEstimate(clock.Now).Now();

        Assert.Equal("building the mesh", progress.Step);
        Assert.Null(progress.Done);
        Assert.Equal(TimeSpan.FromSeconds(0.2), progress.Elapsed);
    }

    [Fact]
    public void The_bar_only_moves_forward_and_ends_when_the_solve_does()
    {
        var clock = new Clock();
        var estimate = new SolveEstimate(clock.Now);
        var (next, solving, done) = (0, false, 0.0);

        for (var t = 0.4; t < 5.9; t += 0.05)
        {
            Play(estimate, clock, t, ref next, ref solving);
            var now = estimate.Now().Done!.Value;
            Assert.True(now >= done, $"at {t:0.00} s the bar went back from {done:0.000} to {now:0.000}");
            Assert.InRange(now, 0, 0.99);
            done = now;
        }
        estimate.Finished();

        Assert.Equal(1, estimate.Now().Done);
        Assert.Equal(TimeSpan.Zero, estimate.Now().Remaining);
    }

    [Fact]
    public void It_says_which_pass_of_how_many_and_how_big()
    {
        var clock = new Clock();
        var estimate = new SolveEstimate(clock.Now);
        var (next, solving) = (0, false);

        Play(estimate, clock, 1.5, ref next, ref solving);

        Assert.Equal($"pass 3 of about 4, {57_135:N0} unknowns", estimate.Now().Step); // in the user's own digit grouping
        Play(estimate, clock, 2.5, ref next, ref solving);
        Assert.Equal("refining after pass 3", estimate.Now().Step);
    }

    [Theory]
    [InlineData(1.40)] // the third pass starts: two small passes behind it, the time per unknown still a guess
    [InlineData(3.00)] // the last pass starts
    [InlineData(4.50)] // half way through the last pass
    public void The_time_left_is_right_within_a_factor_of_two(double at)
    {
        var clock = new Clock();
        var estimate = new SolveEstimate(clock.Now);
        var (next, solving) = (0, false);

        Play(estimate, clock, at, ref next, ref solving);
        var left = estimate.Now().Remaining!.Value.TotalSeconds;

        var actual = Bracket[^1].Passed - at;
        Assert.InRange(left / actual, 0.5, 2);
    }
}
