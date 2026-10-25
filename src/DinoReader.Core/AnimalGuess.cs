using System.Text.RegularExpressions;

namespace DinoReader.Core;

/// <summary>
/// A best guess of a character's animal from the story text (Slice 125, Decision 132), for a character whose name has no animal word ("Pip", "Pim"). Pure and
/// deterministic, and written on each language's own animal words (<see cref="AnimalSoundWords"/>). Two things count, both inside one sentence:
/// an animal word right next to the name ("Pip the pig", "a pig called Pip", "the smallest pig, Pim": at most two words between), or a group
/// introduction ("three little pigs ... Pip, Pop and Pim": a plural animal word in that sentence before the names, or in the sentence before it, and two or
/// more undecided names in the sentence). It is only a guess: the grown-up can change it on the cast screen.
/// </summary>
public static class AnimalGuess
{
    private const int MaxWordsBetween = 1;
    private const int MaxWordsBetweenWithComma = 2;
    private const int MaxCharsBetweenUnspaced = 4;
    private const int MinGroup = 2;

    // The endings that make an animal word a plural in the spaced languages that add one ("pig" and "pigs", "kanin" and "kaniner"). A word that only starts the same way
    // ("pigeon") is not one.
    private static readonly string[] PluralEndings = ["s", "es", "en", "er", "ar", "ne", "n", "ner"];

    private static readonly Regex SentenceEnd = new(@"(?<=[.!?…。！？؟])\s*|\n+", RegexOptions.Compiled);
    // What people say ("We must sell the cow, Jack,") is not the narrator describing them; the guess reads the narration only.
    private static readonly Regex Spoken = new("\"[^\"]*\"|“[^”]*”|«[^»]*»|「[^」]*」|„[^“”]*[“”]", RegexOptions.Compiled);
    private static readonly Regex Letters = new(@"[\p{L}\p{M}\p{N}]+", RegexOptions.Compiled);

    private sealed record Mention(int Start, int End, bool Plural);

    /// <summary>The animal of each of <paramref name="characters"/> the story text says, by character id (the characters it says nothing about are left out).</summary>
    public static IReadOnlyDictionary<string, AnimalKind> Guess(IReadOnlyList<Character> characters, string text, string? language)
    {
        var found = new Dictionary<string, AnimalKind>();
        if (characters.Count == 0 || string.IsNullOrWhiteSpace(text))
        {
            return found;
        }

        bool unspaced = LanguageRules.For(language).Script == ScriptStyle.Unspaced;
        var animalPatterns = AnimalSoundWords.For(language).Entries
            .SelectMany(e => e.Names.Select(n => (e.Kind, Pattern: PatternOf(n, unspaced, plural: true))))
            .ToList();
        var namePatterns = characters.Select(c => (Character: c, Pattern: PatternOf(c.Name, unspaced, plural: false))).ToList();
        string previous = string.Empty;
        foreach (string sentence in SentenceEnd.Split(WithoutSpeech(text)).Where(s => s.Length > 0))
        {
            GuessSentence(sentence, previous, namePatterns, animalPatterns, unspaced, found);
            previous = sentence;
        }

        return found;
    }

    private static string WithoutSpeech(string text) => Spoken.Replace(text, m => new string(' ', m.Length));

    private static void GuessSentence(
        string sentence, string previous, List<(Character Character, Regex Pattern)> names, List<(AnimalKind Kind, Regex Pattern)> animals,
        bool unspaced, Dictionary<string, AnimalKind> found)
    {
        var named = names.Where(n => !found.ContainsKey(n.Character.Id)).Select(n => (n.Character, Mention: MentionIn(n.Pattern, sentence))).Where(n => n.Mention is not null).ToList();
        if (named.Count == 0)
        {
            return;
        }

        var animalMentions = AnimalMentions(sentence, animals);
        foreach (var (character, mention) in named)
        {
            AnimalKind? next = NearbyAnimal(sentence, mention!, animalMentions, named.Select(n => n.Mention!).ToList(), unspaced);
            if (next is { } kind)
            {
                found[character.Id] = kind;
            }
        }

        GuessGroup(sentence, previous, named, animalMentions, animals, unspaced, found);
    }

