using DinoReader.Core;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Tests;

/// <summary>Jack and the Beanstalk's cast (Slice 121): Jack and the Giant get men's voices and Mother and the Peddler women's, in every language's roster,
/// cast the way <see cref="StoreStoryTests.TheUglyDuckling_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster"/> casts the Ugly Duckling.
/// The Giant also gets one of the roster's deep, low voices: the Big, Giant or Grumpy flavour, the family <see cref="CharacterRoster.FlavourDistance"/> counts as one
/// (since Slice 156 the book suggests the Friendly Giant, which every language's roster has; Jack is the Cheeky Piglet, Mother the Kind Mummy and the Peddler the Busy Bee,
/// see <see cref="StarterVoices"/>). Goldie the hen, Milky-White the cow, the donkey and the birds never speak, so they are not in the cast.</summary>
public sealed class BeanstalkStoryTests
{
    private const string GiantName = "Giant";

    private static readonly IReadOnlyDictionary<string, Gender> ExpectedGenders = new Dictionary<string, Gender>
    {
        ["Jack"] = Gender.Male, ["Mother"] = Gender.Female, [GiantName] = Gender.Male, ["Peddler"] = Gender.Female,
    };

    private static readonly IReadOnlySet<VoiceFlavour> DeepFlavours = new HashSet<VoiceFlavour> { VoiceFlavour.Big, VoiceFlavour.Giant, VoiceFlavour.Grumpy };

    public static TheoryData<string> Languages() => new(UiLanguage.Supported);

    [Theory]
    [MemberData(nameof(Languages))]
    public void JackAndTheBeanstalk_GivesEachCharacterAVoiceOfTheirGender_AndTheGiantADeepOne_InEveryLanguagesRoster(string language)
    {
        Story story = StoreBuilder.Build(StoreTexts.All.Single(s => s.Id == StoreTexts.BeanstalkId));
        IReadOnlyList<CastMember> stored = language == story.Language
            ? story.Cast
            : story.Cast.Where(m => m.HasAnimalSetting).Select(m => m with { VoiceId = string.Empty }).ToList();
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
            if (character.Name == GiantName)
            {
                Assert.True(DeepFlavours.Contains(cast.Flavours[character.Id]), $"{language}: the Giant sounds {cast.Flavours[character.Id]}, played by {played?.Name}");
            }
        }
    }
}
