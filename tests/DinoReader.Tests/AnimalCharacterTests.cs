using DinoReader.Core;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using Xunit;

namespace DinoReader.Tests;

/// <summary>Each cast character knows its animal, and animal voices to match (Slice 125, Decision 132).</summary>
public class AnimalCharacterTests
{
    private static readonly string[] NewAnimalIds = ["lamb", "puppy", "kitten", "duckling", "cub", "bigbear", "wolf", "ewe"];

    private static Character C(string id, string name, Gender gender = Gender.Male) => new(id, name, gender, 10);

    private static Story StoryWith(params CastMember[] cast) =>
        Story.NewOwnBook(Guid.NewGuid().ToString("N"), "en", DateTimeOffset.UnixEpoch, 0) with { Cast = cast };

    public static TheoryData<string> Languages() => new(UiLanguage.Supported);

    // ---- the stored animal ----

    [Fact]
    public void The_animal_is_saved_with_the_cast_and_none_is_its_own_value()
    {
        Story story = StoryWith(
            new CastMember("pip", "Pip", Gender.Male, "M1", Animal: AnimalKind.Pig),
            new CastMember("wolf", "Wolf", Gender.Male, "M2", NotAnimal: true),
            new CastMember("pop", "Pop", Gender.Male, "M3"));

        string json = StoryJson.Serialize(story);
        Story back = StoryJson.Deserialize(json);

        Assert.Contains("\"Animal\": \"Pig\"", json.Replace("\r", string.Empty));
        Assert.Contains("\"Animal\": \"none\"", json.Replace("\r", string.Empty));
        Assert.Equal(AnimalKind.Pig, back.Cast[0].Animal);
        Assert.True(back.Cast[1].NotAnimal);
        Assert.Null(back.Cast[1].Animal);
        Assert.False(back.Cast[2].HasAnimalSetting);
        Assert.DoesNotContain("\"Animal\":", StoryJson.Serialize(StoryWith(new CastMember("pop", "Pop", Gender.Male, "M3"))));
    }

    [Fact]
    public void An_animal_this_build_does_not_know_reads_as_absent()
    {
        string json = StoryJson.Serialize(StoryWith(new CastMember("pip", "Pip", Gender.Male, "M1", Animal: AnimalKind.Pig))).Replace("\"Pig\"", "\"Unicorn\"");

        CastMember member = StoryJson.Deserialize(json).Cast.Single();

        Assert.False(member.HasAnimalSetting);
    }

    [Fact]
    public void A_recast_keeps_the_stored_animal_and_the_builders_animals_fill_the_rest()
    {
        var characters = new[] { C("pip", "Pip"), C("pop", "Pop"), C("jo", "Jo") };
        var cast = new VoiceCaster().Cast(characters, [new VoiceOption("M1", Gender.Male), new VoiceOption("M2", Gender.Male), new VoiceOption("M3", Gender.Male)], "N");
        var stored = new[] { new CastMember("pip", "Pip", Gender.Male, "M1", Animal: AnimalKind.Pig), new CastMember("jo", "Jo", Gender.Male, "M2", NotAnimal: true) };

        IReadOnlyList<CastMember> members = StoryCast.ToMembers(
            characters, cast, LanguageRules.English, stored, new Dictionary<string, AnimalKind> { ["pop"] = AnimalKind.Goat });

        Assert.Equal(AnimalKind.Pig, members.Single(m => m.CharacterId == "pip").Animal);
        Assert.Equal(AnimalKind.Goat, members.Single(m => m.CharacterId == "pop").Animal);
        Assert.True(members.Single(m => m.CharacterId == "jo").NotAnimal);
    }

    [Fact]
    public void Setting_the_animal_changes_the_main_cast_and_every_language_cast()
    {
        Story story = StoryWith(new CastMember("pip", "Pip", Gender.Male, "M1")) with
        {
            LanguageCasts = new Dictionary<string, IReadOnlyList<CastMember>> { ["fa"] = [new CastMember("pip", "Pip", Gender.Male, "F1", Edited: true)] },
        };

        Story pig = CastEditor.WithAnimal(story, "pip", AnimalKind.Pig);
        Story none = CastEditor.WithAnimal(pig, "pip", null);

        Assert.Equal(AnimalKind.Pig, pig.Cast.Single().Animal);
        Assert.Equal(AnimalKind.Pig, pig.LanguageCasts!["fa"].Single().Animal);
        Assert.True(pig.LanguageCasts["fa"].Single().Edited);
        Assert.True(none.Cast.Single().NotAnimal);
        Assert.Null(none.Cast.Single().Animal);
        Assert.True(none.LanguageCasts!["fa"].Single().NotAnimal);
    }

