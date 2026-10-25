using DinoReader.Core;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Tests;

public class FlavourGuessTests
{
    private static VoiceFlavour GuessEnglish(string name) => FlavourGuess.Guess(name, LanguageRules.English);

    [Theory]
    [InlineData("Little Pig", VoiceFlavour.Small)]
    [InlineData("The Wolf", VoiceFlavour.Big)]
    [InlineData("Mother Pig", VoiceFlavour.Normal)]
    [InlineData("Baby Bear", VoiceFlavour.Small)]
    [InlineData("Big Bear", VoiceFlavour.Big)]
    [InlineData("the lamb", VoiceFlavour.Small)]
    [InlineData("Mouse", VoiceFlavour.Small)]
    [InlineData("Rabbit", VoiceFlavour.Small)]
    [InlineData("Elephant", VoiceFlavour.Big)]
    [InlineData("The Giant", VoiceFlavour.Big)]
    [InlineData("Old Owl", VoiceFlavour.Sleepy)]
    [InlineData("Grandad", VoiceFlavour.Sleepy)]
    [InlineData("Grandpa Bear", VoiceFlavour.Sleepy)]
    [InlineData("Little Wolf", VoiceFlavour.Small)]
    [InlineData("Big Owl", VoiceFlavour.Big)]
    [InlineData("Peter", VoiceFlavour.Normal)]
    [InlineData("", VoiceFlavour.Normal)]
    public void English_names_are_guessed_as_in_the_mock(string name, VoiceFlavour expected)
    {
        Assert.Equal(expected, GuessEnglish(name));
    }

    [Theory]
    [InlineData("ja", "アリス")]
    [InlineData("ja", "クリス")]
    [InlineData("ja", "おにいさん")]
    [InlineData("ja", "おにいちゃん")]
    [InlineData("ja", "竜太")]
    [InlineData("zh", "小明")]
    public void Unspaced_names_never_match_a_part_in_the_middle(string language, string name)
    {
        Assert.Equal(VoiceFlavour.Normal, FlavourGuess.Guess(name, LanguageRules.For(language)));
    }

    [Fact]
    public void A_word_inside_another_word_does_not_count()
    {
        Assert.Equal(VoiceFlavour.Normal, GuessEnglish("Bearnard"));
    }

    [Theory]
    [InlineData("fr", "Le Loup", VoiceFlavour.Big)]
    [InlineData("fr", "Petit Cochon", VoiceFlavour.Small)]
    [InlineData("es", "El Lobo", VoiceFlavour.Big)]
    [InlineData("es", "Cerdito pequeño", VoiceFlavour.Small)]
    [InlineData("pt", "O Lobo", VoiceFlavour.Big)]
    [InlineData("pt", "O Coelho", VoiceFlavour.Small)]
    [InlineData("ru", "Волк", VoiceFlavour.Big)]
    [InlineData("ru", "Мышонок", VoiceFlavour.Small)]
    [InlineData("fa", "گرگ", VoiceFlavour.Big)]
    [InlineData("fa", "خرگوش کوچولو", VoiceFlavour.Small)]
    [InlineData("fa", "جغد", VoiceFlavour.Sleepy)]
    [InlineData("ar", "الذئب", VoiceFlavour.Big)]
    [InlineData("ar", "أرنب", VoiceFlavour.Small)]
    [InlineData("zh", "大灰狼", VoiceFlavour.Big)]
    [InlineData("zh", "小兔子", VoiceFlavour.Small)]
    [InlineData("zh", "猫头鹰", VoiceFlavour.Sleepy)]
    [InlineData("ja", "オオカミ", VoiceFlavour.Big)]
    [InlineData("ja", "うさぎ", VoiceFlavour.Small)]
    [InlineData("ja", "小さなうさぎ", VoiceFlavour.Small)]
    [InlineData("fa", "پدر بزرگ", VoiceFlavour.Sleepy)]
    [InlineData("da", "Ulv", VoiceFlavour.Big)]
    [InlineData("da", "Lille Gris", VoiceFlavour.Small)]
    [InlineData("sv", "Varg", VoiceFlavour.Big)]
    [InlineData("nb", "Ugle", VoiceFlavour.Sleepy)]
    [InlineData("fi", "Karhu", VoiceFlavour.Big)]
    [InlineData("fi", "Pieni Hiiri", VoiceFlavour.Small)]
    [InlineData("nl", "De Wolf", VoiceFlavour.Big)]
    [InlineData("nl", "Het Konijntje", VoiceFlavour.Small)]
    public void Every_language_has_its_own_word_lists(string language, string name, VoiceFlavour expected)
    {
        Assert.Equal(expected, FlavourGuess.Guess(name, LanguageRules.For(language)));
    }

