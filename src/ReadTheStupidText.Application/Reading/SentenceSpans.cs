namespace ReadTheStupidText.Application.Reading;

/// <summary>One sentence unit's range in the read's full text (Slice 190).</summary>
public readonly record struct ChunkSentenceSpan(int Start, int End);

/// <summary>
/// Finds where each sentence unit of a chunk sits in the read's full text, for the phone's
/// one-sentence highlight (Slice 190). Units are the ones the engine synthesizes
/// (<see cref="SentenceUnits"/>), so unit <c>i</c> here is unit <c>i</c> of the chunk's audio.
/// </summary>
public static class SentenceSpans
{
    /// <summary>
    /// The span of each unit of <paramref name="chunkText"/>, located by matching its non-whitespace
    /// characters in order from <paramref name="sourceStart"/> (chunking only normalizes whitespace).
    /// Null when the chunk is not such a rewrite of the text, so the caller keeps its chunk highlight.
    /// </summary>
    public static IReadOnlyList<ChunkSentenceSpan>? Compute(string chunkText, string fullText, int sourceStart)
    {
        if (sourceStart < 0 || sourceStart > fullText.Length)
        {
            return null;
        }

        var spans = new List<ChunkSentenceSpan>();
        int cursor = sourceStart;
        foreach (string unit in SentenceUnits.Split(chunkText))
        {
            if (!TryLocate(unit, fullText, ref cursor, out ChunkSentenceSpan span))
            {
                return null;
            }

            spans.Add(span);
        }

        return spans;
    }

    private static bool TryLocate(string unit, string fullText, ref int cursor, out ChunkSentenceSpan span)
    {
        int start = -1;
        int end = cursor;
        foreach (char expected in unit.Where(c => !char.IsWhiteSpace(c)))
        {
            while (cursor < fullText.Length && char.IsWhiteSpace(fullText[cursor]))
            {
                cursor++;
            }

            if (cursor >= fullText.Length || fullText[cursor] != expected)
            {
                span = default;
                return false;
            }

            start = start < 0 ? cursor : start;
            cursor++;
            end = cursor;
        }

        span = new ChunkSentenceSpan(start, end);
        return start >= 0;
    }
}
