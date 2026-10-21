using System.Text.RegularExpressions;

namespace DinoReader.Core;

/// <summary>
/// The regex fragments the name and speaker-tag readers build from a language's rule tables, so
/// the algorithms stay the same for every language and only the fragments change (Slice 66).
/// </summary>
internal static class TagPatterns
{
    /// <summary>A word edge: none in a script written without spaces.</summary>
    public static string Boundary(LanguageRules rules) =>
        rules.Script == ScriptStyle.Unspaced ? "" : @"\b";

    /// <summary>Any one of the speech verbs (case-insensitive, longest first), with the optional prefix such as Arabic "wa-".</summary>
    public static string Verb(LanguageRules rules) =>
        $"(?:{rules.VerbPrefix}(?i:{Alternation(rules.SpeechVerbs)})){Boundary(rules)}";

    /// <summary>The gap between a name or pronoun and its verb.</summary>
    public static string SubjectVerbGap(LanguageRules rules) => rules.NameVerbGap;

    /// <summary>The gap between a verb and the name or pronoun after it ("said Peter", French "dit-il").</summary>
    public static string VerbSubjectGap(LanguageRules rules) =>
        rules.Script == ScriptStyle.Unspaced ? @"\s*" : IsFrench(rules) ? @"[\s\-]+" : @"\s+";

    // The inverted pronoun is hyphenated only in French ("dit-il"); every other language keeps whitespace.
    private static bool IsFrench(LanguageRules rules) => ReferenceEquals(rules, LanguageRules.For("fr"));

    /// <summary>An optional adverb and its gap before the verb ("Peter loudly said"), or nothing when the language has none.</summary>
    public static string AdverbBefore(LanguageRules rules) =>
        rules.AdverbSuffix.Length == 0 ? "" : $@"(?:\p{{L}}+{Regex.Escape(rules.AdverbSuffix)}{rules.NameVerbGap})?";

    /// <summary>An optional adverb after the verb ("said Peter loudly"), or nothing.</summary>
    public static string AdverbAfter(LanguageRules rules) =>
        rules.AdverbSuffix.Length == 0 ? "" : $@"(?:\s+\p{{L}}+{Regex.Escape(rules.AdverbSuffix)})?";

    /// <summary>The subject pronouns (he, she, ...), or null when the language's subject pronouns carry no gender.</summary>
    public static string? Pronouns(LanguageRules rules)
    {
        var all = rules.MaleSubjectPronouns.Concat(rules.FemaleSubjectPronouns).ToList();
        if (all.Count == 0) return null;
        var edge = Boundary(rules);
        return $"{edge}(?i:{Alternation(all)}){edge}";
    }

    /// <summary>The characters that may sit between a quote and its tag: spaces, commas and colons of every script.</summary>
    public const string TagSeparators = @"\s,:،，、：";

    public static string Alternation(IEnumerable<string> words) =>
        string.Join("|", words.OrderByDescending(w => w.Length).Select(Regex.Escape));
}
