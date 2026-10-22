namespace DinoReader.Core;

/// <summary>A box on a story page (0-based), in that page's own 0..1 coordinates.</summary>
public readonly record struct PageBox(int Page, NormalizedRect Rect);

/// <summary>A sentence span for a generated page with the sentence's real index in the story.</summary>
public readonly record struct GeneratedSpan(int Sentence, SentenceSpan Span);

/// <summary>
/// Picture mode on real pages (Slice 64): where each sentence of a story sits on the scanned page
/// pictures. The recognised lines are matched to the story text once (<see cref="RangeToBoxMapper.Locate"/>);
/// after that every question (the boxes of a sentence, the sentence under a tap, the page a sentence
/// is on) is answered from that match. A page counts as <see cref="IsScanned"/> when it has a
/// picture (Slice 77: also when none of its lines is found any more, e.g. edited text, then it has no boxes);
/// a page without a picture is drawn as a generated page, so a story that mixes both still reads.
/// Boxes stay in the page's own coordinates, so they follow the picture at any size, rotation or zoom.
/// </summary>
public sealed class ScanLayout
{
    private readonly IReadOnlyList<StoryPage> _pages;
    private readonly IReadOnlyList<SentenceSpan> _sentences;
    private readonly int[] _starts;
    private readonly bool[] _scanned;
    private readonly List<int>[] _sentencesOnPage;
    private readonly List<(int Page, NormalizedRect Rect, int Start, int End)> _lines = [];

    public ScanLayout(IReadOnlyList<StoryPage> pages, IReadOnlyList<SentenceSpan> sentences)
    {
        _pages = pages;
        _sentences = sentences;
        _starts = PageStarts(pages);
        _scanned = new bool[pages.Count];
        _sentencesOnPage = SentencesByPage(pages.Count, sentences, _starts);
        string text = Story.JoinPages(pages);
        // Only the included lines are in the story text, so only they are matched; the index map leads back to their boxes.
        var includedIndexes = pages.Select(p => p.ImageFile is null ? [] : p.IncludedLineIndexes).ToList();
        var lineTexts = pages.Select((p, i) => (IReadOnlyList<string>)includedIndexes[i].Select(k => p.Lines![k].Text).ToList()).ToList();
        foreach (LocatedLine located in RangeToBoxMapper.Locate(text, lineTexts))
        {
            (int page, int line) = (located.Ref.Page, located.Ref.Line);
            _lines.Add((page, pages[page].Lines![includedIndexes[page][line]].Box, located.Start, located.End));
            _scanned[page] = true;
        }

        // A page with a picture is drawn from it even when none of its lines is found any more (edited text): picture, no boxes.
        for (int page = 0; page < pages.Count; page++)
        {
            _scanned[page] |= pages[page].ImageFile is not null;
        }
    }

    public int PageCount => _pages.Count;

    public bool IsScanned(int page) => page >= 0 && page < _scanned.Length && _scanned[page];

    /// <summary>Whether any page is drawn from its picture.</summary>
    public bool HasScannedPages => _scanned.Any(s => s);

    /// <summary>The story index the page's text starts at.</summary>
    public int PageStart(int page) => _starts[page];

    /// <summary>The story index just past the page's text.</summary>
    public int PageEnd(int page) => _starts[page] + _pages[page].Text.Length;

    /// <summary>The page whose text holds the story index (the last page for an index past the end), or -1 for no pages.</summary>
    public int PageContaining(int position) => PageIndexOf(_starts, position);

    private static int PageIndexOf(int[] starts, int position)
    {
        int found = -1;
        for (int i = 0; i < starts.Length && starts[i] <= position; i++)
        {
            found = i;
        }

        return found;
    }

    /// <summary>The page a sentence is on: the one holding <paramref name="position"/> (a story index inside it) when given and
    /// inside the sentence, else the one its first word is on.</summary>
    public int PageOfSentence(int sentence, int position = -1)
    {
        if (sentence < 0 || sentence >= _sentences.Count)
        {
            return -1;
        }

        SentenceSpan span = _sentences[sentence];
        bool inside = position >= span.Start && position < span.End;
        return PageContaining(inside ? position : span.Start);
    }

