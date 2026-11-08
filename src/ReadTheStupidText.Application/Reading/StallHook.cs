#if DEBUG
using System.Diagnostics;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Slice 320 item 7 (Debug only): the <c>stall-read</c> test hook arms one held chunk wait. The next first-chunk wait that starts after arming (a <see cref="StallReporter.WatchAsync{T}"/> call with firstChunk true, i.e. chunk 0 of a read; later chunks never take it:
/// the request is used up by it) then counts as a stall for <see cref="HoldFor"/> (90 s), or until its read is stopped, even though the chunk is ready, so the "Still getting the voice ready" overlay and its
/// Cancel can be reached on a device (Android and iOS alike: the hook arrives through each platform's entry point). The request names no read: a flow arms it right before it starts a read of ONE short sentence,
/// so that read's only chunk wait is the next one; arming while another read is running, or for a longer text, holds whichever chunk wait comes first. A request nobody took within two minutes lapses, so it
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
    private long _armedAt; // Stopwatch timestamp of the request, 0 = none

    /// <summary>Where the markers go (the app's Debug test log); set by the app's own Debug code. Null writes nothing.</summary>
    public Action<string>? Log { get; set; }

    /// <summary>Arms one held wait.</summary>
    public void Arm() => Interlocked.Exchange(ref _armedAt, Stopwatch.GetTimestamp());

    /// <summary>True once per request (and clears it), unless the request has lapsed.</summary>
    public bool Take()
    {
        long armed = Interlocked.Exchange(ref _armedAt, 0);
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
