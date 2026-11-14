namespace DinoReader.Core;

/// <summary>
/// Dialogue written with dashes instead of quote marks, as in Russian, Spanish, Portuguese and
/// French books: <c>— Hello, — said Peter. — Come in!</c>. A dash opens a quote at the start of a
/// line, after a colon, or after a comma or full stop on a line that opened with a dash. The quote
/// runs to the end of the line, or to a second dash that has a space before it and either follows a
/// comma, follows a letter, or follows a full stop and is followed by a lowercase word (a tag such
/// as "said Peter"). Anything else, for instance <c>— Hi! — How are you?</c>, stays one quote.
/// A dash quote counts as closed: a dialogue line ends with the line.
/// </summary>
internal static class DashDialogue
{
    public static bool Opens(string text, int pos, LanguageRules rules)
    {
        if (!rules.IsDialogueDash(text, pos)) return false;
        int before = PreviousOnLine(text, pos);
        if (before < 0) return true;
        if (text[before] == ':') return true;
        bool afterPunctuation = text[before] == ',' || rules.SentenceTerminators.Contains(text[before]);
        return afterPunctuation && char.IsWhiteSpace(text[pos - 1]) && LineOpensWithDash(text, pos, rules);
    }

    public static Segment Read(string text, int open, LanguageRules rules)
    {
        int lineEnd = LineEnd(text, open);
        int close = FindClosingDash(text, open + 1, lineEnd, rules);
        if (rules.DashTagAfterQuote && close < 0 && FindTrailingTag(text, open + 1, lineEnd, rules) is { } tag)
        {
            return new Segment(SegmentKind.Quote, text[(open + 1)..tag.QuoteEnd].Trim(), open, tag.SourceEnd);
        }

        bool dashClosed = close >= 0;
        int innerEnd = dashClosed ? close : TrimEnd(text, open + 1, lineEnd);
        int sourceEnd = dashClosed ? close + 1 : innerEnd;
        return new Segment(SegmentKind.Quote, text[(open + 1)..innerEnd].Trim(), open, sourceEnd);
    }

    private readonly record struct TrailingTag(int QuoteEnd, int SourceEnd);

    // Second person subjects: a speech verb followed by one of these opens a question inside the quote ("– Vad? Sa du något?"), not a tag.
    private static readonly HashSet<string> SecondPersonPronouns = new(StringComparer.OrdinalIgnoreCase)
    {
        "du", "ni", "dere", "je", "jij", "u", "jullie", "sinä", "te",
    };

    // First person subjects: "sa jag." is a tag, but "sa jag inget" goes on as a clause of the quote.
    private static readonly HashSet<string> FirstPersonPronouns = new(StringComparer.OrdinalIgnoreCase)
    {
        "jag", "vi", "jeg", "ik", "wij", "we", "minä", "me",
    };

    private const string TagStopChars = ".!?…";

    // Finnish, Swedish, Danish, Norwegian and Dutch write "– Hei, sanoi Pekka.": the first speech verb that follows a comma
    // (dropped from the quote) or a ? ! … (kept in it) starts the tag, so the quote ends there and the tag stays narration.
    // Not a tag: the verb is followed by you, or by I/we and more words, or the tag is followed by a comma and more speech ("– Jag kommer, sa Peter, men ...").
    private static TrailingTag? FindTrailingTag(string text, int from, int limit, LanguageRules rules)
    {
        for (int i = from; i < limit; i++)
        {
            bool comma = text[i] == ',';
            if (!comma && text[i] != '?' && text[i] != '!' && text[i] != '…') continue;
            int word = i + 1;
            while (word < limit && char.IsWhiteSpace(text[word])) word++;
            if (word == i + 1 || word >= limit) continue;
            int end = word;
            while (end < limit && char.IsLetter(text[end])) end++;
            if (end == word || !rules.SpeechVerbs.Contains(text[word..end])) continue;
            if (text.AsSpan(from, i - from).IsWhiteSpace()) continue;
            if (SubjectIsPartOfQuote(text, end, limit)) continue;
            return SpeechContinuesAfterTag(text, end, limit) ? null : new TrailingTag(comma ? i : i + 1, i + 1);
        }

        return null;
    }

    private static bool SubjectIsPartOfQuote(string text, int verbEnd, int limit)
    {
        int start = verbEnd;
        while (start < limit && char.IsWhiteSpace(text[start])) start++;
        int end = start;
        while (end < limit && char.IsLetter(text[end])) end++;
        if (end == start) return false;
        string word = text[start..end];
        if (SecondPersonPronouns.Contains(word)) return true;
        return FirstPersonPronouns.Contains(word) && WordsFollow(text, end, limit);
    }

    // True when more words follow before the next stop or comma ("sa jag inget"), false for "sa jag." or "sa jag,".
    private static bool WordsFollow(string text, int from, int limit)
    {
        for (int i = from; i < limit; i++)
        {
            if (TagStopChars.Contains(text[i]) || text[i] == ',') return false;
            if (char.IsLetterOrDigit(text[i])) return true;
        }

        return false;
    }

    private static bool SpeechContinuesAfterTag(string text, int verbEnd, int limit)
    {
        int stop = verbEnd;
        while (stop < limit && !TagStopChars.Contains(text[stop]) && text[stop] != ',') stop++;
        if (stop >= limit || text[stop] != ',') return false;
        for (int i = stop + 1; i < limit; i++)
        {
            if (char.IsLetter(text[i])) return true;
        }

        return false;
    }

    private static int FindClosingDash(string text, int from, int lineEnd, LanguageRules rules)
    {
        for (int i = from; i < lineEnd; i++)
        {
            if (rules.IsDialogueDash(text, i) && char.IsWhiteSpace(text[i - 1]) && ClosesQuote(text, i, rules))
                return i;
        }
        return -1;
    }

    private static bool ClosesQuote(string text, int dash, LanguageRules rules)
    {
        int prev = PreviousOnLine(text, dash);
        if (prev < 0) return false;
        char p = text[prev];
        if (p == ',' || char.IsLetterOrDigit(p)) return true;
        if (!rules.SentenceTerminators.Contains(p)) return false;
        int next = dash + 1;
        while (next < text.Length && text[next] != '\n' && char.IsWhiteSpace(text[next])) next++;
        return next < text.Length && char.IsLower(text[next]);
    }

    private static bool LineOpensWithDash(string text, int pos, LanguageRules rules)
    {
        int start = pos;
        while (start > 0 && text[start - 1] != '\n') start--;
        while (start < pos && char.IsWhiteSpace(text[start])) start++;
        return start < pos && rules.IsDialogueDash(text, start);
    }

    // The index of the closest non-blank character before pos on the same line, or -1 at the line start.
    private static int PreviousOnLine(string text, int pos)
    {
        for (int i = pos - 1; i >= 0 && text[i] != '\n'; i--)
        {
            if (!char.IsWhiteSpace(text[i])) return i;
        }
        return -1;
    }

    private static int LineEnd(string text, int from)
    {
        int end = text.IndexOf('\n', from);
        return end < 0 ? text.Length : end;
    }

    private static int TrimEnd(string text, int from, int end)
    {
        while (end > from && char.IsWhiteSpace(text[end - 1])) end--;
        return end;
    }
}
