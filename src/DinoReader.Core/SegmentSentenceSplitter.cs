namespace DinoReader.Core;

/// <summary>A segment piece with the story index where its text begins (a quote's piece after the first has no mark of its own).</summary>
public sealed record SplitSegment(Segment Segment, int TextStart);

/// <summary>
/// Cuts segments at the sentence starts inside them, so the reader has at least one chunk per
/// sentence and its chunk events move the highlight sentence by sentence (a three-sentence
/// narration paragraph would otherwise be one chunk and one highlight step). Each piece keeps
/// exact story positions: the first piece keeps the segment's <c>SourceStart</c> (a quote's
/// opening mark), the last keeps its <c>SourceEnd</c>, the ones between start and end on their text.
/// A segment whose text is not an exact slice of the story is left whole.
/// </summary>
public static class SegmentSentenceSplitter
{
    public static IReadOnlyList<SplitSegment> Split(IReadOnlyList<Segment> segments, string story,
        IReadOnlyList<SentenceSpan> sentences)
    {
        var result = new List<SplitSegment>();
        foreach (Segment segment in segments)
        {
            AddPieces(result, segment, story, sentences);
        }

        return result;
    }

    private static void AddPieces(List<SplitSegment> result, Segment segment, string story, IReadOnlyList<SentenceSpan> sentences)
    {
        int textStart = SegmentTextLocator.TextStart(story, segment);
        List<int> cuts = IsExactSlice(story, segment, textStart) ? CutsIn(sentences, textStart, textStart + segment.Text.Length) : [];
        if (cuts.Count == 0)
        {
            result.Add(new SplitSegment(segment, textStart));
            return;
        }

        var bounds = new List<int> { textStart };
        bounds.AddRange(cuts);
        bounds.Add(textStart + segment.Text.Length);
        for (int k = 0; k + 1 < bounds.Count; k++)
        {
            AddPiece(result, segment, textStart, bounds[k], bounds[k + 1], k == 0, k + 2 == bounds.Count);
        }
    }

    private static void AddPiece(List<SplitSegment> result, Segment segment, int textStart, int from, int to,
        bool first, bool last)
    {
        string text = segment.Text[(from - textStart)..(to - textStart)].TrimEnd();
        if (text.Length == 0)
        {
            return;
        }

        Segment piece = segment with
        {
            Text = text,
            SourceStart = first ? segment.SourceStart : from,
            SourceEnd = last ? segment.SourceEnd : from + text.Length,
            IsClosed = !last || segment.IsClosed,
        };
        result.Add(new SplitSegment(piece, from));
    }

    private static bool IsExactSlice(string story, Segment segment, int textStart) =>
        segment.Text.Length > 0 && textStart >= 0 && textStart + segment.Text.Length <= story.Length
        && string.CompareOrdinal(story, textStart, segment.Text, 0, segment.Text.Length) == 0;

    // The sentence starts strictly inside the text range (the first sentence start at its edge is no cut).
    private static List<int> CutsIn(IReadOnlyList<SentenceSpan> sentences, int from, int to)
    {
        var cuts = new List<int>();
        for (int i = FirstStartAfter(sentences, from); i < sentences.Count && sentences[i].Start < to; i++)
        {
            cuts.Add(sentences[i].Start);
        }

        return cuts;
    }

    private static int FirstStartAfter(IReadOnlyList<SentenceSpan> sentences, int position)
    {
        int low = 0;
        int high = sentences.Count;
        while (low < high)
        {
            int mid = (low + high) / 2;
            if (sentences[mid].Start > position)
            {
                high = mid;
            }
            else
            {
                low = mid + 1;
            }
        }

        return low;
    }
}