    // ---- which animal a character is ----

    [Fact]
    public void The_stored_animal_wins_over_the_name_and_none_wins_over_both()
    {
        var characters = new[] { C("tom", "Tom the Cat"), C("pip", "Pip"), C("wolf", "Wolf") };
        var cast = new[]
        {
            new CastMember("tom", "Tom the Cat", Gender.Male, "M1", Animal: AnimalKind.Dog),
            new CastMember("wolf", "Wolf", Gender.Male, "M2", NotAnimal: true),
        };

        var animals = AnimalSounds.Resolve(characters, "en", cast, "Pip is nice.");

        Assert.Equal(AnimalKind.Dog, animals["tom"]);
        Assert.False(animals.ContainsKey("wolf"));
        Assert.False(animals.ContainsKey("pip"));
    }

    [Fact]
    public void An_animal_with_no_sound_is_an_animal_but_makes_none()
    {
        var characters = new[] { C("peter", "Peter") };
        var cast = new[] { new CastMember("peter", "Peter", Gender.Male, "M1", Animal: AnimalKind.Rabbit) };

        Assert.Equal(AnimalKind.Rabbit, AnimalSounds.Resolve(characters, "en", cast)["peter"]);
        Assert.Empty(AnimalSounds.AnimalsIn(characters, "en", cast));
        Assert.Null(AnimalSounds.SoundOf(AnimalKind.Rabbit, "en"));
        Assert.Equal("Grr!", AnimalSounds.SoundOf(AnimalKind.Bear, "en"));
    }

    [Fact]
    public void The_pigs_starter_names_its_pigs_so_they_oink_and_the_wolf_comes_from_his_name()
    {
        Story story = StarterBuilder.Build(StarterTexts.PigsId, "The Three Little Pigs", StarterTexts.Pigs, 3);
        var characters = story.Cast.Select(m => C(m.CharacterId, m.Name)).ToList();

        var animals = AnimalSounds.AnimalsIn(characters, "en", story.Cast);

        Assert.Equal(AnimalKind.Pig, animals["Pip"]);
        Assert.Equal(AnimalKind.Pig, animals["Pop"]);
        Assert.Equal(AnimalKind.Pig, animals["Pim"]);
        Assert.Equal(AnimalKind.Wolf, animals["Wolf"]);
    }

    [Fact]
    public void The_story_text_alone_finds_the_pigs_in_the_pigs_starter()
    {
        Story story = StarterBuilder.Build(StarterTexts.PigsId, "The Three Little Pigs", StarterTexts.Pigs, 3);
        var characters = story.Cast.Select(m => C(m.CharacterId, m.Name)).ToList();

        var animals = AnimalSounds.Resolve(characters, "en", [], story.Text);

        Assert.Equal([AnimalKind.Pig, AnimalKind.Pig, AnimalKind.Pig, AnimalKind.Wolf], new[] { "Pip", "Pop", "Pim", "Wolf" }.Select(n => animals[n]));
    }

    // ---- the guess from the text ----

    [Theory]
    [InlineData("Pip the pig lived by the sea.", AnimalKind.Pig)]
    [InlineData("There was a pig called Pip.", AnimalKind.Pig)]
    [InlineData("Here is the smallest pig, Pip.", AnimalKind.Pig)]
    [InlineData("Pip, the little lamb, jumped.", AnimalKind.Sheep)]
    [InlineData("A duck named Pip swam by.", AnimalKind.Duck)]
    public void An_animal_word_next_to_the_name_is_the_guess(string text, AnimalKind kind) =>
        Assert.Equal(kind, AnimalGuess.Guess([C("pip", "Pip")], text, "en")["pip"]);

    [Fact]
    public void A_group_introduction_gives_the_animal_to_every_name_in_it()
    {
        var characters = new[] { C("pip", "Pip"), C("pop", "Pop"), C("pim", "Pim") };

        var found = AnimalGuess.Guess(characters, "Three little pigs lived in a wood. Their names were Pip, Pop and Pim.", "en");

        Assert.All(characters, c => Assert.Equal(AnimalKind.Pig, found[c.Id]));
    }

    [Theory]
    [InlineData("Pip walked to the farm and saw a pig.")]
    [InlineData("Pip said hello. The pig was asleep in the straw.")]
    [InlineData("A pig lived there. Pip and Pop met in the road.")]
    [InlineData("Pip met the nice and friendly old pig at the gate.")]
    public void An_animal_somewhere_else_in_the_text_is_not_a_guess(string text)
    {
        var characters = new[] { C("pip", "Pip"), C("pop", "Pop") };

        Assert.Empty(AnimalGuess.Guess(characters, text, "en"));
    }

