using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public sealed class FileSentenceStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"rtst-sentences-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task A_written_clip_reads_back_and_lists_with_its_duration()
    {
        var store = new FileSentenceStore(_folder);
        byte[] wav = SentenceCacheTestData.Wav(samples: 500);

        StoredSentence stored = await store.WriteAsync("abc", wav);

        Assert.Equal(wav, await store.ReadAsync("abc"));
        StoredSentence listed = Assert.Single(store.List());
        Assert.Equal("abc", listed.Id);
        Assert.Equal(stored.Path, listed.Path);
        Assert.Equal(wav.Length, listed.SizeBytes);
        Assert.Equal(TimeSpan.FromMilliseconds(500), listed.Duration);
    }

    [Fact]
    public async Task Delete_removes_only_that_clip()
    {
        var store = new FileSentenceStore(_folder);
        await store.WriteAsync("keep", SentenceCacheTestData.Wav());
        await store.WriteAsync("drop", SentenceCacheTestData.Wav());

        store.Delete("drop");

        Assert.Null(await store.ReadAsync("drop"));
        Assert.Equal(["keep"], store.List().Select(s => s.Id));
    }

    [Fact]
    public async Task Listing_purges_partial_files_and_skips_clips_with_no_valid_header()
    {
        var store = new FileSentenceStore(_folder);
        await store.WriteAsync("good", SentenceCacheTestData.Wav());
        string partial = Path.Combine(_folder, "half.wav.1234.tmp");
        await File.WriteAllBytesAsync(partial, [1, 2, 3], TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(_folder, "junk.wav"), [1, 2, 3], TestContext.Current.CancellationToken);

        IReadOnlyList<StoredSentence> listed = store.List();

        Assert.Equal(["good"], listed.Select(s => s.Id));
        Assert.False(File.Exists(partial));
    }

    [Fact]
    public async Task Listing_is_oldest_used_first_and_touch_makes_a_clip_newest()
    {
        var store = new FileSentenceStore(_folder);
        await store.WriteAsync("old", SentenceCacheTestData.Wav());
        await store.WriteAsync("new", SentenceCacheTestData.Wav());
        File.SetLastWriteTimeUtc(Path.Combine(_folder, "old.wav"), DateTime.UtcNow.AddHours(-2));
        File.SetLastWriteTimeUtc(Path.Combine(_folder, "new.wav"), DateTime.UtcNow.AddHours(-1));
        Assert.Equal(["old", "new"], store.List().Select(s => s.Id));

        store.Touch("old");

        Assert.Equal(["new", "old"], store.List().Select(s => s.Id));
    }

    [Fact]
    public async Task A_missing_folder_lists_empty_and_is_recreated_on_write()
    {
        var store = new FileSentenceStore(_folder);
        Assert.Empty(store.List());

        await store.WriteAsync("abc", SentenceCacheTestData.Wav());

        Assert.NotNull(await store.ReadAsync("abc"));
    }

    [Fact]
    public async Task Listing_deletes_a_clip_shorter_than_its_header_declares()
    {
        var store = new FileSentenceStore(_folder);
        await store.WriteAsync("good", SentenceCacheTestData.Wav());
        await store.WriteAsync("cut", SentenceCacheTestData.Wav());
        string cut = Path.Combine(_folder, "cut.wav");
        byte[] bytes = await File.ReadAllBytesAsync(cut, TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(cut, bytes[..(WavFile.HeaderBytes + 10)], TestContext.Current.CancellationToken);

        IReadOnlyList<StoredSentence> listed = store.List();

        Assert.Equal(["good"], listed.Select(s => s.Id));
        Assert.False(File.Exists(cut));
    }

    [Fact]
    public async Task Reading_a_missing_clip_is_null()
    {
        Assert.Null(await new FileSentenceStore(_folder).ReadAsync("nope"));
    }
}
