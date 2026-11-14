using DinoReader.Core;
using Sirous.Reading.Application.Usage;

namespace DinoReader.Tests;

/// <summary>Slice 278 A3 (design 2): one predicate decides what an own book is; the library never lists one; the first start after the update deletes them once, book by book, and survives a failure or a kill mid-way.</summary>
public class OwnBooksCleanupTests : IDisposable
{
    private const string StoreId = "0123456789abcdef0123456789abcdef";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "dino-own-books-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static Story Book(string? sourceId) =>
        new(StoryIds.New(), "Title", "en", DateTimeOffset.UnixEpoch, [new StoryPage("Once.")], [], CoverStyle.Colour, 0, "v") { SourceId = sourceId };

    private async Task<(FileStoryLibrary Library, Story Own, Story Store)> ShelfAsync()
    {
        var library = new FileStoryLibrary(_root);
        Story own = Book(null);
        Story store = Book(StoreId);
        await library.AddAsync(own);
        await library.AddAsync(store);
        return (library, own, store);
    }

    private void WriteRawStory(string id, string json)
    {
        Directory.CreateDirectory(Path.Combine(_root, id));
        File.WriteAllText(Path.Combine(_root, id, "story.json"), json);
    }

    [Theory]
    [InlineData("{\"id\":\"a\"}", true)]
    [InlineData("{\"id\":\"a\",\"sourceId\":null}", true)]
    [InlineData("{\"id\":\"a\",\"sourceId\":\"0123456789abcdef0123456789abcdef\"}", false)]
    [InlineData("{\"id\":\"a\",\"sourceId\":\"not-a-valid-id\"}", false)]
    [InlineData("{\"id\":\"a\",\"sourceId\":\"\"}", false)]
    [InlineData("{\"id\":\"a\",\"sourceId\":42}", false)]
    [InlineData("{\"id\":", false)]
    [InlineData("[]", false)]
    public void Only_an_absent_or_null_source_id_field_counts_as_an_own_book(string json, bool expected)
    {
        Assert.Equal(expected, OwnBook.IsOwn(json));
    }

    [Fact]
    public async Task The_library_never_lists_an_own_book_but_still_finds_it_by_id()
    {
        (FileStoryLibrary library, Story own, Story store) = await ShelfAsync();

        IReadOnlyList<Story> listed = await library.ListAsync();

        Assert.Equal([store.Id], listed.Select(s => s.Id));
        Assert.NotNull(await library.GetAsync(own.Id));
    }

    [Fact]
    public async Task A_book_with_a_present_but_malformed_source_id_stays_listed_and_is_not_cleaned()
    {
        var library = new FileStoryLibrary(_root);
        Story damaged = Book("garbage");
        await library.AddAsync(damaged);
        Assert.Contains("garbage", File.ReadAllText(Path.Combine(_root, damaged.Id, "story.json")));

        Assert.Equal([damaged.Id], (await library.ListAsync()).Select(s => s.Id));
        Assert.Empty((await library.ListOwnFoldersAsync()).Ids);
    }

    [Fact]
    public async Task Own_folders_lists_the_own_books_and_is_complete()
    {
        (FileStoryLibrary library, Story own, _) = await ShelfAsync();

        OwnFolders folders = await library.ListOwnFoldersAsync();

        Assert.Equal([own.Id], folders.Ids);
        Assert.True(folders.Complete);
    }

    [Fact]
    public async Task A_folder_with_no_story_file_yet_is_skipped_and_not_a_failure()
    {
        (FileStoryLibrary library, Story own, _) = await ShelfAsync();
        Directory.CreateDirectory(Path.Combine(_root, StoryIds.New()));

        OwnFolders folders = await library.ListOwnFoldersAsync();

        Assert.Equal([own.Id], folders.Ids);
        Assert.True(folders.Complete);
    }

    [Fact]
    public async Task A_story_file_that_cannot_be_read_makes_the_listing_incomplete()
    {
        (FileStoryLibrary library, Story own, _) = await ShelfAsync();
        string lockedId = StoryIds.New();
        WriteRawStory(lockedId, "{\"id\":\"x\"}");
        using var held = new FileStream(Path.Combine(_root, lockedId, "story.json"), FileMode.Open, FileAccess.Read, FileShare.None);

        OwnFolders folders = await library.ListOwnFoldersAsync();

        Assert.False(folders.Complete);
        Assert.Contains(own.Id, folders.Ids);
    }

