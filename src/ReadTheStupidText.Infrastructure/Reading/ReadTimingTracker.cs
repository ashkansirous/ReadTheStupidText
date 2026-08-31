using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// Pure state machine for the read-through timer (Decision 33). Elapsed is the sum of
/// already-played chunks' real durations plus the position within the chunk currently
/// playing; total becomes known the instant every chunk's audio has been generated —
/// not when it has finished playing — so a read's total can appear well before it's
/// reached. Shared by both <see cref="ISpeechReader"/> implementations, which drive it
/// from their own chunk generation/playback loops.
/// </summary>
public sealed class ReadTimingTracker
{
    private readonly object _gate = new();
    private TimeSpan?[] _chunkDurations = [];
    private TimeSpan _elapsedBeforeCurrentChunk;

    /// <summary>Begins tracking a fresh read of <paramref name="chunkCount"/> chunks,
    /// none of whose durations are known yet.</summary>
    public void Start(int chunkCount)
    {
        lock (_gate)
        {
            _chunkDurations = new TimeSpan?[chunkCount];
            _elapsedBeforeCurrentChunk = TimeSpan.Zero;
        }
    }

    /// <summary>Records the real synthesized duration of the chunk at
    /// <paramref name="index"/>, known as soon as its audio has been generated.</summary>
    public void RecordChunkDuration(int index, TimeSpan duration)
    {
        lock (_gate)
        {
            _chunkDurations[index] = duration;
        }
    }

    /// <summary>Folds the chunk at <paramref name="index"/>'s real duration into the
    /// elapsed baseline once it has finished playing to its natural end, so the next
    /// chunk's position starts from the right offset.</summary>
    public void AdvancePastChunk(int index)
    {
        lock (_gate)
        {
            _elapsedBeforeCurrentChunk += _chunkDurations[index] ?? TimeSpan.Zero;
        }
    }

    /// <summary>Returns to the initial state (no read in progress): zero elapsed,
    /// unknown total.</summary>
    public void Reset() => Start(0);

    /// <summary>Repositions the elapsed baseline to a skip target's chunk start
    /// (Decision 32), without touching any already-recorded chunk durations —
    /// replaying a chunk after a skip folds its duration back in the same way a
    /// first playthrough does.</summary>
    public void SeekTo(SkipTarget target)
    {
        lock (_gate)
        {
            _elapsedBeforeCurrentChunk = target.ChunkStart;
        }
    }

    /// <summary>
    /// Finds the chunk to resume at for a ±10s skip (Decision 32). Skips land on a
    /// chunk's start, not an exact 10.000s offset: a forward skip rounds up to the
    /// nearest known chunk start at/after <paramref name="elapsed"/> + <paramref
    /// name="delta"/>, clamped to the furthest boundary reached so far — the start of
    /// the last chunk whose predecessors are all known, even if that chunk itself
    /// hasn't finished synthesizing yet (the caller waits on it). A backward skip
    /// rounds down to the nearest known chunk start at/before the target, clamped to
    /// zero. Returns null when no read is in progress.
    /// </summary>
    public SkipTarget? ComputeSkipTarget(TimeSpan elapsed, TimeSpan delta)
    {
        lock (_gate)
        {
            if (_chunkDurations.Length == 0)
            {
                return null;
            }

            (TimeSpan[] starts, int maxChunkIndex) = KnownChunkStarts();
            int index = delta < TimeSpan.Zero
                ? FloorChunkIndex(starts, maxChunkIndex, Max(elapsed + delta, TimeSpan.Zero))
                : CeilingChunkIndex(starts, maxChunkIndex, Min(elapsed + delta, starts[maxChunkIndex]));

            return new SkipTarget(index, starts[index]);
        }
    }

    // Caller holds _gate. The cumulative start time of each chunk in the contiguous
    // known-duration prefix; maxChunkIndex is the furthest reachable chunk — either
    // the last one whose own duration already landed, or (while more remain) the next
    // one, whose start is known even though it isn't synthesized yet.
    private (TimeSpan[] Starts, int MaxChunkIndex) KnownChunkStarts()
    {
        var starts = new TimeSpan[_chunkDurations.Length];
        TimeSpan sum = TimeSpan.Zero;
        int known = 0;
        for (int i = 0; i < _chunkDurations.Length; i++)
        {
            starts[i] = sum;
            if (_chunkDurations[i] is not { } duration)
            {
                break;
            }

            sum += duration;
            known = i + 1;
        }

        return (starts, Math.Min(known, _chunkDurations.Length - 1));
    }

    private static int FloorChunkIndex(TimeSpan[] starts, int maxChunkIndex, TimeSpan target)
    {
        int index = 0;
        for (int i = 1; i <= maxChunkIndex && starts[i] <= target; i++)
        {
            index = i;
        }

        return index;
    }

    private static int CeilingChunkIndex(TimeSpan[] starts, int maxChunkIndex, TimeSpan target)
    {
        for (int i = 0; i <= maxChunkIndex; i++)
        {
            if (starts[i] >= target)
            {
                return i;
            }
        }

        return maxChunkIndex;
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    /// <summary>The current elapsed/total, given the playback position within the
    /// chunk currently playing.</summary>
    public ReadTiming CurrentTiming(TimeSpan positionInCurrentChunk)
    {
        lock (_gate)
        {
            return new ReadTiming(_elapsedBeforeCurrentChunk + positionInCurrentChunk, ComputeTotal());
        }
    }

    // Caller holds _gate.
    private TimeSpan? ComputeTotal()
    {
        if (_chunkDurations.Length == 0)
        {
            return null;
        }

        TimeSpan sum = TimeSpan.Zero;
        foreach (TimeSpan? duration in _chunkDurations)
        {
            if (duration is not { } value)
            {
                return null;
            }

            sum += value;
        }

        return sum;
    }
}
