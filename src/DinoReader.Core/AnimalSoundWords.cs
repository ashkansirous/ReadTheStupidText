using ReadTheStupidText.Application.Reading;

namespace DinoReader.Core;

/// <summary>
/// One language's animal words and sound words (Slice 117, Decision 123): for each animal the names that make a character that animal, the sound it says
/// ("Baa!", written for the language) and any other spellings of that sound a story may already use. Each table line is
/// <c>Animal|name, name|Sound|other spelling, other spelling</c>; the sound's first word is always counted as a spelling too. An animal with no sound (a rabbit) leaves the sound empty. Every
/// <see cref="AppLanguage"/> has a table (a test checks it); an unknown language reads with English.
/// </summary>
internal sealed class AnimalSoundWords
{
    private const char FieldSeparator = '|';
    private const int NameField = 1;
    private const int SoundField = 2;
    private const int StemField = 3;

    /// <summary>One animal in one language.</summary>
    internal sealed record Entry(AnimalKind Kind, IReadOnlyList<string> Names, string Sound, IReadOnlyList<string> Stems);

    internal IReadOnlyList<Entry> Entries { get; }

    private AnimalSoundWords(IReadOnlyList<Entry> entries) => Entries = entries;

    /// <summary>The table of a story or spoken language code ("fa", "pt-BR", ...); English for an unknown code.</summary>
    public static AnimalSoundWords For(string? language)
    {
        AppLanguage which = AppLanguages.TryParse(RuleSets.PrimaryCode(language), out AppLanguage parsed) ? parsed : AppLanguage.English;
        return Tables.GetOrAdd(which, w => Parse(AnimalSoundTables.TableOf(w)));
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<AppLanguage, AnimalSoundWords> Tables = new();

    internal static AnimalSoundWords Parse(string table)
    {
        var entries = new List<Entry>();
        foreach (string line in table.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] fields = line.Split(FieldSeparator, StringSplitOptions.TrimEntries);
            AnimalKind kind = Enum.Parse<AnimalKind>(fields[0]);
            string sound = fields.Length > SoundField ? fields[SoundField] : string.Empty;
            var stems = new List<string>(fields.Length > StemField ? RuleSets.List(fields[StemField]) : []);
            if (sound.Length > 0)
            {
                stems.Add(AnimalSounds.Words(sound)[0]);
            }

            entries.Add(new Entry(kind, RuleSets.List(fields[NameField]), sound, stems));
        }

        // Tried in AnimalKind order, so a more specific animal wins.
        return new AnimalSoundWords(entries.OrderBy(e => e.Kind).ToList());
    }
}
