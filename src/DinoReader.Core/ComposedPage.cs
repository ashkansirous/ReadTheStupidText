namespace DinoReader.Core;

/// <summary>One laid-out line: its text, the sentence it belongs to, the story index its first word starts at,
/// where its text sits and the box that marks it while its sentence is spoken (both normalized to the page).</summary>
public sealed record ComposedLine(string Text, int SentenceIndex, int Start, NormalizedRect TextBounds, NormalizedRect Box);

/// <summary>A generated book page: its lines, top to bottom, and the sentences they carry.</summary>
public sealed record ComposedPage(IReadOnlyList<ComposedLine> Lines)
{
    /// <summary>The same page with every line's sentence index replaced by <paramref name="real"/>[index]: a page
    /// composed from part of a story (sentences numbered from 0) takes the numbers they have in the whole story.</summary>
    public ComposedPage RemapSentences(IReadOnlyList<int> real) =>
        new(Lines.Select(l => l with { SentenceIndex = real[l.SentenceIndex] }).ToList());

    public int FirstSentence => Lines.Count == 0 ? -1 : Lines[0].SentenceIndex;

    public int LastSentence => Lines.Count == 0 ? -1 : Lines[^1].SentenceIndex;

    /// <summary>The story index where the first line of the sentence on this page starts, or -1 when it is not here.</summary>
    public int StartOf(int sentenceIndex) => Lines.FirstOrDefault(l => l.SentenceIndex == sentenceIndex)?.Start ?? -1;

    /// <summary>The story index of the page's first word, or -1 for an empty page.</summary>
    public int FirstStart => Lines.Count == 0 ? -1 : Lines[0].Start;

    public bool Holds(int sentenceIndex) => Lines.Any(l => l.SentenceIndex == sentenceIndex);

    /// <summary>The boxes that mark one sentence on this page: one per line, so a two-line sentence is two stacked boxes.</summary>
    public IReadOnlyList<NormalizedRect> BoxesOf(int sentenceIndex) =>
        Lines.Where(l => l.SentenceIndex == sentenceIndex).Select(l => l.Box).ToList();

    /// <summary>The sentence under a point (page units), or -1 when the point is not on a line.</summary>
    public int SentenceAt(double x, double y) =>
        Lines.FirstOrDefault(l => y >= l.Box.Y && y <= l.Box.Bottom && x >= l.Box.X && x <= l.Box.Right)?.SentenceIndex ?? -1;
}
