using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class SentenceAudioCacheTests
{
    private static readonly int ClipBytes = SentenceCacheTestData.Wav().Length;

    [Fact]
    public async Task A_stored_sentence_is_a_hit_for_the_same_sentence_voice_and_language()
    {
        var cache = new SentenceAudioCache(new InMemorySentenceStore());
        byte[] wav = SentenceCacheTestData.Wav();
        await cache.PutAsync(SentenceCacheTestData.Key(), wav);

        Assert.Equal(wav, await cache.TryGetAsync(SentenceCacheTestData.Key()));
    }

    [Theory]
    [InlineData("Another sentence.", "F1", "en")]
    [InlineData("Hello there, world.", "M1", "en")]
    [InlineData("Hello there, world.", "F1", "fr")]
    public async Task Any_change_to_sentence_voice_or_language_is_a_miss(string text, string voice, string language)
    {
        var cache = new SentenceAudioCache(new InMemorySentenceStore());
        await cache.PutAsync(SentenceCacheTestData.Key(), SentenceCacheTestData.Wav());

        Assert.Null(await cache.TryGetAsync(SentenceCacheTestData.Key(text, voice, language)));
    }

    [Fact]
    public async Task Switching_voice_misses_and_switching_back_hits()
    {
        var cache = new SentenceAudioCache(new InMemorySentenceStore());
        byte[] f1 = SentenceCacheTestData.Wav(level: 0.2f);
        byte[] m1 = SentenceCacheTestData.Wav(level: 0.8f);
        await cache.PutAsync(SentenceCacheTestData.Key(voice: "F1"), f1);

        Assert.Null(await cache.TryGetAsync(SentenceCacheTestData.Key(voice: "M1")));
        await cache.PutAsync(SentenceCacheTestData.Key(voice: "M1"), m1);

        Assert.Equal(m1, await cache.TryGetAsync(SentenceCacheTestData.Key(voice: "M1")));
        Assert.Equal(f1, await cache.TryGetAsync(SentenceCacheTestData.Key(voice: "F1")));
    }

    [Fact]
    public async Task The_least_recently_used_sentence_is_evicted_over_the_budget()
    {
        var store = new InMemorySentenceStore();
        var cache = new SentenceAudioCache(store, maxBytes: ClipBytes * 2);
        await cache.PutAsync(SentenceCacheTestData.Key("One."), SentenceCacheTestData.Wav());
        await cache.PutAsync(SentenceCacheTestData.Key("Two."), SentenceCacheTestData.Wav());
        await cache.TryGetAsync(SentenceCacheTestData.Key("One.")); // One is now newer than Two

        await cache.PutAsync(SentenceCacheTestData.Key("Three."), SentenceCacheTestData.Wav());

        Assert.Equal([SentenceCacheTestData.Key("Two.").Id], store.Deleted);
        Assert.Null(await cache.TryGetAsync(SentenceCacheTestData.Key("Two.")));
        Assert.NotNull(await cache.TryGetAsync(SentenceCacheTestData.Key("One.")));
        Assert.NotNull(await cache.TryGetAsync(SentenceCacheTestData.Key("Three.")));
        Assert.Equal(ClipBytes * 2, cache.TotalBytes);
    }

    [Fact]
    public async Task Only_evicted_files_are_deleted()
    {
        var store = new InMemorySentenceStore();
        var cache = new SentenceAudioCache(store, maxBytes: ClipBytes * 10);

        for (int i = 0; i < 5; i++)
        {
            await cache.PutAsync(SentenceCacheTestData.Key($"Sentence {i}."), SentenceCacheTestData.Wav());
        }

        Assert.Empty(store.Deleted);
        Assert.Equal(5, cache.Count);
    }

    [Fact]
    public async Task A_clip_larger_than_the_budget_is_kept_for_this_read()
    {
        var cache = new SentenceAudioCache(new InMemorySentenceStore(), maxBytes: 10);
        await cache.PutAsync(SentenceCacheTestData.Key(), SentenceCacheTestData.Wav());

        Assert.NotNull(await cache.TryGetAsync(SentenceCacheTestData.Key()));
    }

    [Fact]
    public async Task Putting_an_existing_sentence_again_stores_nothing_new()
    {
        var cache = new SentenceAudioCache(new InMemorySentenceStore());
        await cache.PutAsync(SentenceCacheTestData.Key(), SentenceCacheTestData.Wav());
        await cache.PutAsync(SentenceCacheTestData.Key(), SentenceCacheTestData.Wav());

        Assert.Equal(1, cache.Count);
        Assert.Equal(ClipBytes, cache.TotalBytes);
    }

    [Fact]
    public async Task A_clip_whose_file_vanished_is_a_miss_and_leaves_the_index()
    {
        var store = new InMemorySentenceStore();
        var cache = new SentenceAudioCache(store);
        await cache.PutAsync(SentenceCacheTestData.Key(), SentenceCacheTestData.Wav());
        store.Lose(SentenceCacheTestData.Key().Id);

        Assert.Null(await cache.TryGetAsync(SentenceCacheTestData.Key()));
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.TotalBytes);
    }

    [Fact]
    public async Task A_truncated_clip_is_a_miss_and_is_deleted()
    {
        var store = new InMemorySentenceStore();
        var cache = new SentenceAudioCache(store);
        byte[] truncated = SentenceCacheTestData.Wav()[..(WavFile.HeaderBytes + 10)];
        await cache.PutAsync(SentenceCacheTestData.Key(), truncated);

        Assert.Null(await cache.TryGetAsync(SentenceCacheTestData.Key()));

        Assert.Equal(0, cache.Count);
        Assert.Contains(SentenceCacheTestData.Key().Id, store.Deleted);
        Assert.False(store.Exists(SentenceCacheTestData.Key().Id));
    }

    [Fact]
    public async Task A_previous_runs_clips_are_indexed_and_trimmed_to_the_budget_on_first_use()
    {
        var store = new InMemorySentenceStore();
        var seeder = new SentenceAudioCache(store);
        await seeder.PutAsync(SentenceCacheTestData.Key("Old."), SentenceCacheTestData.Wav());
        await seeder.PutAsync(SentenceCacheTestData.Key("New."), SentenceCacheTestData.Wav());

        var restarted = new SentenceAudioCache(store, maxBytes: ClipBytes);

        Assert.Equal(1, restarted.Count);
        Assert.Single(store.Deleted);
    }
}
