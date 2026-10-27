using DinoReader.Core;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Tests;

public class CastEditorTests
{
    private static readonly VoiceOption[] Voices =
    [
        new("M1", Gender.Male), new("M2", Gender.Male), new("M3", Gender.Male),
        new("F1", Gender.Female), new("F2", Gender.Female), new("F3", Gender.Female),
    ];

    private static readonly Character[] Characters =
    [
        new("peter", "Peter", Gender.Male, 212),
        new("cotton", "Cotton Tail", Gender.Male, 148),
        new("mum", "Mum", Gender.Female, 31),
    ];

    private static CastEdit Start() => new(
        [
            new CastMember("peter", "Peter", Gender.Male, "M1"),
            new CastMember("cotton", "Cotton Tail", Gender.Male, "M2"),
            new CastMember("mum", "Mum", Gender.Female, "F1"),
        ]);

    private static CastMember Of(CastEdit state, string id) => state.Cast.Single(m => m.CharacterId == id);

    private static StorytellerCharacter Pick(string voiceId) =>
        new("pick", voiceId, VoiceFlavour.Normal, Gender.Male, CharacterKind.Speaker, "Pick", "hint");

    private static CastEdit Pinned(CastEdit state, string characterId, string voiceId) => CastEditor.SetCharacter(state, characterId, Pick(voiceId));


    [Fact]
    public void SetCharacter_PinsTheVoiceAndFlavourAndMarksItEdited()
    {
        CastEdit edited = Pinned(Start(), "cotton", "F2");

        Assert.Equal(new CastMember("cotton", "Cotton Tail", Gender.Male, "F2", true, VoiceFlavour.Normal), Of(edited, "cotton"));
        Assert.Equal("M1", Of(edited, "peter").VoiceId);
        Assert.False(Of(edited, "peter").Edited);
    }

    [Fact]
    public void RunningTheCasterAgain_NeverOverridesAnEditedVoice()
    {
        CastEdit edited = Pinned(Start(), "cotton", "F2");

        VoiceCast recast = new VoiceCaster().Cast(Characters, Voices, "F3", StoryCast.Pinned(edited.Cast));

        Assert.Equal("F2", recast.VoiceFor("cotton"));
        Assert.Equal("M1", recast.VoiceFor("peter"));
        Assert.Equal("F1", recast.VoiceFor("mum"));
    }

    [Fact]
    public void ToMembers_KeepsTheEditedFlagWhateverVoiceTheCasterEndsWith()
    {
        CastEdit edited = Pinned(Start(), "cotton", "F2");
        VoiceCast recast = new VoiceCaster().Cast(Characters, Voices, "F2", StoryCast.Pinned(edited.Cast));

        var members = StoryCast.ToMembers(Characters, recast, LanguageRules.English, edited.Cast);

        Assert.True(members.Single(m => m.CharacterId == "cotton").Edited);
        Assert.NotEqual("F2", members.Single(m => m.CharacterId == "cotton").VoiceId);
        Assert.False(members.Single(m => m.CharacterId == "peter").Edited);
    }

    [Fact]
    public void NarratorTakingAPinnedVoice_UnpinsThatCharacter()
    {
        var before = new CastMember[] { new("peter", "Peter", Gender.Male, "M1", true), new("mum", "Mum", Gender.Female, "F1", true) };
        var recast = new CastMember[] { new("peter", "Peter", Gender.Male, "M2", true), new("mum", "Mum", Gender.Female, "F1", true) };
        var unpinned = CastEditor.UnpinOverridden(before, recast, "M1");
        Assert.False(unpinned[0].Edited);
        Assert.True(unpinned[1].Edited);
    }
}

public class StoryCastResolveTests
{
    private static Segment Quote(string? who, int start) => new(SegmentKind.Quote, "Hi", start, start + 4, who);

    private static AttributionResult Sample() => new(
        [Quote("peter", 0), Quote("rabbit", 10), Quote("mum", 20), Quote(null, 30)],
        [
            new Character("peter", "Peter", Gender.Male, 212),
            new Character("rabbit", "the rabbit", Gender.Unknown, 40),
            new Character("mum", "Mum", Gender.Female, 31),
        ]);

    [Fact]
    public void Resolve_TakesAnEditedCharactersStoredGender()
    {
        CastMember[] stored =
        [
            new("peter", "Peter", Gender.Female, "F1", true),
            new("mum", "Mum", Gender.Male, "M1"),
        ];

        var resolved = StoryCast.Resolve(Sample(), stored);

        Assert.Equal(Gender.Female, resolved.Characters.Single(c => c.Id == "peter").Gender);
        Assert.Equal(Gender.Female, resolved.Characters.Single(c => c.Id == "mum").Gender);
    }

    [Fact]
    public void Resolve_KeepsEveryCharacterAndLineSeparate()
    {
        var resolved = StoryCast.Resolve(Sample(), []);

        Assert.Equal(["peter", "rabbit", "mum"], resolved.Characters.Select(c => c.Id));
        Assert.Equal(["peter", "rabbit", "mum", null], resolved.Segments.Select(s => s.CharacterId));
    }
}

