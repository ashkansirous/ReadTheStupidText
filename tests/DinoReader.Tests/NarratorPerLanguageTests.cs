using System.IO.Compression;
using System.Text.Json;
using DinoReader.Core;
using ReadTheStupidText.Domain.Reading;

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

    [Fact]
    public void HasVoiceOutside_FlagsACastWhoseVoicesLeftThePool()
    {
        CastMember[] cast = [new("a", "A", Gender.Male, "supertonic:M1"), new("b", "B", Gender.Female, "kokoro:af_bella")];
        VoiceOption[] kokoro = [new("kokoro:af_heart", Gender.Female), new("kokoro:af_bella", Gender.Female)];

        Assert.True(CastEditor.HasVoiceOutside(cast, kokoro));
        Assert.False(CastEditor.HasVoiceOutside(cast.Skip(1).ToList(), kokoro));
    }

    [Fact]
    public void HasVoiceOutside_IgnoresAnEmptyPool()
    {
        CastMember[] cast = [new("a", "A", Gender.Male, "supertonic:M1")];

        Assert.False(CastEditor.HasVoiceOutside(cast, []));
    }

    [Fact]
    public void HasVoiceOutside_IgnoresPoolsAndVoicesThatAreNotTheSupertonicKokoroSwitch()
    {
        VoiceOption[] piper = [new("piper:fa-a", Gender.Female)];
        CastMember[] persian = [new("a", "A", Gender.Female, "piper:fa-gone")];
        CastMember[] english = [new("b", "B", Gender.Male, "supertonic:M1")];

        Assert.False(CastEditor.HasVoiceOutside(persian, piper));
        Assert.False(CastEditor.HasVoiceOutside(english, piper));
        Assert.False(CastEditor.HasVoiceOutside(persian, [new("kokoro:af_heart", Gender.Female)]));
    }

    private static readonly VoiceOption[] KokoroPool = [new("kokoro:af_heart", Gender.Female), new("kokoro:am_puck", Gender.Male)];

    [Fact]
    public void HasVoiceOutside_FlagsAnExplicitSupertonicPick_AndAnExplicitKokoroPickWhoseVoiceLeft()
    {
        CastMember[] pickedSupertonic = [new("a", "A", Gender.Male, "supertonic:M1", Edited: true)];
        CastMember[] pickedKokoro = [new("b", "B", Gender.Female, "kokoro:af_bella", Edited: true)];

        Assert.True(CastEditor.HasVoiceOutside(pickedSupertonic, KokoroPool));
        Assert.True(CastEditor.HasVoiceOutside(pickedKokoro, KokoroPool));
        Assert.False(CastEditor.HasVoiceOutside(pickedSupertonic, [new("supertonic:M1", Gender.Male)]));
    }

    [Fact]
    public void KeepExplicitPicks_RecastsASupertonicPickToKokoro_KeepingGenderAndFlavour()
    {
        CastMember[] before =
        [
            new("a", "A", Gender.Male, "supertonic:M1", Edited: true),
            new("b", "B", Gender.Female, "supertonic:F1"),
        ];
        CastMember[] recast =
        [
            new("a", "A", Gender.Male, "kokoro:am_puck", Edited: true, Flavour: VoiceFlavour.Big),
            new("b", "B", Gender.Female, "kokoro:af_heart"),
        ];

        IReadOnlyList<CastMember> kept = CastEditor.KeepExplicitPicks(before, recast, KokoroPool, "kokoro:af_nicole");

        Assert.Equal("kokoro:am_puck", kept[0].VoiceId);
        Assert.Equal(Gender.Male, kept[0].Gender);
        Assert.Equal(VoiceFlavour.Big, kept[0].Flavour);
        Assert.True(kept[0].Edited);
        Assert.Equal("kokoro:af_heart", kept[1].VoiceId);
    }

    [Fact]
    public void KeepExplicitPicks_KeepsASupertonicPickWhenThePoolIsNotTheStorytellers()
    {
        VoiceOption[] supertonicPool = [new("supertonic:F1", Gender.Female), new("supertonic:M2", Gender.Male)];
        CastMember[] before = [new("a", "A", Gender.Male, "supertonic:M1", Edited: true)];
        CastMember[] recast = [new("a", "A", Gender.Male, "supertonic:M2")];

        IReadOnlyList<CastMember> kept = CastEditor.KeepExplicitPicks(before, recast, supertonicPool, "supertonic:F1");

        Assert.Equal("supertonic:M1", kept[0].VoiceId);
    }

    [Fact]
    public void StorytellerInstall_RecastsAnEditedSupertonicCastToKokoroByGender_KeepingFlavours()
    {
        Character[] characters = [new("pig", "Pig", Gender.Female, 50), new("wolf", "Wolf", Gender.Male, 40)];
        CastMember[] stored =
        [
            new("pig", "Pig", Gender.Female, "supertonic:F2", Edited: true, Flavour: VoiceFlavour.Small),
            new("wolf", "Wolf", Gender.Male, "supertonic:M2", Edited: true, Flavour: VoiceFlavour.Big),
        ];
        VoiceOption[] pool =
        [
            new("kokoro:af_heart", Gender.Female), new("kokoro:af_bella", Gender.Female),
            new("kokoro:am_puck", Gender.Male), new("kokoro:am_adam", Gender.Male),
        ];
        Assert.True(CastEditor.HasVoiceOutside(stored, pool));

        VoiceCast cast = new VoiceCaster().Cast(characters, pool, "kokoro:af_bella", StoryCast.Pinned(stored));
        IReadOnlyList<CastMember> members = StoryCast.ToMembers(characters, cast, LanguageRules.English, stored);
        IReadOnlyList<CastMember> result = CastEditor.KeepExplicitPicks(stored, members, pool, "kokoro:af_bella");

        Assert.StartsWith("kokoro:af_", result.Single(m => m.CharacterId == "pig").VoiceId);
        Assert.StartsWith("kokoro:am_", result.Single(m => m.CharacterId == "wolf").VoiceId);
        Assert.Equal(VoiceFlavour.Small, result.Single(m => m.CharacterId == "pig").Flavour);
        Assert.Equal(VoiceFlavour.Big, result.Single(m => m.CharacterId == "wolf").Flavour);
        Assert.False(CastEditor.HasVoiceOutside(result, pool));
    }

    [Fact]
    public void KeepExplicitPicks_KeepsAnExplicitPickWhoseVoiceIsStillInThePool()
    {
        CastMember[] before = [new("a", "A", Gender.Male, "kokoro:am_puck", Edited: true)];
        CastMember[] recast = [new("a", "A", Gender.Male, "kokoro:am_puck", Edited: true)];

        Assert.Equal("kokoro:am_puck", CastEditor.KeepExplicitPicks(before, recast, KokoroPool, "kokoro:af_nicole")[0].VoiceId);
    }

    [Fact]
    public void KeepExplicitPicks_GivesAPickWhoseVoiceLeftWithItsPackTheClosestVoice()
    {
        CastMember[] before = [new("a", "A", Gender.Female, "kokoro:af_bella", Edited: true)];
        VoiceOption[] supertonic = [new("supertonic:F1", Gender.Female), new("supertonic:M1", Gender.Male)];
        CastMember[] recast = [new("a", "A", Gender.Female, "supertonic:F1", Edited: true)];

        IReadOnlyList<CastMember> kept = CastEditor.KeepExplicitPicks(before, recast, supertonic, "supertonic:M1");

        Assert.Equal("supertonic:F1", kept[0].VoiceId);
    }

    [Fact]
    public void KeepExplicitPicks_NeverKeepsThePickThatIsNowTheNarrators()
    {
        CastMember[] before = [new("a", "A", Gender.Male, "supertonic:M1", Edited: true)];
        CastMember[] recast = [new("a", "A", Gender.Male, "kokoro:am_puck", Edited: true)];

        IReadOnlyList<CastMember> kept = CastEditor.KeepExplicitPicks(before, recast, KokoroPool, "supertonic:M1");

        Assert.Equal("kokoro:am_puck", kept[0].VoiceId);
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
