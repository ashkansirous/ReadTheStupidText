using System.Text.RegularExpressions;

namespace DinoReader.Core;

/// <summary>
/// Guesses a character's gender from, in order: a title (Mr, Queen, ...), a known first
/// name, then the pronouns that follow the name in the story. Anything else is Unknown.
/// </summary>
public sealed class GenderGuesser
{
    private const int PronounWindowChars = 100;
    private const int MinEndingNameLength = 3;
    private readonly LanguageRules _rules;
    private readonly NameExtractor _names;

    public GenderGuesser(LanguageRules rules) : this(rules, new NameExtractor(rules)) { }

    internal GenderGuesser(LanguageRules rules, NameExtractor names)
    {
        _rules = rules;
        _names = names;
    }

    public Gender Guess(string name, string storyText) =>
        Guess(name, _names.ForStory(storyText));

    internal Gender Guess(string name, StoryNames story)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.TrimEnd('.')).ToArray();
        if (words.Length == 0) return Gender.Unknown;
        var byTitle = FromTitle(words[0]);
        if (byTitle != Gender.Unknown) return byTitle;
        var byName = FromFirstName(words.SkipWhile(IsTitle).ToArray());
        if (byName != Gender.Unknown) return byName;
        var byCues = FromPronouns(name, story);
        return byCues != Gender.Unknown ? byCues : FromEnding(words[^1]);
    }

    // A last resort for languages whose name endings carry gender (Spanish "-a"/"-o", Russian "-а"/"-я").
    private Gender FromEnding(string word)
    {
        if (word.Length < MinEndingNameLength) return Gender.Unknown;
        if (_rules.FemaleEndings.Any(e => word.EndsWith(e, StringComparison.OrdinalIgnoreCase))) return Gender.Female;
        return _rules.MaleEndings.Any(e => word.EndsWith(e, StringComparison.OrdinalIgnoreCase)) ? Gender.Male : Gender.Unknown;
    }

    private bool IsTitle(string word) =>
        _rules.MaleTitles.Contains(word) || _rules.FemaleTitles.Contains(word)
        || _rules.TitleAbbreviations.Contains(word);

    private Gender FromTitle(string word)
    {
        if (_rules.MaleTitles.Contains(word)) return Gender.Male;
        return _rules.FemaleTitles.Contains(word) ? Gender.Female : Gender.Unknown;
    }

    private Gender FromFirstName(string[] words)
    {
        if (words.Length == 0) return Gender.Unknown;
        var byFirst = FromTable(words[0]);
        if (byFirst != Gender.Unknown || words.Length == 1) return byFirst;
        return FromTable(string.Concat(words));
    }

    private Gender FromTable(string word)
    {
        if (_rules.MaleNames.Contains(word)) return Gender.Male;
        return _rules.FemaleNames.Contains(word) ? Gender.Female : Gender.Unknown;
    }

    private Gender FromPronouns(string name, StoryNames story)
    {
        int male = 0, female = 0;
        foreach (int end in story.OccurrenceEnds(name))
        {
            var (m, f) = CountCues(PronounWindow(story, end));
            var (bm, bf) = VerbBefore(story.Text, end - name.Length);
            male += m + bm;
            female += f + bf;
        }
        if (male == female) return Gender.Unknown;
        return male > female ? Gender.Male : Gender.Female;
    }

    // A gendered speech verb right before the name ("сказала Мария", Arabic "قالت مريم"); a terminator or quote in between breaks the link.
    private (int Male, int Female) VerbBefore(string text, int nameStart)
    {
        if (_rules.Script == ScriptStyle.Unspaced) return (0, 0);
        int end = nameStart;
        while (end > 0 && char.IsWhiteSpace(text[end - 1])) end--;
        int start = end;
        while (start > 0 && char.IsLetter(text[start - 1])) start--;
        var word = text[start..end];
        return (_rules.GenderedMaleVerbs.Contains(word) ? 1 : 0, _rules.GenderedFemaleVerbs.Contains(word) ? 1 : 0);
    }

    // Pronouns and gendered speech verbs (Russian "сказала", Arabic "قالت") in a stretch of text.
    private (int Male, int Female) CountCues(string window) =>
        _rules.Script == ScriptStyle.Unspaced ? CountInUnspaced(window) : CountInWords(window);

    private (int Male, int Female) CountInWords(string window)
    {
        int male = 0, female = 0;
        var words = Regex.Matches(window, @"[\p{L}‌]+");
        for (int i = 0; i < words.Count; i++)
        {
            string word = words[i].Value;
            if (_rules.MalePronouns.Contains(word) || _rules.GenderedMaleVerbs.Contains(word)) male++;
            else if (IsSingularFemaleCue(words, i)) female++;
        }
        return (male, female);
    }

    // A female pronoun that is also "they" (German "sie") counts only when the next word is not a plural verb ("sie lachten").
    private bool IsSingularFemaleCue(MatchCollection words, int i)
    {
        string word = words[i].Value;
        if (_rules.GenderedFemaleVerbs.Contains(word)) return true;
        if (!_rules.FemalePronouns.Contains(word)) return false;
        return i + 1 >= words.Count || !_rules.IsPluralVerb(words[i + 1].Value);
    }

    // No spaces: count the female forms first and take them out, so Japanese "彼女" is not also read as "彼".
    private (int Male, int Female) CountInUnspaced(string window)
    {
        int female = TakeOut(ref window, _rules.FemalePronouns);
        int male = TakeOut(ref window, _rules.MalePronouns);
        return (male, female);
    }

    private static int TakeOut(ref string window, IReadOnlySet<string> words)
    {
        int count = 0;
        foreach (var word in words.OrderByDescending(w => w.Length))
        {
            count += (window.Length - window.Replace(word, "", StringComparison.Ordinal).Length) / word.Length;
            window = window.Replace(word, " ", StringComparison.Ordinal);
        }
        return count;
    }

    private string PronounWindow(StoryNames story, int from)
    {
        var text = story.Text;
        int end = Math.Min(text.Length, from + PronounWindowChars);
        for (int i = from; i < end; i++)
            if (_rules.IsQuote(text[i]) || _rules.IsDialogueDash(text, i)) { end = i; break; }
        var next = story.FirstFrom(from, end);
        return text[from..(next?.Start ?? end)];
    }
}
