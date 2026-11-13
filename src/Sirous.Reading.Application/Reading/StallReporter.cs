namespace Sirous.Reading.Application.Reading;

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

#if DEBUG
    /// <summary>Slice 320 item 7 (Debug only): where the stall-read test hook is looked up; the app's shared one, a test's own instance in tests (no state shared between parallel tests).</summary>
    internal StallHook Hook { get; init; } = StallHook.Shared;
#endif

    /// <summary>Slice 345: the one chunk of a read whose wait the Debug <see cref="StallHook"/> may hold (chunk 0's wait is the "Preparing the voice" wait). All three readers ask <see cref="IsHoldable"/>.</summary>
    public const int HoldableChunkIndex = 1;

    /// <summary>True when the chunk at <paramref name="index"/> of a read may take an armed stall-read request.</summary>
    public static bool IsHoldable(int index) => index == HoldableChunkIndex;

    /// <summary>
    /// Slice 345: a read calls this once when it starts and keeps the claim. It names the Debug stall-read request armed at that moment (0 = none), so a
    /// request armed later belongs to a later read. Always 0 in Release.
    /// </summary>
    public long ClaimArmedRequest()
    {
#if DEBUG
        return Hook.Claim();
#else
        return 0;
#endif
    }

    /// <summary>
    /// Slice 345: a read that played to its natural end (token not cancelled) without reaching the holdable chunk (a one-chunk read) calls this with its claim, so the request it
    /// claimed cannot hang a later read. It clears only that request: one armed after the read started stays. A stopped or replaced read does not call it.
    /// Debug-only effect; the held chunk's index is absolute, so after a skip-back or voice-swap restart the hold can fire at a different point.
    /// </summary>
    public void ReadPlayedToEnd(long claim)
    {
#if DEBUG
        Hook.Disarm(claim);
#endif
    }

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
    /// <paramref name="holdable"/> is true for chunk 1 of a read (Slice 345): only that wait may take an armed Debug <see cref="StallHook"/> request. Chunk 0's wait is the page's "Preparing the voice" wait, which owns the overlay, so a hold there never shows the stall line.
    /// </summary>
    public async Task<T> WatchAsync<T>(Task<T> pending, CancellationToken token, bool holdable = false)
    {
#if DEBUG
        bool held = holdable && Hook.Take(); // Slice 320 item 7, Slice 345: the stall-read test hook holds a read's second chunk wait (a read that had started, as Slice 148 defines a stall) so the overlay can be reached
#else
        const bool held = false;
#endif
        if (!held && pending.IsCompleted)
        {
            return await pending;
        }

        Move(+1);
        int released = 0;
        void Release()
        {
            if (Interlocked.Exchange(ref released, 1) == 0)
            {
                Move(-1);
            }
        }

        // Slice 314: the ticket goes at whichever comes first, the chunk arriving or the read being stopped, so a stopped read whose
        // native synthesis is still running no longer counts as a stall.
        using CancellationTokenRegistration registration = token.Register(Release);
        try
        {
#if DEBUG
            if (held)
            {
                await Hook.HoldAsync(token);
            }
#endif
            return await pending;
        }
        finally
        {
            Release();
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
