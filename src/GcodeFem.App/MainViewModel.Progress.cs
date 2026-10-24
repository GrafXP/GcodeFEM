using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using GcodeFem.Core;

namespace GcodeFem.App;

/// <summary>A piece of work that can say what it is doing but not how far it has come: slicing, reading a toolpath.</summary>
sealed class StepClock(string step)
{
    readonly Stopwatch watch = Stopwatch.StartNew();

    /// <summary>Set from whichever thread does the work.</summary>
    public volatile string Step = step;

    public WorkProgress Now() => new(Step, watch.Elapsed);
}

/// <summary>
/// The progress bar of the status bar. Long work (a slice, a solve, and the orientation sweeps to
/// come) hands it a function that says where the work stands, which is read five times a second:
/// the bar fills by the share done, or runs to and fro while the work cannot tell, and beside it
/// stand the time taken and, where known, about how long is left.
/// </summary>
public sealed partial class MainViewModel
{
    readonly DispatcherTimer workTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    Func<WorkProgress>? work;
    string workTitle = "";

    [ObservableProperty]
    public partial bool IsWorking { get; set; }

    /// <summary>0 to 1.</summary>
    [ObservableProperty]
    public partial double WorkDone { get; set; }

    [ObservableProperty]
    public partial bool WorkIsIndeterminate { get; set; }

    /// <summary>"12.4 s, about 20 s left".</summary>
    [ObservableProperty]
    public partial string WorkTime { get; set; } = "";

    /// <summary>Shows <paramref name="progress"/> in the status bar until <see cref="EndWork"/>; <paramref name="title"/> leads the status text.</summary>
    void BeginWork(string title, Func<WorkProgress> progress)
    {
        (workTitle, work) = (title, progress);
        IsWorking = true;
        ShowWork();
        workTimer.Start();
    }

    void EndWork()
    {
        workTimer.Stop();
        work = null;
        IsWorking = false;
    }

    void ShowWork()
    {
        if (work is null) return;
        var progress = work();
        Status = $"{workTitle}: {progress.Step}…";
        WorkIsIndeterminate = progress.Done is null;
        WorkDone = progress.Done ?? 0;
        // The first moments of an estimate say little; the time taken is shown from the start.
        WorkTime = progress is { Remaining: { } left, Done: > 0.03 }
            ? $"{Duration(progress.Elapsed)}, {(left.TotalSeconds < 1 ? "almost done" : $"about {Duration(left)} left")}"
            : Duration(progress.Elapsed);
    }

    static string Duration(TimeSpan time) => time.TotalSeconds switch
    {
        < 10 => $"{time.TotalSeconds:0.0} s",
        < 60 => $"{time.TotalSeconds:0} s",
        _ => $"{(int)time.TotalMinutes}:{time.Seconds:00} min",
    };
}
