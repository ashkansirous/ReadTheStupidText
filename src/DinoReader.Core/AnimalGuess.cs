using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using ReadTheStupidText.Application.Reading;

namespace DinoReader.Core;

/// <summary>
/// A best guess of a character's animal from the story text (Slice 125, Decision 132), for a character whose name has no animal word ("Pip", "Pim"). Pure and
/// deterministic, and written on each language's own animal words (<see cref="AnimalSoundWords"/>). Two things count, both inside the narration (never speech):
/// an animal word right next to the name ("Pip the pig", "a pig called Pip", "the smallest pig, Pip": nothing between them, or a comma, or an article or "called"),
/// or a group introduction: a list of two or more names ("Pip, Pop and Pim") that comes right after a plural animal word ("three little pigs: Pip, Pop and Pim",
/// "...pigs called Pip, Pop and Pim"), right before one ("Pip, Pop and Pim were three little pigs"), or after a "their names were" lead in the sentence that follows one.
/// It prefers to miss a guess than to make a wrong one, and the grown-up can change it on the cast screen.
/// </summary>
public static class AnimalGuess
{
    private const int MaxCharsBetweenUnspaced = 4;
    private const int MaxCharsInListUnspaced = 2;
    private const int MaxNamePatterns = 512;
    private const int MaxWordsWithComma = 2;
    private const int MaxGroupLeadWords = 3;
    private const int MinGroup = 2;
    private const string PluralEndGroup = "end";

    private static readonly Regex SentenceEnd = new(@"(?<=[.!?…。！？؟])\s*|\n+", RegexOptions.Compiled);
    // What people say ("We must sell the cow, Jack,") is not the narrator describing them; the guess reads the narration only.
    private static readonly Regex Spoken = new("\"[^\"]*\"|“[^”]*”|«[^»]*»|「[^」]*」|„[^“”]*[“”]", RegexOptions.Compiled);
    private static readonly Regex Letters = new(@"[\p{L}\p{M}\p{N}]+", RegexOptions.Compiled);

    // The compiled patterns are built once per language and per name, not on every call (a long book is guessed over on each render).
    private static readonly ConcurrentDictionary<string, IReadOnlyList<AnimalPattern>> AnimalPatterns = new();
    private static readonly ConcurrentDictionary<(string Name, bool Unspaced), Regex> NamePatterns = new();

    // PluralOnly: the word can only be a plural ("pigs", "wolves"), so it cannot describe one name; "sheep" is plural and singular alike.
    private sealed record Mention(int Start, int End, bool Plural, bool PluralOnly = false);

    private sealed record AnimalPattern(AnimalKind Kind, Regex Pattern, bool AlwaysPlural, bool FormOnlyPlural = false, bool EndingOnlyPlural = false);

    /// <summary>What the guess knows about the language of the text.</summary>
    private sealed record Reading(bool Unspaced, IReadOnlyList<AnimalPattern> Animals, HashSet<string> Links, HashSet<string> Cues, HashSet<string> Conjunctions, HashSet<string> Prepositions);

    /// <summary>The animal of each of <paramref name="characters"/> the story text says, by character id (the characters it says nothing about are left out).</summary>
    public static IReadOnlyDictionary<string, AnimalKind> Guess(IReadOnlyList<Character> characters, string text, string? language)
    {
        var found = new Dictionary<string, AnimalKind>();
        if (characters.Count == 0 || string.IsNullOrWhiteSpace(text))
        {
            return found;
        }

        bool unspaced = LanguageRules.For(language).Script == ScriptStyle.Unspaced;
        var reading = new Reading(unspaced, PatternsFor(language, unspaced), AnimalGuessWords.Links(language), AnimalGuessWords.Cues(language), AnimalGuessWords.Conjunctions(language), AnimalGuessWords.Prepositions(language));
        var names = characters.Select(c => (Character: c, Pattern: NamePattern(c.Name.Trim(), unspaced))).ToList();
        string previous = string.Empty;
        foreach (string sentence in SentenceEnd.Split(WithoutSpeech(text)).Where(s => s.Length > 0))
        {
            GuessSentence(sentence, previous, names, reading, found);
            previous = sentence;
        }

        return found;
    }