    [Fact]
    public void Every_app_language_lists_words_for_all_three_flavours()
    {
        foreach (string code in RuleSets.Codes)
        {
            FlavourWords words = LanguageRules.For(code).FlavourWordLists;
            Assert.True(words.Small.Count > 0 && words.Big.Count > 0 && words.Sleepy.Count > 0 && words.Diminutives.Count > 0, code);
        }
    }

    [Fact]
    public void A_stored_flavour_wins_over_the_guess()
    {
        var member = new CastMember("pig", "Little Pig", Gender.Male, "M1", true, VoiceFlavour.Big);

        Assert.Equal(VoiceFlavour.Big, FlavourGuess.Effective(member, LanguageRules.English));
    }

    [Fact]
    public void A_stored_flavour_of_an_unedited_member_still_stands_over_the_guess()
    {
        var member = new CastMember("pig", "Little Pig", Gender.Male, "M1", false, VoiceFlavour.Big);

        Assert.Equal(VoiceFlavour.Big, FlavourGuess.Effective(member, LanguageRules.English));
    }

    [Fact]
    public void A_member_saved_before_flavours_is_guessed_from_its_name()
    {
        var member = new CastMember("pig", "Little Pig", Gender.Male, "M1");

        Assert.Equal(VoiceFlavour.Small, FlavourGuess.Effective(member, LanguageRules.English));
    }

    [Fact]
    public void An_unedited_member_gets_a_new_guess_when_the_cast_is_rebuilt()
    {
        Character[] pig = [new("pig", "Little Pig", Gender.Male, 40)];
        var stored = new[] { new CastMember("pig", "Little Pig", Gender.Male, "M1", false, VoiceFlavour.Big) };
        VoiceCast cast = new VoiceCaster().Cast(pig, [new VoiceOption("M1", Gender.Male), new VoiceOption("N", Gender.Male)], "N");

        var members = StoryCast.ToMembers(pig, cast, LanguageRules.English, stored);

        Assert.Equal(VoiceFlavour.Small, members.Single().Flavour);
    }
}

public class FlavourCastTests
{
    private static readonly Character[] Characters =
    [
        new("pig", "Little Pig", Gender.Male, 40),
        new("wolf", "The Wolf", Gender.Male, 60),
        new("mum", "Mother Pig", Gender.Female, 10),
    ];

    private static readonly VoiceOption[] Voices =
        [new("M1", Gender.Male), new("M2", Gender.Male), new("F1", Gender.Female), new("N", Gender.Male)];

    private static VoiceCast Cast() => new VoiceCaster().Cast(Characters, Voices, "N")
        .WithFlavours(FlavourGuess.ForCast(Characters, [], LanguageRules.English));

    [Fact]
    public void ToMembers_stores_the_guess_for_each_character()
    {
        var members = StoryCast.ToMembers(Characters, Cast(), LanguageRules.English);

        Assert.Equal(VoiceFlavour.Small, members.Single(m => m.CharacterId == "pig").Flavour);
        Assert.Equal(VoiceFlavour.Big, members.Single(m => m.CharacterId == "wolf").Flavour);
        Assert.Equal(VoiceFlavour.Normal, members.Single(m => m.CharacterId == "mum").Flavour);
    }

    [Fact]
    public void ToMembers_keeps_an_edited_pick_when_the_cast_is_rebuilt()
    {
        var stored = new[] { new CastMember("pig", "Little Pig", Gender.Male, "M1", true, VoiceFlavour.Sleepy) };

        var members = StoryCast.ToMembers(Characters, Cast(), LanguageRules.English, stored);

        Assert.Equal(VoiceFlavour.Sleepy, members.Single(m => m.CharacterId == "pig").Flavour);
    }

    [Fact]
    public void FlavourForLine_gives_narration_and_unknown_speakers_the_storyteller_pace()
    {
        VoiceCast cast = Cast();

        Assert.Equal(VoiceFlavour.Storyteller, cast.FlavourForLine(null));
        Assert.Equal(VoiceFlavour.Storyteller, cast.FlavourForLine("nobody"));
        Assert.Equal(VoiceFlavour.Big, cast.FlavourForLine("wolf"));
    }

