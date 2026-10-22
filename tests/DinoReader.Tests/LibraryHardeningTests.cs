using DinoReader.Core;

namespace DinoReader.Tests;

public sealed class LibraryHardeningTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dino-harden-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static Story Make(string id, string title = "T") => new(
        id, title, "en", new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
        [new StoryPage("Text.")], [], CoverStyle.Colour, 0, "F1");

    [Fact]
    public async Task OldJsonWithoutCoverFields_StillLoads_AndDeleteRemovesTheCoverPhoto()
    {
        var library = new FileStoryLibrary(_root);
        Story story = Make(StoryIds.New());
        await library.AddAsync(story);
        string path = Path.Combine(library.FolderOf(story.Id), "story.json");
        string json = File.ReadAllText(path);
        Assert.DoesNotContain("\"CoverFile\": \"", json);

        File.WriteAllText(path, json.Replace("\"CoverFile\": null,", string.Empty).Replace("\"CoverPageIndex\": null,", string.Empty));
        Story? loaded = await library.GetAsync(story.Id);
        Assert.NotNull(loaded);
        Assert.Null(loaded.CoverFile);
        Assert.Null(loaded.CoverPageIndex);

        File.WriteAllText(Path.Combine(library.FolderOf(story.Id), "cover-abcd1234.jpg"), "x");
        await library.DeleteAsync(story.Id);
        Assert.False(Directory.Exists(library.FolderOf(story.Id)));
    }

    [Fact]
    public async Task StoreFile_WritesAFreshNamedFile_AndDeleteFileRemovesOnlyThat()
    {
        var library = new FileStoryLibrary(_root);
        Story story = Make(StoryIds.New());
        await library.AddAsync(story);

        string first = await library.StoreFileAsync(story.Id, Story.CoverFilePrefix, Story.CoverFileExtension, t => File.WriteAllTextAsync(t, "a"));
        string second = await library.StoreFileAsync(story.Id, Story.CoverFilePrefix, Story.CoverFileExtension, t => File.WriteAllTextAsync(t, "b"));

        Assert.NotEqual(first, second);
        Assert.StartsWith("cover-", first);
        Assert.Equal("a", File.ReadAllText(library.TryResolveFile(story.Id, first)!));
        await library.DeleteFileAsync(story.Id, first);
        await library.DeleteFileAsync(story.Id, "story.json");
        await Assert.ThrowsAsync<ArgumentException>(() => library.StoreFileAsync(story.Id, "page", Story.CoverFileExtension, t => File.WriteAllTextAsync(t, "p")));
        await Assert.ThrowsAsync<ArgumentException>(() => library.StoreFileAsync(story.Id, Story.CoverFilePrefix, ".png", t => File.WriteAllTextAsync(t, "p")));
        string page = "page-1.jpg";
        File.WriteAllText(Path.Combine(library.FolderOf(story.Id), page), "p");
        await library.DeleteFileAsync(story.Id, page);
        Assert.True(File.Exists(library.TryResolveFile(story.Id, page)!));
        Assert.False(File.Exists(library.TryResolveFile(story.Id, first)!));
        Assert.True(File.Exists(library.TryResolveFile(story.Id, second)!));
        Assert.NotNull(await library.GetAsync(story.Id));
        Assert.Empty(Directory.GetFiles(library.FolderOf(story.Id), "*.tmp"));
    }

    [Fact]
    public async Task StoreFile_WhenTheWriteFails_LeavesNothingBehind()
    {
        var library = new FileStoryLibrary(_root);
        Story story = Make(StoryIds.New());
        await library.AddAsync(story);

        await Assert.ThrowsAsync<IOException>(() => library.StoreFileAsync(story.Id, Story.CoverFilePrefix, Story.CoverFileExtension, async t =>
        {
            await File.WriteAllTextAsync(t, "half");
            throw new IOException("boom");
        }));

        Assert.Single(Directory.GetFiles(library.FolderOf(story.Id)));
    }

    [Fact]
    public async Task ConcurrentSavesOfOneStory_AllSucceed_AndLeaveNoTempFiles()
    {
        var library = new FileStoryLibrary(_root);
        Story story = Make(StoryIds.New());
        await library.AddAsync(story);

        await Task.WhenAll(Enumerable.Range(0, 20).Select(i => library.UpdateAsync(story with { Title = "v" + i })));

        Assert.NotNull(await library.GetAsync(story.Id));
        Assert.Empty(Directory.GetFiles(library.FolderOf(story.Id), "*.tmp"));
    }

    [Fact]
    public async Task ConcurrentAddsOfOneId_ExactlyOneWins()
    {
        var library = new FileStoryLibrary(_root);
        Story story = Make(StoryIds.New());

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            try { await library.AddAsync(story); return true; }
            catch (InvalidOperationException) { return false; }
        }));

        Assert.Equal(1, results.Count(r => r));
    }

    [Fact]
    public async Task FailedWrite_LeavesNoTempFile()
    {
        // The failure is injected with an exclusive lock on story.json, which only Windows
        // enforces; on Linux a rename-over succeeds despite FileShare.None.
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Needs mandatory file locking (Windows).");
        var library = new FileStoryLibrary(_root);
        Story story = Make(StoryIds.New());
        await library.AddAsync(story);
        string target = Path.Combine(library.FolderOf(story.Id), "story.json");
        using var hold = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.None);

        await Assert.ThrowsAnyAsync<Exception>(() => library.UpdateAsync(story with { Title = "x" }));

        Assert.Empty(Directory.GetFiles(library.FolderOf(story.Id), "*.tmp"));
    }

    [Fact]
    public async Task Delete_FinishesAHalfDeletedFolder_AndToleratesAMissingOne()
    {
        var library = new FileStoryLibrary(_root);
        string id = StoryIds.New();
        Directory.CreateDirectory(library.FolderOf(id));
        await File.WriteAllTextAsync(Path.Combine(library.FolderOf(id), "page-01.jpg"), "x", TestContext.Current.CancellationToken);

        await library.DeleteAsync(id);
        await library.DeleteAsync(id);

        Assert.False(Directory.Exists(library.FolderOf(id)));
    }

    [Fact]
    public async Task StoryWhoseIdDiffersFromItsFolder_IsSkipped()
    {
        var library = new FileStoryLibrary(_root);
        string folderId = StoryIds.New();
        Directory.CreateDirectory(library.FolderOf(folderId));
        await File.WriteAllTextAsync(
            Path.Combine(library.FolderOf(folderId), "story.json"),
            StoryJson.Serialize(Make(StoryIds.New())), TestContext.Current.CancellationToken);

        Assert.Empty(await library.ListAsync());
        Assert.Null(await library.GetAsync(folderId));
    }

    [Fact]
    public void StoryJson_RefusesAnInvalidId()
    {
        string json = StoryJson.Serialize(Make(StoryIds.New())).Replace("\"Id\": \"", "\"Id\": \"../");

        Assert.Throws<StoryFormatException>(() => StoryJson.Deserialize(json));
    }

    [Theory]
    [InlineData("page-01.jpg", true)]
    [InlineData("..", false)]
    [InlineData("../story.json", false)]
    [InlineData("a/b.jpg", false)]
    [InlineData("a\u005cb.jpg", false)]
    [InlineData("/etc/passwd", false)]
    [InlineData("C:\u005cx.jpg", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TryResolveFile_AcceptsOnlyAPlainFileName(string? name, bool ok)
    {
        var library = new FileStoryLibrary(_root);
        string id = StoryIds.New();

        string? path = library.TryResolveFile(id, name);

        Assert.Equal(ok, path is not null);
        if (ok)
        {
            Assert.Equal(Path.Combine(library.FolderOf(id), name!), path);
        }
    }

    [Fact]
    public void TryResolveFile_RejectsAnInvalidStoryId() =>
        Assert.Null(new FileStoryLibrary(_root).TryResolveFile("..", "a.jpg"));
}

public class AwayTrackerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void QuickReturn_KeepsThePages()
    {
        var tracker = new AwayTracker();
        tracker.Stopped(T0);
        Assert.False(tracker.ReturnedTooLate(T0.AddSeconds(30)));
    }

    [Fact]
    public void LongAbsence_Resets_AndOnlyOnce()
    {
        var tracker = new AwayTracker();
        tracker.Stopped(T0);
        Assert.True(tracker.ReturnedTooLate(T0.AddMinutes(3)));
        Assert.False(tracker.ReturnedTooLate(T0.AddMinutes(4)));
    }

    [Fact]
    public void EarliestStop_Counts() 
    {
        var tracker = new AwayTracker();
        tracker.Stopped(T0);
        tracker.Stopped(T0.AddMinutes(2));
        Assert.True(tracker.ReturnedTooLate(T0.AddMinutes(2).AddSeconds(30)));
    }

    [Fact]
    public void NoStop_NeverResets() => Assert.False(new AwayTracker().ReturnedTooLate(T0));
}
