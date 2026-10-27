using DinoReader.Core;
using ReadTheStupidText.Domain.Reading;
using Xunit;

namespace DinoReader.Tests;

/// <summary>A character's animal comes from the book, never from its voice (Slice 152, Decision 157, reversing Decision 132).</summary>
public class AnimalFromBookTests
{
    private static Character C(string id, string name, Gender gender = Gender.Male) => new(id, name, gender, 10);

    private static IReadOnlyList<StorytellerCharacter> Speakers => CharacterRoster.Speakers("en");

    private static CastMember Recast(string id, string name, AnimalKind? animal, StorytellerCharacter to) =>
        new(id, name, to.Gender, to.VoiceId, true, to.Flavour, Animal: animal, NotAnimal: animal is null);

    [Fact]
    public void A_goat_recast_to_the_brave_knight_still_bleats()
    {
        StorytellerCharacter knight = Speakers.First(c => AnimalCharacters.Of(c.Id) is null && c.Gender == Gender.Male);
        var cast = new[] { Recast("big", "Big Billy", AnimalKind.Goat, knight) };

        var animals = AnimalSounds.OfCast([C("big", "Big Billy")], "en", cast, Speakers);

        Assert.Equal(AnimalKind.Goat, animals["big"]);
        Assert.NotNull(AnimalSounds.SoundOf(AnimalKind.Goat, "en"));
    }

    [Fact]
    public void A_pig_recast_to_the_wolf_still_oinks()
    {
        StorytellerCharacter wolf = Speakers.First(c => AnimalCharacters.Of(c.Id)?.Kinds.Contains(AnimalKind.Wolf) == true);
        var cast = new[] { Recast("pip", "Pip", AnimalKind.Pig, wolf) };

        Assert.Equal(AnimalKind.Pig, AnimalSounds.OfCast([C("pip", "Pip")], "en", cast, Speakers)["pip"]);
    }

    [Fact]
    public void A_character_recast_to_an_animals_voice_stays_what_the_book_says()
    {
        StorytellerCharacter piglet = Speakers.First(c => AnimalCharacters.Of(c.Id)?.Kinds.Contains(AnimalKind.Pig) == true);
        var cast = new[] { Recast("knight", "Sir Rowan", null, piglet), Recast("wolf", "Wolf", AnimalKind.Wolf, piglet) };

        var animals = AnimalSounds.OfCast([C("knight", "Sir Rowan"), C("wolf", "Wolf")], "en", cast, Speakers);

        Assert.False(animals.ContainsKey("knight"));
        Assert.Equal(AnimalKind.Wolf, animals["wolf"]);
    }

    [Fact]
    public void A_member_the_book_decided_keeps_its_animal_while_an_undecided_one_follows_the_old_rule()
    {
        StorytellerCharacter knight = Speakers.First(c => AnimalCharacters.Of(c.Id) is null && c.Gender == Gender.Male);
        var cast = new[]
        {
            Recast("pip", "Pip", AnimalKind.Pig, knight),
            new CastMember("pop", "Pop", Gender.Male, knight.VoiceId, true, knight.Flavour),
        };

        var animals = AnimalSounds.OfCast([C("pip", "Pip"), C("pop", "Pop")], "en", cast, Speakers);

        Assert.Equal(AnimalKind.Pig, animals["pip"]);
        Assert.False(animals.ContainsKey("pop"));
    }

    [Fact]
    public void A_character_the_cast_does_not_list_is_found_by_its_name()
    {
        var animals = AnimalSounds.OfCast([C("wolf", "Wolf")], "en", [Recast("pip", "Pip", AnimalKind.Pig, Speakers[0])], Speakers);

        Assert.Equal(AnimalKind.Wolf, animals["wolf"]);
    }

    [Fact]
    public void The_builder_refuses_an_animal_left_undecided()
    {
        var pig = new Character("pip", "Pip", Gender.Male, 3);
        var members = new[] { new CastMember("pip", "Pip", Gender.Male, "v") };

        Assert.Throws<InvalidOperationException>(() => StarterAnimals.Explicit("x", "Pip the pig said hello. \"Hello,\" said Pip.", [pig], members));
    }

    [Fact]
    public void Switching_animal_sounds_off_silences_every_book()
    {
        foreach (Story story in AllShippedBooks())
        {
            var characters = story.Cast.Select(m => C(m.CharacterId, m.Name)).ToList();
            var animals = AnimalSounds.OfCast(characters, story.Language, story.Cast, Speakers);
            var plan = AnimalSounds.Plan([], animals, story.Language);

            Assert.Empty(AnimalSounds.Audible(animals, soundsOn: false));
            Assert.Empty(AnimalSounds.Audible(plan, soundsOn: false));
            Assert.Equal(animals.Count, AnimalSounds.Audible(animals, soundsOn: true).Count);
        }
    }

    [Fact]
    public void The_voicing_applies_the_app_wide_switch_the_settings_card_saves()
    {
        string app = Path.Combine(StarterBuilder.RepoRoot(), "src", "DinoReader.App");
        string voicing = File.ReadAllText(Path.Combine(app, "StoryVoicing.cs"));
        string settings = File.ReadAllText(Path.Combine(app, "SettingsRows.cs"));

        Assert.Contains("AnimalSounds.Audible(AnimalPlan(story, prepared, spokenLanguage), AnimalSoundsPreference.Load())", voicing);
        Assert.Contains("toggle.Toggled += (_, _) => SaveQuietly(toggle.IsOn);", settings);
    }

    [Fact]
    public void Every_starter_and_store_book_names_an_animal_or_none_for_every_character()
    {
        foreach (Story story in AllShippedBooks())
        {
            Assert.NotEmpty(story.Cast);
            Assert.True(story.Cast.All(m => m.HasAnimalSetting), $"{story.Title} has a character with no animal decided: {string.Join(", ", story.Cast.Where(m => !m.HasAnimalSetting).Select(m => m.Name))}");
        }
    }

    [Fact]
    public void The_billy_goats_and_the_pigs_keep_their_animals_in_the_book()
    {
        Story goats = StoreBuilder.Build(StoreTexts.All.Single(s => s.Id == StoreTexts.BillyGoatsId));
        Story pigs = StarterBuilder.Build(StarterTexts.PigsId, "The Three Little Pigs", StarterTexts.Pigs, 3);

        Assert.All(goats.Cast.Where(m => m.Name.EndsWith("Billy")), m => Assert.Equal(AnimalKind.Goat, m.Animal));
        Assert.All(pigs.Cast.Where(m => m.Name is "Pip" or "Pop" or "Pim"), m => Assert.Equal(AnimalKind.Pig, m.Animal));
    }

    private static IEnumerable<Story> AllShippedBooks() =>
        StarterBuilder.Starters.Select(s => StarterBuilder.Build(s.Id, s.Title, s.Text, s.ColourIndex))
            .Concat(StoreTexts.All.Select(StoreBuilder.Build));
}
