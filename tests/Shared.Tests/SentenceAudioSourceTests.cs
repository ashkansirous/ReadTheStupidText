using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class SentenceAudioSourceTests
{
    private static (SentenceAudioSource Source, SentenceAudioCache Cache, RecordingSystemLog Log, InMemorySentenceStore Store) Create()
    {
        var store = new InMemorySentenceStore();
        var cache = new SentenceAudioCache(store);
        var log = new RecordingSystemLog();
        return (new SentenceAudioSource(cache, log), cache, log, store);
    }

    [Fact]
    public async Task A_miss_synthesizes_stores_and_logs_the_hash_and_length()
    {
        (SentenceAudioSource source, SentenceAudioCache cache, RecordingSystemLog log, _) = Create();
        SentenceCacheKey key = SentenceCacheTestData.Key("Hello there, world.");
        byte[] wav = SentenceCacheTestData.Wav();

        byte[] result = await source.GetAsync(key, _ => Task.FromResult(wav), CancellationToken.None);

        Assert.Equal(wav, result);
        Assert.Equal(wav, await cache.TryGetAsync(key));
        Assert.Contains($"cache miss {key.ShortHash} (19 chars)", log.Lines);
    }

    [Fact]
    public async Task A_hit_does_not_synthesize_and_logs_a_hit()
    {
        (SentenceAudioSource source, _, RecordingSystemLog log, _) = Create();
        SentenceCacheKey key = SentenceCacheTestData.Key();
        await source.GetAsync(key, _ => Task.FromResult(SentenceCacheTestData.Wav()), CancellationToken.None);
        int calls = 0;

        await source.GetAsync(key, _ => { calls++; return Task.FromResult(SentenceCacheTestData.Wav()); }, CancellationToken.None);

        Assert.Equal(0, calls);
        Assert.Contains($"cache hit {key.ShortHash} (19 chars)", log.Lines);
    }

    [Fact]
    public async Task Only_the_edited_sentence_misses()
    {
        (SentenceAudioSource source, _, RecordingSystemLog log, _) = Create();
        string[] sentences = ["One is here.", "Two is here.", "Three is here.", "Four is here."];
        foreach (string s in sentences)
        {
            await source.GetAsync(SentenceCacheTestData.Key(s), _ => Task.FromResult(SentenceCacheTestData.Wav()), CancellationToken.None);
        }

        log.Lines.Clear();
        sentences[3] = "Four was edited.";
        foreach (string s in sentences)
        {
            await source.GetAsync(SentenceCacheTestData.Key(s), _ => Task.FromResult(SentenceCacheTestData.Wav()), CancellationToken.None);
        }

        Assert.Equal(3, log.Lines.Count(l => l.StartsWith("cache hit")));
        Assert.Single(log.Lines, l => l.StartsWith("cache miss"));
    }

    [Fact]
    public async Task Two_callers_for_one_sentence_share_one_synthesis()
    {
        (SentenceAudioSource source, _, _, _) = Create();
        SentenceCacheKey key = SentenceCacheTestData.Key();
        var release = new TaskCompletionSource<byte[]>();
        int calls = 0;
        Task<byte[]> Synthesize(CancellationToken _) { Interlocked.Increment(ref calls); return release.Task; }

        Task<byte[]> first = source.GetAsync(key, Synthesize, CancellationToken.None);
        Task<byte[]> second = source.GetAsync(key, Synthesize, CancellationToken.None);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        release.SetResult(SentenceCacheTestData.Wav());

        await Task.WhenAll(first, second);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task A_cancelled_caller_gets_nothing_but_the_finished_clip_is_cached()
    {
        (SentenceAudioSource source, SentenceAudioCache cache, _, _) = Create();
        SentenceCacheKey key = SentenceCacheTestData.Key();
        using var cts = new CancellationTokenSource();
        var release = new TaskCompletionSource<byte[]>();

        Task<byte[]> read = source.GetAsync(key, _ => release.Task, cts.Token);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        cts.Cancel();
        release.SetResult(SentenceCacheTestData.Wav());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
        await WaitForAsync(async () => await cache.TryGetAsync(key) is not null);
    }

    [Fact]
    public async Task The_shared_synthesis_is_cached_even_when_its_starter_was_cancelled_and_nobody_waits()
    {
        (SentenceAudioSource source, SentenceAudioCache cache, _, _) = Create();
        SentenceCacheKey key = SentenceCacheTestData.Key();
        using var starterCts = new CancellationTokenSource();
        var release = new TaskCompletionSource<byte[]>();
        Task<byte[]> starter = source.GetAsync(key, _ => release.Task, starterCts.Token); // ignores its token, like a native call

        await Task.Delay(50, TestContext.Current.CancellationToken);
        starterCts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => starter);
        release.SetResult(SentenceCacheTestData.Wav());

        await WaitForAsync(async () => await cache.TryGetAsync(key) is not null);
    }

    [Fact]
    public async Task An_invalidated_clip_is_synthesized_afresh()
    {
        (SentenceAudioSource source, _, _, _) = Create();
        SentenceCacheKey key = SentenceCacheTestData.Key();
        int calls = 0;
        Task<byte[]> Synthesize(CancellationToken _) { calls++; return Task.FromResult(SentenceCacheTestData.Wav()); }
        await source.GetAsync(key, Synthesize, CancellationToken.None);

        source.Invalidate(key);
        await source.GetAsync(key, Synthesize, CancellationToken.None);

        Assert.Equal(2, calls);
    }

    private static async Task WaitForAsync(Func<Task<bool>> condition)
    {
        for (int i = 0; i < 100; i++)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.Fail("Condition was not met in time.");
    }

    [Fact]
    public async Task A_caller_that_joined_a_cancelled_synthesis_starts_its_own()
    {
        (SentenceAudioSource source, _, _, _) = Create();
        SentenceCacheKey key = SentenceCacheTestData.Key();
        using var starterCts = new CancellationTokenSource();
        var firstStarted = new TaskCompletionSource();

        Task<byte[]> starter = source.GetAsync(key, async t =>
        {
            firstStarted.SetResult();
            await Task.Delay(Timeout.Infinite, t); // waits for a slot that never comes
            return [];
        }, starterCts.Token);
        await firstStarted.Task;

        Task<byte[]> joiner = source.GetAsync(key, _ => Task.FromResult(SentenceCacheTestData.Wav()), CancellationToken.None);
        starterCts.Cancel();

        Assert.NotEmpty(await joiner);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => starter);
    }

    [Fact]
    public async Task A_clip_stored_between_the_cache_miss_and_the_in_flight_lookup_is_a_hit()
    {
        var cache = new SentenceAudioCache(new InMemorySentenceStore());
        SentenceCacheKey key = SentenceCacheTestData.Key();
        var log = new HookedLog(() => cache.PutAsync(key, SentenceCacheTestData.Wav()).GetAwaiter().GetResult());
        var source = new SentenceAudioSource(cache, log);
        int calls = 0;

        byte[] result = await source.GetAsync(
            key, _ => { calls++; return Task.FromResult(SentenceCacheTestData.Wav()); }, CancellationToken.None);

        Assert.NotEmpty(result);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task A_joiner_never_sees_a_cancel_it_did_not_ask_for_however_many_starters_are_cancelled()
    {
        (SentenceAudioSource source, _, _, _) = Create();
        for (int i = 0; i < 3; i++)
        {
            SentenceCacheKey key = SentenceCacheTestData.Key($"Sentence number {i} is here.");
            var cts = new CancellationTokenSource();
            var begun = new TaskCompletionSource();
            Task<byte[]> t = source.GetAsync(key, async tk => { begun.TrySetResult(); await Task.Delay(Timeout.Infinite, tk); return []; }, cts.Token);
            await begun.Task;
            Task<byte[]> joiner = source.GetAsync(key, _ => Task.FromResult(SentenceCacheTestData.Wav()), CancellationToken.None);
            cts.Cancel();
            Assert.NotEmpty(await joiner);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => t);
        }
    }

    private sealed class HookedLog(Action onMiss) : ISystemLog
    {
        public void Info(string message, int? activityId = null)
        {
        }

        public void Debug(string message, int? activityId = null)
        {
            if (message.StartsWith("cache miss"))
            {
                onMiss();
            }
        }

        public void Warning(string message, int? activityId = null, Exception? error = null)
        {
        }

        public void Error(string message, int? activityId = null, Exception? error = null)
        {
        }
    }

    [Fact]
    public async Task A_failing_cache_store_never_fails_the_read()
    {
        var store = new ThrowingStore();
        var source = new SentenceAudioSource(new SentenceAudioCache(store), new RecordingSystemLog());
        byte[] wav = SentenceCacheTestData.Wav();

        byte[] result = await source.GetAsync(SentenceCacheTestData.Key(), _ => Task.FromResult(wav), CancellationToken.None);

        Assert.Equal(wav, result);
    }

    private sealed class ThrowingStore : ISentenceStore
    {
        public IReadOnlyList<StoredSentence> List() => [];

        public Task<byte[]?> ReadAsync(string id) => Task.FromResult<byte[]?>(null);

        public Task<StoredSentence> WriteAsync(string id, byte[] wav) => throw new IOException("disk full");

        public void Touch(string id)
        {
        }

        public void Delete(string id)
        {
        }
    }
}