public class CastSheetTests
{
    private static AttributionResult Result(params (string Id, int Words)[] characters) => new(
        [new Segment(SegmentKind.Quote, "Hi", 0, 4, null), new Segment(SegmentKind.Quote, "Yo", 5, 9, "a"), new Segment(SegmentKind.Narration, "x", 10, 11)],
        characters.Select(c => new Character(c.Id, c.Id, Gender.Unknown, c.Words)).ToList());

    private static CastMember Member(string id, string voice) => new(id, id, Gender.Unknown, voice);

    [Fact]
    public void Build_MarksTheTwoWordiestAsMain_AndCountsUnknownLines()
    {
        var sheet = CastSheet.Build(Result(("a", 5), ("b", 50), ("c", 20)), [Member("a", "V1"), Member("b", "V2"), Member("c", "V3")], 9, LanguageRules.English);

        Assert.Equal([false, true, true], sheet.Rows.Select(r => r.IsMain));
        Assert.Equal([5, 50, 20], sheet.Rows.Select(r => r.Words));
        Assert.Equal(1, sheet.UnknownLines);
        Assert.False(sheet.VoicesAreShort);
    }

    [Fact]
    public void Build_TagsLaterHoldersOfASharedVoice_OnlyWhenTheVoicesAreShort()
    {
        var cast = new[] { Member("a", "V1"), Member("b", "V1"), Member("c", "V2") };

        var plenty = CastSheet.Build(Result(("a", 3), ("b", 2), ("c", 1)), cast, 5, LanguageRules.English);
        var few = CastSheet.Build(Result(("a", 3), ("b", 2), ("c", 1)), cast, 2, LanguageRules.English);

        Assert.All(plenty.Rows, r => Assert.False(r.SharesVoice));
        Assert.True(few.VoicesAreShort);
        Assert.Equal([false, true, false], few.Rows.Select(r => r.SharesVoice));
    }

    [Fact]
    public void Build_SkipsACharacterWithNoStoredVoice()
    {
        var sheet = CastSheet.Build(Result(("a", 3), ("b", 2)), [Member("b", "V1")], 5, LanguageRules.English);

        Assert.Equal(["b"], sheet.Rows.Select(r => r.Member.CharacterId));
    }
}

public class CastStorageTests
{
    private static Story Sample() => new(
        StoryIds.New(), "Title", "en", DateTimeOffset.UnixEpoch, [new StoryPage("Text")],
        [new CastMember("peter", "Peter", Gender.Female, "F1", true), new CastMember("mum", "Mum", Gender.Female, "F2")],
        CoverStyle.Colour, 0, "M1");

    [Fact]
    public void RoundTrip_KeepsTheEditedFlags()
    {
        Story story = Sample();

        Story back = StoryJson.Deserialize(StoryJson.Serialize(story));

        Assert.Equal(story.Cast, back.Cast);
    }

    [Fact]
    public void AFileWrittenBeforeSlice65_ReadsAsNothingEdited()
    {
        string json = StoryJson.Serialize(Sample()).Replace("\"Edited\": true", "\"Other\": 1");

        Story back = StoryJson.Deserialize(json);

        Assert.All(back.Cast, m => Assert.False(m.Edited));
    }

    [Fact]
    public void AFileWithMergesAndWordsOverPictures_StillOpens_AndWritesNeitherAgain()
    {
        string json = StoryJson.Serialize(Sample()).Insert(1, "\"Merges\": { \"rabbit\": \"peter\" }, \"WordsOverPictures\": false,");
        Assert.Contains("\"Merges\"", json);

        string rewritten = StoryJson.Serialize(StoryJson.Deserialize(json));

        Assert.DoesNotContain("Merges", rewritten);
        Assert.DoesNotContain("WordsOverPictures", rewritten);
    }
}

public class UnknownSpeakerRuleTests
{
    [Fact]
    public void VoiceForLine_UsesTheNarratorForNarrationUnknownSpeakersAndVoicelessCharacters()
    {
        var cast = new VoiceCast("N", [new VoiceAssignment("peter", "M1", false)]);

        Assert.Equal("M1", cast.VoiceForLine("peter"));
        Assert.Equal("N", cast.VoiceForLine(null));
        Assert.Equal("N", cast.VoiceForLine("nobody"));
    }

    [Fact]
    public void AvatarPalette_WrapsAndTakesTheFirstLetter()
    {
        Assert.Equal("#4FC060", AvatarPalette.ColourAt(0));
        Assert.Equal("#4FC060", AvatarPalette.ColourAt(4));
        Assert.Equal("#9AA5B8", AvatarPalette.ColourAt(-1));
        Assert.Equal("T", AvatarPalette.InitialOf("the rabbit"));
        Assert.Equal("?", AvatarPalette.InitialOf("  "));
    }
}
