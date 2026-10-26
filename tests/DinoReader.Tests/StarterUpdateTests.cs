using DinoReader.Core;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Tests;

public sealed class StarterUpdateTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dino-starter-update-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Theory]
    [InlineData(null, 1, false)]
    [InlineData(null, 2, true)]
    [InlineData(1, 2, true)]
    [InlineData(2, 2, false)]
    [InlineData(3, 2, false)]
    public void NeedsUpdate_ComparesTheInstalledVersionWithTheBundledOne(int? installed, int bundled, bool expected)
    {
        Assert.Equal(expected, StarterUpdate.NeedsUpdate(installed, bundled));
    }

    // A member that sounds like a plain (no animal) character of the English roster, so a recast can tell what it plays.
    private static readonly StorytellerCharacter PlainCharacter = CharacterRoster.Speakers("en").First(c => AnimalCharacters.Of(c.Id) is null);

    private static CastMember Plain(string id) => new CastMember(id, id, Gender.Female, PlainCharacter.VoiceId, false, Flavour: PlainCharacter.Flavour);

    private static CastMember Member(string id, string voice, bool edited = false) => new(id, id, Gender.Female, voice, edited);

    private static Story StoryWith(string id, params CastMember[] cast) =>
        new(id, "Title", "en", DateTimeOffset.UnixEpoch, [new StoryPage("Page.")], cast, CoverStyle.Picture, 1, "narrator", CoverFile: "cover-x.jpg");

    [Fact]
    public void Refresh_FillsAnimalsFromTheBundledCopy_ForKeptMembersWithNone_ButNeverOverridesTheGrownUp()
    {
        Story stored = StoryWith("old", Member("pip", "v1"), Member("pop", "v2") with { NotAnimal = true }, Member("pim", "v3") with { Animal = AnimalKind.Cat }) with
        {
            LanguageCasts = new Dictionary<string, IReadOnlyList<CastMember>> { ["fr"] = [Member("pip", "v1"), Member("pop", "v2"), Member("pim", "v3")] },
        };
        Story bundled = StoryWith("new", Member("pip", "b1") with { Animal = AnimalKind.Pig }, Member("pop", "b2") with { Animal = AnimalKind.Pig }, Member("pim", "b3") with { Animal = AnimalKind.Pig }) with
        {
            LanguageCasts = new Dictionary<string, IReadOnlyList<CastMember>> { ["fr"] = [Member("pip", "bf1") with { Animal = AnimalKind.Pig }] },
        };

        Story refreshed = StarterUpdate.Refresh(stored, bundled);

        Assert.Equal(AnimalKind.Pig, refreshed.Cast[0].Animal);
        Assert.Equal("v1", refreshed.Cast[0].VoiceId);
        Assert.True(refreshed.Cast[1].NotAnimal);
        Assert.Null(refreshed.Cast[1].Animal);
        Assert.Equal(AnimalKind.Cat, refreshed.Cast[2].Animal);
        Assert.Equal(AnimalKind.Pig, refreshed.LanguageCasts!["fr"][0].Animal);
    }

    [Fact]
    public void AnimalVoicesToRecast_NamesTheKeptMembersThatGainedAnAnimalAndWereNotPickedByHand()
    {
        Story stored = StoryWith("old", Plain("pip"), Plain("pop") with { Edited = true }, Plain("pim"), Plain("pup") with { NotAnimal = true }) with
        {
            LanguageCasts = new Dictionary<string, IReadOnlyList<CastMember>> { ["fr"] = [Plain("pip"), Plain("pop") with { Edited = true }, Plain("pim"), Plain("pup") with { NotAnimal = true }] },
        };
        Story bundled = StoryWith("new", Member("pip", "b1") with { Animal = AnimalKind.Pig }, Member("pop", "b2") with { Animal = AnimalKind.Pig }, Member("pim", "b3"), Member("pup", "b4") with { Animal = AnimalKind.Pig }) with
        {
            LanguageCasts = new Dictionary<string, IReadOnlyList<CastMember>> { ["fr"] = [Member("pip", "bf1") with { Animal = AnimalKind.Pig }] },
        };

        Story refreshed = StarterUpdate.Refresh(stored, bundled);
        var recast = StarterUpdate.AnimalVoicesToRecast(stored, refreshed, _ => CharacterRoster.Speakers("en"));

        Assert.Equal([("en", "pip"), ("fr", "pip")], recast.OrderBy(r => r.Language, StringComparer.Ordinal));
    }

    [Fact]
    public void Refresh_GivesALanguageCastTheBaseCastsAnimalsWhenTheBundledCopyHasNoCastForIt()
    {
        // Slice 135: the Farsi cast of an installed pigs book kept no animals (Pop and Pim read "Not an animal"); character ids are the same in every language.
        Story stored = StoryWith("old", Plain("pip"), Plain("pop")) with
        {
            LanguageCasts = new Dictionary<string, IReadOnlyList<CastMember>> { ["fa"] = [Plain("pip"), Plain("pop") with { NotAnimal = true }] },
        };
        Story bundled = StoryWith("new", Member("pip", "b1") with { Animal = AnimalKind.Pig }, Member("pop", "b2") with { Animal = AnimalKind.Pig });

        Story refreshed = StarterUpdate.Refresh(stored, bundled);

        Assert.Equal(AnimalKind.Pig, refreshed.LanguageCasts!["fa"][0].Animal);
        Assert.True(refreshed.LanguageCasts["fa"][1].NotAnimal);
        Assert.Equal([("en", "pip"), ("en", "pop"), ("fa", "pip")], StarterUpdate.AnimalVoicesToRecast(stored, refreshed, _ => CharacterRoster.Speakers("en")).OrderBy(r => r.Language, StringComparer.Ordinal).ThenBy(r => r.CharacterId, StringComparer.Ordinal));
    }

    [Fact]
    public void Refresh_GivesALanguageMemberTheBaseCastsAnimalByIdEvenWhenTheNameIsTranslated()
    {
        Story stored = StoryWith("old", Member("pip", "v1")) with
        {
            LanguageCasts = new Dictionary<string, IReadOnlyList<CastMember>> { ["fa"] = [new CastMember("pip", "Translated name", Gender.Male, "f1", false)] },
        };
        Story bundled = StoryWith("new", Member("pip", "b1") with { Animal = AnimalKind.Pig });

        Story refreshed = StarterUpdate.Refresh(stored, bundled);

        Assert.Equal(AnimalKind.Pig, refreshed.LanguageCasts!["fa"][0].Animal);
    }

    private static StorytellerCharacter Roster(string id, string voice, VoiceFlavour flavour) =>
        new(id, voice, flavour, Gender.Male, CharacterKind.Speaker, id, id);

    private static (Story Stored, Story Refreshed) Refreshed(CastMember member)
    {
        Story stored = StoryWith("old", member);
        return (stored, StarterUpdate.Refresh(stored, StoryWith("new", member)));
    }

    [Fact]
    public void AnimalVoicesToRecast_LeavesAMemberWhoseVoiceMatchesNoRosterCharacterAlone()
    {
        var (stored, refreshed) = Refreshed(new CastMember("pip", "Pip", Gender.Male, "unknown:voice", Flavour: VoiceFlavour.Normal) with { Animal = AnimalKind.Pig });

        Assert.Empty(StarterUpdate.AnimalVoicesToRecast(stored, refreshed));
    }

    [Fact]
    public void AnimalVoicesToRecast_RecastsFarsiPigsThatReadAsTheBearCubAndThePuppy()
    {
        // Slice 135 follow-up: installed Farsi pigs on the Gyro voice with the Small and Bouncy flavours are shown as the bear cub and the puppy.
        var roster = CharacterRoster.Speakers("fa");
        string gyro = roster.First(c => c.Id == "piglet2").VoiceId;
        CastMember Pig(string id, VoiceFlavour flavour) => new CastMember(id, id, Gender.Male, gyro, Flavour: flavour) with { Animal = AnimalKind.Pig };
        Story stored = StoryWith("old", Pig("pip", VoiceFlavour.Squeaky), Pig("pop", VoiceFlavour.Small), Pig("pim", VoiceFlavour.Bouncy));
        Story refreshed = StarterUpdate.Refresh(stored, stored);

        var recast = StarterUpdate.AnimalVoicesToRecast(stored, refreshed, _ => roster);

        Assert.Equal([("en", "pim"), ("en", "pop")], recast.OrderBy(r => r.CharacterId, StringComparer.Ordinal));
    }

    [Fact]
    public void AnimalVoicesToRecast_LeavesAMotherPigAloneBecauseNoGrownPigExists()
    {
        var (stored, refreshed) = Refreshed(new CastMember("mother", "Mother Pig", Gender.Female, "kokoro:af_x", Flavour: VoiceFlavour.Normal));
        IReadOnlyList<StorytellerCharacter> roster = [Roster("mummy", "kokoro:af_x", VoiceFlavour.Normal), Roster("piglet", "kokoro:am_a", VoiceFlavour.Small)];

        Assert.Empty(StarterUpdate.AnimalVoicesToRecast(stored, refreshed, _ => roster));
    }

    [Fact]
    public void AnimalVoicesToRecast_LeavesAnOwlAloneWhenTheCastLanguagesRosterHasNoOwl()
    {
        var (stored, refreshed) = Refreshed(new CastMember("owl", "Owl", Gender.Male, "kokoro:am_a", Flavour: VoiceFlavour.Normal));
        IReadOnlyList<StorytellerCharacter> roster = [Roster("knight", "kokoro:am_a", VoiceFlavour.Normal), Roster("piglet", "kokoro:am_b", VoiceFlavour.Small)];

        Assert.Empty(StarterUpdate.AnimalVoicesToRecast(stored, refreshed, _ => roster));
    }

    [Fact]
    public void AnimalVoicesToRecast_LeavesAGrownBearOnTheCubAndRecastsOneOnAPlainCharacter()
    {
        IReadOnlyList<StorytellerCharacter> roster = [Roster("cub", "kokoro:am_a", VoiceFlavour.Small), Roster("bigbear", "kokoro:am_c", VoiceFlavour.Big), Roster("knight", "kokoro:am_b", VoiceFlavour.Normal)];
        var (onCub, refreshedCub) = Refreshed(new CastMember("papa", "Papa Bear", Gender.Male, "kokoro:am_a", Flavour: VoiceFlavour.Small));
        var (onKnight, refreshedKnight) = Refreshed(new CastMember("papa", "Papa Bear", Gender.Male, "kokoro:am_b", Flavour: VoiceFlavour.Normal));

        Assert.Empty(StarterUpdate.AnimalVoicesToRecast(onCub, refreshedCub, _ => roster));
        Assert.Equal([("en", "papa")], StarterUpdate.AnimalVoicesToRecast(onKnight, refreshedKnight, _ => roster));
    }

    [Fact]
    public void AnimalVoicesToRecast_IncludesAWolfFoundByItsNameWhoseVoiceIsAnotherCharacters()
    {
        var wolfAsDragon = new CastMember("wolf", "Wolf", Gender.Male, "kokoro:am_santa", Flavour: VoiceFlavour.Big);
        var wolfAsWolf = new CastMember("wolf2", "Wolf", Gender.Male, "kokoro:bm_fable", Flavour: VoiceFlavour.Grumpy);
        var picked = wolfAsDragon with { CharacterId = "wolf3", Edited = true };
        Story stored = StoryWith("old", wolfAsDragon, wolfAsWolf, picked);

        Story refreshed = StarterUpdate.Refresh(stored, StoryWith("new", wolfAsDragon, wolfAsWolf, picked));

        Assert.Equal([("en", "wolf")], StarterUpdate.AnimalVoicesToRecast(stored, refreshed));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Refresh_KeepsTheAnimalSoundsSwitchAndItSurvivesTheSavedFile(bool on)
    {
        Story stored = StoryWith("old", Member("pip", "v1")) with { AnimalSoundsOn = on };
        Story bundled = StoryWith("new", Member("pip", "b1") with { Animal = AnimalKind.Pig }) with { SourceId = "pkg" };

        Story refreshed = StarterUpdate.Refresh(stored, bundled);

        Assert.Equal(on, refreshed.AnimalSoundsOn);
        Assert.Equal(on, StoryJson.Deserialize(StoryJson.Serialize(refreshed with { Id = StoryIds.New() })).AnimalSoundsOn);
    }

    [Fact]
    public void AnimalVoicesToRecast_IsEmptyWhenTheCastWasReplaced()
    {
        Story stored = StoryWith("old", Member("pip", "v1"));
        Story bundled = StoryWith("new", Member("pip", "b1") with { Animal = AnimalKind.Pig }, Member("pop", "b2") with { Animal = AnimalKind.Pig });

        Assert.Empty(StarterUpdate.AnimalVoicesToRecast(stored, StarterUpdate.Refresh(stored, bundled)));
    }

    [Fact]
    public void Refresh_TakesTheBundledText_AndKeepsTheGrownUpsChoices()
    {
        Story stored = StoryWith("old", Member("pig", "supertonic:F3", edited: true), Member("wolf", "supertonic:M2")) with
        {
            Created = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CharacterMerges = new Dictionary<string, string> { ["big-wolf"] = "wolf" },
            PreferredMode = ReadingMode.Text,
            TranslateToMyLanguage = false,
            ShowOriginalText = true,
            AnimalSoundsOn = false,
            UseRecordings = true,
        };
        Story bundled = StoryWith("new", Member("wolf", "supertonic:M1"), Member("pig", "supertonic:F1")) with
        {
            Pages = [new StoryPage("Gentle page.")],
            SourceId = "pkg",
            TranslateToMyLanguage = true,
        };

        Story refreshed = StarterUpdate.Refresh(stored, bundled);

        Assert.Equal("new", refreshed.Id);
        Assert.Equal("pkg", refreshed.SourceId);
        Assert.Equal("Gentle page.", refreshed.Pages[0].Text);
        Assert.Equal(stored.Created, refreshed.Created);
        Assert.Equal(stored.Cast, refreshed.Cast);
        Assert.Equal(stored.CharacterMerges, refreshed.CharacterMerges);
        Assert.Equal(ReadingMode.Text, refreshed.PreferredMode);
        Assert.False(refreshed.TranslateToMyLanguage);
        Assert.True(refreshed.ShowOriginalText);
        Assert.False(refreshed.AnimalSoundsOn);
        Assert.Null(refreshed.Recordings);
    }

    private static Story Paged(params string[] texts) =>
        StoryWith("s") with { Pages = texts.Select(t => new StoryPage(t)).ToArray() };

    [Fact]
    public void RecordingMoves_FollowTheSameTextToItsNewPage_AndDropChangedText()
    {
        Story stored = Paged("One.", "Two.", "Three.") with
        {
            Recordings = new Dictionary<int, PageRecording> { [0] = new("en", 3), [1] = new("en", 4), [2] = new("en", 5) },
        };
        Story bundled = Paged("Intro.", "Three.", "Two reworded.", "One.");

        var moves = StarterUpdate.RecordingMoves(stored, bundled);

        Assert.Equal([new StarterUpdate.RecordingMove(0, 3), new StarterUpdate.RecordingMove(2, 1)], moves);
    }

    [Fact]
    public void RecordingMoves_GiveRepeatedTextOneRecordingEach()
    {
        Story stored = Paged("Same.", "Same.") with { Recordings = new Dictionary<int, PageRecording> { [0] = new("en", 1), [1] = new("en", 2) } };

        var moves = StarterUpdate.RecordingMoves(stored, Paged("Same.", "Other.", "Same."));

        Assert.Equal([new StarterUpdate.RecordingMove(0, 0), new StarterUpdate.RecordingMove(1, 2)], moves);
    }

    [Fact]
    public void Refresh_KeepsRecordingsOfUnchangedPages_AtTheirNewIndex()
    {
        Story stored = Paged("One.", "Two.") with
        {
            Recordings = new Dictionary<int, PageRecording> { [0] = new("en", 3), [1] = new("en", 9) },
            UseRecordings = true,
        };

        Story refreshed = StarterUpdate.Refresh(stored, Paged("Two.", "New.") with { Id = "new" });

        Assert.Equal(new PageRecording("en", 9), Assert.Single(refreshed.Recordings!).Value);
        Assert.True(refreshed.Recordings!.ContainsKey(0));
        Assert.True(refreshed.UseRecordings);
    }

    [Fact]
    public void Refresh_WhenNoRecordedPageSurvives_LeavesNoRecordings()
    {
        Story stored = Paged("One.") with { Recordings = new Dictionary<int, PageRecording> { [0] = new("en", 3) }, UseRecordings = true };

        Story refreshed = StarterUpdate.Refresh(stored, Paged("Changed."));

        Assert.Null(refreshed.Recordings);
        Assert.False(refreshed.UseRecordings);
    }

    [Fact]
    public void RecordingMoves_PreferTheSamePageIndex_WhenTheTextIsIdentical()
    {
        Story stored = Paged("Refrain.", "Middle.", "Refrain.") with { Recordings = new Dictionary<int, PageRecording> { [2] = new("en", 7) } };

        var moves = StarterUpdate.RecordingMoves(stored, Paged("Refrain.", "Middle.", "Refrain."));

        Assert.Equal([new StarterUpdate.RecordingMove(2, 2)], moves);
    }

    [Fact]
    public void Refresh_KeepsOnlyTheRecordingsWhoseFilesCopied()
    {
        Story stored = Paged("One.", "Two.") with
        {
            Recordings = new Dictionary<int, PageRecording> { [0] = new("en", 3), [1] = new("en", 9) },
            UseRecordings = true,
        };
        Story bundled = Paged("One.", "Two.") with { Id = "new" };

        Story refreshed = StarterUpdate.Refresh(stored, bundled, [new StarterUpdate.RecordingMove(1, 1)]);

        Assert.Equal([1], refreshed.Recordings!.Keys);
        Assert.Null(StarterUpdate.Refresh(stored, bundled, []).Recordings);
    }

    [Fact]
    public void Refresh_KeepsTheTitle_AndTheGrownUpsCoverPick()
    {
        Story stored = Paged("One.") with { Title = "My pigs", CoverStyle = CoverStyle.Colour, CoverColourIndex = 5, CoverFile = null };
        Story bundled = Paged("One.") with { Id = "new", CoverStyle = CoverStyle.Colour, CoverColourIndex = 3 };

        Story refreshed = StarterUpdate.Refresh(stored, bundled);

        Assert.Equal("My pigs", refreshed.Title);
        Assert.Equal(5, refreshed.CoverColourIndex);
    }

    [Fact]
    public void Refresh_KeepsAGalleryPhotoCover_OnlyWhenTheBundledCoverIsNotAPhoto()
    {
        Story stored = Paged("One.") with { CoverStyle = CoverStyle.Picture, CoverFile = "cover-abc.jpg" };
        Story plain = Paged("One.") with { Id = "new", CoverStyle = CoverStyle.Colour, CoverFile = null };
        Story photo = Paged("One.") with { Id = "new", CoverStyle = CoverStyle.Picture, CoverFile = "cover-new.jpg" };

        Assert.True(StarterUpdate.KeepsStoredPhoto(stored, plain));
        Assert.Equal("cover-abc.jpg", StarterUpdate.Refresh(stored, plain).CoverFile);
        Assert.False(StarterUpdate.KeepsStoredPhoto(stored, photo));
        Assert.Equal("cover-new.jpg", StarterUpdate.Refresh(stored, photo).CoverFile);
    }

    [Fact]
    public void IsEdited_IsTrueOnlyWhenTheTextMatchesNeitherTheBundledNorAnEarlierVersion()
    {
        const string pigs = "966400017c494fb5b045786f53852c9e";
        Story bundled = Paged("Gentle.");
        Story edited = Paged("My own words.");

        Assert.False(StarterUpdate.IsEdited(pigs, Paged("Gentle."), bundled));
        Assert.True(StarterUpdate.IsEdited(pigs, edited, bundled));
        Assert.False(StarterUpdate.IsEdited("unknown", Paged("Gentle."), bundled));
    }

    [Fact]
    public void AnUneditedV2CopyWithTheSameText_IsRefreshedToV3_KeepingEveryRecording()
    {
        const string pigs = "966400017c494fb5b045786f53852c9e";
        Story stored = Paged("One.", "Two.") with { CoverStyle = CoverStyle.Colour, CoverFile = null, Recordings = new Dictionary<int, PageRecording> { [0] = new("en", 1), [1] = new("en", 2) }, UseRecordings = true };
        Story bundled = Paged("One.", "Two.") with
        {
            Id = "new",
            Pages = [new StoryPage("One.", "page-1.jpg"), new StoryPage("Two.", "page-2.jpg")],
            CoverStyle = CoverStyle.Picture,
            CoverFile = "cover-new.jpg",
        };

        Assert.True(StarterUpdate.NeedsUpdate(2, 3));
        Assert.False(StarterUpdate.IsEdited(pigs, stored, bundled));
        Story refreshed = StarterUpdate.Refresh(stored, bundled);
        Assert.Equal(2, refreshed.Recordings!.Count);
        Assert.True(refreshed.UseRecordings);
        Assert.Equal(["page-1.jpg", "page-2.jpg"], refreshed.Pages.Select(p => p.ImageFile));
        Assert.Equal(CoverStyle.Picture, refreshed.CoverStyle);
        Assert.Equal("cover-new.jpg", refreshed.CoverFile);
        Assert.True(StarterUpdate.IsEdited(pigs, Paged("My own words."), bundled));
    }

    private static Story PigsCopy(CoverStyle style, int colour, string? coverFile) =>
        Paged("One.") with { SourceId = StarterTexts.PigsId, CoverStyle = style, CoverColourIndex = colour, CoverFile = coverFile };

    private static Story PigsPictureBundle() =>
        Paged("One.") with { Id = "new", Pages = [new StoryPage("One.", "page-1.jpg")], CoverStyle = CoverStyle.Picture, CoverFile = "cover-new.jpg" };

    [Fact]
    public void Refresh_ReplacesAPigsCopyStillOnItsShippedColourCover_WithThePictureCover()
    {
        Story refreshed = StarterUpdate.Refresh(PigsCopy(CoverStyle.Colour, 3, null), PigsPictureBundle());

        Assert.Equal(CoverStyle.Picture, refreshed.CoverStyle);
        Assert.Equal("cover-new.jpg", refreshed.CoverFile);
    }

    [Fact]
    public void Refresh_KeepsAPigsCopyWhoseColourTheUserChanged()
    {
        Story refreshed = StarterUpdate.Refresh(PigsCopy(CoverStyle.Colour, 5, null), PigsPictureBundle());

        Assert.Equal(CoverStyle.Colour, refreshed.CoverStyle);
        Assert.Equal(5, refreshed.CoverColourIndex);
    }

    [Fact]
    public void Refresh_KeepsAPigsCopyWithAGalleryPhotoCover()
    {
        Story stored = PigsCopy(CoverStyle.Picture, 3, "cover-mine.jpg") with { CoverPageIndex = null };

        Story refreshed = StarterUpdate.Refresh(stored, PigsPictureBundle());

        Assert.Equal(CoverStyle.Picture, refreshed.CoverStyle);
        Assert.Equal("cover-mine.jpg", refreshed.CoverFile);
    }

    [Fact]
    public void Refresh_DropsAPageCoverPick_WhenThePageCountChanged()
    {
        Story stored = PigsCopy(CoverStyle.Picture, 3, null) with { Pages = [new StoryPage("A.", "a.jpg"), new StoryPage("B.", "b.jpg")], CoverPageIndex = 1 };
        Story bundled = PigsPictureBundle() with { Pages = [new StoryPage("A.", "x.jpg"), new StoryPage("B.", "y.jpg"), new StoryPage("C.", "z.jpg")] };

        Story refreshed = StarterUpdate.Refresh(stored, bundled);

        Assert.Equal("cover-new.jpg", refreshed.CoverFile);
        Assert.Null(refreshed.CoverPageIndex);
    }

    [Fact]
    public void Refresh_KeepsAPageCoverPick_WhenThePageCountIsTheSame()
    {
        Story stored = PigsCopy(CoverStyle.Picture, 3, null) with { Pages = [new StoryPage("A.", "a.jpg"), new StoryPage("B.", "b.jpg")], CoverPageIndex = 1 };
        Story bundled = PigsPictureBundle() with { Pages = [new StoryPage("A.", "x.jpg"), new StoryPage("B.", "y.jpg")] };

        Assert.Equal(1, StarterUpdate.Refresh(stored, bundled).CoverPageIndex);
    }

    // The 5-page Tortoise and the Hare shipped as v2 and v3 (same text); the 14-page retelling is v4, so an unedited v3 copy must still count as unedited.
    [Fact]
    public void AnUneditedV3TortoiseCopy_WithTheOldFivePageText_IsNotEdited_AndIsRefreshedToTheFourteenPageRetelling()
    {
        Story stored = Paged(OldTortoisePages) with { SourceId = StarterTexts.TortoiseId, Recordings = new Dictionary<int, PageRecording> { [0] = new("en", 1) }, UseRecordings = true };
        Story bundled = Paged("One.", "Two.", "Three.") with { Id = "new" };

        Assert.True(StarterUpdate.NeedsUpdate(3, 4));
        Assert.False(StarterUpdate.IsEdited(StarterTexts.TortoiseId, stored, bundled));
        Assert.Equal(bundled.Pages.Select(p => p.Text), StarterUpdate.Refresh(stored, bundled).Pages.Select(p => p.Text));
        Assert.True(StarterUpdate.IsEdited(StarterTexts.TortoiseId, Paged("My own words."), bundled));
    }

    // The 5-page text shipped as v2 and v3 (identical), taken from StarterTexts at commit 74f5738.
    private static readonly string[] OldTortoisePages =
    [
        "Once upon a time there was a Hare who loved to run. He ran faster than anyone in the whole forest, and he never stopped telling everybody about it.\n\n\"I am the fastest animal in the world,\" Hare said. \"Nobody can ever beat me.\"\n\nOne day he met a Tortoise who was walking slowly down the path.",
        "\"You walk so slowly,\" Hare said with a laugh. \"You would take all day to cross a meadow.\"\n\n\"Perhaps I would,\" Tortoise said calmly. \"But I will race you, if you like.\"\n\n\"Race me?\" Hare said. \"That is the funniest thing I have ever heard. Very well, let us race to the old oak tree on the hill.\"\n\nThe animals gathered to watch. The Owl stood at the start and raised her wing. \"Ready, steady, go!\" Owl said.",
        "The Hare shot off like the wind. In a moment he was far ahead, and he could hardly see the Tortoise at all. The Tortoise did not hurry. He kept walking, one slow step after another.\n\n\"This is too easy,\" Hare said to himself. \"I have time for a little rest in the shade.\"\n\nThe Hare lay down under a tree and shut his eyes. Soon he was fast asleep.",
        "The Tortoise kept on walking, slow and steady. He walked past the Hare. He walked up the hill. The animals began to cheer.\n\n\"Go, Tortoise, go!\" Owl said. \"You are nearly there!\"\n\nAt last the Hare woke up with a start. He looked around and could not see the Tortoise anywhere. \"I had better run,\" Hare said. He raced up the hill as fast as he could.",
        "But it was too late. The Tortoise had already reached the old oak tree, and the animals were cheering his name.\n\n\"I won!\" Tortoise said, smiling.\n\nThe Hare hung his head. \"You were right to keep going,\" Hare said. \"I was too proud, and I did not even try my best.\"\n\n\"Slow and steady wins the race,\" Tortoise said kindly. \"Next time, we can run it together.\"\n\nAnd the Hare and the Tortoise became the best of friends.",
    ];

    [Fact]
    public void EarlierPageTextHashes_HoldTheShippedV3PigsText()
    {
        var field = typeof(StarterUpdate).GetField("EarlierPageTextHashes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var table = (IReadOnlyDictionary<string, string[]>)field.GetValue(null)!;

        Assert.Contains("8C1DD87C17BFC3A1386D16CEF384F13FD3C04DEDA8FD2A7D514FC90130F1CC3D", table[StarterTexts.PigsId]);
    }

    [Fact]
    public void CopyToKeep_PicksTheCopyHoldingTheBundledText()
    {
        Story old = Paged("Old.") with { Id = "old" };
        Story current = Paged("Gentle.") with { Id = "current" };

        Assert.Equal("current", StarterUpdate.CopyToKeep([old, current], Paged("Gentle.")).Id);
        Assert.Equal("old", StarterUpdate.CopyToKeep([old, Paged("Other.") with { Id = "x" }], Paged("Gentle.")).Id);
    }

    [Fact]
    public void WithMovedRecordings_AddsOnlyToPagesWithNone()
    {
        Story target = Paged("One.", "Two.") with { Recordings = new Dictionary<int, PageRecording> { [0] = new("en", 1) } };
        Story source = Paged("One.", "Two.") with { Recordings = new Dictionary<int, PageRecording> { [0] = new("en", 8), [1] = new("en", 9) } };

        Story merged = StarterUpdate.WithMovedRecordings(target, source, [new StarterUpdate.RecordingMove(0, 0), new StarterUpdate.RecordingMove(1, 1)]);

        Assert.Equal(new PageRecording("en", 1), merged.Recordings![0]);
        Assert.Equal(new PageRecording("en", 9), merged.Recordings[1]);
    }

    [Fact]
    public void AnExportedPackage_CarriesNoVersion_WhenNoneIsSet()
    {
        Assert.DoesNotContain("\"version\"", new StoryPackageManifest(1, "0123456789abcdef0123456789abcdef", "T", "en").ToJson(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Refresh_WhenTheCharactersChanged_UsesTheBundledCast()
    {
        Story stored = StoryWith("old", Member("pig", "supertonic:F3", edited: true));
        Story bundled = StoryWith("new", Member("pig", "supertonic:F1"), Member("fox", "supertonic:M1"));

        Story refreshed = StarterUpdate.Refresh(stored, bundled);

        Assert.Equal(bundled.Cast, refreshed.Cast);
    }

    [Fact]
    public async Task ThePackageManifest_CarriesTheContentVersion()
    {
        var library = new FileStoryLibrary(_root);
        Story story = StoryWith(StoryIds.New(), Member("pig", "supertonic:F1")) with { CoverStyle = CoverStyle.Colour, CoverFile = null };
        await library.AddAsync(story);
        await using var package = new MemoryStream();

        await StoryPackageWriter.WriteAsync(story, library, package, version: 2);

        package.Position = 0;
        using var zip = new System.IO.Compression.ZipArchive(package);
        using var reader = new StreamReader(zip.GetEntry(StoryPackageManifest.ManifestFileName)!.Open());
        Assert.Equal(2, StoryPackageManifest.Parse(await reader.ReadToEndAsync()).Version);
        Assert.Null(StoryPackageManifest.Parse("{\"schemaVersion\":1,\"id\":\"0123456789abcdef0123456789abcdef\",\"title\":\"T\",\"language\":\"en\"}").Version);
    }

    [Fact]
    public void EveryBundledStarter_HasAVersion_AndTheGentleOnesAreAtLeastTwo()
    {
        Assert.Equal(StarterBuilder.AllFileNames.Order(), StarterBuilder.StarterVersions.Keys.Order());
        foreach (var (_, _, _, _, fileName) in StarterBuilder.Starters)
        {
            Assert.True(StarterBuilder.StarterVersions[fileName] >= 2, fileName);
        }
    }

    [Fact]
    public async Task TheShippedStarters_CarryTheirVersionInTheManifest()
    {
        foreach (string fileName in StarterBuilder.AllFileNames)
        {
            await using FileStream file = File.OpenRead(Path.Combine(StarterBuilder.StartersFolder(), fileName));
            using var zip = new System.IO.Compression.ZipArchive(file);
            using var reader = new StreamReader(zip.GetEntry(StoryPackageManifest.ManifestFileName)!.Open());

            Assert.Equal(StarterBuilder.StarterVersions[fileName], StoryPackageManifest.Parse(await reader.ReadToEndAsync()).Version);
        }
    }
}
