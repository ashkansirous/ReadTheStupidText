using System.Text.RegularExpressions;
using DinoReader.Core;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Tests;

/// <summary>The storyteller characters (Slice 116, Decision 122): rosters, casting from them and the cast screen's edits.</summary>
public partial class StorytellerCharacterTests
{
    private const int MinCharacters = 16;
    private const int MaxCharacters = 20;
    private const string ChineseCode = "zh";
    private const string PacksWorkflow = ".github/workflows/packs.yml";

    private static Character C(string id, string name, Gender gender, int words) => new(id, name, gender, words);

    private static VoiceFlavour Guess(Character c) => FlavourGuess.Guess(c.Name, LanguageRules.English);

    private static IReadOnlyList<VoiceOption> KokoroPool() =>
        KokoroVoiceTable.Voices.Select(v => new VoiceOption(v.Id, KokoroVoiceTable.IsFemale(v.Id) ? Gender.Female : Gender.Male)).ToList();

    private static string PacksText()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ReadTheStupidText.slnx")))
        {
            dir = dir.Parent;
        }

        return File.ReadAllText(Path.Combine(dir!.FullName, PacksWorkflow));
    }

    // The ids a pack lists in packs.yml ("id": "piper:fa-..."): the roster is checked against the manifest the pack is built from.
    private static HashSet<string> PackVoiceIds(string prefix) =>
        PackVoiceIdRegex().Matches(PacksText()).Select(m => m.Groups[1].Value).Where(id => id.StartsWith(prefix, StringComparison.Ordinal)).ToHashSet();

    [GeneratedRegex(@"""id"":\s*""([a-z]+:[a-z0-9-]+)""")]
    private static partial Regex PackVoiceIdRegex();

    private static HashSet<string> EngineVoices(string language) => UiLanguage.Resolve(language) switch
    {
        "en" or "es" or "pt" or ChineseCode => KokoroVoiceTable.VoicesFor(UiLanguage.Resolve(language)).Select(v => v.Id).ToHashSet(),
        "nb" => NorwegianVoices.Ids.ToHashSet(),
        "fa" => PackVoiceIds("piper:fa-"),
        _ => SupertonicVoiceTable.Voices.Select(v => v.Id).ToHashSet(),
    };

    public static TheoryData<string> Languages() => new(UiLanguage.Supported);

    [Fact]
    public void Every_language_has_a_roster()
    {
        Assert.Equal(14, UiLanguage.Supported.Count);
        Assert.All(UiLanguage.Supported, code => Assert.NotEmpty(CharacterRoster.For(code)));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void A_roster_has_sixteen_to_twenty_characters_and_narrators(string language)
    {
        IReadOnlyList<StorytellerCharacter> roster = CharacterRoster.For(language);

        Assert.InRange(roster.Count, MinCharacters, MaxCharacters);
        Assert.InRange(CharacterRoster.Narrators(language).Count, 2, 3);
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_roster_voice_exists_in_its_engine_table(string language)
    {
        HashSet<string> table = EngineVoices(language);

        Assert.NotEmpty(table);
        Assert.All(CharacterRoster.For(language), c => Assert.Contains(c.VoiceId, table));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void No_two_characters_sound_the_same_or_share_an_id_or_a_name(string language)
    {
        IReadOnlyList<StorytellerCharacter> roster = CharacterRoster.For(language);

        Assert.Equal(roster.Count, roster.Select(c => (c.VoiceId, c.Flavour)).Distinct().Count());
        Assert.Equal(roster.Count, roster.Select(c => c.Id).Distinct().Count());
        Assert.Equal(roster.Count, roster.Select(c => c.Name).Distinct().Count());
        Assert.All(roster, c => Assert.False(string.IsNullOrWhiteSpace(c.Name) || string.IsNullOrWhiteSpace(c.Hint)));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Narrators_have_the_storyteller_flavour_and_speakers_never_do(string language)
    {
        Assert.All(CharacterRoster.Narrators(language), c => Assert.Equal(VoiceFlavour.Storyteller, c.Flavour));
        Assert.All(CharacterRoster.Speakers(language), c => Assert.Contains(c.Flavour, VoiceFlavours.Assignable));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void A_roster_with_known_voice_genders_has_boys_and_girls(string language)
    {
        var speakers = CharacterRoster.Speakers(language);
        if (speakers.All(c => c.Gender == Gender.Unknown))
        {
            return;
        }

        Assert.Contains(speakers, c => c.Gender == Gender.Male);
        Assert.Contains(speakers, c => c.Gender == Gender.Female);
    }

    [Fact]
    public void Roster_voice_genders_agree_with_the_engine_tables()
    {
        Assert.All(CharacterRoster.For("en"), c => Assert.Equal(KokoroVoiceTable.IsFemale(c.VoiceId) ? Gender.Female : Gender.Male, c.Gender));
        Assert.All(CharacterRoster.For("fr"), c => Assert.Equal(SupertonicVoiceTable.IsFemale(c.VoiceId) ? Gender.Female : Gender.Male, c.Gender));
        foreach (string language in new[] { "es", "pt", ChineseCode })
        {
            Assert.All(CharacterRoster.For(language), c => Assert.Equal(KokoroVoiceTable.IsFemale(c.VoiceId) ? Gender.Female : Gender.Male, c.Gender));
        }
        Assert.All(CharacterRoster.For("nb"), c => Assert.Equal(NorwegianVoices.IsFemale(c.VoiceId) == true ? Gender.Female : Gender.Male, c.Gender));
    }

    [Fact]
    public void An_unknown_language_gets_the_english_roster()
    {
        Assert.Equal(CharacterRoster.For("en"), CharacterRoster.For("xx"));
    }

    [Fact]
    public void Casting_gives_the_little_pig_the_small_boy_and_mother_pig_a_plain_girl()
    {
        var characters = new[] { C("pig", "Little Pig", Gender.Male, 40), C("mother", "Mother Pig", Gender.Female, 30) };
        var roster = CharacterRoster.For("en");
        var choices = CharacterRoster.Choices(CharacterRoster.Speakers("en"), "kokoro:af_heart");

        VoiceCast cast = CastCharacters.Cast(characters, choices, roster, "kokoro:af_heart", Guess);

        Assert.Equal("kokoro:am_puck", cast.VoiceFor("pig"));
        Assert.Equal(VoiceFlavour.Small, cast.Flavours!["pig"]);
        Assert.Equal("kokoro:bf_emma", cast.VoiceFor("mother"));
        Assert.Equal(VoiceFlavour.Normal, cast.Flavours["mother"]);
    }

    [Fact]
    public void Casting_the_pigs_in_farsi_gives_no_two_of_them_the_same_character()
    {
        var characters = new[] { C("pip", "Pip", Gender.Male, 40), C("pop", "Pop", Gender.Male, 38), C("pim", "Pim", Gender.Male, 36), C("wolf", "Wolf", Gender.Male, 30) };
        var roster = CharacterRoster.For("fa");
        string narrator = CharacterRoster.Narrators("fa")[0].VoiceId;
        var choices = CharacterRoster.Choices(CharacterRoster.Speakers("fa"), narrator);

        VoiceCast cast = CastCharacters.Cast(characters, choices, roster, narrator, Guess);

        Assert.Equal(4, cast.Assignments.Select(a => (a.VoiceId, cast.Flavours![a.CharacterId])).Distinct().Count());
    }

    [Fact]
    public void Casting_the_pigs_in_farsi_drops_automatic_pins_that_sound_alike()
    {
        var characters = new[] { C("pip", "Pip", Gender.Male, 40), C("pop", "Pop", Gender.Male, 38), C("pim", "Pim", Gender.Male, 36), C("wolf", "Wolf", Gender.Male, 30) };
        var roster = CharacterRoster.For("fa");
        string narrator = CharacterRoster.Narrators("fa")[0].VoiceId;
        var choices = CharacterRoster.Choices(CharacterRoster.Speakers("fa"), narrator);
        string ganji = "piper:fa-ganji";
        var pinned = characters.ToDictionary(c => c.Id, _ => new PinnedCharacter(ganji, VoiceFlavour.Small, Edited: false));

        VoiceCast cast = CastCharacters.Cast(characters, choices, roster, narrator, Guess, pinned);

        Assert.Equal(4, cast.Assignments.Select(a => (a.VoiceId, cast.Flavours![a.CharacterId])).Distinct().Count());
    }

    [Fact]
    public void Casting_gives_nobody_the_narrators_voice_and_shares_no_character_while_the_roster_has_another()
    {
        var characters = Enumerable.Range(1, 14).Select(i => C($"c{i}", $"Person {i}", i % 2 == 0 ? Gender.Male : Gender.Female, 20 - i)).ToList();
        string narrator = "kokoro:af_heart";
        var choices = CharacterRoster.Choices(CharacterRoster.Speakers("en"), narrator);

        VoiceCast cast = CastCharacters.Cast(characters, choices, CharacterRoster.For("en"), narrator, Guess);

        Assert.DoesNotContain(cast.Assignments, a => a.VoiceId == narrator);
        Assert.Equal(14, cast.Assignments.Select(a => (a.VoiceId, cast.Flavours![a.CharacterId])).Distinct().Count());
        Assert.All(cast.Assignments, a => Assert.False(a.IsShared));
    }

    [Fact]
    public void Casting_is_deterministic()
    {
        var characters = new[] { C("a", "Old Owl", Gender.Male, 9), C("b", "Wolf", Gender.Male, 12), C("c", "Fox", Gender.Male, 5) };
        var choices = CharacterRoster.Choices(CharacterRoster.Speakers("en"), "kokoro:af_heart");

        VoiceCast first = CastCharacters.Cast(characters, choices, CharacterRoster.For("en"), "kokoro:af_heart", Guess);
        VoiceCast again = CastCharacters.Cast(characters, choices, CharacterRoster.For("en"), "kokoro:af_heart", Guess);

        Assert.Equal(first.Assignments, again.Assignments);
        Assert.Equal(first.Flavours, again.Flavours);
    }

    [Fact]
    public void Casting_shares_a_character_only_when_the_roster_runs_out()
    {
        var characters = Enumerable.Range(1, 30).Select(i => C($"c{i}", $"Person {i}", Gender.Male, 30 - i)).ToList();
        var choices = CharacterRoster.Choices(CharacterRoster.Speakers("en"), "kokoro:af_heart");

        VoiceCast cast = CastCharacters.Cast(characters, choices, CharacterRoster.For("en"), "kokoro:af_heart", Guess);

        Assert.Equal(30, cast.Assignments.Count);
        Assert.Contains(cast.Assignments, a => a.IsShared);
    }

    [Fact]
    public void A_stored_voice_and_flavour_is_kept_and_taken_out_of_the_free_characters()
    {
        var characters = new[] { C("pig", "Little Pig", Gender.Male, 40), C("wolf", "Wolf", Gender.Male, 30) };
        var choices = CharacterRoster.Choices(CharacterRoster.Speakers("en"), "kokoro:af_heart");
        var pinned = new Dictionary<string, PinnedCharacter> { ["pig"] = new("kokoro:am_michael", VoiceFlavour.Grumpy) };

        VoiceCast cast = CastCharacters.Cast(characters, choices, CharacterRoster.For("en"), "kokoro:af_heart", Guess, pinned);

        Assert.Equal("kokoro:am_michael", cast.VoiceFor("pig"));
        Assert.Equal(VoiceFlavour.Grumpy, cast.Flavours!["pig"]);
        Assert.False(cast.Assignments.Single(a => a.CharacterId == "wolf").IsShared);
        Assert.NotEqual((cast.VoiceFor("wolf"), cast.Flavours["wolf"]), ("kokoro:am_michael", VoiceFlavour.Grumpy));
    }

    [Fact]
    public void A_stored_voice_the_pool_no_longer_has_is_cast_again()
    {
        var characters = new[] { C("pig", "Little Pig", Gender.Male, 40) };
        var choices = CharacterRoster.Choices(CharacterRoster.Speakers("en"), "kokoro:af_heart");
        var pinned = new Dictionary<string, PinnedCharacter> { ["pig"] = new("supertonic:M2", null) };

        VoiceCast cast = CastCharacters.Cast(characters, choices, CharacterRoster.For("en"), "kokoro:af_heart", Guess, pinned);

        Assert.StartsWith(KokoroVoiceTable.IdPrefix, cast.VoiceFor("pig"), StringComparison.Ordinal);
    }

    [Fact]
    public void A_single_voice_language_still_casts_through_flavours()
    {
        // Chinese before the storyteller pack lands: the one Melo voice speaks every character.
        var melo = new[] { new VoiceOption(CharacterRoster.ChineseVoiceId, Gender.Unknown) };
        var roster = CharacterRoster.Adapt(CharacterRoster.For(ChineseCode), melo);
        string narrator = CharacterRoster.ChineseVoiceId;
        var choices = CharacterRoster.Choices(roster.Where(c => c.Kind == CharacterKind.Speaker).ToList(), narrator);
        var characters = new[] { C("a", "小兔子", Gender.Unknown, 5), C("b", "小老鼠", Gender.Unknown, 4) };

        VoiceCast cast = CastCharacters.Cast(characters, choices, roster, narrator, c => FlavourGuess.Guess(c.Name, LanguageRules.For(ChineseCode)));

        Assert.Equal(VoiceFlavour.Small, cast.Flavours!["a"]);
        Assert.Equal(VoiceFlavour.Squeaky, cast.Flavours["b"]);
    }

    [Fact]
    public void Choices_drop_the_narrators_voice_unless_it_is_the_only_one()
    {
        var speakers = CharacterRoster.Speakers("fa");
        string narrator = "piper:fa-ganji-adabi";

        Assert.DoesNotContain(CharacterRoster.Choices(speakers, narrator), c => c.VoiceId == narrator);
        var melo = new[] { new VoiceOption(CharacterRoster.ChineseVoiceId, Gender.Unknown) };
        var chinese = CharacterRoster.Adapt(CharacterRoster.For(ChineseCode), melo).Where(c => c.Kind == CharacterKind.Speaker).ToList();
        Assert.Equal(chinese, CharacterRoster.Choices(chinese, CharacterRoster.ChineseVoiceId));
    }

    [Fact]
    public void Choices_keep_the_girls_in_the_only_persian_womans_voice_when_she_narrates()
    {
        // Decided on purpose (Slice 118): Mana is Persian's one woman's voice, so when she narrates the female characters keep her (a flavour apart) rather than vanish.
        string mana = "piper:fa-mana";
        var speakers = CharacterRoster.Speakers("fa");

        var choices = CharacterRoster.Choices(speakers, mana);

        Assert.Equal(speakers.Where(c => c.Gender == Gender.Female).Select(c => c.Id), choices.Where(c => c.VoiceId == mana).Select(c => c.Id));
        Assert.Contains(choices, c => c.Gender == Gender.Male);
    }

    [Theory]
    [InlineData("es", "kokoro:ef_dora")]
    [InlineData("pt", "kokoro:pf_dora")]
    public void Choices_keep_the_only_womans_voice_for_the_girls_when_she_narrates(string language, string dora)
    {
        var speakers = CharacterRoster.Speakers(language);

        var choices = CharacterRoster.Choices(speakers, dora);

        Assert.Equal(speakers.Where(c => c.Gender == Gender.Female).Select(c => c.Id), choices.Where(c => c.VoiceId == dora).Select(c => c.Id));
        Assert.Contains(choices, c => c.Gender == Gender.Male);
    }

    [Theory]
    [InlineData("es", 3)]
    [InlineData("pt", 3)]
    [InlineData(ChineseCode, 8)]
    public void The_kokoro_languages_use_every_voice_of_their_table(string language, int voices)
    {
        Assert.Equal(voices, KokoroVoiceTable.VoicesFor(language).Count);
        Assert.Equal(voices, CharacterRoster.For(language).Select(c => c.VoiceId).Distinct().Count());
    }

    [Fact]
    public void Spanish_and_portuguese_before_the_storyteller_pack_map_onto_the_supertonic_voices()
    {
        var supertonic = SupertonicVoiceTable.VoicesFor("es")
            .Select(v => new VoiceOption(v.Id, SupertonicVoiceTable.IsFemale(v.Id) ? Gender.Female : Gender.Male)).ToList();

        IReadOnlyList<StorytellerCharacter> adapted = CharacterRoster.Adapt(CharacterRoster.For("es"), supertonic);

        Assert.NotEmpty(adapted);
        Assert.All(adapted, c => Assert.StartsWith(SupertonicVoiceTable.IdPrefix, c.VoiceId, StringComparison.Ordinal));
    }

    [Fact]
    public void English_before_the_storyteller_pack_is_mapped_onto_the_supertonic_voices()
    {
        var supertonic = SupertonicVoiceTable.Voices
            .Select(v => new VoiceOption(v.Id, SupertonicVoiceTable.IsFemale(v.Id) ? Gender.Female : Gender.Male)).ToList();

        IReadOnlyList<StorytellerCharacter> adapted = CharacterRoster.Adapt(CharacterRoster.For("en"), supertonic);

        Assert.NotEmpty(adapted);
        Assert.All(adapted, c => Assert.StartsWith(SupertonicVoiceTable.IdPrefix, c.VoiceId, StringComparison.Ordinal));
        Assert.Equal(adapted.Count, adapted.Select(c => (c.VoiceId, c.Flavour)).Distinct().Count());
        Assert.All(adapted, c => Assert.Equal(SupertonicVoiceTable.IsFemale(c.VoiceId) ? Gender.Female : Gender.Male, c.Gender));
    }

    [Fact]
    public void Adapt_keeps_the_roster_as_is_when_the_pool_has_its_voices_and_gives_nothing_for_an_empty_pool()
    {
        Assert.Equal(CharacterRoster.For("en"), CharacterRoster.Adapt(CharacterRoster.For("en"), KokoroPool()));
        Assert.Empty(CharacterRoster.Adapt(CharacterRoster.For("fa"), []));
    }

    [Fact]
    public void Nearest_finds_the_exact_character_then_the_same_voice_then_the_closest_flavour()
    {
        var roster = CharacterRoster.For("en");

        Assert.Equal("Cheeky Piglet", CharacterRoster.Nearest(roster, "kokoro:am_puck", VoiceFlavour.Small)!.Name);
        Assert.Equal("Squeaky Mouse", CharacterRoster.Nearest(roster, "kokoro:am_puck", VoiceFlavour.Squeaky)!.Name);
        Assert.Equal("kokoro:am_puck", CharacterRoster.Nearest(roster, "kokoro:am_puck", VoiceFlavour.Sleepy)!.VoiceId);
        Assert.Null(CharacterRoster.Nearest([], "x", VoiceFlavour.Normal));
    }

    [Fact]
    public void A_cast_sheet_row_is_named_by_a_speaker_never_a_narrator()
    {
        var roster = CharacterRoster.For("en");
        var characters = new[] { C("a", "A", Gender.Male, 9) };
        string narratorVoice = roster.First(c => c.Kind == CharacterKind.Narrator).VoiceId;
        var cast = new[] { new CastMember("a", "A", Gender.Unknown, narratorVoice, false, VoiceFlavour.Storyteller) };

        CastSheet sheet = CastSheet.Build(new AttributionResult([], characters), cast, 10, LanguageRules.For("en"), roster: roster);

        Assert.Equal(CharacterKind.Speaker, sheet.Rows.Single().Character!.Kind);
    }

    [Fact]
    public void The_persian_roster_has_girls_and_boys_by_voice()
    {
        var speakers = CharacterRoster.Speakers("fa");

        Assert.All(speakers.Where(c => c.VoiceId.EndsWith("fa-mana")), c => Assert.Equal(Gender.Female, c.Gender));
        Assert.All(speakers.Where(c => c.VoiceId.EndsWith("fa-gyro")), c => Assert.Equal(Gender.Male, c.Gender));
    }

    [Fact]
    public void Setting_a_character_pins_its_voice_and_flavour_together()
    {
        var state = new CastEdit([new CastMember("wolf", "The Wolf", Gender.Male, "kokoro:am_liam", false, VoiceFlavour.Big)], new Dictionary<string, string>());
        StorytellerCharacter troll = CharacterRoster.Speakers("en").Single(c => c.Id == "troll");

        CastMember member = CastEditor.SetCharacter(state, "wolf", troll).Cast.Single();

        Assert.Equal((troll.VoiceId, troll.Flavour, true), (member.VoiceId, member.Flavour, member.Edited));
    }

    [Fact]
    public void A_narrator_cannot_be_given_to_a_story_character()
    {
        var state = new CastEdit([new CastMember("wolf", "The Wolf", Gender.Male, "kokoro:am_liam")], new Dictionary<string, string>());

        Assert.Same(state, CastEditor.SetCharacter(state, "wolf", CharacterRoster.Narrators("en")[0]));
    }

    [Fact]
    public void The_cast_screen_counts_two_characters_as_sharing_only_when_they_sound_the_same()
    {
        var characters = new[] { C("a", "A", Gender.Male, 9), C("b", "B", Gender.Male, 8), C("c", "C", Gender.Male, 7) };
        var cast = new[]
        {
            new CastMember("a", "A", Gender.Male, "kokoro:am_puck", true, VoiceFlavour.Small),
            new CastMember("b", "B", Gender.Male, "kokoro:am_puck", true, VoiceFlavour.Squeaky),
            new CastMember("c", "C", Gender.Male, "kokoro:am_puck", true, VoiceFlavour.Small),
        };
        var resolved = new AttributionResult([], characters);

        CastSheet sheet = CastSheet.Build(resolved, cast, voiceCount: 1, LanguageRules.English, roster: CharacterRoster.For("en"));

        Assert.Equal([false, false, true], sheet.Rows.Select(r => r.SharesVoice));
        Assert.Equal("Cheeky Piglet", sheet.Rows[0].Character!.Name);
        Assert.Equal("Squeaky Mouse", sheet.Rows[1].Character!.Name);
    }

    [Theory]
    [InlineData("Crafty Witch", VoiceFlavour.Grumpy)]
    [InlineData("Sly Fox", VoiceFlavour.Whisper)]
    [InlineData("Tiny Fairy", VoiceFlavour.Small)]
    [InlineData("Fairy", VoiceFlavour.Squeaky)]
    [InlineData("Grumpy Troll", VoiceFlavour.Big)]
    [InlineData("Peter", VoiceFlavour.Normal)]
    public void The_new_flavours_are_guessed_after_the_size_and_age_words(string name, VoiceFlavour expected)
    {
        Assert.Equal(expected, FlavourGuess.Guess(name, LanguageRules.English));
    }

    [Theory]
    [InlineData("fr", "Sorcière", VoiceFlavour.Grumpy)]
    [InlineData("es", "Zorro", VoiceFlavour.Whisper)]
    [InlineData("ru", "Фея", VoiceFlavour.Squeaky)]
    [InlineData("fa", "جادوگر", VoiceFlavour.Grumpy)]
    [InlineData("ja", "きつね", VoiceFlavour.Whisper)]
    [InlineData("zh", "仙女", VoiceFlavour.Squeaky)]
    public void The_new_flavours_have_words_in_the_other_languages_too(string language, string name, VoiceFlavour expected)
    {
        Assert.Equal(expected, FlavourGuess.Guess(name, LanguageRules.For(language)));
    }

    [Theory]
    [InlineData("Squeaky")]
    [InlineData("Grumpy")]
    [InlineData("Whisper")]
    [InlineData("Giant")]
    public void A_new_flavour_survives_a_story_file(string name)
    {
        var flavour = Enum.Parse<VoiceFlavour>(name);
        var member = new CastMember("pig", "Little Pig", Gender.Male, "kokoro:am_puck", true, flavour);
        var story = new Story(
            StoryIds.New(), "Title", "en", DateTimeOffset.UnixEpoch, [new StoryPage("Text")], [member], CoverStyle.Colour, 0, "M1", null, null);

        Story back = StoryJson.Deserialize(StoryJson.Serialize(story));

        Assert.Equal(flavour, back.Cast.Single().Flavour);
    }

    [Fact]
    public void The_new_flavours_stay_within_four_semitones()
    {
        Assert.All(VoiceFlavours.Assignable, f => Assert.InRange(Math.Abs(VoiceFlavours.ShapeOf(f).Semitones), 0, 4));
        Assert.DoesNotContain(VoiceFlavour.Storyteller, VoiceFlavours.Assignable);
    }
}
