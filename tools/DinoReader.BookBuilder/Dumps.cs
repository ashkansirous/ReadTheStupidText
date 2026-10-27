using System.Text;
using System.Text.Json;
using DinoReader.Core;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader.BookBuilder;

/// <summary>
/// <c>dump-voices</c> (Slice 156): a generator for the writers. Casts every starter and store book the way the app's picker does today in English (the Kokoro roster) and writes the result as
/// <c>cast</c> entries in the shape of a book's <c>book.json</c>, as a starting point for the <c>dino-story-writer</c> pass, which replaces each mechanical choice with one made for the
/// character before it is pasted into that book's <c>cast</c>; <c>AuthoredVoiceTests</c> then requires every character of an authored book to have traits and a known suggestion.
/// Nothing it writes ships by itself.
/// </summary>
internal static class VoiceDump
{
    private const string NarratorId = "kokoro:af_heart";
    private const string Language = "en";

    public static async Task<int> RunAsync(string? outPath)
    {
        var books = new List<(string Title, Story Story)>();
        await StarterBuilder.InScratchAsync(async library =>
            books.AddRange((await StarterBuilder.BuildAllAsync(library)).Select(b => (b.Story.Title, b.Story))));
        books.AddRange(Books.Store.Select(s => (s.Title, StoreBuilder.Build(s))));
        var text = new StringBuilder();
        foreach ((string title, Story story) in books)
        {
            text.Append(Dump(title, story));
        }

        string path = outPath ?? Path.Combine(Path.GetTempPath(), "dino-voices.txt");
        await File.WriteAllTextAsync(path, text.ToString());
        Console.WriteLine($"wrote {path}");
        return 0;
    }

    private static string Dump(string title, Story story)
    {
        var rules = LanguageRules.English;
        AttributionResult attribution = new SpeakerAttributor(rules).Attribute(story.Text, new StorySegmenter(rules).Split(story.Text, story.BreakMap()));
        IReadOnlyList<StorytellerCharacter> roster = CharacterRoster.For(Language);
        IReadOnlyList<StorytellerCharacter> choices = CharacterRoster.Choices(roster.Where(c => c.Kind == CharacterKind.Speaker).ToList(), NarratorId);
        var animals = story.Cast.Where(m => m.Animal is not null).ToDictionary(m => m.CharacterId, m => m.Animal!.Value);
        VoiceCast cast = CastCharacters.Cast(
            attribution.Characters, choices, roster, NarratorId, c => FlavourGuess.Guess(c.Name, rules),
            animalOf: c => animals.TryGetValue(c.Id, out AnimalKind kind) ? AnimalCharacters.HintFor(kind, c.Name, Language) : null);
        var lines = new List<AuthoredCharacter>();
        foreach (CastMember member in story.Cast)
        {
            string? voice = cast.VoiceFor(member.CharacterId);
            VoiceFlavour? flavour = cast.Flavours?.GetValueOrDefault(member.CharacterId);
            lines.Add(Entry(member, roster.FirstOrDefault(c => c.Kind == CharacterKind.Speaker && c.VoiceId == voice && c.Flavour == flavour)));
        }

        return $"// {title} ({story.Id})\n{JsonSerializer.Serialize(lines, Books.JsonOptions)}\n";
    }

    private static AuthoredCharacter Entry(CastMember member, StorytellerCharacter? picked)
    {
        string id = picked?.Id ?? "MISSING";
        CharacterAge age = CharacterTraitMatch.AgeOf(id) ?? CharacterAge.Adult;
        CharacterBeing? being = member.Animal is not null ? null : CharacterTraitMatch.BeingOf(id);
        return new AuthoredCharacter(member.Name, id, member.Gender, age, being, CharacterTraitMatch.QualitiesOf(id).ToArray());
    }
}

/// <summary>
/// <c>voice-listen --out &lt;file&gt;</c> (Slice 153): writes the English roster (voice, flavour shape) and the Ugly Duckling cast as the app builds it to a JSON file for <c>tools/voice-listen</c>.
/// </summary>
internal static class VoiceListenExport
{
    private const string Language = "en";
    private const string UglyDucklingStem = "ugly-duckling";

    public static int Run(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var export = new
        {
            roster = CharacterRoster.For(Language).Select(RosterRow).ToList(),
            uglyDuckling = UglyDucklingCast().ToList(),
        };
        File.WriteAllText(path, JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"wrote {path}");
        return 0;
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

    // Cast the way the app casts a story from the roster: the roster character each one is played by.
    private static IEnumerable<object> UglyDucklingCast()
    {
        Story story = StoreBuilder.Build(Books.SpecOf(UglyDucklingStem));
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