    /// <summary>The sentences that start on the page (inclusive range of indexes), or (-1, -1) when none does.</summary>
    public (int First, int Last) SentencesOnPage(int page)
    {
        List<int>? own = SentenceIndexesOn(page);
        return own is { Count: > 0 } ? (own[0], own[^1]) : (-1, -1);
    }

    private List<int>? SentenceIndexesOn(int page) =>
        page >= 0 && page < _sentencesOnPage.Length ? _sentencesOnPage[page] : null;

    /// <summary>The sentences a generated page is composed from: those starting on it, and (when
    /// <paramref name="carryTail"/>, the page before it is a picture that already shows the head) the tail of a
    /// sentence carried over from the page before, cut to start where this page's text starts. Each span carries
    /// the real index of its sentence in the story, so the page's lines can be numbered as the story numbers them
    /// even when a sentence in between is skipped.</summary>
    public IReadOnlyList<GeneratedSpan> SpansForGeneratedPage(int page, bool carryTail)
    {
        var spans = new List<GeneratedSpan>();
        int start = PageStart(page);
        int carried = carryTail ? CarriedSentence(start) : -1;
        if (carried >= 0)
        {
            spans.Add(new GeneratedSpan(carried, new SentenceSpan(start, _sentences[carried].End)));
        }

        foreach (int i in SentenceIndexesOn(page) ?? [])
        {
            spans.Add(new GeneratedSpan(i, _sentences[i]));
        }

        return spans;
    }

    private static List<int>[] SentencesByPage(int pageCount, IReadOnlyList<SentenceSpan> sentences, int[] starts)
    {
        var byPage = new List<int>[pageCount];
        for (int i = 0; i < pageCount; i++)
        {
            byPage[i] = [];
        }

        for (int i = 0; i < sentences.Count && pageCount > 0; i++)
        {
            if (sentences[i].Length > 0)
            {
                byPage[PageIndexOf(starts, sentences[i].Start)].Add(i);
            }
        }

        return byPage;
    }

    // The sentence that began before the position and runs past it, or -1.
    private int CarriedSentence(int position)
    {
        for (int i = 0; i < _sentences.Count; i++)
        {
            if (_sentences[i].Start < position && _sentences[i].End > position)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The boxes that mark a sentence: one per line it covers, in reading order, over one or more pages.</summary>
    public IReadOnlyList<PageBox> BoxesOf(int sentence)
    {
        if (sentence < 0 || sentence >= _sentences.Count)
        {
            return [];
        }

        SentenceSpan span = _sentences[sentence];
        return _lines.Where(l => l.Start < span.End && l.End > span.Start).Select(l => new PageBox(l.Page, l.Rect)).ToList();
    }

    /// <summary>The sentence whose line is under a point of the page (0..1), or -1.</summary>
    public int SentenceAt(int page, double x, double y)
    {
        foreach (var line in _lines.Where(l => l.Page == page))
        {
            bool hit = x >= line.Rect.X && x <= line.Rect.Right && y >= line.Rect.Y && y <= line.Rect.Bottom;
            if (hit)
            {
                return SentenceHolding(line.Start);
            }
        }

        return -1;
    }

    // The sentence holding the line's first word.
    private int SentenceHolding(int position)
    {
        for (int i = 0; i < _sentences.Count; i++)
        {
            if (position >= _sentences[i].Start && position < _sentences[i].End)
            {
                return i;
            }
        }

        return -1;
    }

    private static int[] PageStarts(IReadOnlyList<StoryPage> pages)
    {
        var starts = new int[pages.Count];
        int at = 0;
        for (int i = 0; i < pages.Count; i++)
        {
            starts[i] = at;
            at += pages[i].Text.Length + Story.PageSeparator.Length;
        }

        return starts;
    }
}
