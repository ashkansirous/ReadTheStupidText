namespace ReadTheStupidText.Mobile.Shared.ModelDownload;

/// <summary>
/// Keeps one platform model-download call in flight per page (Slice 187). A session attempt that stalls or is cancelled only stops waiting for the
/// call; the platform call itself keeps running. The next attempt for the same pair reuses that call, and a call for another pair waits for it to end,
/// so two downloads of one model never run side by side.
/// </summary>
public sealed class SingleModelCall
{
    private readonly object _gate = new();
    private string? _key;
    private Task? _running;

    /// <summary>Joins the call still running for <paramref name="key"/>, else starts one with <paramref name="start"/>. The returned task gives up when <paramref name="token"/> is cancelled; the call itself is left running.</summary>
    public async Task RunAsync(string key, Func<Task> start, CancellationToken token)
    {
        Task? other = RunningForOtherKey(key);
        if (other is not null)
        {
            await SettleAsync(other, token);
        }

        await JoinOrStart(key, start).WaitAsync(token);
    }

    private Task? RunningForOtherKey(string key)
    {
        lock (_gate)
        {
            return _running is { IsCompleted: false } && _key != key ? _running : null;
        }
    }

    private Task JoinOrStart(string key, Func<Task> start)
    {
        lock (_gate)
        {
            if (_running is { IsCompleted: false } && _key == key)
            {
                return _running;
            }

            _key = key;
            _running = start();
            return _running;
        }
    }

    // The other call's own failure is not this one's: only its end matters.
    private static async Task SettleAsync(Task other, CancellationToken token)
    {
        try
        {
            await other.WaitAsync(token);
        }
        catch (Exception) when (!token.IsCancellationRequested)
        {
            // it ended with an error: the way is clear
        }
    }
}
