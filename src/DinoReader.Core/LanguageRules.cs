namespace DinoReader.Core;

/// <summary>How a language's words are written, which decides how character names are found.</summary>
public enum ScriptStyle
{
    /// <summary>Words separated by spaces, names start with a capital letter (Latin, Cyrillic).</summary>
    Cased,

    /// <summary>Words separated by spaces but no capitals (Farsi, Arabic): names come from tags and the name list.</summary>
    CaselessSpaced,

    /// <summary>No spaces between words (Chinese, Japanese): names come from the name list and name patterns.</summary>
    Unspaced,
}

/// <summary>
/// Every language-specific rule table the parser, attributor and gender guesser use, so
/// another language is a new instance rather than a change to the algorithms. The trailing
/// optional members (Slice 66) default to English behaviour.
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
    int DialogueGapChars,
    string ExtraOpenQuotes = "",
    string ExtraCloseQuotes = "",
    string DialogueDashes = "",
    ScriptStyle Script = ScriptStyle.Cased,
    IReadOnlySet<string>? MaleVerbs = null,
    IReadOnlySet<string>? FemaleVerbs = null,
    IReadOnlyList<string>? MaleNameEndings = null,
    IReadOnlyList<string>? FemaleNameEndings = null,
    string VerbPrefix = "",
    string NameVerbGap = @"\s+",
    string NameLead = "",
    string ExtraNamePattern = "")
{
    private static readonly IReadOnlySet<string> NoWords = new HashSet<string>();
    private static readonly IReadOnlyList<string> NoEndings = [];

    /// <summary>Speech-verb forms that show the speaker is male (Russian past tense, Arabic).</summary>
    public IReadOnlySet<string> GenderedMaleVerbs => MaleVerbs ?? NoWords;

    /// <summary>Speech-verb forms that show the speaker is female.</summary>
    public IReadOnlySet<string> GenderedFemaleVerbs => FemaleVerbs ?? NoWords;

    /// <summary>Name endings that suggest a male name (a last resort after names, titles and pronouns).</summary>
    public IReadOnlyList<string> MaleEndings => MaleNameEndings ?? NoEndings;

    public IReadOnlyList<string> FemaleEndings => FemaleNameEndings ?? NoEndings;

    public bool IsOpeningQuote(char c) => c == StraightQuote || IsOpeningNonStraight(c);

    public bool IsClosingQuote(char c) => c == StraightQuote || c == CurlyCloseQuote || ExtraCloseQuotes.Contains(c);

    public bool IsQuote(char c) => IsOpeningQuote(c) || IsClosingQuote(c);

    /// <summary>An opening mark that cannot also close (every opening mark but the straight quote).</summary>
    public bool IsOpeningNonStraight(char c) => c == CurlyOpenQuote || ExtraOpenQuotes.Contains(c);

    public bool IsDialogueDash(char c) => DialogueDashes.Contains(c);

    /// <summary>
    /// Whether the character at <paramref name="pos"/> is a dialogue dash: an em, en or bar dash the language lists, or (in a
    /// language that writes dash dialogue) an ASCII hyphen that starts a line and is followed by a space, never one inside a word.
    /// </summary>
    public bool IsDialogueDash(string text, int pos)
    {
        char c = text[pos];
        if (IsDialogueDash(c)) return true;
        return c == '-' && DialogueDashes.Length > 0 && StartsLine(text, pos)
            && pos + 1 < text.Length && char.IsWhiteSpace(text[pos + 1]);
    }

    private static bool StartsLine(string text, int pos)
    {
        for (int i = pos - 1; i >= 0 && text[i] != '\n'; i--)
            if (!char.IsWhiteSpace(text[i])) return false;
        return true;
    }

    public static LanguageRules English { get; } = EnglishRules.Create();

    /// <summary>The rules for a story language code ("fa", "pt-BR", ...); English for an unknown code.</summary>
    public static LanguageRules For(string? language) => RuleSets.For(language);
}
