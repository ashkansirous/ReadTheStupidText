namespace DinoReader.Core;

/// <summary>
/// What the reading screen shows while a story is read in the grown-up's language (Slice 110, Decision 112): the pages with their
/// translated text, the joined text and one span for each source sentence, in the same order. Sentence <c>i</c> here is sentence
/// <c>i</c> of the source, so the spoken-sentence index, the page dots and a tap on a sentence work unchanged; only the text differs.
/// </summary>
public sealed record TranslatedStory(IReadOnlyList<StoryPage> Pages, string Text, IReadOnlyList<SentenceSpan> Sentences);

/// <summary>Builds a <see cref="TranslatedStory"/> from the translated pieces a read speaks (Slice 110). Pure, so it is unit-tested.</summary>
public static class TranslatedStoryBuilder
{
    private const string Space = " ";
    private const char OpeningQuote = '“';
    private const char ClosingQuote = '”';

    /// <summary>
    /// The translated pages. A piece belongs to the source sentence holding its <see cref="Segment.SourceStart"/>; a sentence shows its pieces'
    /// texts in order (a quote's pieces wrapped in quote marks), or its source text when it has none. Paragraph breaks of the source stay
    /// paragraph breaks; every page keeps its picture file but drops its recognised lines, so a scanned page becomes a picture + text page
    /// (Slice 98) and no source-language boxes are drawn. A page in <paramref name="keepSource"/> (a page with a parent recording read in the story's own language,
    /// Slice 111) shows its source text instead of the translation.
    /// </summary>
    public static TranslatedStory Build(
        IReadOnlyList<StoryPage> sourcePages, IReadOnlyList<SentenceSpan> sourceSentences, IReadOnlyList<SplitSegment> translated,
        IReadOnlySet<int>? keepSource = null)
    {
        string source = Story.JoinPages(sourcePages);
        IReadOnlyList<string> texts = KeepSourceText(source, SentenceTexts(source, sourceSentences, translated), sourceSentences, sourcePages, keepSource);
        var pages = new List<StoryPage>();
        var spans = new SentenceSpan[sourceSentences.Count];
        var placed = new bool[sourceSentences.Count];
        int offset = 0;
        int pageStart = 0;
        foreach (StoryPage page in sourcePages)
        {
            int pageEnd = pageStart + page.Text.Length;
            string text = PageText(source, sourceSentences, texts, pageStart, pageEnd, offset, spans, placed);
            pages.Add(new StoryPage(text, page.ImageFile, PicturePrintsWords: page.Lines is { Count: > 0 }));
            offset += text.Length + Story.PageSeparator.Length;
            pageStart = pageEnd + Story.PageSeparator.Length;
        }

        ParkUnplaced(spans, placed);
        return new TranslatedStory(pages, Story.JoinPages(pages), spans);
    }

    // The sentences that start on a kept page show their own words, whatever the translation made of them.
    private static IReadOnlyList<string> KeepSourceText(
        string source, IReadOnlyList<string> texts, IReadOnlyList<SentenceSpan> sentences, IReadOnlyList<StoryPage> pages, IReadOnlySet<int>? keepSource)
    {
        if (keepSource is not { Count: > 0 })
        {
            return texts;
        }

        IReadOnlyList<int> starts = StoryRecordings.PageStarts(pages);
        var kept = texts.ToList();
        for (int i = 0; i < sentences.Count; i++)
        {
            if (keepSource.Contains(StoryRecordings.PageOfPosition(starts, sentences[i].Start)))
            {
                kept[i] = source[sentences[i].Start..sentences[i].End];
            }
        }

        return kept;
    }

    // A sentence that lies wholly between pages shows nowhere; it gets an empty span where the one before it ended, never the start of the story.
    private static void ParkUnplaced(SentenceSpan[] spans, bool[] placed)
    {
        int end = 0;
        for (int i = 0; i < spans.Length; i++)
        {
            if (placed[i])
            {
                end = spans[i].End;
            }
            else
            {
                spans[i] = new SentenceSpan(end, end);
            }
        }
    }

