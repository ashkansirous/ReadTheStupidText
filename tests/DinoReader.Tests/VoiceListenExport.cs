using DinoReader.Core;
using System.Text.Json;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Tests;

/// <summary>
/// Slice 153: with <c>DINO_VOICE_LISTEN_OUT=&lt;file&gt;</c> set, writes the English roster (voice, flavour shape) and the Ugly Duckling cast as the app
/// builds it to a JSON file for <c>tools/voice-listen</c>. Without the variable it does nothing, so the normal run is unaffected.
/// </summary>
public class VoiceListenExport
{
    public const string OutSwitch = "DINO_VOICE_LISTEN_OUT";
    private const string Language = "en";

    [Fact]
    public void WritesTheEnglishRosterAndTheUglyDucklingCast_WhenAsked()
    {
        string? path = Environment.GetEnvironmentVariable(OutSwitch);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        var export = new
        {
            roster = CharacterRoster.For(Language).Select(RosterRow).ToList(),
            uglyDuckling = UglyDucklingCast().ToList(),
        };
        File.WriteAllText(path, JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static object RosterRow(StorytellerCharacter c)
    {
        FlavourShape shape = VoiceFlavours.ShapeOf(c.Flavour);
        return new
        {
            id = c.Id, name = c.Name, hint = c.Hint, voiceId = c.VoiceId, flavour = c.Flavour.ToString(), gender = c.Gender.ToString(),
            kind = c.Kind.ToString(), semitones = shape.Semitones, pace = shape.Pace, gapMs = shape.SentenceGap.TotalMilliseconds,
        };
    }

    // Cast the way the app casts a story from the roster (StoreStoryTests does the same): the roster character each one is played by.
    private static IEnumerable<object> UglyDucklingCast()
    {
        Story story = StoreBuilder.Build(StoreTexts.All.Single(s => s.Id == StoreTexts.UglyDucklingId));
        IReadOnlyList<CastMember> stored = story.Cast.Where(m => m.HasAnimalSetting).Select(m => m with { VoiceId = string.Empty }).ToList();
        List<Character> characters = story.Cast.Select(m => new Character(m.CharacterId, m.Name, m.Gender, 1)).ToList();
        IReadOnlyDictionary<string, AnimalKind> animals = AnimalSounds.Resolve(characters, story.Language, stored, story.Text);
        IReadOnlyList<StorytellerCharacter> speakers = CharacterRoster.Speakers(Language);
        string narrator = CharacterRoster.Narrators(Language)[0].VoiceId;
        VoiceCast cast = CastCharacters.Cast(
            characters, CharacterRoster.Choices(speakers, narrator), CharacterRoster.For(Language), narrator, c => FlavourGuess.Guess(c.Name, LanguageRules.English),
            StoryCast.PinnedCharacters(stored), animalOf: c => animals.TryGetValue(c.Id, out AnimalKind kind) ? AnimalCharacters.HintFor(kind, c.Name, story.Language) : null);
        foreach (Character character in characters)
        {
            VoiceFlavour flavour = cast.Flavours![character.Id];
            string voiceId = cast.VoiceFor(character.Id)!;
            StorytellerCharacter? played = CharacterRoster.Nearest(speakers, voiceId, flavour, character.Gender);
            CastMember storedMember = story.Cast.Single(m => m.CharacterId == character.Id);
            yield return new
            {
                name = character.Name, voiceId, flavour = flavour.ToString(), rosterId = played?.Id,
                storedVoiceId = storedMember.VoiceId, storedFlavour = (storedMember.Flavour ?? VoiceFlavour.Normal).ToString(),
            };
        }
    }
}
