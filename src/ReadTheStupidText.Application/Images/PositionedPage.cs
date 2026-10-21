namespace ReadTheStupidText.Application.Images;

/// <summary>
/// One page of a story source: its 0-based index, the kept page image when there is one
/// (null for a text-only source), and its lines in reading order (Slice 63).
/// </summary>
public sealed record PositionedPage(int Index, string? ImagePath, IReadOnlyList<TextLine> Lines);
