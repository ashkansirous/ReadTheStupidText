using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 108, Decision 113: a separate cast per reading language.</summary>
public sealed class LanguageCastTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dino-langcast-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static readonly CastMember[] English =
    [
        new("peter", "Peter", Gender.Male, "supertonic:M1", Edited: true),
        new("mum", "Mum", Gender.Female, "supertonic:F1"),
    ];

    private static readonly CastMember[] Farsi =
    [
        new("peter", "Peter", Gender.Male, "piper-fa:amir", Edited: true),
        new("mum", "Mum", Gender.Female, "piper-fa:ganji"),
    ];

    private static Story Make() => new(
        StoryIds.New(), "Peter Rabbit", "en", new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
        [new StoryPage("\"Hello,\" said Peter.")], English, CoverStyle.Colour, 1, "supertonic:M3",
        CharacterMerges: new Dictionary<string, string> { ["rabbit"] = "peter" });

    [Fact]
    public void HasCastIn_TreatsAnEmptyStoredCastAsNone()
    {
        Story story = Make().WithCastIn("fa", []);

        Assert.False(story.HasCastIn("fa"));
    }

    [Fact]
    public void CastIn_TreatsAnUnsupportedStoryLanguageAsEnglish()
    {
        Story story = Make() with { Language = "de" };

        Assert.True(story.HasCastIn("en"));
        Assert.Same(story.Cast, story.CastIn("en"));
    }

    [Fact]
    public void CastIn_ReadsTheOwnCastForTheStoryLanguageAndNothingForAnUnseenOne()
    {
        Story story = Make();

        Assert.Equal(English, story.CastIn("en"));
        Assert.Equal(English, story.CastIn("EN"));
        Assert.Empty(story.CastIn("fa"));
        Assert.True(story.HasCastIn("en"));
        Assert.False(story.HasCastIn("fa"));
    }

    [Fact]
    public void WithCastIn_KeepsEachLanguagesCastApart()
    {
        Story story = Make().WithCastIn("fa-IR", Farsi);

        Assert.Equal(Farsi, story.CastIn("fa"));
        Assert.Equal(English, story.CastIn("en"));
        Assert.True(story.HasCastIn("fa"));
    }

    [Fact]
    public void Edits_PinInTheReadingLanguageOnly()
    {
        Story story = Make().WithCastIn("fa", Farsi);

        CastEdit edit = CastEditor.SetVoice(CastEditor.Open(story, "fa"), "mum", "piper-fa:other", "piper-fa:narrator");
        Story saved = CastEditor.Close(story, "fa", edit);

        CastMember mum = saved.CastIn("fa").Single(m => m.CharacterId == "mum");
        Assert.True(mum.Edited);
        Assert.Equal("piper-fa:other", mum.VoiceId);
        Assert.Equal(English, saved.CastIn("en"));
    }

    [Fact]
    public void Merges_AreSharedByEveryLanguage()
    {
        Story story = Make().WithCastIn("fa", Farsi);

        Story saved = CastEditor.Close(story, "fa", CastEditor.Merge(CastEditor.Open(story, "fa"), "peter", "mum"));

        Assert.Equal("peter", saved.Merges["mum"]);
        Assert.Single(saved.CastIn("fa"));
        Assert.Equal(2, saved.CastIn("en").Count);
        Assert.Equal("peter", saved.Merges["mum"]);
    }

    [Fact]
    public void ARoundTrip_KeepsEveryLanguagesCast()
    {
        Story story = Make().WithCastIn("fa", Farsi);

        Story back = StoryJson.Deserialize(StoryJson.Serialize(story));

        Assert.Equal(English, back.Cast);
        Assert.Equal(Farsi, back.CastIn("fa"));
    }

    [Fact]
    public void AnOldStoryFile_OpensWithItsSingleCastAsTheOwnLanguagesCast()
    {
        JsonNode json = JsonNode.Parse(StoryJson.Serialize(Make()))!;
        Assert.Null(json["LanguageCasts"]);

        Story back = StoryJson.Deserialize(json.ToJsonString());

        Assert.Equal(English, back.CastIn("en"));
        Assert.Null(back.LanguageCasts);
    }

    [Fact]
    public void ADamagedOrOwnLanguageEntry_IsDroppedNotTheStory()
    {
        JsonNode json = JsonNode.Parse(StoryJson.Serialize(Make().WithCastIn("fa", Farsi)))!;
        JsonObject casts = json["LanguageCasts"]!.AsObject();
        casts["en"] = casts["fa"]!.DeepClone();
        casts["fr"] = new JsonArray(new JsonObject { ["CharacterId"] = null, ["Name"] = "X", ["VoiceId"] = "v" });

        Story back = StoryJson.Deserialize(json.ToJsonString());

        Assert.Equal(English, back.Cast);
        Assert.Equal(["fa"], back.LanguageCasts!.Keys);
    }

    [Fact]
    public void AnEntryForTheLanguageAnUnsupportedStoryResolvesTo_IsDropped()
    {
        Story story = Make() with { Language = "de" };
        JsonNode json = JsonNode.Parse(StoryJson.Serialize(story.WithCastIn("fa", Farsi)))!;
        json["LanguageCasts"]!.AsObject()["en"] = json["LanguageCasts"]!["fa"]!.DeepClone();

        Story back = StoryJson.Deserialize(json.ToJsonString());

        Assert.Equal(["fa"], back.LanguageCasts!.Keys);
    }

    [Fact]
    public void AnEditedThenRevertedDraft_KeepsTheOtherLanguagesCasts()
    {
        var draft = new StoryDraft(Make().WithCastIn("fa", Farsi));
        StoryPage original = draft.Pages[0];

        draft.Pages[0] = new StoryPage("Another text.");
        Assert.True(draft.RecastNeeded);
        Assert.Null(draft.Current.LanguageCasts);

        draft.Pages[0] = original;
        Assert.False(draft.RecastNeeded);
        Assert.Equal(Farsi, draft.Current.CastIn("fa"));
    }

    [Fact]
    public async Task APackageAndABackup_CarryEveryLanguagesCast()
    {
        var library = new FileStoryLibrary(Path.Combine(_root, "source"));
        Story story = Make().WithCastIn("fa", Farsi);
        await library.AddAsync(story);

        using var package = new MemoryStream();
        await StoryPackageWriter.WriteAsync(story, library, package);
        package.Position = 0;
        var target = new FileStoryLibrary(Path.Combine(_root, "target"));
        Story imported = await StoryPackageReader.ImportAsync(package, target);

        Assert.Equal(Farsi, imported.CastIn("fa"));
        Assert.Equal(English, imported.CastIn("en"));
        Assert.Equal(Farsi, Zipped(await BackupAsync(library)).CastIn("fa"));
    }

    private static async Task<byte[]> BackupAsync(FileStoryLibrary library)
    {
        using var stream = new MemoryStream();
        await StoryBackupWriter.WriteAsync(await library.ListAsync(), library, stream);
        return stream.ToArray();
    }

    // The story.json of the one package inside a backup zip.
    private static Story Zipped(byte[] backup)
    {
        using var outer = new ZipArchive(new MemoryStream(backup));
        ZipArchiveEntry package = outer.Entries.Single(e => e.Name.EndsWith(".dino", StringComparison.Ordinal));
        using var inner = new ZipArchive(package.Open(), ZipArchiveMode.Read);
        using var reader = new StreamReader(inner.GetEntry(StoryPackageManifest.StoryFileName)!.Open(), Encoding.UTF8);
        return StoryJson.Deserialize(reader.ReadToEnd());
    }
}
