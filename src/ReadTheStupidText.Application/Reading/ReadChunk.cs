namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// One chunk of the current read as it begins playing: its text, its 0-based
/// position among the read's chunks, and the span of the read's full text it
/// corresponds to (Decision 50) — the reading text box's whole-chunk highlight
/// source (Decision 46).
/// </summary>
public readonly record struct ReadChunk(int Index, int ChunkCount, string Text, int SourceStart, int SourceEnd)
{
    public int SourceLength => SourceEnd - SourceStart;

    /// <summary>
    /// True when this chunk really is a span of <paramref name="fullText"/>: the range fits and the
    /// non-whitespace characters agree (chunking only normalizes whitespace). A late chunk of an earlier
    /// read fails this against the next read's text, so it is never highlighted there.
    /// </summary>
    public bool MatchesSource(string fullText)
    {
        if (SourceStart < 0 || SourceEnd < SourceStart || SourceEnd > fullText.Length)
        {
            return false;
        }

        string expected = string.Concat(fullText.AsSpan(SourceStart, SourceLength).ToString().Where(c => !char.IsWhiteSpace(c)));
        string actual = string.Concat(Text.Where(c => !char.IsWhiteSpace(c)));
        return string.Equals(expected, actual, StringComparison.Ordinal);
    }
}
