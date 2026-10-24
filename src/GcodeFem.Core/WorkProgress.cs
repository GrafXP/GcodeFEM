namespace GcodeFem.Core;

/// <summary>Where a long piece of work stands, for a progress bar: what it is doing, for how long, and as far as it can tell, how much is done and how long is left.</summary>
/// <param name="Step">What it is doing now, in a few words.</param>
/// <param name="Done">The share done, 0 to 1; null while it cannot tell.</param>
/// <param name="Remaining">About how long it still takes; null while it cannot tell.</param>
public readonly record struct WorkProgress(string Step, TimeSpan Elapsed, double? Done = null, TimeSpan? Remaining = null);
