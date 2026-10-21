using System.Text.RegularExpressions;

namespace DinoReader.Core;

/// <summary>What a speaker tag named: a character, or only a gendered pronoun.</summary>
internal readonly record struct SpeakerTag(string? Name, Gender PronounGender);

/// <summary>
/// Reads speaker tags ("said Peter", "Peter said", "she whispered") from the narration
/// right after a quote (its first sentence) or right before it (its unfinished last
/// sentence).
/// </summary>
internal sealed class SpeakerTagParser
{
    private readonly LanguageRules _rules;
    private readonly NameExtractor _names;
    private readonly Regex _after;
    private readonly Regex _before;
    private readonly Regex _verbAtEnd;
    private readonly Regex _pronoun;

    public SpeakerTagParser(LanguageRules rules, NameExtractor names)
    {
        _rules = rules;
        _names = names;
        var verb = TagPatterns.Verb(rules);
        string? pron = TagPatterns.Pronouns(rules);
        var core = $"(?:{string.Join("|", CoreBranches(rules, names, verb, pron))})";
        var after = TagPatterns.AdverbAfter(rules);
        var separators = TagPatterns.TagSeparators;
        _after = new Regex($@"^[{separators}]*{core}", RegexOptions.CultureInvariant);
        _before = new Regex($@"{core}{after}[{separators}]*$", RegexOptions.CultureInvariant);
        _verbAtEnd = new Regex($@"{verb}{after}[{separators}]*$", RegexOptions.CultureInvariant);
        _pronoun = new Regex(pron ?? "(?!)", RegexOptions.CultureInvariant);
    }

    // The four tag shapes: "he said", "Peter said", "said he", "said Peter". A language
    // whose subject pronouns carry no gender (Farsi) has only the two name shapes.
    private static List<string> CoreBranches(LanguageRules rules, NameExtractor names, string verb, string? pron)
    {
        var gap = TagPatterns.SubjectVerbGap(rules);
        var adverb = TagPatterns.AdverbBefore(rules);
        var verbGap = TagPatterns.VerbSubjectGap(rules);
        var branches = new List<string>();
        if (pron is not null) branches.Add($"{rules.NameLead}(?<p>{pron}){gap}{adverb}{verb}");
        branches.Add($"{rules.NameLead}(?<n>{names.Pattern}){gap}{adverb}{verb}");
        if (pron is not null) branches.Add($"{verb}{verbGap}(?<p>{pron})");
        branches.Add($"{verb}{verbGap}(?<n>{names.Pattern})");
        return branches;
    }

    public SpeakerTag? ParseAfter(string narration, StoryNames story)
    {
        var sentence = SentenceBoundaries.First(narration, _rules);
        return ToTag(_after.Match(sentence), story);
    }

    public SpeakerTag? ParseBefore(string narration, StoryNames story)
    {
        var sentence = SentenceBoundaries.UnfinishedLast(narration, _rules);
        if (sentence is null) return null;
        return ToTag(_before.Match(sentence), story) ?? FromVerbAtEnd(sentence, story);
    }

    private SpeakerTag? FromVerbAtEnd(string sentence, StoryNames story)
    {
        if (!_verbAtEnd.IsMatch(sentence)) return null;
        foreach (var name in _names.Find(sentence).Where(n => story.Accepts(n.Name))) return new SpeakerTag(name.Name, Gender.Unknown);
        var pronoun = _pronoun.Match(sentence);
        return pronoun.Success ? PronounTag(pronoun.Value) : null;
    }

    private SpeakerTag? ToTag(Match match, StoryNames story)
    {
        if (!match.Success) return null;
        var name = match.Groups["n"].Success ? _names.Normalize(match.Groups["n"].Value) : null;
        if (name is not null) return story.Accepts(name) ? new SpeakerTag(name, Gender.Unknown) : null;
        var pronoun = match.Groups["p"].Success ? match.Groups["p"].Value : match.Groups["n"].Value;
        return PronounTagOrNull(pronoun);
    }

    private SpeakerTag? PronounTagOrNull(string word)
    {
        var tag = PronounTag(word);
        return tag.PronounGender == Gender.Unknown ? null : tag;
    }

    private SpeakerTag PronounTag(string word)
    {
        if (_rules.MaleSubjectPronouns.Contains(word)) return new SpeakerTag(null, Gender.Male);
        var female = _rules.FemaleSubjectPronouns.Contains(word);
        return new SpeakerTag(null, female ? Gender.Female : Gender.Unknown);
    }
}
