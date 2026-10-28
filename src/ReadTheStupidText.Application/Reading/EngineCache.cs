using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

/// <summary>An engine a reader can share: it knows its model and is reference counted, so it is disposed after its last holder.</summary>
internal interface IEngineHandle
{
    VoiceModelPaths Model { get; }

    /// <summary>Takes one reference; false when the engine was already disposed.</summary>
    bool TryAcquire();

    void Release();

    /// <summary>True while a read or generation other than the cache holds the engine (Slice 177: a warm-up must not evict such an engine).</summary>
    bool InUse => false;
}

/// <summary>
/// The engines one reader keeps built, keyed by (model folder, pack version, language for a Kokoro pack: its engine is configured for one language,
/// Slice 118) and holding at most
/// <see cref="Capacity"/> of them (a Kokoro pack's engine: only the latest language's), least recently used out first (Slice 89: a cast read alternates
/// between the model folders of one pack, and a pack update is a new folder, hence a new key). The
/// cache owns one reference per engine; a caller gets its own from <see cref="Acquire"/> and an
/// engine that is evicted is disposed only after its last user releases it. A model load takes a lock
/// of its own model only, so loading one never blocks a caller of another. <c>create(previous, paths)</c>
/// returns an engine that holds a reference of its own: it shares the previous engine's model when there
/// is one still alive (a re-label for another language), else it loads the model.
/// </summary>
internal sealed class EngineCache<TEngine>(Func<TEngine?, VoiceModelPaths, TEngine> create) : IDisposable
    where TEngine : class, IEngineHandle
{
    public const int Capacity = 4; // the most model folders a pack has (the Farsi pack); a Kokoro pack keeps only its latest language (EvictOtherKokoro)

    private const int AcquireAttempts = 2;

    private readonly object _lock = new();
    private readonly List<Slot> _slots = []; // least recently used first
    private bool _disposed;

    private sealed class Slot(ModelKey key)
    {
        public ModelKey Key { get; } = key;

        public object Load { get; } = new();

        public TEngine? Engine { get; set; }

        public bool Evicted { get; set; }

        public bool Loading { get; set; }
    }

    private readonly record struct ModelKey(string Folder, string Version, string Language, bool IsKokoro);

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _slots.Count;
            }
        }
    }

    /// <summary>
    /// The engine for <paramref name="paths"/> with a reference taken for the caller (who must
    /// <c>Release()</c> it), building it on first use; null once the cache is disposed. The same folder and
    /// version under another language re-labels the live engine instead of loading the model again (not for a Kokoro pack: one engine per language, and only the latest stays).
    /// A <paramref name="forWarmUp"/> call (Slice 177) returns null instead of evicting an engine that is in use; the next trigger tries again.
    /// </summary>
    public TEngine? Acquire(VoiceModelPaths paths, bool forWarmUp = false)
    {
        for (int attempt = 0; attempt < AcquireAttempts; attempt++)
        {
            if (Touch(paths, forWarmUp) is not { } slot)
            {
                return null;
            }

            if (AcquireFrom(slot, paths) is { } engine)
            {
                return engine;
            }
        }

        return null;
    }

    /// <summary><see cref="Acquire"/> on the thread pool, for a load that must not stall the caller's loop.</summary>
    public Task<TEngine?> AcquireAsync(VoiceModelPaths paths) => Task.Run(() => Acquire(paths));

    /// <summary>Gives back the cache's reference on every engine; each is disposed once its users let go.</summary>
    public void Dispose()
    {
        List<TEngine> dropped;
        lock (_lock)
        {
            _disposed = true;
            dropped = [.. _slots.Select(Evict).OfType<TEngine>()];
            _slots.Clear();
        }

        ReleaseAll(dropped);
    }

    private Slot? Touch(VoiceModelPaths paths, bool forWarmUp = false)
    {
        bool isKokoro = paths.Kind == VoiceEngineKind.Kokoro;
        var key = new ModelKey(paths.ModelFolder, paths.PackVersion, isKokoro ? paths.Language : string.Empty, isKokoro);
        List<TEngine> dropped = [];
        Slot slot;
        lock (_lock)
        {
            if (_disposed)
            {
                return null;
            }

            slot = _slots.Find(s => s.Key == key) ?? new Slot(key);
            if (forWarmUp && WouldEvictEngineInUse(slot))
            {
                return null;
            }

            _slots.Remove(slot);
            _slots.Add(slot);
            dropped.AddRange(EvictOtherKokoro(slot));
            while (_slots.Count > Capacity && _slots.Find(s => !s.Loading && s != slot) is { } victim)
            {
                dropped.AddRange(Evict(victim) is { } engine ? [engine] : []);
                _slots.Remove(victim);
            }
        }

        ReleaseAll(dropped);
        return slot;
    }

    // Called under _lock, before the slot is touched: would making it the most recent evict a slot whose engine a read is using?
    private bool WouldEvictEngineInUse(Slot slot)
    {
        List<Slot> remaining = [.. _slots.Where(s => s != slot)];
        List<Slot> victims = slot.Key.IsKokoro ? remaining.Where(s => s.Key.IsKokoro && !s.Loading).ToList() : [];
        remaining.RemoveAll(victims.Contains);
        while (remaining.Count + 1 > Capacity && remaining.Find(s => !s.Loading) is { } victim)
        {
            victims.Add(victim);
            remaining.Remove(victim);
        }

        return victims.Any(v => v.Engine?.InUse == true);
    }

    // Called under _lock. A Kokoro engine is one 114 MB model per language and a book reads one language, so at most one stays resident (Slice 118):
    // loading another language's drops the previous one (one still loading is left, as in Touch).
    private IEnumerable<TEngine> EvictOtherKokoro(Slot current)
    {
        if (!current.Key.IsKokoro)
        {
            return [];
        }

        List<Slot> others = _slots.Where(s => s != current && s.Key.IsKokoro && !s.Loading).ToList();
        List<TEngine> engines = [];
        foreach (Slot other in others)
        {
            engines.AddRange(Evict(other) is { } engine ? [engine] : []);
            _slots.Remove(other);
        }

        return engines;
    }

    private TEngine? AcquireFrom(Slot slot, VoiceModelPaths paths)
    {
        lock (slot.Load)
        {
            TEngine? current = ReadEngine(slot);
            bool reused = current is not null && current.Model == paths;
            TEngine engine = reused ? current! : CreateMarkingLoad(slot, current, paths);
            if (!engine.TryAcquire())
            {
                Forget(slot, engine);
                return null;
            }

            if (!reused)
            {
                Publish(slot, engine);
            }

            return engine;
        }
    }

    // A slot whose model is loading is not evicted (the finished load would be thrown away and loaded again);
    // the cache may briefly hold one more than Capacity instead.
    private TEngine CreateMarkingLoad(Slot slot, TEngine? current, VoiceModelPaths paths)
    {
        SetLoading(slot, true);
        try
        {
            return create(current, paths);
        }
        finally
        {
            SetLoading(slot, false);
        }
    }

    private void SetLoading(Slot slot, bool loading)
    {
        lock (_lock)
        {
            slot.Loading = loading;
        }
    }

    private TEngine? ReadEngine(Slot slot)
    {
        lock (_lock)
        {
            return slot.Engine;
        }
    }

    // A refused reference means the engine was disposed under us: the slot starts over.
    private void Forget(Slot slot, TEngine engine)
    {
        lock (_lock)
        {
            if (ReferenceEquals(slot.Engine, engine))
            {
                slot.Engine = null;
            }
        }
    }

    // A new engine carries its own cache reference: it replaces the slot's old engine (whose reference is
    // given back), or, for a slot evicted while the model was loading, is given straight back.
    private void Publish(Slot slot, TEngine engine)
    {
        TEngine? replaced;
        lock (_lock)
        {
            replaced = slot.Evicted ? engine : slot.Engine;
            if (!slot.Evicted)
            {
                slot.Engine = engine;
            }
        }

        replaced?.Release();
    }

    // Called under _lock. The slot's engine is returned for release outside the lock.
    private static TEngine? Evict(Slot slot)
    {
        slot.Evicted = true;
        TEngine? engine = slot.Engine;
        slot.Engine = null;
        return engine;
    }

    private static void ReleaseAll(List<TEngine> engines)
    {
        foreach (TEngine engine in engines)
        {
            engine.Release();
        }
    }
}
