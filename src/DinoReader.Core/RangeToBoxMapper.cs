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
    /// line texts in the order they were joined into the story; each line is located by searching
    /// forward from the previous one, so differing whitespace between lines does not matter. A line
    /// whose text cannot be found is skipped.
    /// </summary>
    public static IReadOnlyList<LineRef> Map(
        string story, IReadOnlyList<IReadOnlyList<string>> pageLines, int start, int end)
    {
        var hits = new List<LineRef>();
        int cursor = 0;
        for (int page = 0; page < pageLines.Count; page++)
        {
            for (int line = 0; line < pageLines[page].Count; line++)
            {
                string text = pageLines[page][line].Trim();
                int at = text.Length == 0 ? -1 : story.IndexOf(text, cursor, StringComparison.Ordinal);
                if (at < 0)
                {
                    continue;
                }

                cursor = at + text.Length;
                if (at < end && cursor > start)
                {
                    hits.Add(new LineRef(page, line));
                }
            }
        }

        return hits;
    }
}
