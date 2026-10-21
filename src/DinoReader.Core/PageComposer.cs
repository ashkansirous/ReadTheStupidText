namespace DinoReader.Core;

/// <summary>
/// Lays a story onto book-style pages (Slice 62, picture mode). Every sentence starts on a new line
/// and is kept whole on one page when it fits, so its box is a stack of whole lines (mock screen 3).
/// The result is pure geometry: Slices 63 and 64 fill the same per-sentence line boxes from real
/// page pictures. Text is vertically centred on its page.
/// </summary>
public sealed class PageComposer(PageGeometry geometry, ITextMeasurer measurer)
{
    private readonly record struct PendingLine(string Text, int Sentence, int Start);

    public IReadOnlyList<ComposedPage> Compose(string text, IReadOnlyList<SentenceSpan> sentences)
    {
        var pages = new List<ComposedPage>();
        var current = new List<PendingLine>();
        int capacity = geometry.LinesPerPage;
        for (int i = 0; i < sentences.Count; i++)
        {
            SentenceSpan span = sentences[i];
            IReadOnlyList<IReadOnlyList<WordAt>> lines =
                LineWrapper.WrapWords(text[span.Start..span.End], span.Start, geometry.TextWidth, measurer);

            // A sentence that does not fit what is left of a partly filled page starts a fresh one,
            // even when it is longer than a page and will run over onto the next.
            if (current.Count > 0 && current.Count + lines.Count > capacity)
            {
                pages.Add(BuildPage(current));
                current.Clear();
            }

            AddLines(pages, current, lines, i, capacity);
        }

        if (current.Count > 0)
        {
            pages.Add(BuildPage(current));
        }

        return pages;
    }

    /// <summary>The page holding the sentence, or -1. A sentence that runs over several pages is on the page
    /// holding <paramref name="position"/> (a story index inside it) when given, else on its first page.</summary>
    public static int PageOf(IReadOnlyList<ComposedPage> pages, int sentenceIndex, int position = -1)
    {
        int chosen = -1;
        for (int i = 0; i < pages.Count; i++)
        {
            if (pages[i].Holds(sentenceIndex)
                && (chosen < 0 || (position >= 0 && position >= pages[i].StartOf(sentenceIndex))))
            {
                chosen = i;
            }
        }

        return chosen;
    }

    private void AddLines(List<ComposedPage> pages, List<PendingLine> current,
        IReadOnlyList<IReadOnlyList<WordAt>> lines, int sentence, int capacity)
    {
        foreach (IReadOnlyList<WordAt> line in lines)
        {
            if (current.Count == capacity)
            {
                pages.Add(BuildPage(current));
                current.Clear();
            }

            current.Add(new PendingLine(string.Join(' ', line.Select(w => w.Text)), sentence, line[0].Start));
        }
    }

    private ComposedPage BuildPage(List<PendingLine> lines)
    {
        double blockHeight = lines.Count * geometry.LineHeight + (lines.Count - 1) * geometry.LineGap;
        double top = (geometry.Height - blockHeight) / 2;
        var composed = lines
            .Select((l, i) => BuildLine(l, top + i * (geometry.LineHeight + geometry.LineGap)))
            .ToList();
        return new ComposedPage(composed);
    }

    private ComposedLine BuildLine(PendingLine line, double y)
    {
        double outset = geometry.HighlightOutset;
        NormalizedRect textRect = Normalize(geometry.PaddingX, y, geometry.TextWidth, geometry.LineHeight);
        NormalizedRect box = Normalize(geometry.PaddingX - outset, y, geometry.TextWidth + 2 * outset, geometry.LineHeight);
        return new ComposedLine(line.Text, line.Sentence, line.Start, textRect, box);
    }

    private NormalizedRect Normalize(double x, double y, double width, double height) =>
        new(x / geometry.Width, y / geometry.Height, width / geometry.Width, height / geometry.Height);
}
