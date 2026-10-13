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
}
