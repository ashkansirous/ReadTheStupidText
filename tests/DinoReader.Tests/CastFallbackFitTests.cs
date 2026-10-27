using DinoReader.Core;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using Xunit;

namespace DinoReader.Tests;

/// <summary>The trait fallback and the three-voice roster fit the character better (Slice 156 follow-ups).</summary>
public class CastFallbackFitTests
{
    private const int KindMiss = 40;
    private static readonly CharacterTraits Loud = new(CharacterAge.Adult, CharacterBeing.Pirate, [VoiceQuality.Bold, VoiceQuality.Bright]);
    private static readonly CharacterTraits Whispery = new(CharacterAge.Adult, CharacterBeing.Pirate, [VoiceQuality.Whispery]);

    public static TheoryData<string> WhisperingLanguages() => ["es", "pt", "fa"];

    private static VoiceCast Cast(IReadOnlyList<StorytellerCharacter> roster, Character character, PinnedCharacter pinned, AnimalHint? animal = null)
    {
        string narrator = roster.First(c => c.Kind == CharacterKind.Narrator).VoiceId;
        var choices = CharacterRoster.Choices(roster.Where(c => c.Kind == CharacterKind.Speaker).ToList(), narrator);
        return CastCharacters.Cast([character], choices, roster, narrator, _ => VoiceFlavour.Normal,
            new Dictionary<string, PinnedCharacter> { [character.Id] = pinned }, animalOf: animal is null ? null : _ => animal);
    }

    private static StorytellerCharacter Played(IReadOnlyList<StorytellerCharacter> roster, VoiceCast cast, string id) =>
        roster.First(c => c.Kind == CharacterKind.Speaker && c.VoiceId == cast.VoiceFor(id) && c.Flavour == cast.Flavours![id]);

    [Theory]
    [MemberData(nameof(WhisperingLanguages))]
    public void A_loud_character_is_not_cast_on_a_whispered_place(string language)
    {
        var roster = CharacterRoster.For(language);
        var kathryn = new Character("k", "Kathryn", Gender.Female, 10);

        VoiceCast cast = Cast(roster, kathryn, new PinnedCharacter(string.Empty, null, false, "princess", Loud));

        Assert.DoesNotContain(Played(roster, cast, "k").Flavour, new[] { VoiceFlavour.Whisper, VoiceFlavour.Hushed });
    }

    [Theory]
    [MemberData(nameof(WhisperingLanguages))]
    public void A_whispery_character_keeps_the_whispered_place_the_book_chose(string language)
    {
        var roster = CharacterRoster.For(language);
        var kathryn = new Character("k", "Kathryn", Gender.Female, 10);

        VoiceCast cast = Cast(roster, kathryn, new PinnedCharacter(string.Empty, null, false, "princess", Whispery));

        Assert.Equal("princess", Played(roster, cast, "k").Id);
    }

    [Fact]
    public void An_old_against_young_age_miss_costs_more_than_a_kind_miss()
    {
        StorytellerCharacter mouse = CharacterRoster.Speakers("en").First(c => c.Id == "mouse");

        int youngInstead = CharacterTraitMatch.Cost(mouse, Gender.Unknown, new CharacterTraits(CharacterAge.Old), null);

        Assert.True(youngInstead > KindMiss);
    }

    [Fact]
    public void An_old_owl_without_an_owl_in_the_language_is_not_cast_as_a_young_bird()
    {
        var roster = CharacterRoster.For("fa").Where(c => c.Id != "owl").ToList();
        var owl = new Character("o", "Old Owl", Gender.Unknown, 10);
        var traits = new CharacterTraits(CharacterAge.Old, null, [VoiceQuality.Sleepy, VoiceQuality.Soft]);

        VoiceCast cast = Cast(roster, owl, new PinnedCharacter(string.Empty, null, false, "owl", traits), new AnimalHint(AnimalKind.Owl, null));

        Assert.NotEqual(CharacterAge.Young, CharacterTraitMatch.AgeOf(Played(roster, cast, "o").Id));
    }

