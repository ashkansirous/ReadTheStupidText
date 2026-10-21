namespace DinoReader.Core;

/// <summary>
/// Finds where a <see cref="Segment"/>'s <see cref="Segment.Text"/> begins inside the story it
/// came from. A quote's source range includes its quote marks (and may carry other leading
/// characters), so the start is searched for rather than assumed to be one past the range start.
/// </summary>
public static class SegmentTextLocator
{
    /// <summary>
    /// The story index where <paramref name="segment"/>'s text begins; the segment's
    /// <see cref="Segment.SourceStart"/> when the text isn't found inside its range.
    /// </summary>
    public static int TextStart(string story, Segment segment)
    {
        if (segment.Text.Length == 0 || segment.SourceStart < 0 || segment.SourceEnd > story.Length
            || segment.SourceEnd < segment.SourceStart)
        {
            return segment.SourceStart;
        }

        ReadOnlySpan<char> range = story.AsSpan(segment.SourceStart, segment.SourceEnd - segment.SourceStart);
        int index = range.IndexOf(segment.Text.AsSpan(), StringComparison.Ordinal);
        return index < 0 ? segment.SourceStart : segment.SourceStart + index;
    }
}
