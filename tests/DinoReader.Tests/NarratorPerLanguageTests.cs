using System.IO.Compression;
using System.Text.Json;
using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 109: the narrator is one voice per language, not a story field.</summary>
public class NarratorPerLanguageTests
{
    private static readonly IReadOnlyList<VoiceOption> Voices =
        [new VoiceOption("F1", Gender.Female), new VoiceOption("M1", Gender.Male), new VoiceOption("M2", Gender.Male)];

    [Fact]
    public void Resolve_UsesTheStoredPick_WhileThePackStillHasIt()
    {
        Assert.Equal("M2", NarratorSetting.Resolve("M2", Voices, "M1"));
    }

    [Fact]
    public void Resolve_FallsBackToTheDefault_WhenNothingIsStoredOrThePackLostTheVoice()
    {
        Assert.Equal("M1", NarratorSetting.Resolve(null, Voices, "M1"));
        Assert.Equal("M1", NarratorSetting.Resolve(string.Empty, Voices, "M1"));
        Assert.Equal("M1", NarratorSetting.Resolve("gone", Voices, "M1"));
    }

    [Fact]
    public void Resolve_KeepsTheStoredPick_WhileThePackIsNotInstalled()
    {
        Assert.Equal("nb:a", NarratorSetting.Resolve("nb:a", [], "supertonic:M1"));
    }

    [Fact]
    public void UsesVoice_FindsACharacterWhoHasTheNarratorsVoice()
    {
        CastMember[] cast = [new("a", "A", Gender.Male, "M1"), new("b", "B", Gender.Female, "F1")];
        Assert.True(CastEditor.UsesVoice(cast, "F1"));
        Assert.False(CastEditor.UsesVoice(cast, "M2"));
    }

    private const string OldStory = """
        {"Version":1,"Id":"{ID}","Title":"T","Language":"en","Created":"2026-10-01T00:00:00+00:00","CoverStyle":"Colour","CoverColourIndex":0,
         "NarratorVoiceId":"F1","Pages":[{"Text":"Hi."}],"Cast":[]}
        """;

    [Fact]
    public void OldStoryWithANarratorField_StillLoads()
    {
        Story story = StoryJson.Deserialize(OldStory.Replace("{ID}", StoryIds.New()));
        Assert.Equal("T", story.Title);
    }

    [Fact]
    public void StoryWithoutANarratorField_Loads()
    {
        string json = OldStory.Replace("{ID}", StoryIds.New()).Replace("\"NarratorVoiceId\":\"F1\",", string.Empty);
        Assert.DoesNotContain("NarratorVoiceId", json);
        Assert.Equal("T", StoryJson.Deserialize(json).Title);
    }

    [Fact]
    public void NewStories_StillWriteANarrator_ForOlderBuilds()
    {
        Func<string, string> before = StoryJson.LegacyNarratorOf;
        try
        {
            StoryJson.LegacyNarratorOf = language => language + ":voice";
            Story story = Story.NewOwnBook(StoryIds.New(), "en", DateTimeOffset.UnixEpoch, 0);
            using JsonDocument written = JsonDocument.Parse(StoryJson.Serialize(story));
            Assert.Equal("en:voice", written.RootElement.GetProperty("NarratorVoiceId").GetString());
        }
        finally
        {
            StoryJson.LegacyNarratorOf = before;
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OldPackage_WithOrWithoutANarratorField_Imports(bool withField)
    {
        string root = Path.Combine(Path.GetTempPath(), "dino-narrator-" + Guid.NewGuid().ToString("N"));
        try
        {
            string id = StoryIds.New();
            string story = OldStory.Replace("{ID}", id);
            if (!withField)
            {
                story = story.Replace("\"NarratorVoiceId\":\"F1\",", string.Empty);
            }

            using var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                AddEntry(zip, StoryPackageManifest.ManifestFileName, $$"""{"schemaVersion":{{StoryPackageManifest.CurrentSchemaVersion}},"id":"{{id}}","title":"T","language":"en"}""");
                AddEntry(zip, StoryPackageManifest.StoryFileName, story);
            }

            stream.Position = 0;
            Story imported = await StoryPackageReader.ImportAsync(stream, new FileStoryLibrary(root));
            Assert.Equal("T", imported.Title);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void AddEntry(ZipArchive zip, string name, string content)
    {
        using Stream entry = zip.CreateEntry(name).Open();
        entry.Write(System.Text.Encoding.UTF8.GetBytes(content));
    }
}
