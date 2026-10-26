namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Tells the UI that a read which had started has to wait for its next chunk (Slice 148): the reader wraps the wait for each chunk's
/// audio in <see cref="WatchAsync{T}"/>, which raises <see cref="Changed"/> with true when the chunk is not ready and with false when it is
/// (or the wait ends by cancel or failure). A chunk that is already ready raises nothing, so a smooth read never flickers. Shared by every
/// chunking <see cref="ISpeechReader"/>; events are raised on the thread that waits, so handlers marshal to the UI themselves.
/// </summary>
public sealed class StallReporter(object owner)
{
    private readonly object _gate = new();
    private int _watching;

    /// <summary>Raised with true when the next chunk is not ready and with false when the wait ends.</summary>
    public event EventHandler<bool>? Changed;

    /// <summary>True while the next chunk is awaited.</summary>
    public bool IsStalled
    {
        get { lock (_gate) { return _watching > 0; } }
    }

    /// <summary>
    /// Awaits <paramref name="pending"/>; a task that is not finished yet counts as a stall for as long as it takes. Watches are counted,
    /// so the stall ends only when the last of overlapping watches (a replaced read ending late, its replacement still waiting) is done.
    /// </summary>
    public async Task<T> WatchAsync<T>(Task<T> pending)
    {
        if (pending.IsCompleted)
        {
            return await pending;
        }

        Move(+1);
        try
        {
            return await pending;
        }
        finally
        {
            Move(-1);
        }
    }

    private void Move(int delta)
    {
        bool stalled;
        lock (_gate)
        {
            int before = _watching;
            _watching += delta;
            if ((before > 0) == (_watching > 0))
            {
                return;
            }

            stalled = _watching > 0;
        }

        Changed?.Invoke(owner, stalled);
    }
}
