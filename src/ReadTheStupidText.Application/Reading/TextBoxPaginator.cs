using System.Text.RegularExpressions;

namespace ReadTheStupidText.Application.Reading;

/// <summary>One "page" of the reading text box: as many whole sentences as fit,
/// plus the span of the full read text it came from.</summary>
public readonly record struct TextPage(string Text, int SourceStart, int SourceEnd);

/// <summary>
/// Greedy, sentence-boundary-respecting pagination for the reading text box
/// (Decision 47) — its own segmentation, independent of
/// <see cref="SpeechTextChunker"/> (which is sized for synthesis latency, not
/// display). A page never splits a sentence: each page takes as many whole
/// sentences, in order, as the caller's <c>fits</c> predicate still accepts; a
/// single sentence that alone doesn't fit still gets its own page rather than
/// being dropped or truncated. The predicate is supplied by the UI layer (real
/// text measurement against the box's current size/font) — this class only
/// decides *which* sentences go together.
/// </summary>
public static partial class TextBoxPaginator
{
    public static IReadOnlyList<TextPage> Paginate(string text, Func<string, bool> fits)
    {
        IReadOnlyList<(string Text, int Start, int End)> sentences = SplitSentences(text);
        var pages = new List<TextPage>();

        int index = 0;
        while (index < sentences.Count)
        {
            int pageStart = sentences[index].Start;
            int pageEnd = sentences[index].End;
            int cursor = index;

            while (cursor + 1 < sentences.Count)
            {
                int candidateEnd = sentences[cursor + 1].End;
                if (!fits(text[pageStart..candidateEnd]))
                {
                    break;
                }

                pageEnd = candidateEnd;
                cursor++;
            }

            pages.Add(new TextPage(text[pageStart..pageEnd], pageStart, pageEnd));
            index = cursor + 1;
        }

        return pages;
    }

    /// <summary>The index of the page containing <paramref name="sourcePosition"/>,
    /// or the last page if the position is past the end (e.g. the read's final
    /// chunk ends exactly at the text length). -1 if there are no pages.</summary>
    public static int PageIndexContaining(IReadOnlyList<TextPage> pages, int sourcePosition)
    {
        for (int i = 0; i < pages.Count; i++)
        {
            if (sourcePosition < pages[i].SourceEnd || i == pages.Count - 1)
            {
                return i;
            }
        }

        return -1;
    }

    private static List<(string Text, int Start, int End)> SplitSentences(string text)
    {
        var sentences = new List<(string, int, int)>();
        int start = 0;
        foreach (Match boundary in SentenceBoundary().Matches(text))
        {
            AddTrimmedSpan(text, start, boundary.Index, sentences);
            start = boundary.Index + boundary.Length;
        }

        AddTrimmedSpan(text, start, text.Length, sentences);
        return sentences;
    }

    private static void AddTrimmedSpan(string text, int start, int end, List<(string, int, int)> sentences)
    {
        while (start < end && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        if (end > start)
        {
            sentences.Add((text[start..end], start, end));
        }
    }

    [GeneratedRegex(@"(?<=[.!?])\s+|\r?\n")]
    private static partial Regex SentenceBoundary();
}
