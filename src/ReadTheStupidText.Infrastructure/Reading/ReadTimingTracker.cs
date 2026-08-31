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
