using System.IO.Compression;
using System.Text;
using DinoReader.Core;

namespace DinoReader.Tests;

public sealed class StoryPackageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dino-package-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private FileStoryLibrary Library(string name) => new(Path.Combine(_root, name));

    private static Story Make(string id) => new(
        id, "The Pigs", "en", new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
        [new StoryPage("\"Hello,\" said the wolf.", "page-1.jpg"), new StoryPage("Page two.")],
        [new CastMember("wolf", "Wolf", Gender.Male, "supertonic:M3", Edited: true), new CastMember("pig", "Pig", Gender.Female, "supertonic:F2")],
        CoverStyle.Picture, 2, "supertonic:M1",
        CharacterMerges: new Dictionary<string, string> { ["big-bad-wolf"] = "wolf" }, CoverFile: "cover-ab12cd34.jpg");

    private static async Task<byte[]> ExportAsync(Story story, IStoryLibrary library)
    {
        using var stream = new MemoryStream();
        await StoryPackageWriter.WriteAsync(story, library, stream);
        return stream.ToArray();
    }

    private static byte[] Zip(params (string Name, string Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, string content) in entries)
            {
                using Stream entry = zip.CreateEntry(name).Open();
                entry.Write(Encoding.UTF8.GetBytes(content));
            }
        }

        return stream.ToArray();
    }

    private static string ManifestJson(int schema, string id) => $$"""{"schemaVersion":{{schema}},"id":"{{id}}","title":"T","language":"en"}""";

    private async Task<(FileStoryLibrary Source, Story Story)> StoredAsync()
    {
        FileStoryLibrary source = Library("source");
        Story story = Make(StoryIds.New());
        await source.AddAsync(story);
        await File.WriteAllTextAsync(source.TryResolveFile(story.Id, "page-1.jpg")!, "page-bytes");
        await File.WriteAllTextAsync(source.TryResolveFile(story.Id, "cover-ab12cd34.jpg")!, "cover-bytes");
        return (source, story);
    }

    [Fact]
    public async Task RoundTrip_KeepsCastMergesAndPinnedVoices_UnderANewId()
    {
        (FileStoryLibrary source, Story story) = await StoredAsync();
        byte[] package = await ExportAsync(story, source);

        FileStoryLibrary target = Library("target");
        Story imported = await StoryPackageReader.ImportAsync(new MemoryStream(package), target);

        Assert.NotEqual(story.Id, imported.Id);
        Assert.Equal(story.Id, imported.SourceId);
        Story? stored = await target.GetAsync(imported.Id);
        Assert.NotNull(stored);
        Assert.Equal(story.Cast, stored.Cast);
        Assert.True(stored.Cast[0].Edited);
        Assert.Equal("wolf", stored.Merges["big-bad-wolf"]);
        Assert.Equal(story.Pages.Select(p => p.Text), stored.Pages.Select(p => p.Text));
        Assert.Equal("page-bytes", await File.ReadAllTextAsync(target.TryResolveFile(imported.Id, "page-1.jpg")!));
        Assert.Equal("cover-bytes", await File.ReadAllTextAsync(target.TryResolveFile(imported.Id, "cover-ab12cd34.jpg")!));
    }

    [Fact]
    public async Task ImportingTwice_GivesTwoStories_AndReExportKeepsTheSourceId()
    {
        (FileStoryLibrary source, Story story) = await StoredAsync();
        byte[] package = await ExportAsync(story, source);
        FileStoryLibrary target = Library("target");

        Story first = await StoryPackageReader.ImportAsync(new MemoryStream(package), target);
        Story second = await StoryPackageReader.ImportAsync(new MemoryStream(package), target);
        byte[] again = await ExportAsync(first, target);
        Story third = await StoryPackageReader.ImportAsync(new MemoryStream(again), Library("other"));

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, (await target.ListAsync()).Count);
        Assert.Equal(story.Id, third.SourceId);
    }

    [Fact]
    public async Task KeepId_RestoresTheStoryUnderItsOwnId()
    {
        (FileStoryLibrary source, Story story) = await StoredAsync();
        byte[] package = await ExportAsync(story, source);

        Story restored = await StoryPackageReader.ImportAsync(new MemoryStream(package), Library("target"), keepId: true);

        Assert.Equal(story.Id, restored.Id);
    }

    [Fact]
    public async Task NewerSchema_IsRefused_AndNothingIsStored()
    {
        Story story = Make(StoryIds.New());
        byte[] package = Zip(
            (StoryPackageManifest.ManifestFileName, ManifestJson(StoryPackageManifest.CurrentSchemaVersion + 1, story.Id)),
            (StoryPackageManifest.StoryFileName, StoryJson.Serialize(story)));
        FileStoryLibrary target = Library("target");

        await Assert.ThrowsAsync<StoryPackageException>(() => StoryPackageReader.ImportAsync(new MemoryStream(package), target));

        Assert.Empty(await target.ListAsync());
    }

    [Theory]
    [InlineData("../evil.jpg")]
    [InlineData("..\\evil.jpg")]
    [InlineData("sub/page-1.jpg")]
    [InlineData("/abs.jpg")]
    public async Task ZipSlipEntry_IsRefused_AndNothingIsStored(string entryName)
    {
        Story story = Make(StoryIds.New());
        byte[] package = Zip(
            (StoryPackageManifest.ManifestFileName, ManifestJson(1, story.Id)),
            (StoryPackageManifest.StoryFileName, StoryJson.Serialize(story)),
            (entryName, "x"));
        FileStoryLibrary target = Library("target");

        await Assert.ThrowsAsync<StoryPackageException>(() => StoryPackageReader.ImportAsync(new MemoryStream(package), target));

        Assert.Empty(await target.ListAsync());
        Assert.False(File.Exists(Path.Combine(_root, "evil.jpg")));
    }

    [Fact]
    public async Task OversizedPicture_IsRefused_AndNothingIsStored()
    {
        Story story = Make(StoryIds.New());
        byte[] package = Zip(
            (StoryPackageManifest.ManifestFileName, ManifestJson(1, story.Id)),
            (StoryPackageManifest.StoryFileName, StoryJson.Serialize(story)),
            ("page-1.jpg", new string('x', (int)StoryPackageReader.MaxPictureBytes + 1)));
        FileStoryLibrary target = Library("target");

        await Assert.ThrowsAsync<StoryPackageException>(() => StoryPackageReader.ImportAsync(new MemoryStream(package), target));

        Assert.Empty(await target.ListAsync());
    }

    [Fact]
    public async Task NotAZip_AndMissingStory_AreRefused()
    {
        FileStoryLibrary target = Library("target");

        await Assert.ThrowsAsync<StoryPackageException>(() => StoryPackageReader.ImportAsync(new MemoryStream(Encoding.UTF8.GetBytes("nope")), target));
        byte[] noStory = Zip((StoryPackageManifest.ManifestFileName, ManifestJson(1, StoryIds.New())));
        await Assert.ThrowsAsync<StoryPackageException>(() => StoryPackageReader.ImportAsync(new MemoryStream(noStory), target));
    }

    [Fact]
    public async Task FailedImport_LeavesNoFolderBehind()
    {
        FileStoryLibrary target = Library("target");
        Story story = Make(StoryIds.New());
        PackageFile broken = new("page-1.jpg", () => throw new IOException("disk"));

        await Assert.ThrowsAsync<IOException>(() => target.ImportAsync(story, [broken]));

        Assert.False(Directory.Exists(target.FolderOf(story.Id)));
    }

    [Fact]
    public async Task KeepId_OnAStoryFromAPackage_KeepsItsSourceId()
    {
        (FileStoryLibrary source, Story story) = await StoredAsync();
        FileStoryLibrary middle = Library("middle");
        Story fromPackage = await StoryPackageReader.ImportAsync(new MemoryStream(await ExportAsync(story, source)), middle);

        Story restored = await StoryPackageReader.ImportAsync(new MemoryStream(await ExportAsync(fromPackage, middle)), Library("target"), keepId: true);

        Assert.Equal(fromPackage.Id, restored.Id);
        Assert.Equal(story.Id, restored.SourceId);
    }

    [Theory]
    [InlineData("story.json")]
    [InlineData("Story.JSON")]
    [InlineData("package.json")]
    public async Task StoryNamingAReservedFileAsAPicture_IsRefused(string reserved)
    {
        Story story = Make(StoryIds.New()) with { Pages = [new StoryPage("x", reserved)], CoverFile = null };
        byte[] package = Zip(
            (StoryPackageManifest.ManifestFileName, ManifestJson(1, story.Id)),
            (StoryPackageManifest.StoryFileName, StoryJson.Serialize(story)));
        FileStoryLibrary target = Library("target");

        await Assert.ThrowsAsync<StoryPackageException>(() => StoryPackageReader.ImportAsync(new MemoryStream(package), target));

        Assert.Empty(await target.ListAsync());
    }

    [Theory]
    [InlineData("NUL.png")]
    [InlineData("com1.jpg")]
    public async Task DeviceNamedEntry_IsRefused(string entryName)
    {
        Story story = Make(StoryIds.New());
        byte[] package = Zip(
            (StoryPackageManifest.ManifestFileName, ManifestJson(1, story.Id)),
            (StoryPackageManifest.StoryFileName, StoryJson.Serialize(story)),
            (entryName, "x"));

        await Assert.ThrowsAsync<StoryPackageException>(() => StoryPackageReader.ImportAsync(new MemoryStream(package), Library("target")));
    }

    [Fact]
    public async Task NewerSchemaWithAChangedShape_IsReportedAsNewer()
    {
        Story story = Make(StoryIds.New());
        byte[] package = Zip(
            (StoryPackageManifest.ManifestFileName, """{"schemaVersion":2,"id":42}"""),
            (StoryPackageManifest.StoryFileName, StoryJson.Serialize(story)));

        var ex = await Assert.ThrowsAsync<StoryPackageException>(() => StoryPackageReader.ImportAsync(new MemoryStream(package), Library("target")));

        Assert.Contains("newer", ex.Message);
    }

    [Fact]
    public async Task PictureMissingFromThePackage_OrWithABadName_IsDroppedFromTheStory()
    {
        Story story = Make(StoryIds.New()) with { Pages = [new StoryPage("x", "page-1.jpg"), new StoryPage("y", "../../x.png")], CoverFile = "cover-ab12cd34.jpg" };
        byte[] package = Zip(
            (StoryPackageManifest.ManifestFileName, ManifestJson(1, story.Id)),
            (StoryPackageManifest.StoryFileName, StoryJson.Serialize(story)),
            ("page-1.jpg", "bytes"));

        Story imported = await StoryPackageReader.ImportAsync(new MemoryStream(package), Library("target"));

        Assert.Equal("page-1.jpg", imported.Pages[0].ImageFile);
        Assert.Null(imported.Pages[1].ImageFile);
        Assert.Null(imported.CoverFile);
    }

    [Fact]
    public async Task FolderAndMacMetadataEntries_AreIgnored()
    {
        Story story = Make(StoryIds.New()) with { Pages = [new StoryPage("x")], CoverFile = null };
        byte[] package = Zip(
            ("pictures/", ""),
            (StoryPackageManifest.ManifestFileName, ManifestJson(1, story.Id)),
            (StoryPackageManifest.StoryFileName, StoryJson.Serialize(story)),
            ("__MACOSX/._story.json", "x"));

        Story imported = await StoryPackageReader.ImportAsync(new MemoryStream(package), Library("target"));

        Assert.Equal(story.Title, imported.Title);
    }

    [Fact]
    public async Task FailedImport_IntoAnExistingFolder_KeepsTheFilesAlreadyThere()
    {
        FileStoryLibrary target = Library("target");
        Story story = Make(StoryIds.New());
        string existing = target.TryResolveFile(story.Id, "other.jpg")!;
        Directory.CreateDirectory(target.FolderOf(story.Id));
        await File.WriteAllTextAsync(existing, "keep");
        PackageFile broken = new("page-1.jpg", () => throw new IOException("disk"));

        await Assert.ThrowsAsync<IOException>(() => target.ImportAsync(story, [broken]));

        Assert.True(File.Exists(existing));
    }
}
