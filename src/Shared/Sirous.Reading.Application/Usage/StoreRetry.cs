namespace Sirous.Reading.Application.Usage;

/// <summary>
/// Slice 460: the store's automatic retry. A failed call is tried again after 1 s, 3 s and 7 s (four attempts in all) before the caller gives up and shows its offline page, so a first launch that met the network or the
/// Windows Store a moment too early recovers without a press on Try again. Only the last attempt may report the failure, so a call that recovered leaves no fallback row. Cancel ends the wait or the attempt at once.
/// </summary>
public static class StoreRetry
{
    /// <summary>The gaps before the second, third and fourth attempt.</summary>
    public static IReadOnlyList<TimeSpan> Backoff { get; } = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(7)];

    /// <summary>The most time the whole sequence may spend waiting (attempts and gaps) before the last failure is given back: about 30 s.</summary>
    public static TimeSpan Budget { get; } = TimeSpan.FromSeconds(30);

    /// <summary>The longest a retried attempt may take; the first attempt keeps the caller's own (longer) timeout.</summary>
    public static TimeSpan RetryAttemptTimeout { get; } = TimeSpan.FromSeconds(8);

    /// <summary>The timeout of attempt number <paramref name="attemptIndex"/> (0 is the first): the caller's <paramref name="first"/> for the first, else <see cref="RetryAttemptTimeout"/> cut to what is left of the <see cref="Budget"/> (never under a second).</summary>
    public static TimeSpan AttemptTimeout(int attemptIndex, TimeSpan first, TimeSpan elapsed)
    {
        if (attemptIndex == 0)
        {
            return first;
        }

        TimeSpan left = Budget - elapsed;
        TimeSpan shorter = left < RetryAttemptTimeout ? left : RetryAttemptTimeout;
        return shorter < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : shorter;
    }

    /// <summary>The wait before the next attempt after attempt number <paramref name="attempt"/> (1 is the first) failed, as <see cref="RunAsync{T}"/> decides it; zero when the sequence ends there (the last attempt, or the budget cap).</summary>
    public static TimeSpan NextWait(int attempt, TimeSpan elapsed) =>
        attempt >= 1 && attempt <= Backoff.Count && elapsed + Backoff[attempt - 1] < Budget ? Backoff[attempt - 1] : TimeSpan.Zero;

    /// <summary>The Debug test-log line for one first-run list attempt: <c>[Store] list attempt=2 source=prefetch cause=NoNetwork nextWait=3</c> (cause is "ok" for an answer; nextWait in whole seconds, 0 when the sequence ends).</summary>
    public static string AttemptLine(int attempt, string source, string cause, TimeSpan nextWait) =>
        $"[Store] list attempt={attempt} source={source} cause={cause} nextWait={(int)nextWait.TotalSeconds}";

    /// <summary>The Debug test-log line for what a sequence's final failure did: <c>reported</c> (the session's one row), <c>suppressed</c> (the session already reported) or <c>silent</c> (this source never reports).</summary>
    public static string OutcomeLine(string source, string outcome) => $"[Store] list failure source={source} outcome={outcome}";

    /// <summary>A clock that starts now; its value is the time since this call.</summary>
    public static Func<TimeSpan> StartClock()
    {
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        return () => System.Diagnostics.Stopwatch.GetElapsedTime(start);
    }

    /// <summary>
    /// Runs <paramref name="attempt"/> until it succeeds or the schedule is spent; its argument is true on the last attempt (the one that reports a failure). <paramref name="failed"/> says whether a result is a failure.
    /// <paramref name="delay"/> is the wait between attempts (a test passes a recorder); <paramref name="onRetry"/> gets the number of the attempt about to run (a Debug log line). Cancellation throws <see cref="OperationCanceledException"/>.
    /// </summary>
    public static async Task<T> RunAsync<T>(Func<bool, Task<T>> attempt, Func<T, bool> failed, CancellationToken cancel, Func<TimeSpan, CancellationToken, Task>? delay = null, Action<int>? onRetry = null, Func<TimeSpan>? elapsed = null)
    {
        Func<TimeSpan, CancellationToken, Task> wait = delay ?? ((span, token) => Task.Delay(span, token));
        Func<TimeSpan> clock = elapsed ?? StartClock();
        for (int index = 0; ; index++)
        {
            cancel.ThrowIfCancellationRequested();
            bool last = index == Backoff.Count;
            T result = await attempt(last);
            if (last || !failed(result) || clock() + Backoff[index] >= Budget)
            {
                return result; // the cap: a retry that would start after the budget is spent is not waited for (a hung server must not hold the wait screen for minutes)
            }

            await wait(Backoff[index], cancel);
            onRetry?.Invoke(index + 2);
        }
    }
}