    [Fact]
    public async Task A_fresh_install_with_no_story_folder_is_a_clean_pass()
    {
        var keys = new FakeKeys();
        var sink = new RecordingSink();

        int deleted = await OwnBooksCleanupRun.RunOnceAsync(new FileStoryLibrary(_root), keys, sink);

        Assert.Equal(0, deleted);
        Assert.True(keys.Done);
        Assert.Equal(0, keys.FailedStarts);
        Assert.Empty(sink.Reports);
    }

    [Fact]
    public async Task The_cleanup_deletes_own_books_and_keeps_every_book_with_a_source_id()
    {
        (FileStoryLibrary library, Story own, Story store) = await ShelfAsync();
        var keys = new FakeKeys();

        int deleted = await OwnBooksCleanupRun.RunOnceAsync(library, keys, new RecordingSink());

        Assert.Equal(1, deleted);
        Assert.True(keys.Done);
        Assert.False(Directory.Exists(library.FolderOf(own.Id)));
        Assert.True(Directory.Exists(library.FolderOf(store.Id)));
    }

    [Fact]
    public async Task An_incomplete_listing_is_a_failed_start_and_never_marks_done_on_a_partial_view()
    {
        (FileStoryLibrary inner, _, _) = await ShelfAsync();
        var library = new FlakyLibrary(inner) { Incomplete = true };
        var keys = new FakeKeys();

        for (int start = 1; start < OwnBooksCleanupRun.MaxFailedStarts; start++)
        {
            await OwnBooksCleanupRun.RunOnceAsync(library, keys, new RecordingSink());
            Assert.False(keys.Done);
        }

        await OwnBooksCleanupRun.RunOnceAsync(library, keys, new RecordingSink());

        Assert.True(keys.Done);
        Assert.Equal(OwnBooksCleanupRun.MaxFailedStarts, keys.FailedStarts);
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(InvalidOperationException))]
    public async Task One_failing_book_is_reported_the_rest_still_go_and_the_next_start_retries(Type failure)
    {
        var inner = new FileStoryLibrary(_root);
        Story[] owns = [Book(null), Book(null), Book(null)];
        foreach (Story story in owns)
        {
            await inner.AddAsync(story);
        }

        string poisoned = (await inner.ListOwnFoldersAsync()).Ids[0]; // first in the list: it must not stop the others
        var library = new FlakyLibrary(inner) { FailOn = poisoned, Failure = failure };
        var sink = new RecordingSink();
        var keys = new FakeKeys();

        int deleted = await OwnBooksCleanupRun.RunOnceAsync(library, keys, sink);

        Assert.Equal(2, deleted);
        Assert.False(keys.Done);
        Assert.Equal(1, keys.FailedStarts);
        Assert.Equal([poisoned], (await inner.ListOwnFoldersAsync()).Ids);
        Assert.Equal([(FallbackArea.Library, FallbackReason.StoryUnreadable)], sink.Reports);

        library.FailOn = null;
        int second = await OwnBooksCleanupRun.RunOnceAsync(library, keys, new RecordingSink());

        Assert.Equal(1, second);
        Assert.True(keys.Done);
        Assert.Empty((await inner.ListOwnFoldersAsync()).Ids);
    }

    [Fact]
    public async Task A_book_that_never_deletes_stops_being_retried_after_three_failed_starts()
    {
        var inner = new FileStoryLibrary(_root);
        Story own = Book(null);
        await inner.AddAsync(own);
        var library = new FlakyLibrary(inner) { FailOn = own.Id, Failure = typeof(IOException) };
        var keys = new FakeKeys();

        for (int start = 1; start < OwnBooksCleanupRun.MaxFailedStarts; start++)
        {
            await OwnBooksCleanupRun.RunOnceAsync(library, keys, new RecordingSink());
            Assert.False(keys.Done);
        }

        await OwnBooksCleanupRun.RunOnceAsync(library, keys, new RecordingSink());

        Assert.True(keys.Done);
        var sink = new RecordingSink();
        await OwnBooksCleanupRun.RunOnceAsync(library, keys, sink);
        Assert.Empty(sink.Reports);
    }

    [Fact]
    public async Task Once_done_a_later_start_does_not_touch_the_shelf()
    {
        (FileStoryLibrary library, Story own, _) = await ShelfAsync();
        var keys = new FakeKeys { Done = true };

        int deleted = await OwnBooksCleanupRun.RunOnceAsync(library, keys, new RecordingSink());

        Assert.Equal(0, deleted);
        Assert.True(Directory.Exists(library.FolderOf(own.Id)));
    }

    [Fact]
    public async Task The_flight_runs_once_per_app_run_and_survives_a_listing_that_throws()
    {
        (FileStoryLibrary inner, _, _) = await ShelfAsync();
        var library = new FlakyLibrary(inner);
        var keys = new FakeKeys();
        var flight = new OwnBooksCleanupFlight(library, keys, new RecordingSink());

        int first = await flight.RunOnceAsync();
        int second = await flight.RunOnceAsync();

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal(1, library.OwnListings);

        var broken = new FlakyLibrary(inner) { ThrowOnListing = true };
        var brokenKeys = new FakeKeys();
        int result = await new OwnBooksCleanupFlight(broken, brokenKeys, new RecordingSink()).RunOnceAsync();

        Assert.Equal(0, result);
        Assert.Equal(1, brokenKeys.FailedStarts);
    }

    [Fact]
    public async Task A_restore_drop_removes_the_own_books_and_reports_only_the_kept_ones()
    {
        (FileStoryLibrary library, Story own, Story store) = await ShelfAsync();

        OwnBooksCleanupResult cleaned = await OwnBooksCleanupRun.RunAsync(library, new RecordingSink());

        Assert.Equal([own.Id], cleaned.Deleted);
        Assert.Equal(0, cleaned.Failed);
        Assert.True(cleaned.Complete);
        Assert.Equal([store.Id], (await library.ListAsync()).Select(s => s.Id));
    }

    private sealed class FakeKeys : IOwnBooksCleanupKeys
    {
        public bool Done { get; set; }

        public int FailedStarts { get; set; }
    }

    [Fact]
    public async Task A_delete_that_fails_before_the_last_step_leaves_the_own_book_listed()
    {
        // The failure is injected with an exclusive lock on a file, which only Windows enforces;
        // on Linux a delete succeeds despite FileShare.None (same pattern as LibraryHardeningTests).
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Needs mandatory file locking (Windows).");
        var library = new FileStoryLibrary(_root);
        Story own = Book(null);
        await library.AddAsync(own);
        string recording = Path.Combine(library.FolderOf(own.Id), "rec-1.m4a");
        File.WriteAllText(recording, "x");

        using (new FileStream(recording, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsAnyAsync<IOException>(() => library.DeleteAsync(own.Id));

            Assert.True(File.Exists(Path.Combine(library.FolderOf(own.Id), "story.json")));
            Assert.Contains(own.Id, (await library.ListOwnFoldersAsync()).Ids);
        }

        await library.DeleteAsync(own.Id);
        Assert.False(Directory.Exists(library.FolderOf(own.Id)));
    }

    private sealed class RecordingSink : IFallbackSink
    {
        public List<(string Area, string Reason)> Reports { get; } = [];

        public void Report(string area, string reason, string? language = null) => Reports.Add((area, reason));
    }

    /// <summary>A real on-disk library with failures injected where the cleanup touches it.</summary>
    private sealed class FlakyLibrary(FileStoryLibrary inner) : IStoryLibrary
    {
        public string? FailOn { get; set; }

        public Type Failure { get; set; } = typeof(IOException);

        public bool Incomplete { get; set; }

        public bool ThrowOnListing { get; set; }

        public int OwnListings { get; private set; }

        public async Task<OwnFolders> ListOwnFoldersAsync()
        {
            OwnListings++;
            if (ThrowOnListing)
            {
                throw new InvalidOperationException("listing failed");
            }

            OwnFolders real = await inner.ListOwnFoldersAsync();
            return Incomplete ? real with { Complete = false } : real;
        }

        public Task DeleteAsync(string id) =>
            id == FailOn ? throw (Exception)Activator.CreateInstance(Failure, "boom")! : inner.DeleteAsync(id);

        public Task<IReadOnlyList<Story>> ListAsync() => inner.ListAsync();

        public Task<Story?> GetAsync(string id) => inner.GetAsync(id);

        public Task AddAsync(Story story) => inner.AddAsync(story);

        public Task UpdateAsync(Story story) => inner.UpdateAsync(story);

        public string FolderOf(string id) => inner.FolderOf(id);

        public string? TryResolveFile(string storyId, string? fileName) => inner.TryResolveFile(storyId, fileName);

        public Task<string> StoreFileAsync(string storyId, string prefix, string extension, Func<string, Task> write) => inner.StoreFileAsync(storyId, prefix, extension, write);

        public Task DeleteFileAsync(string storyId, string fileName) => inner.DeleteFileAsync(storyId, fileName);
    }
}
