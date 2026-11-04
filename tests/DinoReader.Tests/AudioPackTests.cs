using System.IO.Compression;
using System.Text.Json;
using DinoReader.BookBuilder;

namespace DinoReader.Tests;

/// <summary>Slice 246 B2: the per-language audio pack the book builder makes from a generator clips folder.</summary>
public sealed class AudioPackTests : IDisposable
{
    private const string Language = "fa";
    private const string BookId = "a02c0731b7ac475a896e9184f3575e7b";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "dino-audio-pack-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Decide_FirstPackIsOne_SameContentKeepsTheVersion_ChangedContentRaisesIt()
    {
        Assert.Equal(1, AudioPacks.Decide(null, "aa"));
        Assert.Equal(3, AudioPacks.Decide(new AudioPackRecord(3, "aa", 10), "aa"));
        Assert.Equal(4, AudioPacks.Decide(new AudioPackRecord(3, "aa", 10), "bb"));
    }

    [Fact]
    public void Build_PacksTheManifestAndOnlyTheClipsItNames_AtTheRootOfTheZip()
    {
        (BookFolder book, string clips, string store) = Arrange(["a.m4a", "b.m4a"], extra: ["stray.wav"]);

        AudioPackRecord record = AudioPacks.Build(book, Language, clips, store);

        Assert.Equal(1, record.Version);
        string zip = Path.Combine(store, BookId, "audio-fa-v1.zip");
        using ZipArchive archive = ZipFile.OpenRead(zip);
        Assert.Equal(["a.m4a", "b.m4a", "manifest.json"], archive.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal));
        Assert.Equal(new FileInfo(zip).Length, record.Bytes);
    }

    [Fact]
    public void Build_SameContentKeepsVersionAndBytes_AChangedClipRaisesTheVersion_AndTheRecordIsKeptInTheCast()
    {
        (BookFolder book, string clips, string store) = Arrange(["a.m4a"]);
        AudioPackRecord first = AudioPacks.Build(book, Language, clips, store);
        byte[] firstBytes = File.ReadAllBytes(Path.Combine(store, BookId, "audio-fa-v1.zip"));

        AudioPackRecord again = AudioPacks.Build(book, Language, clips, store);
        Assert.Equal(first, again);
        Assert.Equal(firstBytes, File.ReadAllBytes(Path.Combine(store, BookId, "audio-fa-v1.zip")));

        File.WriteAllText(Path.Combine(clips, "a.m4a"), "changed");
        AudioPackRecord changed = AudioPacks.Build(book, Language, clips, store);
        Assert.Equal(2, changed.Version);
        Assert.True(File.Exists(Path.Combine(store, BookId, "audio-fa-v2.zip")));
        Assert.Equal(changed, AudioPacks.RecordsIn(book.Dir)[Language]);
        using JsonDocument cast = JsonDocument.Parse(File.ReadAllText(Path.Combine(book.Dir, "audio-cast.json")));
        Assert.Equal("Hali", cast.RootElement.GetProperty("narrator").GetProperty("voice").GetString()); // the rest of the cast is kept
    }

    [Fact]
    public void Build_AnotherLanguageHasItsOwnVersion()
    {
        (BookFolder book, string clips, string store) = Arrange(["a.m4a"]);
        AudioPacks.Build(book, Language, clips, store);
        string norwegian = Path.Combine(_root, "clips-nb");
        Directory.CreateDirectory(norwegian);
        File.WriteAllText(Path.Combine(norwegian, "n.m4a"), "n");
        File.WriteAllText(Path.Combine(norwegian, "manifest.json"), Manifest("nb", ["n.m4a"]));

        Assert.Equal(1, AudioPacks.Build(book, "nb", norwegian, store).Version);
        Assert.Equal(["fa", "nb"], AudioPacks.RecordsIn(book.Dir).Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void PackFiles_RejectsAMissingClip_AWrongLanguage_AWrongManifestVersion_AndAMissingManifest()
    {
        (_, string clips, _) = Arrange(["a.m4a"]);
        File.Delete(Path.Combine(clips, "a.m4a"));
        Assert.Contains("a.m4a", Assert.Throws<InvalidOperationException>(() => AudioPacks.PackFiles(clips, Language)).Message);

        File.WriteAllText(Path.Combine(clips, "a.m4a"), "x");
        Assert.Throws<InvalidOperationException>(() => AudioPacks.PackFiles(clips, "nb"));
        File.WriteAllText(Path.Combine(clips, "manifest.json"), Manifest(Language, ["a.m4a"]).Replace("\"version\":3", "\"version\":2"));
        Assert.Throws<InvalidOperationException>(() => AudioPacks.PackFiles(clips, Language));
        File.Delete(Path.Combine(clips, "manifest.json"));
        Assert.Throws<InvalidOperationException>(() => AudioPacks.PackFiles(clips, Language));
    }

    [Fact]
    public void PackFiles_RejectsAClipNamedManifest_AndNamesThatDifferOnlyInCase()
    {
        (_, string clips, _) = Arrange(["manifest.json"]);
        Assert.Throws<InvalidOperationException>(() => AudioPacks.PackFiles(clips, Language));

        (_, string caseClips, _) = Arrange(["A.m4a", "a.m4a"]);
        Assert.Throws<InvalidOperationException>(() => AudioPacks.PackFiles(caseClips, Language));
    }

    [Fact]
    public void Build_RejectsALanguageThatIsNotAnAppBookLanguage()
    {
        (BookFolder book, string clips, string store) = Arrange(["a.m4a"]);

        Assert.Contains("fa-IR", Assert.Throws<InvalidOperationException>(() => AudioPacks.Build(book, "fa-IR", clips, store)).Message);
        Assert.Throws<InvalidOperationException>(() => AudioPacks.Build(book, "FA", clips, store));
    }

    [Fact]
    public void Build_NeedsTheBooksCast()
    {
        (BookFolder book, string clips, string store) = Arrange(["a.m4a"]);
        File.Delete(Path.Combine(book.Dir, "audio-cast.json"));

        Assert.Throws<InvalidOperationException>(() => AudioPacks.Build(book, Language, clips, store));
    }

    [Fact]
    public void MetaOf_ListsEachLanguageWithVersionAndBytes_AndIsNullWithoutPacks()
    {
        (BookFolder book, string clips, string store) = Arrange(["a.m4a"]);
        Assert.Null(AudioPacks.MetaOf(book));
        Assert.Null(AudioPacks.MetaOf(null));

        AudioPackRecord record = AudioPacks.Build(book, Language, clips, store);

        string json = JsonSerializer.Serialize(AudioPacks.MetaOf(book));
        Assert.Equal("{\"fa\":{\"version\":1,\"bytes\":" + record.Bytes + "}}", json);
    }

    [Fact]
    public void NoCommittedBookClaimsAPackYet_SoNoMetaGainsAnAudioFieldBeforeAPackIsUploaded()
    {
        Assert.All(Books.All, b => Assert.True(AudioPacks.RecordsIn(b.Dir).Count == 0 || b.Info.Kind == BookKind.Store, b.Stem));
    }

    private (BookFolder Book, string Clips, string Store) Arrange(string[] clipNames, string[]? extra = null)
    {
        string bookDir = Path.Combine(_root, "book");
        string clips = Path.Combine(_root, "clips");
        Directory.CreateDirectory(bookDir);
        Directory.CreateDirectory(clips);
        File.WriteAllText(Path.Combine(bookDir, "audio-cast.json"), """{"version":1,"narrator":{"voice":"Hali","style":"warm"},"characters":{}}""");
        foreach (string name in clipNames.Concat(extra ?? []))
        {
            File.WriteAllText(Path.Combine(clips, name), "clip " + name);
        }

        File.WriteAllText(Path.Combine(clips, "manifest.json"), Manifest(Language, clipNames));
        var info = new BookInfo(BookKind.Store, BookId, "Tiny", 1, 3, 6, "Me", "Mine", 0, "2026-10-27", 0, null, null, null, null, null);
        return (new BookFolder("tiny", bookDir, info), clips, Path.Combine(_root, "store"));
    }

    private static string Manifest(string language, string[] clipNames) =>
        JsonSerializer.Serialize(new
        {
            version = 3,
            book = "tiny",
            language,
            entries = clipNames.Select((clip, index) => new { index, clip, hash = "h" + index }),
        });
}
