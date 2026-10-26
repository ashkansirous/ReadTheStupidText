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
        Assert.Empty(AnimalSounds.WithSound(AnimalSounds.OfCast(characters, "en", cast, CharacterRoster.Speakers("en")), "en"));
        Assert.Null(AnimalSounds.SoundOf(AnimalKind.Rabbit, "en"));
        Assert.Equal("Grr!", AnimalSounds.SoundOf(AnimalKind.Bear, "en"));
    }

    [Fact]
    public void The_pigs_starter_cast_as_piglets_oink_and_the_wolf_growls()
    {
        Story story = StarterBuilder.Build(StarterTexts.PigsId, "The Three Little Pigs", StarterTexts.Pigs, 3);
        var characters = story.Cast.Select(m => C(m.CharacterId, m.Name)).ToList();

        var animals = AnimalSounds.OfCast(characters, "en", CastAsRoster(characters, story.Text, "en"), CharacterRoster.Speakers("en"), story.Text);

        Assert.Equal(AnimalKind.Pig, animals["Pip"]);
        Assert.Equal(AnimalKind.Pig, animals["Pop"]);
        Assert.Equal(AnimalKind.Pig, animals["Pim"]);
        Assert.Equal(AnimalKind.Wolf, animals["Wolf"]);
    }

    [Fact]
    public void A_pig_cast_as_a_plain_character_is_silent_and_another_stays_a_pig()
    {
        var characters = new[] { C("pip", "Pip"), C("pop", "Pop") };
        var speakers = CharacterRoster.Speakers("en");
        StorytellerCharacter plain = speakers.First(c => AnimalCharacters.Of(c.Id) is null && c.Gender == Gender.Male);
        StorytellerCharacter piglet = speakers.First(c => c.Id == "piglet");
        var cast = new[]
        {
            new CastMember("pip", "Pip", Gender.Male, plain.VoiceId, true, plain.Flavour, Animal: AnimalKind.Pig),
            new CastMember("pop", "Pop", Gender.Male, piglet.VoiceId, true, piglet.Flavour, Animal: AnimalKind.Cat),
        };

        var animals = AnimalSounds.OfCast(characters, "en", cast, speakers);

        Assert.False(animals.ContainsKey("pip"));
        Assert.Equal(AnimalKind.Pig, animals["pop"]);
    }

    [Fact]
    public void An_own_book_with_a_pig_first_casts_it_as_a_piglet()
    {
        var pip = C("pip", "Pip");
        var cast = CastAsRoster([pip], "Pip the pig lived by the sea.", "en");

        var playing = CharacterRoster.Nearest(CharacterRoster.Speakers("en"), cast.Single().VoiceId, cast.Single().Flavour!.Value, Gender.Male)!;

        Assert.StartsWith("piglet", playing.Id);
        Assert.Equal(AnimalKind.Pig, AnimalSounds.OfCast([pip], "en", cast, CharacterRoster.Speakers("en"), "Pip the pig lived by the sea.")["pip"]);
    }

    [Fact]
    public void Every_animal_roster_character_has_a_kind_and_every_plain_one_has_none_in_every_language()
    {
        foreach (string language in UiLanguage.Supported)
        {
            var speakers = CharacterRoster.Speakers(language);
            Assert.All(speakers.Where(c => AnimalCharacters.RosterIds.Contains(c.Id)), c => Assert.NotEmpty(AnimalCharacters.Of(c.Id)!.Value.Kinds));
            Assert.All(speakers.Where(c => !AnimalCharacters.RosterIds.Contains(c.Id)), c => Assert.Null(AnimalCharacters.Of(c.Id)));
        }

        Assert.All(AnimalCharacters.RosterIds, id => Assert.Contains(UiLanguage.Supported, l => CharacterRoster.For(l).Any(c => c.Id == id)));
    }

    // The members of a first cast, the way the app makes it: each story character gets the roster character that fits its animal (found from the name, then the text).
    private static IReadOnlyList<CastMember> CastAsRoster(IReadOnlyList<Character> characters, string text, string language)
    {
        VoiceCast cast = FirstCast(characters, text, language);
        return characters.Select(c => new CastMember(c.Id, c.Name, c.Gender, cast.VoiceFor(c.Id)!, Flavour: cast.Flavours![c.Id])).ToList();
    }

    private static VoiceCast FirstCast(IReadOnlyList<Character> characters, string text, string language)
    {
        var roster = CharacterRoster.For(language);
        string narrator = CharacterRoster.Narrators(language)[0].VoiceId;
        var choices = CharacterRoster.Choices(CharacterRoster.Speakers(language), narrator);
        var found = AnimalSounds.Resolve(characters, language, null, text);
        return CastCharacters.Cast(
            characters, choices, roster, narrator, c => FlavourGuess.Guess(c.Name, LanguageRules.For(language)),
            animalOf: c => found.TryGetValue(c.Id, out AnimalKind kind) ? AnimalCharacters.HintFor(kind, c.Name, language) : null);
    }

    [Fact]
    public void A_cow_with_no_cow_character_still_moos_until_a_grown_up_recasts_it()
    {
        var cow = C("cow", "Cow");
        const string text = "The Cow went to the farm.";
        var speakers = CharacterRoster.Speakers("en");
        var cast = CastAsRoster([cow], text, "en");

        Assert.Equal(AnimalKind.Cow, AnimalSounds.OfCast([cow], "en", cast, speakers, text)["cow"]);

        StorytellerCharacter other = speakers.First(c => AnimalCharacters.Of(c.Id) is null && (c.VoiceId != cast[0].VoiceId || c.Flavour != cast[0].Flavour));
        var recast = new[] { cast[0] with { VoiceId = other.VoiceId, Flavour = other.Flavour, Edited = true } };

        Assert.Empty(AnimalSounds.WithSound(AnimalSounds.OfCast([cow], "en", recast, speakers, text), "en"));
    }

    [Fact]
    public void A_duck_auto_cast_as_a_piglet_still_quacks_but_a_duck_a_grown_up_gave_a_piglet_oinks()
    {
        var duck = C("duck", "Duck");
        var speakers = CharacterRoster.Speakers("en");
        StorytellerCharacter piglet = speakers.First(c => c.Id == "piglet");
        var asAuto = new[] { new CastMember("duck", "Duck", Gender.Male, piglet.VoiceId, false, piglet.Flavour) };
        var changed = new[] { new CastMember("duck", "Duck", Gender.Male, piglet.VoiceId, true, piglet.Flavour) };

        Assert.Equal(AnimalKind.Duck, AnimalSounds.OfCast([duck], "en", asAuto, speakers)["duck"]);
        Assert.Equal(AnimalKind.Pig, AnimalSounds.OfCast([duck], "en", changed, speakers)["duck"]);
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
    [InlineData("The sheep grazed in the field. Jack and Jill went up the hill.")]
    [InlineData("The pigs saw Jack and Jill.")]
    [InlineData("The pigs ran. They saw Jack and Jill.")]
    [InlineData("The wolf chased Jack.")]
    [InlineData("Jack loves cats.")]
    [InlineData("Jack milked cows.")]
    [InlineData("Jack has been busy.")]
    [InlineData("Jack and Jill were afraid of the pigs.")]
    public void A_verb_or_a_far_animal_word_does_not_make_a_person_an_animal(string text)
    {
        var characters = new[] { C("jack", "Jack"), C("jill", "Jill") };

        Assert.Empty(AnimalGuess.Guess(characters, text, "en"));
    }

    [Fact]
    public void An_animal_that_belongs_to_the_name_before_it_is_not_given_to_the_next_name()
    {
        var found = AnimalGuess.Guess([C("pip", "Pip"), C("pop", "Pop")], "Pip the pig and Pop the cow lived there.", "en");

        Assert.Equal(AnimalKind.Pig, found["pip"]);
        Assert.Equal(AnimalKind.Cow, found["pop"]);
    }

    [Theory]
    [InlineData("Three little sheep lived in a meadow. They were called Baa, Bo and Boo.", AnimalKind.Sheep)]
    [InlineData("Three wolves lived in a cave. Their names were Baa, Bo and Boo.", AnimalKind.Wolf)]
    [InlineData("The pigs ran. Three rabbits, Baa, Bo and Boo, hid.", AnimalKind.Rabbit)]
    [InlineData("Baa, Bo and Boo were three little pigs.", AnimalKind.Pig)]
    [InlineData("There were three little pigs called Baa, Bo and Boo.", AnimalKind.Pig)]
    public void A_group_binds_only_the_names_listed_next_to_the_plural_animal(string text, AnimalKind kind)
    {
        var characters = new[] { C("baa", "Baa"), C("bo", "Bo"), C("boo", "Boo") };

        var found = AnimalGuess.Guess(characters, text, "en");

        Assert.All(characters, c => Assert.Equal(kind, found[c.Id]));
    }

    [Theory]
    [InlineData("Pip", "Pip has been busy all day.")]
    [InlineData("Pip", "Pip would cower, cater and bear it.")]
    public void Ordinary_english_words_are_not_animal_plurals(string name, string text)
    {
        Assert.Empty(AnimalGuess.Guess([C("pip", name)], text, "en"));
    }

    [Fact]
    public void A_non_animal_character_does_not_take_an_animal_character()
    {
        var roster = CharacterRoster.For("en");
        string narrator = CharacterRoster.Narrators("en")[0].VoiceId;
        var choices = CharacterRoster.Choices(CharacterRoster.Speakers("en"), narrator);
        var goldilocks = new Character("g", "Little Goldilocks", Gender.Female, 20);

        VoiceCast cast = CastCharacters.Cast([goldilocks], choices, roster, narrator, _ => VoiceFlavour.Small);

        Assert.Null(AnimalCharacters.Of(CharacterRoster.Nearest(roster, cast.VoiceFor("g")!, cast.Flavours!["g"])!.Id));
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
        VoiceFlavour[] young = [VoiceFlavour.Small, VoiceFlavour.Squeaky, VoiceFlavour.Bouncy, VoiceFlavour.Chirpy, VoiceFlavour.Perky]; // Slice 144 added Chirpy and Perky as young-sounding
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

    // Slice 135: the first of a kind takes the original character (piglet, lamb); a numbered extra (piglet2) only when its flavour matches the name's guess strictly better.
    private static void AssertOriginalUnlessNumberedFitsBetter(StorytellerCharacter pick, IReadOnlyList<StorytellerCharacter> roster, string originalId, string name)
    {
        if (pick.Id == originalId)
        {
            return;
        }

        VoiceFlavour wanted = FlavourGuess.Guess(name, LanguageRules.For("en"));
        Assert.Equal(originalId, pick.Id.TrimEnd('2', '3'));
        Assert.True(CharacterRoster.FlavourDistance(pick.Flavour, wanted) < CharacterRoster.FlavourDistance(roster.Single(c => c.Id == originalId).Flavour, wanted));
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
        var (pick, roster) = CastOne(language, C("pip", "Pip"), new AnimalHint(AnimalKind.Pig, null));

        AssertOriginalUnlessNumberedFitsBetter(pick, roster, "piglet", "Pip");
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
        var (pick, roster) = CastOne("en", C("x", name), AnimalCharacters.HintFor(kind, name, "en"));

        AssertOriginalUnlessNumberedFitsBetter(pick, roster, rosterId, name);
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
        Assert.All(sounds, s => Assert.StartsWith("piglet", CharacterRoster.Nearest(roster, s.Item1!, s.Item2)!.Id));
    }

    // Slice 135: the Three Little Pigs, cast the way the app casts a pig starter, get three different piglets in English and Farsi, never a puppy or a cub.
    [Theory]
    [InlineData("en", 3)]
    [InlineData("fa", 3)]
    [InlineData("en", 6)]
    [InlineData("fa", 6)]
    public void Pigs_are_always_piglets_and_the_first_three_sound_different(string language, int pigs)
    {
        var characters = Enumerable.Range(0, pigs).Select(i => C($"pig{i}", $"Pig {i}")).ToList();
        var roster = CharacterRoster.For(language);
        string narrator = CharacterRoster.Narrators(language).First(n => roster.Where(c => c.Id.StartsWith("piglet")).All(c => c.VoiceId != n.VoiceId)).VoiceId;
        var choices = CharacterRoster.Choices(CharacterRoster.Speakers(language), narrator);

        VoiceCast cast = CastCharacters.Cast(
            characters, choices, roster, narrator, _ => VoiceFlavour.Normal, animalOf: _ => new AnimalHint(AnimalKind.Pig, null));

        var sounds = characters.Select(c => (cast.VoiceFor(c.Id)!, cast.Flavours![c.Id])).ToList();
        var pigletVoices = roster.Where(c => c.Id.StartsWith("piglet")).Select(c => c.VoiceId).ToHashSet();
        Assert.All(sounds, s => Assert.Contains(s.Item1, pigletVoices));
        Assert.Equal(3, sounds.Take(3).Distinct().Count());
        Assert.All(sounds.Take(3), s => Assert.StartsWith("piglet", CharacterRoster.Nearest(roster, s.Item1, s.Item2)!.Id));
    }

    // Slice 135 follow-up: the Farsi narrator shares a voice with two piglets; they are still offered to the pigs (their flavours are not the narrator's), so the three pigs are
    // three different piglets, named so by the cast screen's lookup, and none is shown as the bear cub or the puppy.
    [Fact]
    public void Farsi_pigs_with_the_default_narrator_are_three_distinct_piglets()
    {
        var characters = new[] { C("pip", "Pip"), C("pop", "Pop"), C("pim", "Pim") };
        var roster = CharacterRoster.For("fa");
        string narrator = CharacterRoster.Narrators("fa")[0].VoiceId;
        var choices = CharacterRoster.Choices(CharacterRoster.Speakers("fa"), narrator);

        VoiceCast cast = CastCharacters.Cast(characters, choices, roster, narrator, _ => VoiceFlavour.Normal, animalOf: _ => new AnimalHint(AnimalKind.Pig, null));

        var speakers = CharacterRoster.Speakers("fa");
        var named = characters.Select(c => CharacterRoster.Nearest(speakers, cast.VoiceFor(c.Id)!, cast.Flavours![c.Id])!.Id).ToList();
        Assert.All(named, id => Assert.StartsWith("piglet", id));
        Assert.Equal(3, named.Distinct().Count());
        Assert.All(cast.Assignments, a => Assert.False(a.IsShared));
    }

    // Slice 135: an animal with a single character of its own (a bunny) is reused for its second story character with another flavour, never another animal's character.
    [Fact]
    public void Two_bunnies_sound_different_and_both_stay_bunnies()
    {
        var characters = new[] { C("m1", "Bunny One"), C("m2", "Bunny Two") };
        var roster = CharacterRoster.For("en");
        string narrator = CharacterRoster.Narrators("en").First().VoiceId;
        var choices = CharacterRoster.Choices(CharacterRoster.Speakers("en"), narrator);

        VoiceCast cast = CastCharacters.Cast(characters, choices, roster, narrator, _ => VoiceFlavour.Normal, animalOf: _ => new AnimalHint(AnimalKind.Rabbit, null));

        var sounds = characters.Select(c => (cast.VoiceFor(c.Id)!, cast.Flavours![c.Id])).ToList();
        Assert.Equal(2, sounds.Distinct().Count());
        Assert.All(sounds, s => Assert.Equal(sounds[0].Item1, s.Item1));
        Assert.Equal("bunny", roster.Single(c => c.Id == "bunny").Id);
    }

    [Fact]
    public void Many_reuses_of_a_single_character_never_switch_to_another_animal()
    {
        var characters = Enumerable.Range(0, 40).Select(i => C($"b{i}", $"Bunny {i}")).ToList();
        var roster = CharacterRoster.For("en");
        string narrator = CharacterRoster.Narrators("en").First().VoiceId;
        var choices = CharacterRoster.Choices(CharacterRoster.Speakers("en"), narrator);
        string bunnyVoice = roster.Single(c => c.Id == "bunny").VoiceId;

        VoiceCast cast = CastCharacters.Cast(characters, choices, roster, narrator, _ => VoiceFlavour.Normal, animalOf: _ => new AnimalHint(AnimalKind.Rabbit, null));

        Assert.All(characters, c => Assert.Equal(bunnyVoice, cast.VoiceFor(c.Id)));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_roster_has_a_second_piglet_and_the_big_rosters_have_the_other_young_farm_animals(string language)
    {
        var roster = CharacterRoster.For(language);

        Assert.Contains(roster, c => c.Id == "piglet2");
        string[] others = ["piglet3", "lamb2", "duckling2", "puppy2", "kitten2"];
        Assert.All(others, id => Assert.Equal(language is not ("es" or "pt"), roster.Any(c => c.Id == id)));
        Assert.All(roster.Where(c => c.Id.EndsWith('2') || c.Id.EndsWith('3')), c => Assert.NotNull(AnimalCharacters.Of(c.Id)));
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

    [Theory]
    [InlineData("Jack hugged Jill, the cows laughed.")]
    [InlineData("She fed the cats, then Jack and Jill ran off.")]
    [InlineData("Jack and Jill, chased by wolves, ran.")]
    [InlineData("Jack and Jill were near cats.")]
    public void Names_that_only_stand_near_a_plural_animal_are_not_a_group(string text)
    {
        var characters = new[] { C("jack", "Jack"), C("jill", "Jill") };

        Assert.Empty(AnimalGuess.Guess(characters, text, "en"));
    }

    private static (IReadOnlyList<StorytellerCharacter> Roster, StorytellerCharacter Plain, StorytellerCharacter Animal) PlainAndAnimal()
    {
        var roster = CharacterRoster.For("en");
        var speakers = roster.Where(c => c.Kind == CharacterKind.Speaker).ToList();
        StorytellerCharacter animal = speakers.First(c => AnimalCharacters.Of(c.Id) is not null);
        StorytellerCharacter plain = speakers.First(c => AnimalCharacters.Of(c.Id) is null && c.Gender == animal.Gender);
        return (roster, plain, animal);
    }

    [Fact]
    public void A_non_animal_character_takes_a_free_plain_character_over_an_animal_one()
    {
        var (roster, plain, animal) = PlainAndAnimal();
        string narrator = CharacterRoster.Narrators("en")[0].VoiceId;

        VoiceCast cast = CastCharacters.Cast([new Character("g", "Goldie", animal.Gender, 10)], [animal, plain], roster, narrator, _ => animal.Flavour);

        Assert.Equal(plain.VoiceId, cast.VoiceFor("g"));
    }

    [Fact]
    public void A_non_animal_character_still_gets_a_voice_when_every_plain_character_is_taken()
    {
        var (roster, plain, animal) = PlainAndAnimal();
        string narrator = CharacterRoster.Narrators("en")[0].VoiceId;
        var pinned = new Dictionary<string, PinnedCharacter> { ["a"] = new(plain.VoiceId, plain.Flavour, Edited: true) };

        VoiceCast cast = CastCharacters.Cast(
            [new Character("a", "Ann", animal.Gender, 10), new Character("b", "Bea", animal.Gender, 9)], [animal, plain], roster, narrator, _ => plain.Flavour, pinned);

        Assert.Equal(animal.VoiceId, cast.VoiceFor("b"));
    }
}
