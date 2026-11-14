using System.IO.Compression;
using System.Text;
using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 346 part C: a backup holds state and recordings, never book content; old backups with .dino files still restore their recordings and shelf list.</summary>
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
        CoverStyle.Picture, 2, "supertonic:M1", SourceId: sourceId ?? StoryIds.New());

    // Stores the story with a recording on page 1 (the second page) whose audio says what it is.
    private static async Task<Story> StoreAsync(FileStoryLibrary library, Story story, string tag = "audio")
    {
        await library.AddAsync(story);
        await File.WriteAllTextAsync(library.TryResolveFile(story.Id, "page-1.jpg")!, "page-bytes");
        await library.StoreRecordingAsync(story.Id, 1, path => File.WriteAllTextAsync(path, tag));
        Story recorded = StoryRecordings.With(story, 1, new PageRecording("en", 4));
        await library.UpdateAsync(recorded);
        return recorded;
    }

    private static async Task<byte[]> BackupAsync(FileStoryLibrary library, IReadOnlyDictionary<string, string>? settings = null)
    {
        using var stream = new MemoryStream();
        await StoryBackupWriter.WriteAsync(await library.ListAsync(), library, stream, BackupFormat.StateOnly, settings);
        return stream.ToArray();
    }

    // A schema 1 backup as the app made them before Slice 346: one .dino package (recordings included) per story.
    private static async Task<byte[]> LegacyBackupAsync(FileStoryLibrary library)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            IReadOnlyList<Story> stories = await library.ListAsync();
            foreach (Story story in stories)
            {
                using var package = new MemoryStream();
                await StoryPackageWriter.WriteAsync(story, library, package, includeRecordings: true);
                using Stream entry = zip.CreateEntry(story.Id + StoryPackageManifest.Extension).Open();
                entry.Write(package.ToArray());
            }

            using Stream manifest = zip.CreateEntry(StoryBackupManifest.ManifestFileName).Open();
            manifest.Write(Text(ManifestJson(1)));
        }

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

    private static IReadOnlyList<string> NamesIn(byte[] backup)
    {
        using var zip = new ZipArchive(new MemoryStream(backup));
        return zip.Entries.Select(e => e.FullName).ToList();
    }

    [Fact]
    public async Task ANewBackup_HoldsTheShelfStateAndRecordings_AndNoBookContent()
    {
        FileStoryLibrary source = Library("source");
        Story a = await StoreAsync(source, Make("A"));
        byte[] backup = await BackupAsync(source);

        IReadOnlyList<string> names = NamesIn(backup);
        Assert.DoesNotContain(names, n => n.EndsWith(".dino", StringComparison.Ordinal));
        Assert.DoesNotContain(names, n => n.EndsWith(".jpg", StringComparison.Ordinal));
        Assert.Contains(StoryBackupState.StateFile(a.SourceId!), names);
        Assert.Contains(names, n => n.StartsWith($"books/{a.SourceId}/rec-", StringComparison.Ordinal));
        using var zip = new ZipArchive(new MemoryStream(backup));
        string everything = string.Concat(zip.Entries.Where(e => e.Name.EndsWith(".json", StringComparison.Ordinal)).Select(e => new StreamReader(e.Open()).ReadToEnd()));
        Assert.DoesNotContain("said the wolf", everything); // no book text, only a hash of the recorded page
        Assert.DoesNotContain("Page two", everything);
        StoryBackupManifest manifest = StoryBackupManifest.Parse(new StreamReader(zip.GetEntry("backup.json")!.Open()).ReadToEnd());
        Assert.Equal((2, 1), (manifest.SchemaVersion, manifest.Count));
        Assert.Equal(a.SourceId, Assert.Single(manifest.Shelf!).Id);
    }

    [Fact]
    public async Task TheDefaultBackup_WhileTheSwitchIsOffOrUnknown_IsTheSchema1BackupWithBookFiles()
    {
        FileStoryLibrary source = Library("source");
        Story a = await StoreAsync(source, Make("A"), "rec-a");
        using var stream = new MemoryStream();
        await StoryBackupWriter.WriteAsync(await source.ListAsync(), source, stream);
        byte[] backup = stream.ToArray();

        Assert.Equal(new[] { StoryBackupManifest.ManifestFileName, a.Id + ".dino" }.Order(StringComparer.Ordinal), NamesIn(backup).Order(StringComparer.Ordinal));
        using var zip = new ZipArchive(new MemoryStream(backup));
        StoryBackupManifest manifest = StoryBackupManifest.Parse(new StreamReader(zip.GetEntry("backup.json")!.Open()).ReadToEnd());
        Assert.Equal((1, 1, true), (manifest.SchemaVersion, manifest.Count, manifest.Shelf is null && manifest.Settings is null));

        FileStoryLibrary target = Library("target");
        StoryRestoreResult result = await StoryBackupReader.RestoreAsync(new MemoryStream(backup), target);
        Assert.Equal(1, result.Added); // the book comes back from the file, as before Slice 346
        Assert.Null(result.Missing);
    }

    [Fact]
    public async Task Restore_AddsNoBook_ListsTheOnesThePhoneLacks_AndMergesIntoThoseItHas()
    {
        FileStoryLibrary source = Library("source");
        Story a = await StoreAsync(source, Make("A"), "bak-a");
        Story b = await StoreAsync(source, Make("B"), "bak-b");
        byte[] backup = await BackupAsync(source);

        FileStoryLibrary target = Library("target");
        await target.AddAsync(a with { Recordings = null, UseRecordings = false, PieceRecordings = null });
        StoryRestoreResult result = await StoryBackupReader.RestoreAsync(new MemoryStream(backup), target);

        Assert.Equal((0, 1, 0), (result.Added, result.Merged, result.Damaged));
        Assert.Equal(new MissingBook(b.SourceId!, "B"), Assert.Single(result.Missing!));
        Assert.Single(await target.ListAsync());
        Assert.Equal("bak-a", await File.ReadAllTextAsync(target.TryResolveFile(a.Id, StoryRecordings.FileNameOf(1))!));
    }

    [Fact]
    public async Task ApplyToBook_AfterTheStoreDownload_BringsRecordingsAndReadingViewBack()
    {
        FileStoryLibrary source = Library("source");
        Story a = await StoreAsync(source, Make("A"), "bak-a");
        await source.UpdateAsync((await source.GetAsync(a.Id))! with { PreferredMode = ReadingMode.PictureOnly });
        byte[] backup = await BackupAsync(source);
        FileStoryLibrary target = Library("target");

        StoryRestoreResult result = await StoryBackupReader.RestoreAsync(new MemoryStream(backup), target);
        Assert.Equal(a.SourceId, Assert.Single(result.Missing!).Id);

        Story downloaded = Make("A", a.SourceId) with { Cast = [] }; // what the store gives: the book without the family's state
        await target.AddAsync(downloaded);
        bool applied = await StoryBackupReader.ApplyToBookAsync(new MemoryStream(backup), target, a.SourceId!);

        Assert.True(applied);
        Story back = (await target.GetAsync(downloaded.Id))!;
        Assert.Equal([1], StoryRecordings.RecordedPages(back));
        Assert.Equal(ReadingMode.PictureOnly, back.PreferredMode);
        Assert.Equal("bak-a", await File.ReadAllTextAsync(target.TryResolveFile(downloaded.Id, StoryRecordings.FileNameOf(1))!));
        Assert.False(await StoryBackupReader.ApplyToBookAsync(new MemoryStream(backup), target, StoryIds.New()));
    }

    [Fact]
    public async Task ARewordedPage_NeverGetsAnotherPagesRecording()
    {
        FileStoryLibrary source = Library("source");
        Story a = await StoreAsync(source, Make("A"));
        byte[] backup = await BackupAsync(source);
        FileStoryLibrary target = Library("target");
        await target.AddAsync(Make("A", a.SourceId) with { Pages = [new StoryPage("Other."), new StoryPage("Page two, reworded.")] });

        StoryRestoreResult result = await StoryBackupReader.RestoreAsync(new MemoryStream(backup), target);

        Assert.Equal(0, result.Merged);
        Assert.Empty(StoryRecordings.RecordedPages((await target.ListAsync()).Single()));
    }

    [Fact]
    public async Task RestoringTwice_ChangesNothingTheSecondTime()
    {
        FileStoryLibrary source = Library("source");
        Story a = await StoreAsync(source, Make("A"));
        byte[] backup = await BackupAsync(source);
        FileStoryLibrary target = Library("target");
        await target.AddAsync(a with { Recordings = null, UseRecordings = false });

        await StoryBackupReader.RestoreAsync(new MemoryStream(backup), target);
        StoryRestoreResult again = await StoryBackupReader.RestoreAsync(new MemoryStream(backup), target);

        Assert.Equal(new StoryRestoreResult(0, 1, 0), again);
    }

    [Fact]
    public async Task Settings_ComeBack_ButOnlyTheAllowedOnes()
    {
        FileStoryLibrary source = Library("source");
        await StoreAsync(source, Make("A"));
        var settings = new Dictionary<string, string>
        {
            [BackupSettings.ReadingSpeedKey] = BackupSettings.SpeedText(1.1),
            [BackupSettings.AnimalSoundsKey] = "false",
            ["dino.developer"] = "true",
        };
        byte[] backup = await BackupAsync(source, settings);

        IReadOnlyDictionary<string, string> back = await StoryBackupReader.ReadSettingsAsync(new MemoryStream(backup));

        Assert.Equal(2, back.Count);
        Assert.True(BackupSettings.TryParseSpeed(back[BackupSettings.ReadingSpeedKey], out double speed) && speed == 1.1);
        Assert.Equal("false", back[BackupSettings.AnimalSoundsKey]);
        Assert.Empty(BackupSettings.Sanitize(new Dictionary<string, string> { [BackupSettings.ReadingSpeedKey] = "fast", [BackupSettings.AnimalSoundsKey] = "yes" }));
    }

    [Fact]
    public async Task AnOldBackup_WithTheSwitchOff_RestoresItsBookFilesAsBefore()
    {
        FileStoryLibrary source = Library("source");
        Story a = await StoreAsync(source, Make("A"), "old-a");
        Story b = await StoreAsync(source, Make("B"), "old-b");
        byte[] old = await LegacyBackupAsync(source);

        FileStoryLibrary target = Library("target");
        await target.AddAsync(a with { Recordings = null, UseRecordings = false });
        var added = new List<Story>();
        StoryRestoreResult result = await StoryBackupReader.RestoreAsync(new MemoryStream(old), target, LegacyBookFiles.Restore, added.Add);

        Assert.Equal((1, 1, 0), (result.Added, result.Merged, result.Damaged));
        Assert.Null(result.Missing);
        Assert.Equal(b.SourceId, Assert.Single(added).SourceId);
        Assert.Equal(2, (await target.ListAsync()).Count);
        Assert.Equal("old-a", await File.ReadAllTextAsync(target.TryResolveFile(a.Id, StoryRecordings.FileNameOf(1))!));
        Assert.Equal(0, (await StoryBackupReader.RestoreAsync(new MemoryStream(old), target)).Added); // restoring twice adds nothing
        Assert.Equal(2, (await target.ListAsync()).Count);
    }

    [Fact]
    public async Task AnOldBackup_WithTheSwitchOn_FallsBackToItsBookFile_WhenTheStoreCannotSupplyTheBook()
    {
        FileStoryLibrary source = Library("source");
        Story b = await StoreAsync(source, Make("B"), "old-b");
        byte[] old = await LegacyBackupAsync(source);

        FileStoryLibrary target = Library("target");
        StoryRestoreResult result = await StoryBackupReader.RestoreAsync(new MemoryStream(old), target, LegacyBookFiles.StoreFirst);
        Assert.Equal(b.SourceId, Assert.Single(result.Missing!).Id);
        Assert.Empty(await target.ListAsync());

        Story? back = await StoryBackupReader.ImportLegacyBookAsync(new MemoryStream(old), target, b.SourceId!);
        Assert.NotNull(back);
        Assert.Equal(b.SourceId, back.SourceId);
        Assert.Equal([1], StoryRecordings.RecordedPages(back));
        Assert.Null(await StoryBackupReader.ImportLegacyBookAsync(new MemoryStream(old), target, b.SourceId!)); // already on the shelf
        Assert.Null(await StoryBackupReader.ImportLegacyBookAsync(new MemoryStream(old), target, StoryIds.New())); // not in the backup
        Assert.Single(await target.ListAsync());
    }

    [Fact]
    public async Task AnOldBackup_UsesItsRecordingsAndShelfList_AndLeavesTheBookFilesToTheStore_WhenStoreFirst()
    {
        FileStoryLibrary source = Library("source");
        Story a = await StoreAsync(source, Make("A"), "old-a");
        Story b = await StoreAsync(source, Make("B"), "old-b");
        byte[] old = await LegacyBackupAsync(source);

        FileStoryLibrary target = Library("target");
        await target.AddAsync(a with { Recordings = null, UseRecordings = false });
        StoryRestoreResult result = await StoryBackupReader.RestoreAsync(new MemoryStream(old), target, LegacyBookFiles.StoreFirst);

        Assert.Equal((0, 1, 0), (result.Added, result.Merged, result.Damaged));
        Assert.Equal(b.SourceId, Assert.Single(result.Missing!).Id);
        Assert.Single(await target.ListAsync()); // B's book files were not imported
        Assert.Equal("old-a", await File.ReadAllTextAsync(target.TryResolveFile(a.Id, StoryRecordings.FileNameOf(1))!));

        await target.AddAsync(Make("B", b.SourceId));
        Assert.True(await StoryBackupReader.ApplyToBookAsync(new MemoryStream(old), target, b.SourceId!));
        Story bBack = (await target.ListAsync()).Single(s => s.SourceId == b.SourceId);
        Assert.Equal([1], StoryRecordings.RecordedPages(bBack));
    }

    [Fact]
    public async Task NewerBackupSchema_IsRefused_BeforeAnythingChanges()
    {
        byte[] newer = Zip((StoryBackupManifest.ManifestFileName, Text(ManifestJson(StoryBackupManifest.CurrentSchemaVersion + 1))));
        FileStoryLibrary target = Library("target");

        await Assert.ThrowsAsync<StoryBackupNewerException>(() => StoryBackupReader.RestoreAsync(new MemoryStream(newer), target));

        Assert.Empty(await target.ListAsync());
    }

    [Fact]
    public async Task ACorruptStateEntry_CostsOnlyItsOwnBook()
    {
        FileStoryLibrary source = Library("source");
        Story a = await StoreAsync(source, Make("A"));
        Story b = await StoreAsync(source, Make("B"));
        byte[] good = await BackupAsync(source);
        using var original = new ZipArchive(new MemoryStream(good));
        var entries = original.Entries.Select(e => (e.FullName, Bytes: ReadAll(e))).ToList();
        byte[] broken = Zip(entries.Select(e => e.FullName == StoryBackupState.StateFile(b.SourceId!) ? (e.FullName, Text("not json")) : (e.FullName, e.Bytes)).ToArray());
        FileStoryLibrary target = Library("target");
        await target.AddAsync(a with { Recordings = null, UseRecordings = false });
        await target.AddAsync(b with { Recordings = null, UseRecordings = false });

        StoryRestoreResult result = await StoryBackupReader.RestoreAsync(new MemoryStream(broken), target);

        Assert.Equal((1, 1), (result.Merged, result.Damaged));
    }

    [Fact]
    public async Task AnExhaustedByteBudget_StopsTheRestore_AndIsNotDamaged()
    {
        FileStoryLibrary source = Library("source");
        Story a = await StoreAsync(source, Make("A"));
        await StoreAsync(source, Make("B"));
        byte[] backup = await BackupAsync(source);
        FileStoryLibrary target = Library("target");
        foreach (Story story in await source.ListAsync())
        {
            await target.AddAsync(story with { Recordings = null, UseRecordings = false });
        }

        StoryRestoreResult result = await StoryBackupReader.RestoreAsync(new MemoryStream(backup), target, 1);

        Assert.Equal(0, result.Damaged);
        Assert.True(result.TooLarge > 0);
        Assert.NotNull(a);
    }

    [Fact]
    public async Task AStoryWhoseRecordingCannotBeRead_IsLeftOut_AndCounted()
    {
        FileStoryLibrary source = Library("source");
        await StoreAsync(source, Make("A"));
        Story locked = await StoreAsync(source, Make("Locked"));
        using var hold = new FileStream(source.TryResolveFile(locked.Id, StoryRecordings.FileNameOf(1))!, FileMode.Open, FileAccess.Read, FileShare.None);
        using var stream = new MemoryStream();

        int skipped = await StoryBackupWriter.WriteAsync(await source.ListAsync(), source, stream, BackupFormat.StateOnly);

        Assert.Equal(1, skipped);
        Assert.Equal(1, StoryBackupManifest.Parse(new StreamReader(new ZipArchive(new MemoryStream(stream.ToArray())).GetEntry("backup.json")!.Open()).ReadToEnd()).Count);
    }

    [Fact]
    public async Task TwoShelfBooksWithOneStoreId_AreOneEntry_WithEveryCopysRecordings()
    {
        FileStoryLibrary source = Library("source");
        string storeId = StoryIds.New();
        Story first = await StoreAsync(source, Make("A", storeId));
        Story second = Make("A again", storeId);
        await source.AddAsync(second);
        await source.StoreRecordingAsync(second.Id, 0, path => File.WriteAllTextAsync(path, "second-copy"));
        await source.UpdateAsync(StoryRecordings.With(second, 0, new PageRecording("en", 3)));
        using var stream = new MemoryStream();

        int notWritten = await StoryBackupWriter.WriteAsync(await source.ListAsync(), source, stream, BackupFormat.StateOnly);

        byte[] backup = stream.ToArray();
        Assert.Equal(0, notWritten);
        IReadOnlyList<string> names = NamesIn(backup);
        Assert.Single(names, n => n == StoryBackupState.StateFile(storeId));
        Assert.Contains($"books/{storeId}/{StoryRecordings.FileNameOf(0)}", names);
        Assert.Contains($"books/{storeId}/{StoryRecordings.FileNameOf(1)}", names);
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        using var zip = new ZipArchive(new MemoryStream(backup));
        Assert.Equal(1, StoryBackupManifest.Parse(new StreamReader(zip.GetEntry("backup.json")!.Open()).ReadToEnd()).Count);
        Assert.NotNull(first);
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

    private static byte[] ReadAll(ZipArchiveEntry entry)
    {
        using var copy = new MemoryStream();
        using Stream stream = entry.Open();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}
