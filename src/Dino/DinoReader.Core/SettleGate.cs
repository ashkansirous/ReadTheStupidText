namespace DinoReader.Core;

/// <summary>
/// One shared, bounded wait for a piece of background work (Slice 346, review): every caller that needs the same <c>work</c> to finish joins the same wait, so the budget is spent once, not once per caller, and the
/// timeout is reported once. A caller that arrives after the timeout returns at once. A different task (a newer listing's read) starts a new wait.
/// </summary>
public sealed class SettleGate
{
    private readonly object _lock = new();
    private Task? _work;
    private Task<bool>? _wait;

    /// <summary>
    /// Completes with true when <paramref name="work"/> finished within <paramref name="budget"/>, false when the budget ran out first (<paramref name="onTimeout"/> ran once, before this completes). Never throws
    /// for the work's own failure. A caller's <paramref name="cancel"/> only stops that caller's wait: it returns false and the shared wait goes on for the others.
    /// </summary>
    public async Task<bool> WaitAsync(Task work, TimeSpan budget, Action onTimeout, CancellationToken cancel)
    {
        Task<bool> shared;
        lock (_lock)
        {
            if (!ReferenceEquals(_work, work))
            {
                _work = work;
                _wait = RunAsync(work, budget, onTimeout);
            }

            shared = _wait!;
        }

        try
        {
            return await shared.WaitAsync(cancel);
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            // no-fallback: the caller itself gave up waiting; it carries on (and handles its own cancellation) exactly as it did before the wait
            return false;
        }
    }

    private static async Task<bool> RunAsync(Task work, TimeSpan budget, Action onTimeout)
    {
        using var stop = new CancellationTokenSource();
        await Task.WhenAny(work, Task.Delay(budget, stop.Token));
        await stop.CancelAsync();
        if (work.IsCompleted)
        {
            return true;
        }

        RunTimeoutCallback(onTimeout);
        return false;
    }

    // The callback is only the caller's report of the timeout: whatever it throws must never fault the shared wait every joined caller awaits.
    private static void RunTimeoutCallback(Action onTimeout)
    {
        try
        {
            onTimeout();
        }
        catch (Exception ex)
        {
            // no-fallback: this is the report of a timeout itself; losing it must not turn a bounded wait into an exception for every caller
            System.Diagnostics.Debug.WriteLine($"Dino Reader: the settle timeout report failed: {ex.Message}");
        }
    }
}
