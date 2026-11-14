namespace DinoReader;

/// <summary>
/// Slice 346 E3: the store can lag behind a purchase the grown-up just made, so the Worker's 403 "purchase not valid" on a paid download is retried on a short fixed schedule instead of failing the
/// book at once. The app only offers a download for a book the store already says is owned, so a 403 there is store lag; it is retried every time, with no memory of when the purchase was made.
/// Pure: the caller passes the work, the delay and what to show while waiting.
/// </summary>
public static class PurchaseLagRetry
{
    /// <summary>The fixed pauses before each retry (seconds 3, 6, 10, 15, 20, 30: about 84 s in all); after the last one the refusal goes to the caller.</summary>
    public static readonly IReadOnlyList<TimeSpan> Pauses =
        [TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(30)];

    /// <summary>The number of retries after the first try.</summary>
    public static int MaxRetries => Pauses.Count;

    /// <summary>The pause before the next try, or null when the tries are used up.</summary>
    public static TimeSpan? NextPause(int retriesDone) =>
        retriesDone >= 0 && retriesDone < Pauses.Count ? Pauses[retriesDone] : null;

    /// <summary>
    /// Runs <paramref name="attempt"/>; when it throws what <paramref name="isLag"/> accepts and a retry is left, tells <paramref name="waiting"/> (true), waits the pause with
    /// <paramref name="delay"/> and tries again, then tells it false once the wait is over. Any other exception, or the last one, goes to the caller.
    /// </summary>
    public static async Task<T> RunAsync<T>(Func<Task<T>> attempt, Func<Exception, bool> isLag, Func<TimeSpan, CancellationToken, Task> delay, Action<bool> waiting, CancellationToken cancel)
    {
        for (int retries = 0; ; retries++)
        {
            TimeSpan pause;
            try
            {
                return await attempt();
            }
            catch (Exception ex) when (isLag(ex) && NextPause(retries) is { } next)
            {
                // no-fallback: a refusal that may be the store lagging is retried; only the refusal that finally reaches the caller is reported by the caller
                pause = next;
            }

            waiting(true);
            try
            {
                await delay(pause, cancel);
            }
            finally
            {
                waiting(false);
            }
        }
    }
}
