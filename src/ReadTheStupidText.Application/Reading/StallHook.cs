#if DEBUG
using System.Diagnostics;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Slice 320 item 7 (Debug only): the <c>stall-read</c> test hook arms one held chunk wait. The next second-chunk wait that starts after arming (a <see cref="StallReporter.WatchAsync{T}"/> call with holdable true, i.e. chunk 1 of a read, Slice 345: chunk 0's wait is the "Preparing the voice" wait, which owns the overlay; other chunks never take it:
/// the request is used up by it) then counts as a stall for <see cref="HoldFor"/> (90 s), or until its read is stopped, even though the chunk is ready, so the "Still getting the voice ready" overlay and its
/// Cancel can be reached on a device (Android and iOS alike: the hook arrives through each platform's entry point). The request names no read: a flow arms it right before it starts a read of a text over 200 characters (shorter text is one chunk),
/// so that read's second chunk wait is the next one; arming while another read is running holds whichever read's second chunk wait comes first, and a one-chunk read never takes it. A request nobody took within two minutes lapses, so it
/// cannot hang a later unrelated read. The app uses <see cref="Shared"/>; tests make their own instance (no shared state between parallel tests). Compiled out of Release.
/// </summary>
public sealed class StallHook
{
    /// <summary>The instance the app's readers and the test hook share.</summary>
    public static StallHook Shared { get; } = new();

    /// <summary>How long a held wait lasts when nobody stops the read.</summary>
    public TimeSpan HoldFor { get; init; } = TimeSpan.FromSeconds(90);

    /// <summary>Marker written (through <see cref="Log"/>) when a held wait begins.</summary>
    public const string BeginsMarker = "STALL hold begins";

    /// <summary>Marker written when a held wait ends, by its time running out or by the read being stopped.</summary>
    public const string ReleasedMarker = "STALL hold released";

    private static readonly TimeSpan RequestLife = TimeSpan.FromMinutes(2);
    private readonly object _gate = new();
    private long _armedAt; // Stopwatch timestamp of the request, 0 = none
    private long _requestId; // Slice 345: names the current request; every Arm gets a new id

    /// <summary>Where the markers go (the app's Debug test log); set by the app's own Debug code. Null writes nothing.</summary>
    public Action<string>? Log { get; set; }

    /// <summary>Arms one held wait.</summary>
    public void Arm()
    {
        lock (_gate)
        {
            _armedAt = Stopwatch.GetTimestamp();
            _requestId++;
        }
    }

    /// <summary>Slice 345: a read calls this when it starts; returns the id of the request armed right now (0 = none). Only that read's <see cref="Disarm"/> can clear the request.</summary>
    public long Claim()
    {
        lock (_gate)
        {
            return _armedAt == 0 ? 0 : _requestId;
        }
    }

    /// <summary>Clears the request the read named by <paramref name="claim"/> claimed, if it is still the armed one: a request armed after the read started (a newer id) stays.</summary>
    public void Disarm(long claim)
    {
        lock (_gate)
        {
            if (claim != 0 && claim == _requestId)
            {
                _armedAt = 0;
            }
        }
    }

    /// <summary>True once per request (and clears it), unless the request has lapsed.</summary>
    public bool Take()
    {
        long armed;
        lock (_gate)
        {
            armed = _armedAt;
            _armedAt = 0;
        }

        return armed != 0 && Stopwatch.GetElapsedTime(armed) < RequestLife;
    }

    /// <summary>Waits <see cref="HoldFor"/>; throws <see cref="OperationCanceledException"/> when the read is stopped first (the callers already treat that as a stopped read).</summary>
    public async Task HoldAsync(CancellationToken token)
    {
        Log?.Invoke(BeginsMarker);
        try
        {
            await Task.Delay(HoldFor, token);
        }
        finally
        {
            Log?.Invoke(ReleasedMarker);
        }
    }
}
#endif
