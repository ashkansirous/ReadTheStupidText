namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// The outcome rule every <see cref="IHeadStart"/> shares: its task completes normally only when the clips are stored. A
/// preparation that was superseded, cancelled, skipped (no engine, a read running) or failed ends cancelled, so a caller
/// that awaits it can never mistake "nothing was stored" for "prepared" (Slice 139).
/// </summary>
public static class HeadStartRun
{
    /// <summary>A preparation that did nothing, e.g. a read is running or the text is empty.</summary>
    public static Task Skipped => Task.FromCanceled(new CancellationToken(canceled: true));

    /// <summary>Runs <paramref name="prepare"/> (true when it stored the clips) and throws
    /// <see cref="OperationCanceledException"/> unless it did (clips already stored stay prepared even if the token was cancelled just after).</summary>
    public static async Task CompleteAsync(CancellationToken token, Func<CancellationToken, Task<bool>> prepare)
    {
        token.ThrowIfCancellationRequested();
        bool stored = await prepare(token);
        if (!stored)
        {
            throw new OperationCanceledException(token);
        }
    }
}
