using System.Text.RegularExpressions;

namespace DinoReader.Core;

/// <summary>
/// Guesses a character's gender from, in order: a title (Mr, Queen, ...), a known first
/// name, then the pronouns that follow the name in the story. Anything else is Unknown.
/// </summary>
public sealed class GenderGuesser
{
    private const int PronounWindowChars = 100;
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
        return byName != Gender.Unknown ? byName : FromPronouns(name, story);
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
            var window = PronounWindow(story, end);
            foreach (Match w in Regex.Matches(window, @"\p{L}+"))
            {
                if (_rules.MalePronouns.Contains(w.Value)) male++;
                else if (_rules.FemalePronouns.Contains(w.Value)) female++;
            }
        }
        if (male == female) return Gender.Unknown;
        return male > female ? Gender.Male : Gender.Female;
    }

    private string PronounWindow(StoryNames story, int from)
    {
        var text = story.Text;
        int end = Math.Min(text.Length, from + PronounWindowChars);
        for (int i = from; i < end; i++)
            if (_rules.IsQuote(text[i])) { end = i; break; }
        var next = story.FirstFrom(from, end);
        return text[from..(next?.Start ?? end)];
    }
}