    // Names are arbitrary text, so their patterns are kept only while few (the cache is emptied rather than grown for the life of the process).
    private static Regex NamePattern(string name, bool unspaced)
    {
        if (NamePatterns.Count > MaxNamePatterns)
        {
            NamePatterns.Clear();
        }

        return NamePatterns.GetOrAdd((name, unspaced), k => WholeWord(k.Name, k.Unspaced, string.Empty));
    }

    private static string WithoutSpeech(string text)
    {
        return Spoken.Replace(text, m => new string(' ', m.Length));
    }

    private static void GuessSentence(
        string sentence, string previous, List<(Character Character, Regex Pattern)> names, Reading reading, Dictionary<string, AnimalKind> found)
    {
        var named = names.Select(n => (n.Character, Mention: MentionIn(n.Pattern, sentence))).Where(n => n.Mention is not null)
            .Select(n => (n.Character, Mention: n.Mention!)).OrderBy(n => n.Mention.Start).ToList();
        if (named.Count == 0)
        {
            return;
        }

        var animalMentions = AnimalMentions(sentence, reading.Animals);
        var singular = animalMentions.Where(a => !a.Mention.PluralOnly).ToList();
        var allNames = named.Select(n => n.Mention).ToList();
        foreach (var (character, mention) in named.Where(n => !found.ContainsKey(n.Character.Id)))
        {
            AnimalKind? next = NearbyAnimal(sentence, mention, singular, allNames, reading);
            if (next is { } kind)
            {
                found[character.Id] = kind;
            }
        }

        GuessGroups(sentence, previous, named, animalMentions, reading, found);
    }

    // An introduction of several characters at once: each list of names that stands right next to a plural animal word takes its animal.
    private static void GuessGroups(
        string sentence, string previous, List<(Character Character, Mention Mention)> named, List<(AnimalKind Kind, Mention Mention)> here,
        Reading reading, Dictionary<string, AnimalKind> found)
    {
        var plural = here.Where(a => a.Mention.Plural || reading.Unspaced).ToList();
        foreach (var run in ListRuns(sentence, named, reading).Where(r => r.Count >= MinGroup && r.Any(n => !found.ContainsKey(n.Character.Id))))
        {
            AnimalKind? kind = GroupAnimal(sentence, run, plural, previous, reading);
            if (kind is not { } animal)
            {
                continue;
            }

            foreach (var (character, _) in run.Where(n => !found.ContainsKey(n.Character.Id)))
            {
                found[character.Id] = animal;
            }
        }
    }

    // The names that follow each other as a list ("Pip, Pop and Pim"): a comma or a conjunction between two of them.
    private static List<List<(Character Character, Mention Mention)>> ListRuns(
        string sentence, List<(Character Character, Mention Mention)> named, Reading reading)
    {
        var runs = new List<List<(Character Character, Mention Mention)>>();
        foreach (var name in named)
        {
            bool continues = runs.Count > 0 && name.Mention.Start >= runs[^1][^1].Mention.End
                && IsListGap(sentence[runs[^1][^1].Mention.End..name.Mention.Start], reading);
            if (continues)
            {
                runs[^1].Add(name);
            }
            else
            {
                runs.Add([name]);
            }
        }

        return runs;
    }

    // Only a comma, "&" or a conjunction joins names in a list: "Jack hugged Jill, the cows laughed" is two things, not a list.
    private static bool IsListGap(string gap, Reading reading)
    {
        if (HasStop(gap))
        {
            return false;
        }

        if (reading.Unspaced)
        {
            return gap.Length <= MaxCharsInListUnspaced;
        }

        var words = Letters.Matches(gap);
        return words.Count == 0 || (words.Count == 1 && reading.Conjunctions.Contains(words[0].Value));
    }

