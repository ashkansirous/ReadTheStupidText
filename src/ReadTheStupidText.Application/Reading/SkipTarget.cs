namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// The chunk to resume playback at for a ±10s skip (Decision 32): the chunk index to
/// restart, and its cumulative start time within the read (for repositioning the
/// read-through timer). Skips land on chunk boundaries, not an exact 10.000s offset.
/// </summary>
public sealed record SkipTarget(int ChunkIndex, TimeSpan ChunkStart);
