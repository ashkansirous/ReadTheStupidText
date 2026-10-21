namespace DinoReader.Core;

/// <summary>
/// Maps a text range of the story to the lines that cover it (Slice 63), so picture mode can box
/// the spoken sentence. A line is returned whole even when the range covers only part of it. The
/// caller turns each <see cref="LineRef"/> into the line's box.
/// </summary>
public static class RangeToBoxMapper
{
    /// <summary>
    /// The lines overlapping [<paramref name="start"/>, <paramref name="end"/>) of
    /// <paramref name="story"/>, in reading order. <paramref name="pageLines"/> holds each page's
    /// line texts in the order they were joined into the story. Each line must begin right after the
    /// previous one, past only a run of whitespace, so differing whitespace between lines does not matter (a line
    /// ending in a hyphen also matches a story that joined the word without it), and a line that is not in the story is skipped: a dropped page number (the next line matches at the
    /// cursor) stays in place, otherwise a bounded forward search resyncs past an extra heading or a misread line.
    /// </summary>
    public static IReadOnlyList<LineRef> Map(
        string story, IReadOnlyList<IReadOnlyList<string>> pageLines, int start, int end)
    {
        List<(LineRef Ref, string Text)> lines = Flatten(pageLines);
        var hits = new List<LineRef>();
        int cursor = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            (int at, int matchEnd) = MatchLine(story, cursor, lines, i);
            if (at < 0)
            {
                continue;
            }

            cursor = matchEnd;
            if (at < end && cursor > start)
            {
                hits.Add(lines[i].Ref);
            }
        }

        return hits;
    }

    private static List<(LineRef Ref, string Text)> Flatten(IReadOnlyList<IReadOnlyList<string>> pageLines)
    {
        var lines = new List<(LineRef, string)>();
        for (int page = 0; page < pageLines.Count; page++)
        {
            for (int line = 0; line < pageLines[page].Count; line++)
            {
                lines.Add((new LineRef(page, line), pageLines[page][line].Trim()));
            }
        }

        return lines;
    }

    // At the cursor first. Failing that, a line the next line matches straight after (a dropped
    // page number) is skipped in place; otherwise a bounded forward search resyncs the cursor past
    // an extra heading or a misread line, and a line found nowhere is skipped without moving it.
    private static (int At, int End) MatchLine(
        string story, int cursor, List<(LineRef Ref, string Text)> lines, int index)
    {
        string text = lines[index].Text;
        (int at, int end) = Match(story, cursor, text);
        if (at >= 0 || text.Length == 0 || NextLineMatchesAt(story, cursor, lines, index))
        {
            return (at, end);
        }

        return SearchAhead(story, cursor, text);
    }

    private static bool NextLineMatchesAt(string story, int cursor, List<(LineRef Ref, string Text)> lines, int index)
    {
        for (int next = index + 1; next < lines.Count; next++)
        {
            if (lines[next].Text.Length > 0)
            {
                return Match(story, cursor, lines[next].Text).At >= 0;
            }
        }

        return false;
    }

    // The first place the line occurs within a window past the cursor (2 x its length + 64).
    private static (int At, int End) SearchAhead(string story, int cursor, string text)
    {
        int windowEnd = (int)Math.Min(story.Length, (long)cursor + 2L * text.Length + 64 + text.Length);
        for (int from = SkipWhitespace(story, cursor); from < windowEnd; from++)
        {
            (int at, int end) = Match(story, from, text);
            if (at == from)
            {
                return (at, end);
            }
        }

        return (-1, -1);
    }

    // Where the line sits in the story when it starts at the cursor (past whitespace), else (-1, -1).
    // A line ending in a hyphen also matches a story that joined the word without the hyphen.
    private static (int At, int End) Match(string story, int cursor, string text)
    {
        if (text.Length == 0)
        {
            return (-1, -1);
        }

        int at = SkipWhitespace(story, cursor);
        if (string.CompareOrdinal(story, at, text, 0, text.Length) == 0)
        {
            return (at, at + text.Length);
        }

        int unhyphenated = text.Length - 1;
        return unhyphenated > 0 && IsHyphen(text[^1]) && string.CompareOrdinal(story, at, text, 0, unhyphenated) == 0
            ? (at, at + unhyphenated)
            : (-1, -1);
    }

    private static int SkipWhitespace(string story, int from)
    {
        int at = from;
        while (at < story.Length && char.IsWhiteSpace(story[at]))
        {
            at++;
        }

        return at;
    }

    private static bool IsHyphen(char c) => c is '-' or '\u00AD' or '\u2010' or '\u2011';
}