    private static AnimalKind? GroupAnimal(
        string sentence, List<(Character Character, Mention Mention)> run, List<(AnimalKind Kind, Mention Mention)> plural, string previous, Reading reading)
    {
        int first = run[0].Mention.Start;
        int last = run[^1].Mention.End;
        AnimalKind? best = null;
        int bestDistance = int.MaxValue;
        foreach (var (kind, animal) in plural)
        {
            bool before = animal.End <= first;
            if (!before && animal.Start < last)
            {
                continue;
            }

            string gap = before ? sentence[animal.End..first] : sentence[last..animal.Start];
            if (gap.Length < bestDistance && IsGroupGap(gap, before, reading))
            {
                best = kind;
                bestDistance = gap.Length;
            }
        }

        return best ?? FromPreviousSentence(sentence[..first], previous, reading);
    }

    // "Three little pigs lived in a wood. Their names were Pip, Pop and Pim.": the lead of the list says they are names, and the sentence before has the plural animal.
    private static AnimalKind? FromPreviousSentence(string lead, string previous, Reading reading)
    {
        int words = Letters.Matches(lead).Count;
        if (reading.Unspaced || previous.Length == 0 || words == 0 || words > MaxGroupLeadWords || !EndsWithCue(lead, reading) || HasStop(lead))
        {
            return null;
        }

        var plural = AnimalMentions(previous, reading.Animals).Where(a => a.Mention.Plural).ToList();
        return plural.Count == 0 ? null : plural[^1].Kind;
    }

    // Between a plural animal word and a list of names: nothing or a comma, or a "names were" cue. The cue stands next to the names (the last word when the animal comes first,
    // the first word when the names do), so a verb or another clause ("The pigs saw Jack and Jill", "Mia and Leo were near cats") is not one.
    private static bool IsGroupGap(string gap, bool animalFirst, Reading reading)
    {
        if (HasStop(gap))
        {
            return false;
        }

        if (reading.Unspaced)
        {
            return gap.Length <= MaxCharsBetweenUnspaced;
        }

        var words = Letters.Matches(gap).Select(w => w.Value).ToList();
        if (words.Count == 0)
        {
            return true;
        }

        if (words.Count > MaxGroupLeadWords)
        {
            return false;
        }

        return animalFirst ? reading.Cues.Contains(words[^1]) : reading.Cues.Contains(words[0]) && !words.Skip(1).Any(reading.Prepositions.Contains);
    }

    private static bool EndsWithCue(string text, Reading reading)
    {
        var last = Letters.Matches(text).LastOrDefault();
        return last is not null && reading.Cues.Contains(last.Value);
    }

    private static AnimalKind? NearbyAnimal(
        string sentence, Mention name, List<(AnimalKind Kind, Mention Mention)> animals, List<Mention> allNames, Reading reading)
    {
        AnimalKind? best = null;
        int bestGap = int.MaxValue;
        foreach (var (kind, animal) in animals)
        {
            int? gap = GapIfClose(sentence, name, animal, allNames, reading);
            if (gap is { } size && size < bestGap && !AnotherNameIsAsClose(sentence, name, animal, allNames, reading, size))
            {
                best = kind;
                bestGap = size;
            }
        }

        return best;
    }

    // The animal that belongs to a neighbouring name ("Pip the pig and Pop the cow": the pig is Pip's) is no candidate for this one.
    private static bool AnotherNameIsAsClose(string sentence, Mention name, Mention animal, List<Mention> allNames, Reading reading, int gap)
    {
        return allNames.Where(n => n != name).Any(n => GapIfClose(sentence, n, animal, allNames, reading) is { } other && other <= gap);
    }

    // The length of the text between a name and an animal word when they describe each other, else null.
    private static int? GapIfClose(string sentence, Mention name, Mention animal, List<Mention> allNames, Reading reading)
    {
        if (allNames.Any(o => animal.Start < o.End && o.Start < animal.End))
        {
            return null;
        }

        int start = Math.Min(name.End, animal.End);
        int end = Math.Max(name.Start, animal.Start);
        if (end < start || allNames.Any(n => n.Start >= start && n.End <= end))
        {
            return null;
        }

        string between = sentence[start..end];
        return IsClose(between, reading) ? between.Length : null;
    }

