namespace DinoReader.Core;

/// <summary>One page of flowing text: the sentences it holds and the text range they cover.</summary>
public sealed record FlowPage(int FirstSentence, int LastSentence, int Start, int End)
{
    public bool Holds(int sentenceIndex) => sentenceIndex >= FirstSentence && sentenceIndex <= LastSentence;
}

/// <summary>
/// Paginates the story for text mode (mock screen 4): a page takes whole sentences while their text
/// still fits in <c>linesPerPage</c> lines, so a highlight never straddles two pages. A sentence
/// longer than a page is split across pages by line (the same as picture mode), so no line is clipped.
/// </summary>
public static class FlowPager
{
    public static IReadOnlyList<FlowPage> Paginate(string text, IReadOnlyList<SentenceSpan> sentences,
        double width, int linesPerPage, ITextMeasurer measurer)
    {
        var pages = new List<FlowPage>();
        var counter = new LineCounter(width, measurer);
        int first = 0;
        int position = sentences.Count > 0 ? sentences[0].Start : 0;
        while (first < sentences.Count)
        {
            FlowPage page = FillPage(text, sentences, first, position, linesPerPage, counter, width, measurer, out int continueAt);
            pages.Add(page);
            first = continueAt >= 0 ? page.LastSentence : page.LastSentence + 1;
            position = continueAt >= 0 ? continueAt : first < sentences.Count ? sentences[first].Start : 0;
        }

        return pages;
    }

    // One page starting at <position> inside sentence <first>. When the rest of that sentence is
    // longer than a page, it is cut after the page's last line and <continueAt> is where the next
    // page starts; otherwise whole sentences are added while they fit and <continueAt> is -1.
    private static FlowPage FillPage(string text, IReadOnlyList<SentenceSpan> sentences, int first, int position,
        int linesPerPage, LineCounter counter, double width, ITextMeasurer measurer, out int continueAt)
    {
        continueAt = -1;
        counter.Reset();
        counter.Add(text[position..sentences[first].End]);
        if (counter.Lines > linesPerPage)
        {
            return CutSentence(text, sentences[first], first, position, linesPerPage, width, measurer, out continueAt);
        }

        int last = AddWholeSentences(text, sentences, first, linesPerPage, counter);
        return new FlowPage(first, last, position, sentences[last].End);
    }

    // The last sentence that still fits on the page. Each sentence is added to the lines already
    // counted, so a page costs one pass over its text, not one per sentence.
    private static int AddWholeSentences(string text, IReadOnlyList<SentenceSpan> sentences, int first,
        int linesPerPage, LineCounter counter)
    {
        int last = first;
        while (last + 1 < sentences.Count)
        {
            LineState before = counter.State;
            counter.Add(text[sentences[last].End..sentences[last + 1].End]);
            if (counter.Lines > linesPerPage)
            {
                counter.State = before;
                break;
            }

            last++;
        }

        return last;
    }

    // The first <linesPerPage> lines of a sentence that does not fit one page; the rest goes on.
    private static FlowPage CutSentence(string text, SentenceSpan sentence, int index, int position,
        int linesPerPage, double width, ITextMeasurer measurer, out int continueAt)
    {
        IReadOnlyList<IReadOnlyList<WordAt>> lines =
            LineWrapper.WrapParagraphs(text[position..sentence.End], position, width, measurer);
        List<WordAt> words = lines.SelectMany(l => l).ToList();
        int onPage = lines.Take(linesPerPage).Sum(l => l.Count);
        if (onPage == 0 || onPage >= words.Count)
        {
            continueAt = -1;
            return new FlowPage(index, index, position, sentence.End);
        }

        WordAt lastWord = words[onPage - 1];
        continueAt = words[onPage].Start;
        return new FlowPage(index, index, position, lastWord.Start + lastWord.Text.Length);
    }

    /// <summary>The index of the page holding the sentence, or -1. A sentence that runs over several
    /// pages is on the page holding <paramref name="position"/> (a story index inside it) when given,
    /// else on its first page.</summary>
    public static int PageOf(IReadOnlyList<FlowPage> pages, int sentenceIndex, int position = -1)
    {
        int chosen = -1;
        for (int i = 0; i < pages.Count; i++)
        {
            if (pages[i].Holds(sentenceIndex) && (chosen < 0 || (position >= 0 && position >= pages[i].Start)))
            {
                chosen = i;
            }
        }

        return chosen;
    }
}