    [Fact]
    public void FlavourForLine_is_normal_for_a_cast_made_without_flavours()
    {
        VoiceCast cast = new VoiceCaster().Cast(Characters, Voices, "N");

        Assert.Equal(VoiceFlavour.Normal, cast.FlavourForLine("wolf"));
    }

    [Fact]
    public void The_cast_sheet_shows_the_effective_flavour_of_each_row()
    {
        var attribution = new AttributionResult([], Characters);
        var cast = StoryCast.ToMembers(Characters, Cast(), LanguageRules.English);
        cast = cast.Select(m => m.CharacterId == "wolf" ? m with { Edited = true, Flavour = VoiceFlavour.Sleepy } : m).ToList();

        CastSheet sheet = CastSheet.Build(attribution, cast, 3, LanguageRules.English);

        Assert.Equal(VoiceFlavour.Small, sheet.Rows.Single(r => r.Member.CharacterId == "pig").Flavour);
        Assert.Equal(VoiceFlavour.Sleepy, sheet.Rows.Single(r => r.Member.CharacterId == "wolf").Flavour);
        Assert.Equal(VoiceFlavour.Normal, sheet.Rows.Single(r => r.Member.CharacterId == "mum").Flavour);
    }

    private static Story Sample(CastMember member) => new(
        StoryIds.New(), "Title", "en", DateTimeOffset.UnixEpoch, [new StoryPage("Text")], [member], CoverStyle.Colour, 0, "M1", null, null);

    [Fact]
    public void A_flavour_survives_a_round_trip_through_story_json()
    {
        var member = new CastMember("pig", "Little Pig", Gender.Male, "M1", true, VoiceFlavour.Bouncy);

        Story back = StoryJson.Deserialize(StoryJson.Serialize(Sample(member)));

        Assert.Equal(VoiceFlavour.Bouncy, back.Cast.Single().Flavour);
    }

    [Fact]
    public void A_story_without_a_flavour_writes_no_field_and_reads_as_none()
    {
        string json = StoryJson.Serialize(Sample(new CastMember("pig", "Pig", Gender.Male, "M1")));

        Assert.DoesNotContain("\"Flavour\"", json);
        Assert.Null(StoryJson.Deserialize(json).Cast.Single().Flavour);
    }

    [Theory]
    [InlineData("Storyteller")]
    [InlineData("Whispering")]
    [InlineData("42")]
    public void A_flavour_this_build_does_not_know_reads_as_none(string name)
    {
        string json = StoryJson.Serialize(Sample(new CastMember("pig", "Pig", Gender.Male, "M1", true, VoiceFlavour.Big)))
            .Replace("\"Big\"", $"\"{name}\"");

        Assert.Null(StoryJson.Deserialize(json).Cast.Single().Flavour);
    }
}

public class FlavourReviewTests
{
    [Fact]
    public void ForCast_survives_duplicate_character_ids()
    {
        Character[] twins = [new("pig", "Little Pig", Gender.Male, 4), new("pig", "Little Pig", Gender.Male, 2)];

        var flavours = FlavourGuess.ForCast(twins, [], LanguageRules.English);

        Assert.Equal(VoiceFlavour.Small, flavours["pig"]);
    }
}

public class StoredFarsiPigsTests
{
    // The real stored Farsi cast of The Three Little Pigs on an installed device (not edited, flavours the cast gave).
    private static readonly CastMember[] Stored =
    [
        new("Pip", "Pip", Gender.Male, "piper:fa-ganji", false, VoiceFlavour.Small),
        new("Pop", "Pop", Gender.Male, "piper:fa-ganji", false, VoiceFlavour.Squeaky),
        new("Pim", "Pim", Gender.Male, "piper:fa-gyro", false, VoiceFlavour.Normal),
        new("Wolf", "Wolf", Gender.Male, "piper:fa-gyro", false, VoiceFlavour.Grumpy),
    ];

    [Fact]
    public void An_installed_farsi_cast_names_four_different_characters()
    {
        LanguageRules rules = LanguageRules.For("fa");
        var speakers = CharacterRoster.Speakers("fa");

        var names = Stored.Select(m => CharacterRoster.Nearest(speakers, m.VoiceId, FlavourGuess.Effective(m, rules), m.Gender)!.Name).ToList();

        Assert.Equal(4, names.Distinct().Count());
        Assert.Equal("خرگوش شیطون", names[0]);
        Assert.Equal("موش کوچولو", names[1]);
    }
}