    // The sentences that start on the page, in order. A sentence that runs over a page break shows in full on the page where it starts (the one it first
    // overlaps) and is not repeated on the next; its span is where it was written. Spans are filled in at the page's place in the joined text.
    private static string PageText(
        string source, IReadOnlyList<SentenceSpan> sentences, IReadOnlyList<string> texts, int pageStart, int pageEnd, int offset,
        SentenceSpan[] spans, bool[] placed)
    {
        var text = new System.Text.StringBuilder();
        int previousEnd = -1;
        for (int i = 0; i < sentences.Count; i++)
        {
            SentenceSpan span = sentences[i];
            if (placed[i] || span.End <= pageStart || span.Start >= pageEnd)
            {
                continue;
            }

            placed[i] = true;
            if (texts[i].Length == 0)
            {
                // Its words were shown with an earlier sentence (one translated piece covered several): nothing to write, nothing to highlight.
                spans[i] = new SentenceSpan(offset + text.Length, offset + text.Length);
                continue;
            }

            if (previousEnd >= 0)
            {
                text.Append(Gap(source, previousEnd, span.Start));
            }

            spans[i] = new SentenceSpan(offset + text.Length, offset + text.Length + texts[i].Length);
            text.Append(texts[i]);
            previousEnd = span.End;
        }

        return text.ToString();
    }

    // A paragraph or line break between two sentences stays as it was; a plain space (or a page-internal gap of spaces) becomes one space.
    private static string Gap(string source, int from, int to)
    {
        string gap = source[from..Math.Max(from, to)];
        return gap.Contains('\n') ? gap : Space;
    }

    private static List<string> SentenceTexts(string source, IReadOnlyList<SentenceSpan> sentences, IReadOnlyList<SplitSegment> translated)
    {
        var groups = new List<string>[sentences.Count];
        var covered = new bool[sentences.Count];
        foreach ((SplitSegment first, string text) in Runs(translated))
        {
            int sentence = StorySentences.IndexAt(sentences, first.Segment.SourceStart);
            if (sentence >= 0)
            {
                (groups[sentence] ??= []).Add(text);
                MarkCovered(sentences, covered, sentence, first.Segment.SourceEnd);
            }
        }

        return sentences.Select((span, i) => TextOf(source, span, groups[i], covered[i])).ToList();
    }

    // A piece that covers several source sentences is written once, at the first; the others it covers show nothing of their own rather than their source text.
    private static void MarkCovered(IReadOnlyList<SentenceSpan> sentences, bool[] covered, int first, int sourceEnd)
    {
        for (int i = first + 1; i < sentences.Count && sentences[i].Start < sourceEnd; i++)
        {
            covered[i] = true;
        }
    }

    private static string TextOf(string source, SentenceSpan span, List<string>? group, bool covered)
    {
        if (group is { Count: > 0 })
        {
            return string.Join(Space, group);
        }

        return covered ? string.Empty : source[span.Start..span.End];
    }

    // One run for each piece the story was cut into: a long translation cut for speech shares its piece's source range and reads as one run.
    // A quote is marked where it opens and where it closes, so one that runs over several sentences is a single quote; one the source left open stays open.
    private static List<(SplitSegment First, string Text)> Runs(IReadOnlyList<SplitSegment> translated)
    {
        var pieces = new List<(SplitSegment First, string Text)>();
        int i = 0;
        while (i < translated.Count)
        {
            int j = i;
            while (j + 1 < translated.Count && SamePiece(translated[i], translated[j + 1]))
            {
                j++;
            }

            string run = string.Join(Space, translated.Skip(i).Take(j - i + 1).Select(p => p.Segment.Text));
            SplitSegment? next = j + 1 < translated.Count ? translated[j + 1] : null;
            pieces.Add((translated[i], Marked(translated[i], next, run)));
            i = j + 1;
        }

        return pieces;
    }

    private static string Marked(SplitSegment piece, SplitSegment? next, string run)
    {
        if (piece.Segment.Kind != SegmentKind.Quote)
        {
            return run;
        }

        string opening = ContinuesQuote(piece) ? string.Empty : OpeningQuote.ToString();
        bool closes = piece.Segment.IsClosed && (next is null || !ContinuesQuote(next));
        return opening + run + (closes ? ClosingQuote : string.Empty);
    }

    // A quote's piece after the first starts on its text, with no opening mark of its own in its source range.
    private static bool ContinuesQuote(SplitSegment piece) =>
        piece.Segment.Kind == SegmentKind.Quote && piece.TextStart == piece.Segment.SourceStart;

    private static bool SamePiece(SplitSegment first, SplitSegment next) =>
        first.TextStart == next.TextStart && first.Segment.Kind == next.Segment.Kind
        && first.Segment.SourceStart == next.Segment.SourceStart && first.Segment.SourceEnd == next.Segment.SourceEnd;
}
