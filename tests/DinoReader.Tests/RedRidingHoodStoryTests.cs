using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Little Red Riding Hood's cast (Slice 121): the girls and women get women's voices and the Wolf and the Woodcutter men's, in every language's roster,
/// cast the way <see cref="StoreStoryTests.TheUglyDuckling_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster"/> casts the Ugly Duckling.
/// Button the puppy never speaks, so Button is not in the cast.</summary>
public sealed class RedRidingHoodStoryTests
{
    private static readonly IReadOnlyDictionary<string, Gender> ExpectedGenders = new Dictionary<string, Gender>
    {
        ["Red Riding Hood"] = Gender.Female, ["Mother"] = Gender.Female, ["Grandma"] = Gender.Female, ["Wolf"] = Gender.Male, ["Woodcutter"] = Gender.Male,
    };

    public static TheoryData<string> Languages() => new(UiLanguage.Supported);

    [Theory]
    [MemberData(nameof(Languages))]
    public void LittleRedRidingHood_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster(string language)
    {
        Story story = StoreBuilder.Build(StoreTexts.All.Single(s => s.Id == StoreTexts.RedRidingHoodId));
        IReadOnlyList<CastMember> stored = language == story.Language
            ? story.Cast
            : StoryCast.SeedForOtherLanguage(story.Cast);
        List<Character> characters = story.Cast.Select(m => new Character(m.CharacterId, m.Name, m.Gender, 1)).ToList();
        IReadOnlyDictionary<string, AnimalKind> animals = AnimalSounds.Resolve(characters, story.Language, stored, story.Text);
        IReadOnlyList<StorytellerCharacter> speakers = CharacterRoster.Speakers(language);
        string narrator = CharacterRoster.Narrators(language)[0].VoiceId;

        VoiceCast cast = CastCharacters.Cast(
            characters, CharacterRoster.Choices(speakers, narrator), CharacterRoster.For(language), narrator, c => FlavourGuess.Guess(c.Name, LanguageRules.English),
            StoryCast.PinnedCharacters(stored), animalOf: c => animals.TryGetValue(c.Id, out AnimalKind kind) ? AnimalCharacters.HintFor(kind, c.Name, story.Language) : null);

        Assert.Equal(ExpectedGenders.Keys.Order(), characters.Select(c => c.Name).Order());
        foreach (Character character in characters)
        {
            StorytellerCharacter? played = CharacterRoster.Nearest(speakers, cast.VoiceFor(character.Id)!, cast.Flavours![character.Id], character.Gender);
            Assert.True(played?.Gender == ExpectedGenders[character.Name], $"{language}: {character.Name} is played by {played?.Name} ({played?.Gender})");
        }
    }
}