    // Nothing between them, a comma, or one article or "called": a verb ("The wolf chased Pip", "Mia loves cats") is not a description.
    private static bool IsClose(string between, Reading reading)
    {
        // A possessive ("Jack's cow") or a quote or a stop between them is not a description either.
        if (HasStop(between) || between.Any(c => c is '\'' or '’'))
        {
            return false;
        }

        if (reading.Unspaced)
        {
            return between.Length <= MaxCharsBetweenUnspaced;
        }

        var words = Letters.Matches(between);
        bool comma = between.Any(c => c is ',' or '،');
        return words.Count == 0 || (comma && words.Count <= MaxWordsWithComma) || (words.Count == 1 && reading.Links.Contains(words[0].Value));
    }

    // A stop, a quote or a colon between two words: they are not one description.
    private static bool HasStop(string text)
    {
        return text.Any(c => c is ';' or ':' or '.' or '!' or '?' or '"' or '“' or '”' or '«' or '»');
    }

    private static List<(AnimalKind Kind, Mention Mention)> AnimalMentions(string sentence, IReadOnlyList<AnimalPattern> animals)
    {
        var mentions = new List<(AnimalKind, Mention)>();
        foreach (var (kind, pattern, alwaysPlural, formOnlyPlural, endingOnlyPlural) in animals)
        {
            foreach (Match match in pattern.Matches(sentence))
            {
                bool ending = match.Groups[PluralEndGroup].Length > 0;
                mentions.Add((kind, new Mention(match.Index, match.Index + match.Length, alwaysPlural || ending, formOnlyPlural || (ending && endingOnlyPlural))));
            }
        }

        return mentions.OrderBy(m => m.Item2.Start).ToList();
    }

    private static Mention? MentionIn(Regex pattern, string sentence)
    {
        Match match = pattern.Match(sentence);
        return match.Success ? new Mention(match.Index, match.Index + match.Length, false) : null;
    }

    private static IReadOnlyList<AnimalPattern> PatternsFor(string? language, bool unspaced)
    {
        string key = RuleSets.PrimaryCode(language);
        return AnimalPatterns.GetOrAdd(key, _ => BuildPatterns(language, unspaced));
    }

    private static List<AnimalPattern> BuildPatterns(string? language, bool unspaced)
    {
        string endings = string.Join('|', AnimalGuessWords.PluralEndings(language));
        var irregular = AnimalGuessWords.IrregularPlurals(language);
        bool endingsOnlyPlural = AnimalGuessWords.EndingsAreOnlyPlural(language);
        var patterns = new List<AnimalPattern>();
        foreach (var entry in AnimalSoundWords.For(language).Entries)
        {
            patterns.AddRange(entry.Names.Select(n => new AnimalPattern(entry.Kind, WholeWord(n, unspaced, endings), irregular.ContainsKey(n.Trim()), EndingOnlyPlural: endingsOnlyPlural)));
            patterns.AddRange(irregular.Where(i => i.Value == entry.Kind && !entry.Names.Contains(i.Key, StringComparer.OrdinalIgnoreCase))
                .Select(i => new AnimalPattern(entry.Kind, WholeWord(i.Key, unspaced, string.Empty), AlwaysPlural: true, FormOnlyPlural: true)));
        }

        return patterns;
    }

    // A spaced language matches whole words (an animal word may carry a plural ending); a language without spaces matches the text as it stands.
    private static Regex WholeWord(string word, bool unspaced, string endings)
    {
        string escaped = Regex.Escape(word.Trim());
        if (unspaced)
        {
            return new Regex(escaped, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        string ending = endings.Length > 0 ? "(?<" + PluralEndGroup + ">" + endings + ")?" : string.Empty;
        return new Regex(@"(?<![\p{L}\p{M}\p{N}])" + escaped + ending + @"(?![\p{L}\p{M}\p{N}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
