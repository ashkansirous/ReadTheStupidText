using System.IO.Compression;
using System.Text;
using DinoReader.Core;

namespace DinoReader.Tests;

public sealed class StoryBackupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dino-backup-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private FileStoryLibrary Library(string name) => new(Path.Combine(_root, name));

    private static Story Make(string title, string? sourceId = null) => new(
        StoryIds.New(), title, "en", new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
        [new StoryPage("\"Hello,\" said the wolf.", "page-1.jpg"), new StoryPage("Page two.")],
        [new CastMember("wolf", "Wolf", Gender.Male, "supertonic:M3", Edited: true)],
        CoverStyle.Picture, 2, "supertonic:M1",
        SourceId: sourceId);

    private static async Task<Story> StoreAsync(FileStoryLibrary library, Story story)
    {
        await library.AddAsync(story);
        await File.WriteAllTextAsync(library.TryResolveFile(story.Id, "page-1.jpg")!, "page-bytes");
        return story;
    }

    private static async Task<byte[]> BackupAsync(FileStoryLibrary library)
    {
        using var stream = new MemoryStream();
        await StoryBackupWriter.WriteAsync(await library.ListAsync(), library, stream);
        return stream.ToArray();
    }

    private static byte[] Zip(params (string Name, byte[] Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, byte[] content) in entries)
            {
                using Stream entry = zip.CreateEntry(name).Open();
                entry.Write(content);
            }
        }

        return stream.ToArray();
    }

    private static byte[] Text(string text) => Encoding.UTF8.GetBytes(text);

    private static string ManifestJson(int schema) => $$"""{"schemaVersion":{{schema}},"count":0,"created":"2026-10-01T00:00:00+00:00"}""";

    [Fact]
    public async Task RoundTrip_RestoresEveryStory_WithItsIdCastAndPictures()
    {
        FileStoryLibrary source = Library("source");
        Story a = await StoreAsync(source, Make("A"));
        Story b = await StoreAsync(source, Make("B", sourceId: StoryIds.New()));
        byte[] backup = await BackupAsync(source);

        FileStoryLibrary target = Library("target");
        StoryRestoreResult result = await StoryBackupReader.RestoreAsync(new MemoryStream(backup), target);

        Assert.Equal(new StoryRestoreResult(2, 0, 0), result);
        Story restored = (await target.GetAsync(a.Id))!;
        Assert.Equal(a.Cast, restored.Cast);
        Assert.True(restored.Cast[0].Edited);
        Assert.Equal(a.Created, restored.Created);
        Assert.Null(restored.SourceId);
        Assert.Equal(b.SourceId, (await target.GetAsync(b.Id))!.SourceId);
        Assert.Equal("page-bytes", await File.ReadAllTextAsync(target.TryResolveFile(a.Id, "page-1.jpg")!));
    }

    [Fact]
    public async Task Restore_CallsOnAddedOncePerAddedStory_AndNotForSkippedOnes()
    {
        FileStoryLibrary source = Library("source");
        await StoreAsync(source, Make("A"));
        await StoreAsync(source, Make("B"));
        byte[] backup = await BackupAsync(source);
        FileStoryLibrary target = Library("target");
        var added = new List<Story>();

        await StoryBackupReader.RestoreAsync(new MemoryStream(backup), target, added.Add);
        Assert.Equal(2, added.Count);

        await StoryBackupReader.RestoreAsync(new MemoryStream(backup), target, added.Add);
        Assert.Equal(2, added.Count);
    }

    [Fact]
    public async Task Restore_ReportsAStoreBookWithItsSource_AndAnOwnBookWithout()
    {
        FileStoryLibrary source = Library("source");
        string sourceId = Guid.NewGuid().ToString("N");
        await StoreAsync(source, Make("Store", sourceId));
        await StoreAsync(source, Make("Own"));
        FileStoryLibrary target = Library("target");
        var added = new List<Story>();

        await StoryBackupReader.RestoreAsync(new MemoryStream(await BackupAsync(source)), target, added.Add);

        Assert.Equal(sourceId, Assert.Single(added, s => s.Title == "Store").SourceId);
        Assert.Null(Assert.Single(added, s => s.Title == "Own").SourceId);
    }

    [Fact]
    public async Task Restore_KeepsGoing_WhenTheAddedCallbackThrows()
    {
        FileStoryLibrary source = Library("source");
        await StoreAsync(source, Make("A"));
        await StoreAsync(source, Make("B"));
        FileStoryLibrary target = Library("target");

        StoryRestoreResult result = await StoryBackupReader.RestoreAsync(new MemoryStream(await BackupAsync(source)), target, _ => throw new InvalidOperationException("boom"));

        Assert.Equal(2, result.Added);
        Assert.Equal(2, (await target.ListAsync()).Count);
    }

    [Fact]
    public async Task RestoringTwice_AddsNothing_AndLeavesAnExistingStoryAsItWas()
    {
        FileStoryLibrary source = Library("source");
        await StoreAsync(source, Make("A"));
        await StoreAsync(source, Make("B"));
        byte[] backup = await BackupAsync(source);
        FileStoryLibrary target = Library("target");

        await StoryBackupReader.RestoreAsync(new MemoryStream(backup), target);
        Story first = (await target.ListAsync())[0];
        await target.UpdateAsync(first with { Title = "Edited here" });
        StoryRestoreResult again = await StoryBackupReader.RestoreAsync(new MemoryStream(backup), target);

        Assert.Equal(new StoryRestoreResult(0, 2, 0), again);
        Assert.Equal(2, (await target.ListAsync()).Count);
        Assert.Equal("Edited here", (await target.GetAsync(first.Id))!.Title);
    }

    [Fact]
    public async Task RestoreAddsOnlyTheMissingStories()
    {
        FileStoryLibrary source = Library("source");
        Story a = await StoreAsync(source, Make("A"));
        await StoreAsync(source, Make("B"));
        byte[] backup = await BackupAsync(source);
        FileStoryLibrary target = Library("target");
        await StoreAsync(target, a);

        StoryRestoreResult result = await StoryBackupReader.RestoreAsync(new MemoryStream(backup), target);

        Assert.Equal(new StoryRestoreResult(1, 1, 0), result);
        Assert.Equal(2, (await target.ListAsync()).Count);
    }

    [Fact]
    public async Task AStoryWithTheSameSource_AlreadyOnTheShelf_IsSkipped()
    {
        string sourceId = StoryIds.New();
        FileStoryLibrary source = Library("source");
        await StoreAsync(source, Make("Starter", sourceId: sourceId));
        byte[] backup = await BackupAsync(source);

        FileStoryLibrary target = Library("target");
        await StoreAsync(target, Make("Starter", sourceId: sourceId));
        StoryRestoreResult result = await StoryBackupReader.RestoreAsync(new MemoryStream(backup), target);

        Assert.Equal(new StoryRestoreResult(0, 1, 0), result);
        Assert.Single(await target.ListAsync());
    }

    [Fact]
    public async Task NewerBackupSchema_IsRefused_BeforeAnythingIsAdded()
    {
        FileStoryLibrary source = Library("source");
        await StoreAsync(source, Make("A"));
        byte[] good = await BackupAsync(source);
        using var original = new ZipArchive(new MemoryStream(good));
        byte[] package = await ReadEntryAsync(original, original.Entries.First(e => e.Name.EndsWith(".dino")).Name);
        string name = original.Entries.First(e => e.Name.EndsWith(".dino")).Name;
        byte[] newer = Zip((StoryBackupManifest.ManifestFileName, Text(ManifestJson(StoryBackupManifest.CurrentSchemaVersion + 1))), (name, package));
        FileStoryLibrary target = Library("target");

        await Assert.ThrowsAsync<StoryBackupNewerException>(() => StoryBackupReader.RestoreAsync(new MemoryStream(newer), target));

        Assert.Empty(await target.ListAsync());
    }

    [Fact]
    public async Task ACorruptEntry_CostsOnlyItsOwnStory()
    {
        FileStoryLibrary source = Library("source");
        Story a = await StoreAsync(source, Make("A"));
        byte[] good = await BackupAsync(source);
        using var original = new ZipArchive(new MemoryStream(good));
        string name = original.Entries.First(e => e.Name.EndsWith(".dino")).Name;
        byte[] package = await ReadEntryAsync(original, name);
        byte[] backup = Zip(
            (StoryBackupManifest.ManifestFileName, Text(ManifestJson(1))),
            (StoryIds.New() + ".dino", Text("this is not a zip")),
            (name, package));
        FileStoryLibrary target = Library("target");

        StoryRestoreResult result = await StoryBackupReader.RestoreAsync(new MemoryStream(backup), target);

        Assert.Equal(new StoryRestoreResult(1, 0, 1), result);
        Assert.NotNull(await target.GetAsync(a.Id));
    }

    [Fact]
    public async Task AnExhaustedByteBudget_StopsTheRestore_KeepsWhatWasAdded_AndIsNotDamaged()
    {
        FileStoryLibrary source = Library("source");
        await StoreAsync(source, Make("A"));
        await StoreAsync(source, Make("B"));
        await StoreAsync(source, Make("C"));
        byte[] backup = await BackupAsync(source);
        int onePackage = (int)new ZipArchive(new MemoryStream(backup)).Entries.First(e => e.Name.EndsWith(".dino")).Length;
        FileStoryLibrary target = Library("target");

        StoryRestoreResult result = await StoryBackupReader.RestoreAsync(new MemoryStream(backup), target, onePackage + onePackage / 2);

        Assert.Equal(new StoryRestoreResult(1, 0, 0, 2), result);
        Assert.Single(await target.ListAsync());
    }

    [Fact]
    public async Task AStoryThatCannotBeWritten_IsLeftOut_AndCounted()
    {
        FileStoryLibrary source = Library("source");
        await StoreAsync(source, Make("A"));
        Story locked = await StoreAsync(source, Make("Locked"));
        using var hold = new FileStream(source.TryResolveFile(locked.Id, "page-1.jpg")!, FileMode.Open, FileAccess.Read, FileShare.None);
        using var stream = new MemoryStream();

        int skipped = await StoryBackupWriter.WriteAsync(await source.ListAsync(), source, stream);

        Assert.Equal(1, skipped);
        using var zip = new ZipArchive(new MemoryStream(stream.ToArray()));
        Assert.Equal(2, zip.Entries.Count);
    }

    [Fact]
    public async Task AFileWithoutAManifest_IsNotABackup()
    {
        byte[] notBackup = Zip(("readme.txt", Text("hi")));

        await Assert.ThrowsAsync<StoryBackupException>(() => StoryBackupReader.RestoreAsync(new MemoryStream(notBackup), Library("target")));
    }

    [Fact]
    public async Task NotAZipAtAll_IsNotABackup()
    {
        await Assert.ThrowsAsync<StoryBackupException>(() => StoryBackupReader.RestoreAsync(new MemoryStream(Text("plain text")), Library("target")));
    }

    [Fact]
    public async Task AnEmptyShelf_BacksUpToAnEmptyButValidFile()
    {
        byte[] backup = await BackupAsync(Library("source"));

        StoryRestoreResult result = await StoryBackupReader.RestoreAsync(new MemoryStream(backup), Library("target"));

        Assert.Equal(new StoryRestoreResult(0, 0, 0), result);
    }

    private static async Task<byte[]> ReadEntryAsync(ZipArchive zip, string name)
    {
        using var copy = new MemoryStream();
        await using Stream entry = zip.GetEntry(name)!.Open();
        await entry.CopyToAsync(copy);
        return copy.ToArray();
    }
}
