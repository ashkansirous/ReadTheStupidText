namespace DinoReader.Core;

/// <summary>
/// Every language-specific rule table the parser, attributor and gender guesser use, so
/// another language is a new instance rather than a change to the algorithms.
/// </summary>
public sealed record LanguageRules(
    char StraightQuote,
    char CurlyOpenQuote,
    char CurlyCloseQuote,
    IReadOnlySet<string> SpeechVerbs,
    string AdverbSuffix,
    IReadOnlySet<string> MaleTitles,
    IReadOnlySet<string> FemaleTitles,
    IReadOnlySet<string> TitleAbbreviations,
    IReadOnlySet<string> MaleNames,
    IReadOnlySet<string> FemaleNames,
    IReadOnlySet<string> MaleSubjectPronouns,
    IReadOnlySet<string> FemaleSubjectPronouns,
    IReadOnlySet<string> MalePronouns,
    IReadOnlySet<string> FemalePronouns,
    IReadOnlySet<string> NonNameWords,
    IReadOnlySet<string> IndefinitePronouns,
    string SentenceTerminators,
    int DialogueGapChars)
{
    public bool IsOpeningQuote(char c) => c == StraightQuote || c == CurlyOpenQuote;

    public bool IsClosingQuote(char c) => c == StraightQuote || c == CurlyCloseQuote;

    public bool IsQuote(char c) => c == StraightQuote || c == CurlyOpenQuote || c == CurlyCloseQuote;

    public static LanguageRules English { get; } = EnglishRules.Create();
}
