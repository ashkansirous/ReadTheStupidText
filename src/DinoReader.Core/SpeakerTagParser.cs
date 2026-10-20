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
        var verb = $@"(?:(?i:{string.Join("|", rules.SpeechVerbs.Select(Regex.Escape))}))\b";
        var adverb = $@"(?:\p{{L}}+{Regex.Escape(rules.AdverbSuffix)})";
        var pron = $@"\b(?i:{string.Join("|", rules.MaleSubjectPronouns.Concat(rules.FemaleSubjectPronouns).Select(Regex.Escape))})\b";
        var core = $@"(?:(?<p>{pron})\s+(?:{adverb}\s+)?{verb}"
            + $@"|(?<n>{names.Pattern})\s+(?:{adverb}\s+)?{verb}"
            + $@"|{verb}\s+(?<p>{pron})"
            + $@"|{verb}\s+(?<n>{names.Pattern}))";
        _after = new Regex($@"^[\s,]*{core}", RegexOptions.CultureInvariant);
        _before = new Regex($@"{core}(?:\s+{adverb})?[\s,:]*$", RegexOptions.CultureInvariant);
        _verbAtEnd = new Regex($@"{verb}(?:\s+{adverb})?[\s,:]*$", RegexOptions.CultureInvariant);
        _pronoun = new Regex(pron, RegexOptions.CultureInvariant);
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
