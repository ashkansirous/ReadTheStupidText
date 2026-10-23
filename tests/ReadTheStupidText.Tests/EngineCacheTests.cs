using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class EngineCacheTests
{
    private static VoiceModelPaths Model(string folder, string version = "1", string language = "fa") =>
        new(folder, Language: language, PackVersion: version);

    // Engines built over the same model share one reference count, as the readers' engines share one OfflineTts.
    private sealed class Engine(VoiceModelPaths model, Engine? sharedWith = null) : IEngineHandle
    {
        private readonly Counter _counter = sharedWith?._counter ?? new Counter();

        public VoiceModelPaths Model { get; } = model;

        public int References => _counter.Value;

        public bool Disposed => _counter.Value == 0;

        public bool TryAcquire() => _counter.TryAcquire();

        public void Release() => _counter.Release();

        private sealed class Counter
        {
            private int _value = 1;

            public int Value
            {
                get
                {
                    lock (this)
                    {
                        return _value;
                    }
                }
            }

            public bool TryAcquire()
            {
                lock (this)
                {
                    if (_value == 0)
                    {
                        return false;
                    }

                    _value++;
                    return true;
                }
            }

            public void Release()
            {
                lock (this)
                {
                    _value--;
                }
            }
        }
    }

    // Fills the cache to capacity, the first entry already there being the caller's.
    private static void FillOthers(EngineCache<Engine> cache)
    {
        for (int i = 1; i < EngineCache<Engine>.Capacity; i++)
        {
            cache.Acquire(Model("filler" + i, "1"))!.Release();
        }
    }

    private static Engine Create(Engine? previous, VoiceModelPaths paths) =>
        previous is not null && previous.TryAcquire() ? new Engine(paths, previous) : new Engine(paths);

    [Fact]
    public void The_same_model_folder_and_version_is_loaded_once_and_shared()
    {
        int loads = 0;
        using var cache = new EngineCache<Engine>((prev, p) => { loads++; return Create(prev, p); });

        Engine first = cache.Acquire(Model("a"))!;
        Engine second = cache.Acquire(Model("a"))!;

        Assert.Same(first, second);
        Assert.Equal(1, loads);
        Assert.Equal(3, first.References); // the cache's and the two callers'
    }

    [Fact]
    public void Another_language_of_the_same_folder_relabels_the_loaded_model()
    {
        using var cache = new EngineCache<Engine>(Create);
        Engine farsi = cache.Acquire(Model("a", language: "fa"))!;

        Engine english = cache.Acquire(Model("a", language: "en"))!;

        Assert.NotSame(farsi, english);
        Assert.Equal("en", english.Model.Language);
        Assert.Equal(1, cache.Count);
        Assert.False(farsi.Disposed); // the model is shared, and the first holder still has its reference
    }

    [Fact]
    public void A_new_pack_version_is_a_new_engine_and_the_old_one_goes_when_nothing_uses_it()
    {
        using var cache = new EngineCache<Engine>(Create);
        Engine old = cache.Acquire(Model("a", "1"))!;
        FillOthers(cache);

        Engine updated = cache.Acquire(Model("a", "2"))!; // one key over capacity: the least recently used ("a" v1) is evicted

        Assert.NotSame(old, updated);
        Assert.False(old.Disposed); // still held by its reader: a running read is not cut off
        old.Release();
        Assert.True(old.Disposed);
    }

    [Fact]
    public void At_most_capacity_engines_are_kept_and_the_least_recently_used_leaves_first()
    {
        using var cache = new EngineCache<Engine>(Create);
        Engine[] engines = [.. Enumerable.Range(0, EngineCache<Engine>.Capacity).Select(i => cache.Acquire(Model("m" + i))!)];
        foreach (Engine e in engines)
        {
            e.Release();
        }

        cache.Acquire(Model("m0"))!.Release(); // m0 is now the most recently used

        Engine extra = cache.Acquire(Model("extra"))!;

        Assert.Equal(EngineCache<Engine>.Capacity, cache.Count);
        Assert.True(engines[1].Disposed);
        Assert.False(engines[0].Disposed);
        Assert.False(extra.Disposed);
    }

    [Fact]
    public void An_evicted_engine_that_is_still_in_use_is_disposed_by_its_last_release()
    {
        using var cache = new EngineCache<Engine>(Create);
        Engine a = cache.Acquire(Model("a"))!;
        FillOthers(cache);
        cache.Acquire(Model("over"))!.Release();

        Assert.False(a.Disposed);
        a.Release();
        Assert.True(a.Disposed);
    }

    [Fact]
    public async Task A_slow_load_of_one_model_does_not_block_another()
    {
        using var release = new ManualResetEventSlim();
        using var cache = new EngineCache<Engine>((prev, p) =>
        {
            if (p.RootDir == "slow")
            {
                release.Wait(TestContext.Current.CancellationToken);
            }

            return Create(prev, p);
        });
        Task<Engine?> slow = cache.AcquireAsync(Model("slow"));

        Engine? fast = await cache.AcquireAsync(Model("fast")).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.NotNull(fast);
        Assert.False(slow.IsCompleted);
        release.Set();
        Assert.NotNull(await slow);
    }

    [Fact]
    public async Task Concurrent_requests_for_one_model_load_it_once()
    {
        int loads = 0;
        using var cache = new EngineCache<Engine>((prev, p) => { Interlocked.Increment(ref loads); Thread.Sleep(50); return Create(prev, p); });

        Engine?[] engines = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => cache.AcquireAsync(Model("a"))));

        Assert.Equal(1, loads);
        Assert.All(engines, e => Assert.Same(engines[0], e));
    }

    [Fact]
    public void A_load_that_fails_throws_and_the_next_request_tries_again()
    {
        int calls = 0;
        using var cache = new EngineCache<Engine>((prev, p) => ++calls == 1 ? throw new InvalidOperationException("model") : Create(prev, p));

        Assert.Throws<InvalidOperationException>(() => cache.Acquire(Model("a")));

        Assert.NotNull(cache.Acquire(Model("a")));
    }

    [Fact]
    public void A_disposed_cache_releases_its_engines_and_hands_out_none()
    {
        var cache = new EngineCache<Engine>(Create);
        Engine a = cache.Acquire(Model("a"))!;
        a.Release(); // only the cache's reference is left

        cache.Dispose();

        Assert.True(a.Disposed);
        Assert.Null(cache.Acquire(Model("a")));
    }
}
