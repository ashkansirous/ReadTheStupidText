namespace DinoReader.Core;

/// <summary>
/// Finds who a stretch of narration is about: an accepted name that opens a sentence
/// (after any filler such as "Then") and is followed by a lowercase word, as in
/// "Cotton Tail hopped over." A name in a tag ("said Peter") or after a possessive is not.
/// </summary>
internal static class NarrationSubjects
{
    /// <summary>The last sentence subject named in the narration segment, or null.</summary>
    public static string? Last(StoryNames story, Segment narration, LanguageRules rules) =>
        story.InRange(narration.SourceStart, narration.SourceEnd)
            .Where(n => IsSubject(story.Text, narration.SourceStart, n, rules))
            .Select(n => n.Name)
            .LastOrDefault();

    private static bool IsSubject(string text, int segmentStart, NameMatch name, LanguageRules rules) =>
        OpensSentence(text, segmentStart, name.Start, rules) && IsFollowedByWord(text, name.End);

    private static bool OpensSentence(string text, int segmentStart, int nameStart, LanguageRules rules)
    {
        var prefix = text[segmentStart..nameStart];
        int cut = prefix.LastIndexOfAny(rules.SentenceTerminators.ToCharArray());
        var words = prefix[(cut + 1)..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return words.All(w => rules.NonNameWords.Contains(w));
    }

    private static bool IsFollowedByWord(string text, int nameEnd)
    {
        int i = nameEnd;
        while (i < text.Length && (text[i] == ' ' || text[i] == '\t')) i++;
        return i > nameEnd && i < text.Length && char.IsLower(text[i]);
    }
}
