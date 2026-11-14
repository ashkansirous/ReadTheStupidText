namespace DinoReader.Core;

/// <summary>
/// One run of story text. <see cref="SourceStart"/> (inclusive) and
/// <see cref="SourceEnd"/> (exclusive) index the original text; for a quote the range
/// includes the quote marks while <see cref="Text"/> is the inner text without them.
/// <see cref="CharacterId"/> is the speaker of a quote, or null when unattributed
/// (always null for narration). <see cref="IsClosed"/> is false for a quote that ended
/// without its closing mark (at a paragraph break, a new opening mark or the end of text);
/// narration is always closed.
/// </summary>
public sealed record Segment(
    SegmentKind Kind,
    string Text,
    int SourceStart,
    int SourceEnd,
    string? CharacterId = null,
    bool IsClosed = true);
