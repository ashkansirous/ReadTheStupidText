namespace DinoReader;

/// <summary>
/// Slice 346 E3: the store can lag behind a purchase the grown-up just made, so the Worker's 403 "purchase not valid" in the first minutes is retried after a pause instead of failing the book at once.
/// The Worker keeps an "owns nothing" answer for 30 s (<c>NOT_OWNED_CACHE_MS</c> in worker/entitlements/gate.ts, keyed by the proof), so every pause is longer than that: each retry then reaches the store
/// instead of the cached 403. Pure: the caller says how long ago the purchase completed and how many retries it already made.
/// </summary>
public static class PurchaseLagRetry
{
    /// <summary>After this long since the purchase a 403 is taken at its word.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    /// <summary>The Worker's negative cache (30 s) plus a margin; every retry waits this long.</summary>
    public static readonly TimeSpan Pause = TimeSpan.FromSeconds(35);

    /// <summary>Seven pauses of 35 s: the last retry starts about 245 s after the first 403, inside <see cref="Window"/>.</summary>
    public const int MaxRetries = 7;

    /// <summary>The pause before the next try, or null when the purchase is unknown or older than <see cref="Window"/> or the tries are used up.</summary>
    public static TimeSpan? NextPause(TimeSpan? sincePurchase, int retriesDone)
    {
        if (sincePurchase is not { } since || since > Window || retriesDone < 0 || retriesDone >= MaxRetries)
        {
            return null;
        }

        return Pause;
    }

    /// <summary>
    /// Runs <paramref name="attempt"/>; when it throws what <paramref name="isLag"/> accepts and the purchase is still inside the window, tells <paramref name="waiting"/> (true), waits the pause with
    /// <paramref name="delay"/> and tries again, then tells it false once the wait is over. Any other exception, or the last one, goes to the caller.
    /// </summary>
    public static async Task<T> RunAsync<T>(Func<Task<T>> attempt, Func<Exception, bool> isLag, Func<TimeSpan?> sincePurchase, Func<TimeSpan, CancellationToken, Task> delay, Action<bool> waiting, CancellationToken cancel)
    {
        for (int retries = 0; ; retries++)
        {
            try
            {
                return await attempt();
            }
            catch (Exception ex) when (isLag(ex) && NextPause(sincePurchase(), retries) is not null)
            {
                // no-fallback: a refusal just after the purchase is the store lagging; it is retried, and only the refusal that finally reaches the caller is reported by the caller
                waiting(true);
                try
                {
                    await delay(Pause, cancel);
                }
                finally
                {
                    waiting(false);
                }
            }
        }
    }
}
