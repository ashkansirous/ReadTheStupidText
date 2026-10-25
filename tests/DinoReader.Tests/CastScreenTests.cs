using DinoReader.Core;

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
        ],
        new Dictionary<string, string>());

    private static CastMember Of(CastEdit state, string id) => state.Cast.Single(m => m.CharacterId == id);

    [Fact]
    public void SetVoice_PinsTheVoiceAndMarksItEdited()
    {
        CastEdit edited = CastEditor.SetVoice(Start(), "cotton", "F2", "F3");

        Assert.Equal(new CastMember("cotton", "Cotton Tail", Gender.Male, "F2", true), Of(edited, "cotton"));
        Assert.Equal("M1", Of(edited, "peter").VoiceId);
        Assert.False(Of(edited, "peter").Edited);
    }

    [Fact]
    public void SetVoice_NeverGivesACharacterTheNarratorsVoice()
    {
        CastEdit state = Start();

        CastEdit edited = CastEditor.SetVoice(state, "cotton", "F3", "F3");

        Assert.Same(state, edited);
    }

    [Fact]
    public void RunningTheCasterAgain_NeverOverridesAnEditedVoice()
    {
        CastEdit edited = CastEditor.SetVoice(Start(), "cotton", "F2", "F3");

        VoiceCast recast = new VoiceCaster().Cast(Characters, Voices, "F3", StoryCast.Pinned(edited.Cast));

        Assert.Equal("F2", recast.VoiceFor("cotton"));
        Assert.Equal("M1", recast.VoiceFor("peter"));
        Assert.Equal("F1", recast.VoiceFor("mum"));
    }

    [Fact]
    public void ToMembers_KeepsTheEditedFlagWhateverVoiceTheCasterEndsWith()
    {
        CastEdit edited = CastEditor.SetVoice(Start(), "cotton", "F2", "F3");
        VoiceCast recast = new VoiceCaster().Cast(Characters, Voices, "F2", StoryCast.Pinned(edited.Cast));

        var members = StoryCast.ToMembers(Characters, recast, LanguageRules.English, edited.Cast);

        Assert.True(members.Single(m => m.CharacterId == "cotton").Edited);
        Assert.NotEqual("F2", members.Single(m => m.CharacterId == "cotton").VoiceId);
        Assert.False(members.Single(m => m.CharacterId == "peter").Edited);
    }

    [Fact]
    public void SetGender_RecastsThatCharacterWithinTheGender_WithoutTouchingOtherPins()
    {
        CastEdit state = CastEditor.SetVoice(Start(), "mum", "F2", "M3");

        CastEdit edited = CastEditor.SetGender(state, Characters, Voices, "M3", "cotton", Gender.Female);

        CastMember cotton = Of(edited, "cotton");
        Assert.Equal(Gender.Female, cotton.Gender);
        Assert.Equal("F1", cotton.VoiceId);
        Assert.True(cotton.Edited);
        Assert.Equal("M1", Of(edited, "peter").VoiceId);
        Assert.Equal(new CastMember("mum", "Mum", Gender.Female, "F2", true), Of(edited, "mum"));
    }

    [Fact]
    public void SetGender_KeepsTheVoiceWhenItAlreadyFitsOrWhenTheGenderIsUnknown()
    {
        CastEdit sameGender = CastEditor.SetGender(Start(), Characters, Voices, "F3", "peter", Gender.Male);
        CastEdit unknown = CastEditor.SetGender(Start(), Characters, Voices, "F3", "peter", Gender.Unknown);

        Assert.Equal("M1", Of(sameGender, "peter").VoiceId);
        Assert.Equal("M1", Of(unknown, "peter").VoiceId);
        Assert.Equal(Gender.Unknown, Of(unknown, "peter").Gender);
        Assert.True(Of(unknown, "peter").Edited);
    }

    [Fact]
    public void SetGender_NeverPicksTheNarratorsVoice()
    {
        CastEdit edited = CastEditor.SetGender(Start(), Characters, Voices, "F1", "cotton", Gender.Female);

        Assert.NotEqual("F1", Of(edited, "cotton").VoiceId);
        Assert.Equal(Gender.Female, SupertonicLike(Voices, Of(edited, "cotton").VoiceId));
    }

    private static readonly VoiceOption[] TwoVoices = [new("M1", Gender.Male), new("F1", Gender.Female)];

    private static CastEdit TwoVoiceStart() => new(
        [
            new CastMember("peter", "Peter", Gender.Male, "M1"),
            new CastMember("cotton", "Cotton Tail", Gender.Male, "M1"),
            new CastMember("mum", "Mum", Gender.Female, "F1"),
        ],
        new Dictionary<string, string>());

    [Fact]
    public void SetGender_InATwoVoicePack_SharesTheTargetGendersVoice_NeverTheOpposite()
    {
        CastEdit edited = CastEditor.SetGender(TwoVoiceStart(), Characters, TwoVoices, "none", "cotton", Gender.Female);

        Assert.Equal("F1", Of(edited, "cotton").VoiceId);
        Assert.Equal(Gender.Female, Of(edited, "cotton").Gender);
    }

    [Fact]
    public void SetGender_WhenSharing_PicksTheVoiceWhoseHoldersSpeakTheFewestWords()
    {
        CastMember[] cast =
        [
            new("peter", "Peter", Gender.Male, "M1"), new("cotton", "Cotton Tail", Gender.Male, "M2"),
            new("mum", "Mum", Gender.Female, "F1"), new("gran", "Gran", Gender.Female, "F2"),
        ];
        Character[] characters =
        [
            new("peter", "Peter", Gender.Male, 212), new("cotton", "Cotton Tail", Gender.Male, 148),
            new("mum", "Mum", Gender.Female, 300), new("gran", "Gran", Gender.Female, 20),
        ];
        VoiceOption[] voices = [new("M1", Gender.Male), new("M2", Gender.Male), new("F1", Gender.Female), new("F2", Gender.Female)];

        CastEdit edited = CastEditor.SetGender(new CastEdit(cast, new Dictionary<string, string>()), characters, voices, "none", "cotton", Gender.Female);

        Assert.Equal("F2", Of(edited, "cotton").VoiceId);
    }

    [Fact]
    public void SetGender_WithNoVoiceOfThatGender_KeepsTheCurrentVoice_AndTheCallerCanTell()
    {
        VoiceOption[] maleOnly = [new("M1", Gender.Male), new("M2", Gender.Male)];

        CastEdit edited = CastEditor.SetGender(Start(), Characters, maleOnly, "M2", "mum", Gender.Female);

        Assert.Equal("F1", Of(edited, "mum").VoiceId);
        Assert.Equal(Gender.Female, Of(edited, "mum").Gender);
        Assert.False(CastEditor.HasVoiceOfGender(maleOnly, "M2", Gender.Female));
        Assert.True(CastEditor.HasVoiceOfGender(Voices, "F1", Gender.Female));
    }

    [Fact]
    public void SetGender_NeverTakesTheNarratorsOnlyVoiceOfTheGender()
    {
        CastEdit edited = CastEditor.SetGender(TwoVoiceStart(), Characters, TwoVoices, "F1", "cotton", Gender.Female);

        Assert.Equal("M1", Of(edited, "cotton").VoiceId);
    }

    private static Gender SupertonicLike(VoiceOption[] voices, string id) => voices.Single(v => v.Id == id).Gender;

    [Fact]
    public void Merge_LeavesOneEntryWithTheKeptVoice()
    {
        CastEdit merged = CastEditor.Merge(Start(), "peter", "cotton");

        Assert.Equal(["peter", "mum"], merged.Cast.Select(m => m.CharacterId));
        Assert.Equal("M1", Of(merged, "peter").VoiceId);
        Assert.False(Of(merged, "peter").Edited);
        Assert.Equal("peter", merged.Merges["cotton"]);
    }

    [Fact]
    public void Merge_KeepsAnEditedFlagTheKeptCharacterAlreadyHad()
    {
        CastEdit pinned = CastEditor.SetVoice(Start(), "peter", "M3", "F3");

        Assert.True(Of(CastEditor.Merge(pinned, "peter", "cotton"), "peter").Edited);
    }

    [Fact]
    public void Merge_TakesAKnownGenderOverUnknown_AndKeepsTheKeptOneOnAConflict()
    {
        var state = new CastEdit(
            [
                new CastMember("a", "A", Gender.Unknown, "M1"), new CastMember("b", "B", Gender.Female, "F1"),
                new CastMember("c", "C", Gender.Male, "M2"),
            ],
            new Dictionary<string, string>());

        Assert.Equal(Gender.Female, Of(CastEditor.Merge(state, "a", "b"), "a").Gender);
        Assert.Equal(Gender.Male, Of(CastEditor.Merge(state, "c", "b"), "c").Gender);
        Assert.Equal(Gender.Female, Of(CastEditor.Merge(state, "b", "a"), "b").Gender);
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

    [Fact]
    public void Merge_CanKeepTheSecondName_AndKeepsItsVoice()
    {
        CastEdit merged = CastEditor.Merge(Start(), "cotton", "peter");

        Assert.Equal("M2", Of(merged, "cotton").VoiceId);
        Assert.DoesNotContain(merged.Cast, m => m.CharacterId == "peter");
    }

    [Fact]
    public void Merge_OfACharacterIntoItselfOrAnUnknownOne_ChangesNothing()
    {
        CastEdit state = Start();

        Assert.Same(state, CastEditor.Merge(state, "peter", "peter"));
        Assert.Same(state, CastEditor.Merge(state, "peter", "nobody"));
    }

    [Fact]
    public void VoiceChoices_LeaveOutTheNarrator_AndListTheGendersVoicesFirst()
    {
        var choices = CastEditor.VoiceChoices(Voices, "F1", Gender.Female);

        Assert.DoesNotContain(choices, v => v.Id == "F1");
        Assert.Equal(["F2", "F3", "M1", "M2", "M3"], choices.Select(v => v.Id));
    }
}