    [Theory]
    [InlineData("fa", "پیپ، خوک کوچولو، به خانه رفت.", "پیپ")]
    [InlineData("es", "Pip el cerdito vivía allí.", "Pip")]
    [InlineData("fr", "Pip le cochon vivait là.", "Pip")]
    [InlineData("ru", "Пип, маленькая свинка, жил там.", "Пип")]
    [InlineData("zh", "皮普是一只小猪。", "皮普")]
    [InlineData("ja", "ピップは ぶたでした。", "ピップ")]
    [InlineData("sv", "Pip grisen bodde där.", "Pip")]
    public void The_guess_reads_each_languages_animal_words(string language, string text, string name)
    {
        var found = AnimalGuess.Guess([C("c", name)], text, language);

        Assert.Equal(AnimalKind.Pig, found["c"]);
    }

    [Fact]
    public void Every_store_and_starter_text_guess_is_gentle_and_never_gives_a_person_an_animal()
    {
        foreach (StoreStorySpec spec in StoreTexts.All)
        {
            Story story = StarterBuilder.Build(spec.Id, spec.Title, spec.Text, spec.ColourIndex);
            var people = story.Cast.Where(m => m.Name is "Jack" or "Goldilocks" or "Hans" or "Greta" or "Red Riding Hood").Select(m => C(m.CharacterId, m.Name)).ToList();

            Assert.Empty(AnimalGuess.Guess(people, story.Text, "en"));
        }
    }

