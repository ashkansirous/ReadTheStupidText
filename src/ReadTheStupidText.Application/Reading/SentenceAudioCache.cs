namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// A size-capped, least-recently-used cache of sentence audio (Slice 47, Decision 66). Holds
/// only the index (id, size, duration, recency); the bytes live in an <see cref="ISentenceStore"/>.
/// Thread-safe: reads and writes come from concurrent synthesis tasks.
/// </summary>
internal sealed class SentenceAudioCache
{
    /// <summary>Default disk budget for cached sentences.</summary>
    public const long DefaultMaxBytes = 100L * 1024 * 1024;

    private readonly ISentenceStore _store;
    private readonly long _maxBytes;
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new();
    private long _tick;
    private long _totalBytes;
    private bool _loaded;

    public SentenceAudioCache(ISentenceStore store, long maxBytes = DefaultMaxBytes)
    {
        _store = store;
        _maxBytes = maxBytes;
    }

    public long TotalBytes
    {
        get
        {
            lock (_gate)
            {
                EnsureLoaded();
                return _totalBytes;
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                EnsureLoaded();
                return _entries.Count;
            }
        }
    }

    /// <summary>The cached clip for the key, marking it most recently used; null on a miss
    /// (including a clip whose file vanished or is corrupt or truncated, which is dropped from
    /// the index and deleted).</summary>
    public async Task<byte[]?> TryGetAsync(SentenceCacheKey key)
    {
        string id = key.Id;
        if (!TouchEntry(id))
        {
            return null;
        }

        byte[]? wav = await _store.ReadAsync(id);
        if (wav is null || !WavFile.IsComplete(wav, wav.LongLength))
        {
            Remove(id);
            _store.Delete(id); // a corrupt or truncated clip would fail again; a vanished one is a no-op
            return null;
        }

        _store.Touch(id);
        return wav;
    }

    /// <summary>Drops the clip from the index and deletes its file, so the next request for
    /// it is a miss.</summary>
    public void Invalidate(SentenceCacheKey key)
    {
        Remove(key.Id);
        _store.Delete(key.Id);
    }

    /// <summary>Stores the clip (unless already cached), then evicts least recently used
    /// clips until the cache fits its budget. Only evicted files are deleted.</summary>
    public async Task PutAsync(SentenceCacheKey key, byte[] wav)
    {
        string id = key.Id;
        if (Contains(id))
        {
            return;
        }

        StoredSentence stored = await _store.WriteAsync(id, wav);
        List<string> evicted = AddAndEvict(stored);
        evicted.ForEach(_store.Delete);
    }

    /// <summary>Indexes what a previous run left on disk, so the first lookup doesn't.</summary>
    public void LoadIndex()
    {
        lock (_gate)
        {
            EnsureLoaded();
        }
    }

    public bool Contains(string id)
    {
        lock (_gate)
        {
            EnsureLoaded();
            return _entries.ContainsKey(id);
        }
    }

    private bool TouchEntry(string id)
    {
        lock (_gate)
        {
            EnsureLoaded();
            if (!_entries.TryGetValue(id, out Entry? entry))
            {
                return false;
            }

            entry.LastUsed = ++_tick;
            return true;
        }
    }

    private void Remove(string id)
    {
        lock (_gate)
        {
            if (_entries.Remove(id, out Entry? entry))
            {
                _totalBytes -= entry.Clip.SizeBytes;
            }
        }
    }

    private List<string> AddAndEvict(StoredSentence stored)
    {
        lock (_gate)
        {
            EnsureLoaded();
            if (_entries.Remove(stored.Id, out Entry? replaced))
            {
                _totalBytes -= replaced.Clip.SizeBytes; // two writers raced on one id
            }

            _entries[stored.Id] = new Entry(stored) { LastUsed = ++_tick };
            _totalBytes += stored.SizeBytes;
            return EvictOverBudget(keep: stored.Id);
        }
    }

    // Called under _gate. Never evicts the clip just written, so one oversized clip still
    // plays this read even though it will be the next to go.
    private List<string> EvictOverBudget(string keep)
    {
        var evicted = new List<string>();
        while (_totalBytes > _maxBytes && _entries.Count > 1)
        {
            string oldest = _entries.Where(e => e.Key != keep).MinBy(e => e.Value.LastUsed).Key;
            _totalBytes -= _entries[oldest].Clip.SizeBytes;
            _entries.Remove(oldest);
            evicted.Add(oldest);
        }

        return evicted;
    }

    // Called under _gate: the first use indexes what a previous run left behind, in the
    // store's oldest-first order, then trims to budget.
    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        foreach (StoredSentence stored in _store.List())
        {
            _entries[stored.Id] = new Entry(stored) { LastUsed = ++_tick };
            _totalBytes += stored.SizeBytes;
        }

        EvictOverBudget(keep: string.Empty).ForEach(_store.Delete);
    }

    private sealed class Entry(StoredSentence clip)
    {
        public StoredSentence Clip { get; } = clip;

        public long LastUsed { get; set; }
    }
}