    [Theory]
    [MemberData(nameof(WhisperingLanguages))]
    public void A_loud_character_with_no_usable_suggestion_still_keeps_off_whispered_places(string language)
    {
        var roster = CharacterRoster.For(language);
        var kathryn = new Character("k", "Kathryn", Gender.Female, 10);
        var traits = new CharacterTraits(CharacterAge.Young, CharacterBeing.Person, [VoiceQuality.Bright, VoiceQuality.Bold]);

        VoiceCast cast = Cast(roster, kathryn, new PinnedCharacter(string.Empty, null, false, null, traits));

        Assert.DoesNotContain(Played(roster, cast, "k").Flavour, new[] { VoiceFlavour.Whisper, VoiceFlavour.Hushed });
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    [InlineData("fa")]
    public void A_sly_fox_keeps_the_fox_place_the_book_chose_in_every_language(string language)
    {
        var roster = CharacterRoster.For(language);
        var fox = new Character("f", "Sly Fox", Gender.Male, 10);
        var traits = new CharacterTraits(CharacterAge.Adult, CharacterBeing.Fox, [VoiceQuality.Sly, VoiceQuality.Bold]);

        VoiceCast cast = Cast(roster, fox, new PinnedCharacter(string.Empty, null, false, "fox", traits));

        Assert.Equal("fox", Played(roster, cast, "f").Id);
    }

    [Theory]
    [MemberData(nameof(WhisperingLanguages))]
    public void The_back_to_the_books_voice_link_is_not_offered_for_a_suggestion_dropped_for_its_whisper(string language)
    {
        var speakers = CharacterRoster.Speakers(language);
        var member = new CastMember("k", "Kathryn", Gender.Female, "other", true, VoiceFlavour.Normal, SuggestedVoice: "princess");

        Assert.False(CastEditor.OffersBookVoice(member, VoiceFlavour.Normal, speakers));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    [InlineData("fa")]
    public void A_sly_fox_without_a_book_suggestion_still_takes_the_fox_place_whispered_in_english(string language)
    {
        var roster = CharacterRoster.For(language);
        var fox = new Character("f", "Sly Fox", Gender.Male, 10);
        var traits = new CharacterTraits(CharacterAge.Adult, CharacterBeing.Fox, [VoiceQuality.Sly, VoiceQuality.Bold]);

        VoiceCast cast = Cast(roster, fox, new PinnedCharacter(string.Empty, null, false, null, traits));

        Assert.Equal("fox", Played(roster, cast, "f").Id);
    }

    [Fact]
    public void A_loud_character_on_a_narrator_voice_with_no_normal_place_takes_a_calm_place_not_the_whisper()
    {
        StorytellerCharacter Place(string id, string voice, VoiceFlavour flavour, Gender gender, CharacterKind kind = CharacterKind.Speaker) => new(id, voice, flavour, gender, kind, id, id);
        var roster = new List<StorytellerCharacter>
        {
            Place("n", "w", VoiceFlavour.Storyteller, Gender.Female, CharacterKind.Narrator),
            Place("a", "w", VoiceFlavour.Whisper, Gender.Female),
            Place("b", "w", VoiceFlavour.Sleepy, Gender.Female),
            Place("c", "m", VoiceFlavour.Normal, Gender.Male),
        };
        var choices = CharacterRoster.Choices(roster.Where(c => c.Kind == CharacterKind.Speaker).ToList(), "w");
        var kathryn = new Character("k", "Kathryn", Gender.Female, 10);

        VoiceCast cast = CastCharacters.Cast([kathryn], choices, roster, "w", _ => VoiceFlavour.Normal);

        Assert.NotEqual(VoiceFlavour.Whisper, cast.Flavours!["k"]);
    }

    [Theory]
    [InlineData("es")]
    [InlineData("pt")]
    public void The_cub_stays_among_the_choices_whichever_male_voice_narrates(string language)
    {
        var roster = CharacterRoster.For(language);
        var speakers = roster.Where(c => c.Kind == CharacterKind.Speaker).ToList();

        foreach (string narrator in roster.Where(c => c.Kind == CharacterKind.Narrator).Select(c => c.VoiceId).Distinct())
        {
            var choices = CharacterRoster.Choices(speakers, narrator);
            StorytellerCharacter cub = choices.Single(c => c.Id == "cub");

            Assert.Contains(cub.Flavour, new[] { VoiceFlavour.Chirpy, VoiceFlavour.Perky });
            Assert.Equal(choices.Count, choices.Select(c => (c.VoiceId, c.Flavour)).Distinct().Count());
        }
    }

    [Theory]
    [InlineData("es")]
    [InlineData("pt")]
    public void A_cub_on_the_narrators_voice_moves_to_another_male_voice(string language)
    {
        var roster = CharacterRoster.For(language);
        var speakers = roster.Where(c => c.Kind == CharacterKind.Speaker).ToList();
        string narrator = speakers.Single(c => c.Id == "cub").VoiceId;

        StorytellerCharacter cub = CharacterRoster.Choices(speakers, narrator).Single(c => c.Id == "cub");

        Assert.NotEqual(narrator, cub.VoiceId);
        Assert.Equal(Gender.Male, cub.Gender);
    }

    [Fact]
    public void An_old_place_for_a_child_costs_more_than_a_kind_miss()
    {
        StorytellerCharacter hen = CharacterRoster.Speakers("en").First(c => c.Id == "hen");

        Assert.Equal(CharacterAge.Old, CharacterTraitMatch.AgeOf("hen"));
        Assert.True(CharacterTraitMatch.Cost(hen, Gender.Unknown, new CharacterTraits(CharacterAge.Child), null) > KindMiss);
    }

    [Theory]
    [InlineData("es")]
    [InlineData("pt")]
    public void The_cub_is_a_young_voice_not_the_deep_one(string language)
    {
        var speakers = CharacterRoster.Speakers(language);
        StorytellerCharacter cub = speakers.First(c => c.Id == "cub");

        Assert.EndsWith("_alex", cub.VoiceId);
        Assert.Contains(cub.Flavour, new[] { VoiceFlavour.Chirpy, VoiceFlavour.Perky });
        Assert.Equal(speakers.Count, speakers.Select(c => (c.VoiceId, c.Flavour)).Distinct().Count());
    }

    [Theory]
    [InlineData(VoiceQuality.Bold, VoiceFlavour.Sleepy)]
    [InlineData(VoiceQuality.Bright, VoiceFlavour.Hushed)]
    [InlineData(VoiceQuality.Booming, VoiceFlavour.Squeaky)]
    [InlineData(VoiceQuality.Gruff, VoiceFlavour.Small)]
    public void A_place_whose_flavour_contradicts_a_wanted_quality_costs_more_than_a_kind_miss(VoiceQuality wanted, VoiceFlavour contradicting)
    {
        StorytellerCharacter place = CharacterRoster.Speakers("en").First() with { Flavour = contradicting };
        StorytellerCharacter neutral = place with { Flavour = VoiceFlavour.Normal };
        var traits = new CharacterTraits(null, null, [wanted]);

        Assert.True(CharacterTraitMatch.Cost(place, Gender.Unknown, traits, null) - CharacterTraitMatch.Cost(neutral, Gender.Unknown, traits, null) > KindMiss);
    }

    [Fact]
    public void Grandma_in_farsi_takes_the_narrators_voice_in_normal_not_the_whispering_princess()
    {
        Story story = StoreBuilder.Build(StoreTexts.All.Single(t => t.Title == "Little Red Riding Hood"));
        List<Character> characters = story.Cast.Select(m => new Character(m.CharacterId, m.Name, m.Gender, 1)).ToList();
        IReadOnlyDictionary<string, AnimalKind> animals = AnimalSounds.Resolve(characters, story.Language, story.Cast.Select(m => m with { VoiceId = string.Empty }).ToList(), story.Text);
        var roster = CharacterRoster.For("fa");
        string narrator = CharacterRoster.Narrators("fa")[0].VoiceId;

        VoiceCast cast = CastCharacters.Cast(
            characters, CharacterRoster.Choices(CharacterRoster.Speakers("fa"), narrator), roster, narrator, c => FlavourGuess.Guess(c.Name, LanguageRules.English),
            StoryCast.PinnedCharacters(story.Cast.Select(m => m with { VoiceId = string.Empty }).ToList()),
            animalOf: c => animals.TryGetValue(c.Id, out AnimalKind kind) ? AnimalCharacters.HintFor(kind, c.Name, story.Language) : null);

        string grandma = characters.Single(c => c.Name == "Grandma").Id;
        Assert.Equal(VoiceFlavour.Normal, cast.Flavours![grandma]);
        Assert.Equal(narrator, cast.VoiceFor(grandma));
    }
}
