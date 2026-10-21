namespace DinoReader.Core;

/// <summary>
/// Finds sentence edges in narration. A full stop after a title abbreviation ("Mr.")
/// does not end a sentence.
/// </summary>
internal static class SentenceBoundaries
{
    /// <summary>The first sentence of the text, without its terminator.</summary>
    public static string First(string text, LanguageRules rules)
    {
        for (int i = 0; i < text.Length; i++)
            if (IsSentenceEnd(text, i, rules)) return text[..i];
        return text;
    }

    /// <summary>
    /// The trailing sentence when it is still open (the narration ends mid-sentence, as in
    /// "Peter said, "), or null when the narration ends on a finished sentence.
    /// </summary>
    public static string? UnfinishedLast(string text, LanguageRules rules)
    {
        var trimmed = text.TrimEnd();
        if (trimmed.Length == 0 || IsSentenceEnd(trimmed, trimmed.Length - 1, rules)) return null;
        for (int i = trimmed.Length - 1; i >= 0; i--)
            if (IsSentenceEnd(trimmed, i, rules)) return trimmed[(i + 1)..];
        return trimmed;
    }

    internal static bool IsSentenceEnd(string text, int i, LanguageRules rules)
    {
        if (!rules.SentenceTerminators.Contains(text[i])) return false;
        bool atEdge = i + 1 >= text.Length || char.IsWhiteSpace(text[i + 1]);
        return atEdge && !(text[i] == '.' && FollowsTitle(text, i, rules));
    }

    internal static bool FollowsTitle(string text, int dot, LanguageRules rules)
    {
        int start = dot;
        while (start > 0 && char.IsLetter(text[start - 1])) start--;
        return start < dot && rules.TitleAbbreviations.Contains(text[start..dot]);
    }
}
