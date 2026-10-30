namespace ReadTheStupidText.Application.Reading;

/// <summary>Which sentence unit of which chunk is now playing.</summary>
public readonly record struct SentenceChange(int ChunkIndex, int SentenceIndex);

/// <summary>
/// Where each sentence unit of one chunk's joined audio starts (Slice 190): the chunk's clips play back
/// to back with a fixed gap, so unit <c>i</c> starts after the earlier clips and gaps.
/// </summary>
public sealed class SentenceTimeline
{
    private readonly TimeSpan[] _starts;

    public SentenceTimeline(IReadOnlyList<TimeSpan> clipDurations, TimeSpan gap)
    {
        _starts = new TimeSpan[clipDurations.Count];
        TimeSpan sum = TimeSpan.Zero;
        for (int i = 0; i < clipDurations.Count; i++)
        {
            _starts[i] = sum;
            sum += clipDurations[i] + gap;
        }
    }

    /// <summary>
    /// The timeline of clips that play back to back with <paramref name="gap"/> between them. Null when any
    /// clip's length is unreadable or not positive: starts would be wrong, so tracking stays off instead.
    /// </summary>
    internal static SentenceTimeline? FromClips(IReadOnlyList<byte[]> clips, TimeSpan gap)
    {
        var durations = new List<TimeSpan>(clips.Count);
        foreach (byte[] clip in clips)
        {
            if (WavFile.TryGetDuration(clip) is not { } duration || duration <= TimeSpan.Zero)
            {
                return null;
            }

            durations.Add(duration);
        }

        return new SentenceTimeline(durations, gap);
    }

    public int Count => _starts.Length;

    /// <summary>The unit playing at <paramref name="position"/> within the chunk's audio.</summary>
    public int IndexAt(TimeSpan position)
    {
        int index = 0;
        for (int i = 1; i < _starts.Length && _starts[i] <= position; i++)
        {
            index = i;
        }

        return index;
    }
}

/// <summary>
/// Reports the sentence unit that is playing, from the playback position within the current chunk
/// (Slice 190). Shared by the Android and iOS readers: a reader registers each chunk file's timeline when
/// it writes the file, calls <see cref="Select"/> when it picks a file to play, <see cref="Begin"/> when
/// that chunk is announced, and feeds each position poll to <see cref="Update"/>. Every state change and
/// every <see cref="Changed"/> raise happens under one lock, so a poll thread and the announcing thread can
/// never reorder their reports, and a <see cref="Begin"/> for a file that is no longer the selected one
/// (a skip or stop got in between) is ignored. The raise runs under the lock: handlers must not block.
/// </summary>
public sealed class SentenceProgress
{
    private readonly object _gate = new();
    private readonly Dictionary<string, SentenceTimeline> _timelines = new();
    private SentenceTimeline? _timeline;
    private string? _selectedPath;
    private bool _reading = true;
    private int _chunkIndex;
    private int _current;

    /// <summary>The playing unit changed (the first unit of a tracked chunk, then each later one).</summary>
    public event Action<SentenceChange>? Changed;

    /// <summary>Remembers the sentence start times of the chunk file at <paramref name="path"/> (null: none known).</summary>
    public void Register(string path, SentenceTimeline? timeline)
    {
        lock (_gate)
        {
            if (timeline is null || !_reading)
            {
                _timelines.Remove(path);
                return;
            }

            _timelines[path] = timeline;
        }
    }

    /// <summary>The file at <paramref name="path"/> is gone: forget its timeline.</summary>
    public void Discard(string path)
    {
        lock (_gate)
        {
            _timelines.Remove(path);
        }
    }

    /// <summary>
    /// The chunk file at <paramref name="path"/> was picked to play: stop reporting against the previous chunk's
    /// timeline at once, and accept only a <see cref="Begin"/> for this file.
    /// </summary>
    public void Select(string path)
    {
        lock (_gate)
        {
            ClearActive();
            _selectedPath = path;
        }
    }

    /// <summary>
    /// The chunk read from <paramref name="path"/> began playing. Ignored when <paramref name="path"/> is no longer
    /// the selected file. Otherwise runs <paramref name="announceChunk"/> (the reader's chunk-changed raise) and then,
    /// for a chunk of several units with a known timeline, raises its first unit (the page narrows to it only once
    /// the reader has said it tracks sentences). The timeline stays registered, so a repeated announce still tracks.
    /// </summary>
    public void Begin(int chunkIndex, string? path, Action announceChunk)
    {
        lock (_gate)
        {
            if (path is null || path != _selectedPath)
            {
                return;
            }

            _timelines.TryGetValue(path, out SentenceTimeline? found);
            _chunkIndex = chunkIndex;
            _timeline = found is { Count: > 1 } ? found : null;
            _current = 0;
            announceChunk();
            if (_timeline is not null)
            {
                Changed?.Invoke(new SentenceChange(chunkIndex, 0));
            }
        }
    }

    /// <summary>No chunk is playing and the read is over (stop, end of the read): no timeline is kept.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            ClearActive();
            _reading = false;
            _timelines.Clear();
        }
    }

    /// <summary>A new read starts: nothing playing and no timeline kept from the last read.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            ClearActive();
            _timelines.Clear();
            _reading = true;
        }
    }

    /// <summary>Raises the change at <paramref name="position"/>, unless the unit is the one already reported.</summary>
    public void Update(TimeSpan position)
    {
        lock (_gate)
        {
            if (_timeline is null)
            {
                return;
            }

            int index = _timeline.IndexAt(position);
            if (index == _current)
            {
                return;
            }

            _current = index;
            Changed?.Invoke(new SentenceChange(_chunkIndex, index));
        }
    }

    private void ClearActive()
    {
        _timeline = null;
        _selectedPath = null;
        _current = 0;
    }
}

/// <summary>
/// Decides which <see cref="SentenceChange"/> the page may show (Slice 190). The page narrows its highlight to one
/// sentence only after the reader has announced the chunk's first unit; a change that arrives first is stale
/// (queued by an earlier read) and is dropped. A reader that never reports (the phone's own voice) never arms it,
/// so the chunk highlight stays.
/// </summary>
public sealed class SentenceHighlightGate
{
    private int _chunkIndex = -1;
    private bool _armed;

    /// <summary>A chunk highlight was shown (or cleared with -1): wait for that chunk's first-unit announcement.</summary>
    public void Reset(int chunkIndex)
    {
        _chunkIndex = chunkIndex;
        _armed = false;
    }

    public bool Accept(SentenceChange change)
    {
        if (change.ChunkIndex != _chunkIndex || _chunkIndex < 0)
        {
            return false;
        }

        if (!_armed && change.SentenceIndex != 0)
        {
            return false;
        }

        _armed = true;
        return true;
    }
}
