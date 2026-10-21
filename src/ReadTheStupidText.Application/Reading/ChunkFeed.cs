namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// The chunks of one read, which can still grow (Slice 75): a streamed read starts speaking its first
/// translated sentences while the rest is translated, and each later block is appended here. The playback
/// loop takes chunks by index and, when it runs out while the feed is open, waits for the next change.
/// A read of known text is a feed that is complete from the start. Appended blocks are planned against the
/// whole read's text (joined by <see cref="BlockJoin"/>), so every chunk's source range stays valid for the
/// reading text box. Thread-safe.
/// </summary>
internal sealed class ChunkFeed
{
    /// <summary>What separates two appended blocks in the read's full text.</summary>
    public const string BlockJoin = " ";

    private readonly object _gate = new();
    private readonly List<SpeechChunkPlan> _chunks;
    private TaskCompletionSource _changed = NewSignal();
    private int _textLength;
    private bool _complete;

    public ChunkFeed(IEnumerable<SpeechChunkPlan> initial, int textLength, bool complete)
    {
        _chunks = [.. initial];
        _textLength = textLength;
        _complete = complete;
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _chunks.Count;
            }
        }
    }

    /// <summary>True once nothing more will be appended.</summary>
    public bool IsComplete
    {
        get
        {
            lock (_gate)
            {
                return _complete;
            }
        }
    }

    public SpeechChunkPlan this[int index]
    {
        get
        {
            lock (_gate)
            {
                return _chunks[index];
            }
        }
    }

    public IReadOnlyList<SpeechChunkPlan> Snapshot()
    {
        lock (_gate)
        {
            return [.. _chunks];
        }
    }

    /// <summary>
    /// Plans <paramref name="text"/> as the next block of the read and adds its chunks; returns how many.
    /// <paramref name="grown"/> runs under the feed's lock with the new chunk count, before the playback loop
    /// can see the new chunks, so the read's timing can be extended first.
    /// </summary>
    public int Append(string text, Action<int>? grown = null)
    {
        lock (_gate)
        {
            if (_complete || string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            int offset = _textLength + BlockJoin.Length;
            IReadOnlyList<SpeechChunkPlan> plan = SpeechChunkPlanner.ForText(text, biasFirstChunk: false)
                .Select(c => c with { SourceStart = c.SourceStart + offset, SourceEnd = c.SourceEnd + offset })
                .ToList();
            _chunks.AddRange(plan);
            _textLength = offset + text.Length;
            grown?.Invoke(_chunks.Count);
            Signal();
            return plan.Count;
        }
    }

    /// <summary>Marks the read as having no more text.</summary>
    public void Complete()
    {
        lock (_gate)
        {
            _complete = true;
            Signal();
        }
    }

    /// <summary>Completes when there are more than <paramref name="knownCount"/> chunks or the feed is complete.</summary>
    public async Task WaitForMoreAsync(int knownCount, CancellationToken token)
    {
        Task changed;
        lock (_gate)
        {
            if (_chunks.Count > knownCount || _complete)
            {
                return;
            }

            changed = _changed.Task;
        }

        await changed.WaitAsync(token).ConfigureAwait(false);
    }

    // Caller holds _gate.
    private void Signal()
    {
        TaskCompletionSource old = _changed;
        _changed = NewSignal();
        old.TrySetResult();
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