    // ---- the animal characters of the rosters ----

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_roster_has_the_new_animal_characters_with_a_name_and_words(string language)
    {
        var roster = CharacterRoster.For(language);

        foreach (string id in NewAnimalIds)
        {
            StorytellerCharacter character = roster.Single(c => c.Id == id);
            Assert.Equal(CharacterKind.Speaker, character.Kind);
            Assert.False(string.IsNullOrWhiteSpace(character.Name));
            Assert.False(string.IsNullOrWhiteSpace(character.Hint));
            Assert.NotNull(AnimalCharacters.Of(id));
        }
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Young_animals_sound_young_and_grown_ones_sound_grown(string language)
    {
        var roster = CharacterRoster.For(language);
        VoiceFlavour[] young = [VoiceFlavour.Small, VoiceFlavour.Squeaky, VoiceFlavour.Bouncy];
        VoiceFlavour[] grown = [VoiceFlavour.Big, VoiceFlavour.Giant, VoiceFlavour.Grumpy, VoiceFlavour.Sleepy];

        foreach (string id in new[] { "lamb", "puppy", "kitten", "duckling", "cub" })
        {
            // Spanish and Portuguese have too few free young places on their three voices: the cub's deep voice stays Normal.
            VoiceFlavour[] allowed = language is "es" or "pt" && id == "cub" ? young.Append(VoiceFlavour.Normal).ToArray() : young;
            Assert.Contains(roster.Single(c => c.Id == id).Flavour, allowed);
        }

        foreach (string id in new[] { "bigbear", "wolf", "ewe" })
        {
            Assert.Contains(roster.Single(c => c.Id == id).Flavour, grown);
        }
    }

    private static (StorytellerCharacter Pick, IReadOnlyList<StorytellerCharacter> Roster) CastOne(string language, Character character, AnimalHint? hint, string storyLanguage = "en")
    {
        var roster = CharacterRoster.For(language);
        // A narrator that does not use the piglet's voice (a character on the narrator's own voice is not offered).
        string narrator = CharacterRoster.Narrators(language).First(n => n.VoiceId != roster.Single(c => c.Id == "piglet").VoiceId).VoiceId;
        var choices = CharacterRoster.Choices(CharacterRoster.Speakers(language), narrator);
        VoiceCast cast = CastCharacters.Cast(
            [character], choices, roster, narrator, c => FlavourGuess.Guess(c.Name, LanguageRules.For(storyLanguage)), animalOf: _ => hint);
        return (CharacterRoster.Nearest(roster, cast.VoiceFor(character.Id)!, cast.Flavours![character.Id])!, roster);
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void A_pig_is_cast_as_the_piglet_in_every_language(string language)
    {
        var (pick, _) = CastOne(language, C("pip", "Pip"), new AnimalHint(AnimalKind.Pig, null));

        Assert.Equal("piglet", pick.Id);
    }

    [Theory]
    [InlineData("Papa Bear", false)]
    [InlineData("Baby Bear", true)]
    public void A_bear_is_cast_by_its_age(string name, bool young)
    {
        var (pick, _) = CastOne("en", C("b", name), AnimalCharacters.HintFor(AnimalKind.Bear, name, "en"));

        Assert.Equal(young, AnimalCharacters.Of(pick.Id)!.Value.Young);
        Assert.Contains(AnimalKind.Bear, AnimalCharacters.Of(pick.Id)!.Value.Kinds);
    }

    [Fact]
    public void A_grown_animal_with_no_grown_character_is_cast_like_any_grown_up_and_a_not_animal_is_not_cast_as_one()
    {
        var (mother, _) = CastOne("en", C("m", "Mother Pig", Gender.Female), AnimalCharacters.HintFor(AnimalKind.Pig, "Mother Pig", "en"));
        var (plain, _) = CastOne("en", C("a", "Anna", Gender.Female), null);

        Assert.NotEqual("piglet", mother.Id);
        Assert.Equal(Gender.Female, mother.Gender);
        Assert.NotNull(plain);
    }

    [Theory]
    [InlineData("Wolf", AnimalKind.Wolf, "wolf")]
    [InlineData("Lamb", AnimalKind.Sheep, "lamb")]
    [InlineData("Old Sheep", AnimalKind.Sheep, "ewe")]
    [InlineData("Puppy", AnimalKind.Dog, "puppy")]
    [InlineData("Kitten", AnimalKind.Cat, "kitten")]
    [InlineData("Duckling", AnimalKind.Duck, "duckling")]
    [InlineData("Peter", AnimalKind.Rabbit, "bunny")]
    public void Each_animal_finds_its_own_character(string name, AnimalKind kind, string rosterId)
    {
        var (pick, _) = CastOne("en", C("x", name), AnimalCharacters.HintFor(kind, name, "en"));

        Assert.Equal(rosterId, pick.Id);
    }

    [Fact]
    public void Three_pigs_get_three_different_voices_and_the_first_is_the_piglet()
    {
        var characters = new[] { C("pip", "Pip"), C("pop", "Pop"), C("pim", "Pim") };
        var roster = CharacterRoster.For("en");
        string narrator = CharacterRoster.Narrators("en")[0].VoiceId;
        var choices = CharacterRoster.Choices(CharacterRoster.Speakers("en"), narrator);

        VoiceCast cast = CastCharacters.Cast(
            characters, choices, roster, narrator, _ => VoiceFlavour.Normal, animalOf: _ => new AnimalHint(AnimalKind.Pig, null));

        var sounds = characters.Select(c => (cast.VoiceFor(c.Id), cast.Flavours![c.Id])).ToList();
        Assert.Equal(3, sounds.Distinct().Count());
        Assert.Equal(("kokoro:am_puck", VoiceFlavour.Small), sounds[0]);
        Assert.All(sounds.Skip(1), s => Assert.True(AnimalCharacters.Of(CharacterRoster.Nearest(roster, s.Item1!, s.Item2)!.Id) is not null));
    }

    [Fact]
    public void A_voice_a_grown_up_picked_is_kept_whatever_the_animal()
    {
        var characters = new[] { C("pip", "Pip") };
        var roster = CharacterRoster.For("en");
        string narrator = CharacterRoster.Narrators("en")[0].VoiceId;
        var choices = CharacterRoster.Choices(CharacterRoster.Speakers("en"), narrator);
        var pinned = new Dictionary<string, PinnedCharacter> { ["pip"] = new("kokoro:am_santa", VoiceFlavour.Giant, Edited: true) };

        VoiceCast cast = CastCharacters.Cast(
            characters, choices, roster, narrator, _ => VoiceFlavour.Normal, pinned, animalOf: _ => new AnimalHint(AnimalKind.Pig, null));

        Assert.Equal("kokoro:am_santa", cast.VoiceFor("pip"));
        Assert.Equal(VoiceFlavour.Giant, cast.Flavours!["pip"]);
    }

    [Theory]
    [InlineData("Mrs. Rabbit", false)]
    [InlineData("Papa Bear", false)]
    [InlineData("Baby Bear", true)]
    [InlineData("Little Pig", true)]
    [InlineData("Old Hen", false)]
    public void The_name_says_young_or_grown(string name, bool young) => Assert.Equal(young, AnimalCharacters.AgeOf(name, "en"));

    [Fact]
    public void A_bare_name_says_nothing_about_age() => Assert.Null(AnimalCharacters.AgeOf("Pip", "en"));
}