    // An introduction of several characters at once: the plural animal word comes before the first name, in this sentence or the one before.
    private static void GuessGroup(
        string sentence, string previous, List<(Character Character, Mention? Mention)> named, List<(AnimalKind Kind, Mention Mention)> here,
        List<(AnimalKind Kind, Regex Pattern)> animals, bool unspaced, Dictionary<string, AnimalKind> found)
    {
        var open = named.Where(n => !found.ContainsKey(n.Character.Id)).ToList();
        if (open.Count < MinGroup)
        {
            return;
        }

        int firstName = open.Min(n => n.Mention!.Start);
        var before = here.Where(a => a.Mention.End <= firstName).Concat(AnimalMentions(previous, animals)).Where(a => a.Mention.Plural || unspaced).ToList();
        if (before.Count == 0)
        {
            return;
        }

        AnimalKind kind = before.Last().Kind;
        foreach (var (character, _) in open)
        {
            found[character.Id] = kind;
        }
    }

    private static AnimalKind? NearbyAnimal(
        string sentence, Mention name, List<(AnimalKind Kind, Mention Mention)> animals, List<Mention> allNames, bool unspaced)
    {
        AnimalKind? best = null;
        int bestGap = int.MaxValue;
        foreach (var (kind, animal) in animals)
        {
            if (Overlaps(animal, allNames))
            {
                continue;
            }

            int start = Math.Min(name.End, animal.End);
            int end = Math.Max(name.Start, animal.Start);
            if (end < start)
            {
                continue;
            }

            string between = sentence[start..end];
            if (IsClose(between, unspaced) && !allNames.Any(n => n.Start >= start && n.End <= end) && between.Length < bestGap)
            {
                best = kind;
                bestGap = between.Length;
            }
        }

        return best;
    }

    private static bool IsClose(string between, bool unspaced)
    {
        // A possessive ("Jack's cow") or a quote or a stop between them is not a description.
        if (between.Any(c => c is ';' or ':' or '.' or '!' or '?' or '"' or '“' or '”' or '«' or '»' or '\'' or '’'))
        {
            return false;
        }

        if (unspaced)
        {
            return between.Length <= MaxCharsBetweenUnspaced;
        }

        int words = Letters.Matches(between).Count;
        return words <= MaxWordsBetween || (words <= MaxWordsBetweenWithComma && between.Any(c => c is ',' or '،'));
    }

    private static bool Overlaps(Mention mention, List<Mention> others) =>
        others.Any(o => mention.Start < o.End && o.Start < mention.End);

    private static List<(AnimalKind Kind, Mention Mention)> AnimalMentions(string sentence, List<(AnimalKind Kind, Regex Pattern)> animals)
    {
        var mentions = new List<(AnimalKind, Mention)>();
        foreach (var (kind, pattern) in animals)
        {
            foreach (Match match in pattern.Matches(sentence))
            {
                mentions.Add((kind, new Mention(match.Index, match.Index + match.Length, match.Groups["end"].Length > 0)));
            }
        }

        return mentions.OrderBy(m => m.Item2.Start).ToList();
    }

    private static Mention? MentionIn(Regex pattern, string sentence)
    {
        Match match = pattern.Match(sentence);
        return match.Success ? new Mention(match.Index, match.Index + match.Length, false) : null;
    }

    // A spaced language matches whole words (an animal word may carry a plural ending); a language without spaces matches the text as it stands.
    private static Regex PatternOf(string word, bool unspaced, bool plural)
    {
        string escaped = Regex.Escape(word.Trim());
        if (unspaced)
        {
            return new Regex(escaped, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        string ending = plural ? "(?<end>" + string.Join('|', PluralEndings) + ")?" : string.Empty;
        return new Regex(@"(?<![\p{L}\p{M}\p{N}])" + escaped + ending + @"(?![\p{L}\p{M}\p{N}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
