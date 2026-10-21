namespace DinoReader.Core;

/// <summary>
/// Splits story text into ordered narration and quote segments with source ranges.
/// Double quotes (straight and curly) delimit quotes; single quotes nested inside stay
/// part of the quote. A quote still open at a paragraph break ends there (unclosed), so a
/// missing close mark cannot swallow the next paragraphs; a paragraph that reopens with a
/// quote mark starts a new quote that continues the speech. An unclosed quote at the end
/// of the text runs to the end. Languages with dash dialogue (Russian, Spanish, Portuguese, French)
/// also read a line that opens with a dash as a quote (see <see cref="DashDialogue"/>).
/// </summary>
public sealed class StorySegmenter(LanguageRules rules)
{
    public IReadOnlyList<Segment> Split(string text)
    {
        var segments = new List<Segment>();
        int narrationStart = 0;
        int pos = 0;
        while (pos < text.Length)
        {
            var quote = QuoteAt(text, pos);
            if (quote is null)
            {
                pos++;
                continue;
            }
            AddNarration(segments, text, narrationStart, pos);
            segments.Add(quote);
            pos = narrationStart = quote.SourceEnd;
        }
        AddNarration(segments, text, narrationStart, text.Length);
        return segments;
    }

    private Segment? QuoteAt(string text, int pos)
    {
        if (rules.IsOpeningQuote(text[pos]))
            return BuildQuote(text, pos, FindQuoteEnd(text, pos + 1));
        return DashDialogue.Opens(text, pos, rules) ? DashDialogue.Read(text, pos, rules) : null;
    }

    private static void AddNarration(List<Segment> segments, string text, int start, int end)
    {
        while (start < end && char.IsWhiteSpace(text[start])) start++;
        while (end > start && char.IsWhiteSpace(text[end - 1])) end--;
        if (end > start)
            segments.Add(new Segment(SegmentKind.Narration, text[start..end], start, end));
    }

    private static Segment BuildQuote(string text, int open, QuoteEnd close)
    {
        int sourceEnd = close.IsClosed ? close.Index + 1 : close.Index;
        return new Segment(SegmentKind.Quote, text[(open + 1)..close.Index].Trim(), open, sourceEnd,
            IsClosed: close.IsClosed);
    }

    private QuoteEnd FindQuoteEnd(string text, int from)
    {
        for (int i = from; i < text.Length; i++)
        {
            char c = text[i];
            if (rules.IsClosingQuote(c)) return new QuoteEnd(i, true);
            if (rules.IsOpeningNonStraight(c)) return new QuoteEnd(TrimEnd(text, from, i), false);
            if (ParagraphBreak.IsAt(text, i))
                return new QuoteEnd(TrimEnd(text, from, i), false);
        }
        return new QuoteEnd(TrimEnd(text, from, text.Length), false);
    }

    private static int TrimEnd(string text, int from, int end)
    {
        while (end > from && char.IsWhiteSpace(text[end - 1])) end--;
        return end;
    }

    private readonly record struct QuoteEnd(int Index, bool IsClosed);
}
