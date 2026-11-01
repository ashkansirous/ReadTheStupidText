using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 230: a restore into a story that is already on the phone brings its recordings back (the backup's wins on a page both have) and leaves everything else as it is.</summary>
public sealed class RecordingMergeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dino-merge-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private FileStoryLibrary Library(string name) => new(Path.Combine(_root, name));

    private static Story Make(string? sourceId = null, params string[] pages) => new(
        StoryIds.New(), "Pigs", "en", new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
        (pages.Length > 0 ? pages : ["One.", "Two.", "Three."]).Select(t => new StoryPage(t)).ToList(),
        [new CastMember("pig", "Pig", Gender.Male, "supertonic:M3", Edited: true)],
        CoverStyle.Colour, 0, "supertonic:M1", SourceId: sourceId);

    // Stores the story in the library with an m4a stand-in (the bytes name the page and the source so a test can tell which copy it reads) for each page.
    private static async Task<Story> StoreWithRecordingsAsync(FileStoryLibrary library, Story story, string tag, params int[] pages)
    {
        await library.AddAsync(story);
        Story recorded = story;
        foreach (int page in pages)
        {
            await library.StoreRecordingAsync(story.Id, page, path => File.WriteAllTextAsync(path, $"{tag}-audio-{page}"));
            recorded = StoryRecordings.With(recorded, page, new PageRecording("en", 5 + page));
        }

        await library.UpdateAsync(recorded);
        return recorded;
    }

    private static async Task<byte[]> BackupAsync(FileStoryLibrary library)
    {
        using var stream = new MemoryStream();
        await StoryBackupWriter.WriteAsync(await library.ListAsync(), library, stream);
        return stream.ToArray();
    }

    private static Task<StoryRestoreResult> RestoreAsync(byte[] backup, FileStoryLibrary target) =>
        StoryBackupReader.RestoreAsync(new MemoryStream(backup), target);

    private static string AudioOf(FileStoryLibrary library, string id, int page) =>
        File.ReadAllText(library.TryResolveFile(id, StoryRecordings.FileNameOf(page))!);

    [Fact]
    public async Task FreshRestore_EveryRecordingEntryPointsAtAnExistingFile()
    {
        FileStoryLibrary source = Library("source");
        Story story = await StoreWithRecordingsAsync(source, Make(), "bak", 0, 2);
        byte[] backup = await BackupAsync(source);

        FileStoryLibrary target = Library("target");
        StoryRestoreResult result = await RestoreAsync(backup, target);

        Assert.Equal(1, result.Added);
        Story restored = (await target.GetAsync(story.Id))!;
        Assert.Equal([0, 2], StoryRecordings.RecordedPages(restored));
        Assert.True(restored.UseRecordings);
        foreach (string file in StoryRecordings.Files(restored))
        {
            Assert.True(new FileInfo(target.TryResolveFile(story.Id, file)!).Length > 0);
        }
    }

    [Fact]
    public async Task ExistingStoryWithNoRecordings_GetsThemBack_AndNothingElseChanges()
    {
        FileStoryLibrary source = Library("source");
        Story story = await StoreWithRecordingsAsync(source, Make(), "bak", 0, 1);
        byte[] backup = await BackupAsync(source);

        FileStoryLibrary target = Library("target");
        Story onPhone = story with { Recordings = null, UseRecordings = false, Title = "Edited on this phone" };
        await target.AddAsync(onPhone);
        var restoredIds = new List<Story>();
        StoryRestoreResult result = await StoryBackupReader.RestoreAsync(new MemoryStream(backup), target, restoredIds.Add);

        Assert.Equal(new StoryRestoreResult(0, 0, 0, 0, 1), result);
        Assert.Empty(restoredIds);
        Story merged = (await target.GetAsync(story.Id))!;
        Assert.Equal([0, 1], StoryRecordings.RecordedPages(merged));
        Assert.True(merged.UseRecordings);
        Assert.Equal("bak-audio-1", AudioOf(target, story.Id, 1));
        Assert.Equal(onPhone.Title, merged.Title);
        Assert.Equal(onPhone.Pages.Select(x => x.Text), merged.Pages.Select(x => x.Text));
        Assert.Equal(onPhone.Cast.Select(c => c.VoiceId), merged.Cast.Select(c => c.VoiceId));
    }

    [Fact]
    public async Task SameRecordedPageOnBothSides_TheBackupWins_OtherPhonePagesStay()
    {
        FileStoryLibrary source = Library("source");
        Story story = await StoreWithRecordingsAsync(source, Make(), "bak", 0);
        byte[] backup = await BackupAsync(source);

        FileStoryLibrary target = Library("target");
        await StoreWithRecordingsAsync(target, story with { Recordings = null, UseRecordings = false }, "phone", 0, 2);
        StoryRestoreResult result = await RestoreAsync(backup, target);

        Assert.Equal(1, result.Merged);
        Story merged = (await target.GetAsync(story.Id))!;
        Assert.Equal([0, 2], StoryRecordings.RecordedPages(merged));
        Assert.Equal("bak-audio-0", AudioOf(target, story.Id, 0));
        Assert.Equal("phone-audio-2", AudioOf(target, story.Id, 2));
    }

    [Fact]
    public async Task PhoneThatSwitchedRecordingsOff_KeepsItsChoice()
    {
        FileStoryLibrary source = Library("source");
        Story story = await StoreWithRecordingsAsync(source, Make(), "bak", 1);
        byte[] backup = await BackupAsync(source);

        FileStoryLibrary target = Library("target");
        Story phone = await StoreWithRecordingsAsync(target, story with { Recordings = null, UseRecordings = false }, "phone", 2);
        await target.UpdateAsync(phone with { UseRecordings = false });
        await RestoreAsync(backup, target);

        Story merged = (await target.GetAsync(story.Id))!;
        Assert.False(merged.UseRecordings);
        Assert.Equal([1, 2], StoryRecordings.RecordedPages(merged));
    }

    [Fact]
    public async Task NothingNewToMerge_IsSkipped_AndRestoringTwiceAddsNothing()
    {
        FileStoryLibrary source = Library("source");
        Story story = await StoreWithRecordingsAsync(source, Make(), "bak", 0);
        byte[] backup = await BackupAsync(source);

        FileStoryLibrary target = Library("target");
        await RestoreAsync(backup, target);
        StoryRestoreResult again = await RestoreAsync(backup, target);

        Assert.Equal(new StoryRestoreResult(0, 1, 0, 0, 0), again);
        Assert.Equal([0], StoryRecordings.RecordedPages((await target.GetAsync(story.Id))!));
    }

    [Fact]
    public async Task BackupWithoutRecordings_IsSkipped()
    {
        FileStoryLibrary source = Library("source");
        Story story = Make();
        await source.AddAsync(story);
        byte[] backup = await BackupAsync(source);

        FileStoryLibrary target = Library("target");
        await target.AddAsync(story);
        StoryRestoreResult result = await RestoreAsync(backup, target);

        Assert.Equal(new StoryRestoreResult(0, 1, 0, 0, 0), result);
    }

    [Fact]
    public async Task RecordingWhoseAudioIsMissingFromTheBackup_IsDropped()
    {
        FileStoryLibrary source = Library("source");
        Story story = await StoreWithRecordingsAsync(source, Make(), "bak", 0, 1);
        File.Delete(source.TryResolveFile(story.Id, StoryRecordings.FileNameOf(1))!); // the writer skips a file that is gone
        byte[] backup = await BackupAsync(source);

        FileStoryLibrary target = Library("target");
        await target.AddAsync(story with { Recordings = null, UseRecordings = false });
        StoryRestoreResult result = await RestoreAsync(backup, target);

        Assert.Equal(1, result.Merged);
        Assert.Equal([0], StoryRecordings.RecordedPages((await target.GetAsync(story.Id))!));
        Assert.False(File.Exists(target.TryResolveFile(story.Id, StoryRecordings.FileNameOf(1))!));
    }

    [Fact]
    public async Task RecordingForAPageThePhoneLacks_IsDropped()
    {
        FileStoryLibrary source = Library("source");
        Story story = await StoreWithRecordingsAsync(source, Make(), "bak", 0, 2);
        byte[] backup = await BackupAsync(source);

        FileStoryLibrary target = Library("target");
        Story shorter = story with { Pages = story.Pages.Take(2).ToList(), Recordings = null, UseRecordings = false };
        await target.AddAsync(shorter);
        await RestoreAsync(backup, target);

        Story merged = (await target.GetAsync(story.Id))!;
        Assert.Equal([0], StoryRecordings.RecordedPages(merged));
        Assert.Equal(2, merged.Pages.Count);
        Assert.False(File.Exists(target.TryResolveFile(story.Id, StoryRecordings.FileNameOf(2))!));
    }

    [Fact]
    public async Task RewordedPageOnThePhone_DoesNotTakeTheOldRecording()
    {
        FileStoryLibrary source = Library("source");
        Story story = await StoreWithRecordingsAsync(source, Make(), "bak", 0, 1);
        byte[] backup = await BackupAsync(source);

        FileStoryLibrary target = Library("target");
        List<StoryPage> pages = story.Pages.ToList();
        pages[0] = new StoryPage("Brand new words.");
        await target.AddAsync(story with { Pages = pages, Recordings = null, UseRecordings = false });
        await RestoreAsync(backup, target);

        Assert.Equal([1], StoryRecordings.RecordedPages((await target.GetAsync(story.Id))!));
    }

    [Fact]
    public async Task StarterReinstalledUnderAnotherId_GetsTheRecordingsOfTheBackedUpCopy()
    {
        string starter = StoryIds.New();
        FileStoryLibrary source = Library("source");
        Story story = await StoreWithRecordingsAsync(source, Make(starter), "bak", 0);
        byte[] backup = await BackupAsync(source);

        FileStoryLibrary target = Library("target");
        Story fresh = Make(starter);
        await target.AddAsync(fresh);
        StoryRestoreResult result = await RestoreAsync(backup, target);

        Assert.Equal(new StoryRestoreResult(0, 0, 0, 0, 1), result);
        Assert.Single(await target.ListAsync());
        Assert.Null(await target.GetAsync(story.Id));
        Assert.Equal("bak-audio-0", AudioOf(target, fresh.Id, 0));
        Assert.Equal([0], StoryRecordings.RecordedPages((await target.GetAsync(fresh.Id))!));
    }

    [Fact]
    public async Task MergeFailingOnASecondPage_LeavesThePhonesAudioAndStoryUntouched_AndNoTempFiles()
    {
        FileStoryLibrary source = Library("source");
        Story story = await StoreWithRecordingsAsync(source, Make(), "bak", 0, 1);
        using var package = new MemoryStream();
        await StoryPackageWriter.WriteAsync(story, source, package, includeRecordings: true);

        FileStoryLibrary target = Library("target");
        Story phone = await StoreWithRecordingsAsync(target, story with { Recordings = null, UseRecordings = false }, "phone", 0, 1);
        package.Position = 0;

        // "bak-audio-0" is 11 bytes: the first page fits the budget, the second does not.
        await Assert.ThrowsAsync<StoryPackageException>(() => StoryPackageReader.MergeRecordingsAsync(package, target, phone, new ByteBudget(15)));

        Assert.Equal("phone-audio-0", AudioOf(target, story.Id, 0));
        Assert.Equal("phone-audio-1", AudioOf(target, story.Id, 1));
        Assert.Equal(phone.Recordings, (await target.GetAsync(story.Id))!.Recordings);
        Assert.Empty(Directory.GetFiles(target.FolderOf(story.Id), "*.tmp"));
    }

    // Delegates to a real library but fails the story save.
    private sealed class FailingUpdateLibrary(FileStoryLibrary inner, Exception failure) : IStoryLibrary
    {
        public Task<IReadOnlyList<Story>> ListAsync() => inner.ListAsync();

        public Task<Story?> GetAsync(string id) => inner.GetAsync(id);

        public Task AddAsync(Story story) => inner.AddAsync(story);

        public Task UpdateAsync(Story story) => Task.FromException(failure);

        public Task DeleteAsync(string id) => inner.DeleteAsync(id);

        public string FolderOf(string id) => inner.FolderOf(id);

        public string? TryResolveFile(string storyId, string? fileName) => inner.TryResolveFile(storyId, fileName);

        public Task<string> StoreFileAsync(string storyId, string prefix, string extension, Func<string, Task> write) => inner.StoreFileAsync(storyId, prefix, extension, write);

        public Task DeleteFileAsync(string storyId, string fileName) => inner.DeleteFileAsync(storyId, fileName);
    }

    [Fact]
    public async Task SaveFailingAfterTheFilesMoved_PutsThePhonesAudioBack_AndRemovesNewFiles()
    {
        FileStoryLibrary source = Library("source");
        Story story = await StoreWithRecordingsAsync(source, Make(), "bak", 0, 1);
        using var package = new MemoryStream();
        await StoryPackageWriter.WriteAsync(story, source, package, includeRecordings: true);

        FileStoryLibrary target = Library("target");
        Story phone = await StoreWithRecordingsAsync(target, story with { Recordings = null, UseRecordings = false }, "phone", 0); // page 1 has no file on the phone
        package.Position = 0;

        await Assert.ThrowsAsync<IOException>(() =>
            StoryPackageReader.MergeRecordingsAsync(package, new FailingUpdateLibrary(target, new IOException("disk full")), phone, new ByteBudget(1000)));

        Assert.Equal("phone-audio-0", AudioOf(target, story.Id, 0));
        Assert.False(File.Exists(target.TryResolveFile(story.Id, StoryRecordings.FileNameOf(1))!));
        Assert.Equal(phone.Recordings, (await target.GetAsync(story.Id))!.Recordings);
        Assert.Empty(Directory.GetFiles(target.FolderOf(story.Id), "*.tmp"));
        Assert.Empty(Directory.GetFiles(target.FolderOf(story.Id), "*.bak"));
    }

    [Fact]
    public async Task DiskErrorWhileMerging_CountsThatBookAsDamaged_AndDoesNotAbortTheRestore()
    {
        FileStoryLibrary source = Library("source");
        Story story = await StoreWithRecordingsAsync(source, Make(), "bak", 0);
        byte[] backup = await BackupAsync(source);

        FileStoryLibrary target = Library("target");
        await target.AddAsync(story with { Recordings = null, UseRecordings = false });
        StoryRestoreResult result = await StoryBackupReader.RestoreAsync(new MemoryStream(backup), new FailingUpdateLibrary(target, new UnauthorizedAccessException()));

        Assert.Equal(new StoryRestoreResult(0, 0, 1, 0, 0), result);
    }

    [Fact]
    public async Task LeftoversOfAKilledMerge_AreSweptBeforeTheNextOne()
    {
        FileStoryLibrary source = Library("source");
        Story story = await StoreWithRecordingsAsync(source, Make(), "bak", 0);
        using var package = new MemoryStream();
        await StoryPackageWriter.WriteAsync(story, source, package, includeRecordings: true);

        FileStoryLibrary target = Library("target");
        Story phone = await StoreWithRecordingsAsync(target, story with { Recordings = null, UseRecordings = false }, "phone", 1, 2);
        string stray = target.TryResolveFile(story.Id, StoryRecordings.FileNameOf(0) + ".0123456789abcdef0123456789abcdef.tmp")!;
        string lostBackup = target.TryResolveFile(story.Id, StoryRecordings.FileNameOf(1) + ".bak")!;
        string keptBackup = target.TryResolveFile(story.Id, StoryRecordings.FileNameOf(2) + ".bak")!;
        File.WriteAllText(stray, "half");
        File.Move(target.TryResolveFile(story.Id, StoryRecordings.FileNameOf(1))!, lostBackup); // its file is missing: the backup is the phone's audio
        File.WriteAllText(keptBackup, "old"); // its file is in place: the backup is dropped
        package.Position = 0;

        await StoryPackageReader.MergeRecordingsAsync(package, target, phone, new ByteBudget(1000));

        Assert.False(File.Exists(stray));
        Assert.False(File.Exists(lostBackup));
        Assert.False(File.Exists(keptBackup));
        Assert.Equal("phone-audio-1", AudioOf(target, story.Id, 1));
        Assert.Equal("phone-audio-2", AudioOf(target, story.Id, 2));
        Assert.Equal("bak-audio-0", AudioOf(target, story.Id, 0));
    }

    [Fact]
    public async Task BackupWithSwitchOff_PhoneWithNoRecordings_TurnsTheSwitchOn()
    {
        FileStoryLibrary source = Library("source");
        Story story = await StoreWithRecordingsAsync(source, Make(), "bak", 0);
        await source.UpdateAsync((await source.GetAsync(story.Id))! with { UseRecordings = false });
        byte[] backup = await BackupAsync(source);

        FileStoryLibrary target = Library("target");
        await target.AddAsync(story with { Recordings = null, UseRecordings = false });
        await RestoreAsync(backup, target);

        Assert.True((await target.GetAsync(story.Id))!.UseRecordings);
    }

    [Fact]
    public async Task TwoStarterCopies_TheOneWhoseIdIsTheSourceIsPreferred()
    {
        string starter = StoryIds.New();
        FileStoryLibrary source = Library("source");
        Story backed = await StoreWithRecordingsAsync(source, Make(starter), "bak", 0);
        byte[] backup = await BackupAsync(source);

        FileStoryLibrary target = Library("target");
        Story copy = Make(starter) with { Created = backed.Created.AddDays(1) }; // listed first (newest first)
        Story original = Make() with { Id = starter, Created = backed.Created };
        await target.AddAsync(copy);
        await target.AddAsync(original);
        await RestoreAsync(backup, target);

        Assert.Equal([0], StoryRecordings.RecordedPages((await target.GetAsync(starter))!));
        Assert.Empty(StoryRecordings.RecordedPages((await target.GetAsync(copy.Id))!));
    }

    [Fact]
    public void MergeablePages_AndWithMerged_ArePureAndKeepPagesAsTheyAre()
    {
        Story phone = Make();
        Story backed = StoryRecordings.With(StoryRecordings.With(Make(), 0, new PageRecording("fa", 3)), 2, new PageRecording("en", 4));

        IReadOnlyList<int> pages = StoryRecordings.MergeablePages(phone, backed, name => name == StoryRecordings.FileNameOf(2));
        Story merged = StoryRecordings.WithMerged(phone, backed, pages);

        Assert.Equal([2], pages);
        Assert.Equal(new PageRecording("en", 4), StoryRecordings.Of(merged, 2));
        Assert.Equal(phone.Pages, merged.Pages);
        Assert.Same(phone, StoryRecordings.WithMerged(phone, backed, []));
    }
}