public class CastMergesTests
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
    public void Apply_ReAttributesTheMergedLines_AndAddsTheirWords()
    {
        var merged = CastMerges.Apply(Sample(), new Dictionary<string, string> { ["rabbit"] = "peter" });

        Assert.Equal(["peter", "peter", "mum", null], merged.Segments.Select(s => s.CharacterId));
        Assert.Equal(["peter", "mum"], merged.Characters.Select(c => c.Id));
        Assert.Equal(252, merged.Characters[0].WordCount);
        Assert.Equal("Peter", merged.Characters[0].Name);
    }

    [Fact]
    public void Apply_GivesTheMergedCharacterTheBetterKnownGender()
    {
        var intoRabbit = CastMerges.Apply(Sample(), new Dictionary<string, string> { ["peter"] = "rabbit" });
        var intoPeter = CastMerges.Apply(Sample(), new Dictionary<string, string> { ["mum"] = "peter" });

        Assert.Equal(Gender.Male, intoRabbit.Characters.Single(c => c.Id == "rabbit").Gender);
        Assert.Equal(Gender.Male, intoPeter.Characters.Single(c => c.Id == "peter").Gender);
    }

    [Fact]
    public void Apply_FollowsAChainOfMerges()
    {
        var merges = new Dictionary<string, string> { ["rabbit"] = "mum", ["mum"] = "peter" };

        var merged = CastMerges.Apply(Sample(), merges);

        Assert.Equal(["peter"], merged.Characters.Select(c => c.Id));
        Assert.All(merged.Segments.Where(s => s.CharacterId is not null), s => Assert.Equal("peter", s.CharacterId));
        Assert.Equal(283, merged.Characters[0].WordCount);
    }

    [Fact]
    public void Apply_IgnoresAMergeWhoseCharacterIsGone()
    {
        var merges = new Dictionary<string, string> { ["ghost"] = "peter", ["rabbit"] = "gone" };

        var merged = CastMerges.Apply(Sample(), merges);

        Assert.Equal(3, merged.Characters.Count);
        Assert.Equal("rabbit", merged.Segments[1].CharacterId);
    }

    [Fact]
    public void Apply_SurvivesACycle()
    {
        var merges = new Dictionary<string, string> { ["peter"] = "rabbit", ["rabbit"] = "peter" };

        var merged = CastMerges.Apply(Sample(), merges);

        Assert.Equal(["peter", "rabbit", "mum"], merged.Characters.Select(c => c.Id));
        Assert.Equal(["peter", "rabbit", "mum", null], merged.Segments.Select(s => s.CharacterId));
    }

    [Fact]
    public void Apply_WithNoMerges_ReturnsTheSameResult()
    {
        var sample = Sample();

        Assert.Same(sample, CastMerges.Apply(sample, new Dictionary<string, string>()));
    }

    [Fact]
    public void Add_RepointsEarlierMergesIntoTheMergedAwayCharacter()
    {
        var first = CastMerges.Add(new Dictionary<string, string>(), "rabbit", "bunny");

        var second = CastMerges.Add(first, "peter", "rabbit");

        Assert.Equal("peter", second["rabbit"]);
        Assert.Equal("peter", second["bunny"]);
    }

    [Fact]
    public void Resolve_TakesAnEditedCharactersStoredGender()
    {
        CastMember[] stored =
        [
            new("peter", "Peter", Gender.Female, "F1", true),
            new("mum", "Mum", Gender.Male, "M1"),
        ];

        var resolved = StoryCast.Resolve(Sample(), new Dictionary<string, string>(), stored);

        Assert.Equal(Gender.Female, resolved.Characters.Single(c => c.Id == "peter").Gender);
        Assert.Equal(Gender.Female, resolved.Characters.Single(c => c.Id == "mum").Gender);
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
    private static Story Sample(IReadOnlyDictionary<string, string>? merges = null) => new(
        StoryIds.New(), "Title", "en", DateTimeOffset.UnixEpoch, [new StoryPage("Text")],
        [new CastMember("peter", "Peter", Gender.Female, "F1", true), new CastMember("mum", "Mum", Gender.Female, "F2")],
        CoverStyle.Colour, 0, "M1", null, merges);

    [Fact]
    public void RoundTrip_KeepsTheEditedFlagsAndTheMerges()
    {
        Story story = Sample(new Dictionary<string, string> { ["rabbit"] = "peter" });

        Story back = StoryJson.Deserialize(StoryJson.Serialize(story));

        Assert.Equal(story.Cast, back.Cast);
        Assert.Equal("peter", back.Merges["rabbit"]);
    }

    [Fact]
    public void AFileWrittenBeforeSlice65_ReadsAsNothingEditedAndNoMerges()
    {
        string json = StoryJson.Serialize(Sample()).Replace("\"Edited\": true", "\"Other\": 1");

        Story back = StoryJson.Deserialize(json);

        Assert.All(back.Cast, m => Assert.False(m.Edited));
        Assert.Empty(back.Merges);
    }

    [Fact]
    public void NoMerges_AreNotWritten_AndAnEmptyMergeEntryIsDropped()
    {
        string plain = StoryJson.Serialize(Sample());
        string json = StoryJson.Serialize(Sample(new Dictionary<string, string> { ["a"] = "b" })).Replace("\"a\"", "\"\"");

        Assert.DoesNotContain("\"Merges\": {", plain);
        Assert.Empty(StoryJson.Deserialize(json).Merges);
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

public class MergeSelectionTests
{
    [Fact]
    public void TwoTicks_Complete_AndTheFirstTickedKeepsTheName()
    {
        var selection = new MergeSelection();

        selection.Toggle("peter");
        Assert.False(selection.IsComplete);
        Assert.Null(selection.KeepId);
        selection.Toggle("rabbit");

        Assert.True(selection.IsComplete);
        Assert.Equal("peter", selection.KeepId);
        Assert.Equal("rabbit", selection.DropId);
    }

    [Fact]
    public void AThirdTick_ReplacesTheOldest()
    {
        var selection = new MergeSelection();
        selection.Toggle("a");
        selection.Toggle("b");

        selection.Toggle("c");

        Assert.Equal(["b", "c"], selection.Picked);
        Assert.Equal("b", selection.KeepId);
    }

    [Fact]
    public void Keep_ChoosesTheOtherName_AndIsForgottenWhenThatCharacterIsUnticked()
    {
        var selection = new MergeSelection();
        selection.Toggle("a");
        selection.Toggle("b");

        selection.Keep("b");
        Assert.Equal("b", selection.KeepId);
        Assert.Equal("a", selection.DropId);

        selection.Toggle("b");
        selection.Toggle("c");
        Assert.Equal("a", selection.KeepId);
        selection.Keep("zzz");
        Assert.Equal("a", selection.KeepId);
    }

    [Fact]
    public void TickingATickedCharacter_UnticksIt()
    {
        var selection = new MergeSelection();
        selection.Toggle("a");

        selection.Toggle("a");

        Assert.Empty(selection.Picked);
        Assert.False(selection.Contains("a"));
    }
}
