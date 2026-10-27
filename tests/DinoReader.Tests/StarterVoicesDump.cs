using System.Text;
using DinoReader.Core;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using Xunit;

namespace DinoReader.Tests;

/// <summary>
/// A generator for the writers, run with <c>DINO_DUMP_VOICES=1</c> (Slice 156): casts every starter and store book the way the app's picker does today in English (the Kokoro roster) and writes
/// the result as C# lines in the shape of <see cref="StarterVoices"/>, as a starting point for the <c>dino-story-writer</c> pass, which replaces each mechanical choice with one made for the character
/// before the book is added to <c>StarterVoices.AuthoredBooks</c>. Nothing it writes ships by itself. The output path is <c>DINO_DUMP_VOICES_FILE</c>.
/// </summary>
public class StarterVoicesDump
{
    private const string Switch = "DINO_DUMP_VOICES";
    private const string FileVariable = "DINO_DUMP_VOICES_FILE";
    private const string NarratorId = "kokoro:af_heart";

    [Fact]
    public async Task Dump_the_mechanical_voices()
    {
        if (Environment.GetEnvironmentVariable(Switch) != "1")
        {
            return;
        }

        var books = new List<(string Title, Story Story)>();
        string scratch = Path.Combine(Path.GetTempPath(), "dino-voices-" + Guid.NewGuid().ToString("N"));
        try
        {
            var library = new FileStoryLibrary(scratch);
            books.AddRange((await StarterBuilder.BuildAllAsync(library)).Select(b => (b.Story.Title, b.Story)).Where(b => !StoreTexts.All.Any(s => s.Id == b.Story.Id)));
        }
        finally
        {
            if (Directory.Exists(scratch))
            {
                Directory.Delete(scratch, recursive: true);
            }
        }

        books.AddRange(StoreTexts.All.Select(s => (s.Title, StoreBuilder.Build(s))));
        var text = new StringBuilder();
        foreach ((string title, Story story) in books)
        {
            text.Append(Dump(title, story));
        }

        await File.WriteAllTextAsync(Environment.GetEnvironmentVariable(FileVariable) ?? Path.Combine(Path.GetTempPath(), "dino-voices.txt"), text.ToString());
    }

    private static string Dump(string title, Story story)
    {
        var rules = LanguageRules.English;
        AttributionResult attribution = new SpeakerAttributor(rules).Attribute(story.Text, new StorySegmenter(rules).Split(story.Text, story.BreakMap()));
        IReadOnlyList<StorytellerCharacter> roster = CharacterRoster.For("en");
        IReadOnlyList<StorytellerCharacter> choices = CharacterRoster.Choices(roster.Where(c => c.Kind == CharacterKind.Speaker).ToList(), NarratorId);
        var animals = story.Cast.Where(m => m.Animal is not null).ToDictionary(m => m.CharacterId, m => m.Animal!.Value);
        VoiceCast cast = CastCharacters.Cast(
            attribution.Characters, choices, roster, NarratorId, c => FlavourGuess.Guess(c.Name, rules),
            animalOf: c => animals.TryGetValue(c.Id, out AnimalKind kind) ? AnimalCharacters.HintFor(kind, c.Name, "en") : null);
        var text = new StringBuilder($"// {title} ({story.Id})\n");
        foreach (CastMember member in story.Cast)
        {
            string? voice = cast.VoiceFor(member.CharacterId);
            VoiceFlavour? flavour = cast.Flavours?.GetValueOrDefault(member.CharacterId);
            StorytellerCharacter? picked = roster.FirstOrDefault(c => c.Kind == CharacterKind.Speaker && c.VoiceId == voice && c.Flavour == flavour);
            text.Append(Line(member, picked));
        }

        return text.ToString();
    }

    private static string Line(CastMember member, StorytellerCharacter? picked)
    {
        string id = picked?.Id ?? "MISSING";
        CharacterAge age = CharacterTraitMatch.AgeOf(id) ?? CharacterAge.Adult;
        string being = member.Animal is not null || CharacterTraitMatch.BeingOf(id) is null ? "null" : "CharacterBeing." + CharacterTraitMatch.BeingOf(id);
        string qualities = string.Join(", ", CharacterTraitMatch.QualitiesOf(id).Select(q => "VoiceQuality." + q));
        return $"    new(\"{member.Name}\", \"{id}\", Gender.{member.Gender}, CharacterAge.{age}, {being}, [{qualities}]),\n";
    }
}
