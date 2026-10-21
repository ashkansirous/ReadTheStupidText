namespace DinoReader.Core;

/// <summary>
/// Cuts a story's segments so a read can start at a chosen place (tap a sentence, skip to the next
/// one): segments that end before the position are dropped and the one holding it is trimmed to
/// start there. A quote keeps its opening mark while the position is at or before the quote's text.
/// </summary>
public static class SegmentSlicer
{
    public static IReadOnlyList<Segment> From(IReadOnlyList<Segment> segments, string story, int position)
    {
        var result = new List<Segment>();
        foreach (Segment segment in segments)
        {
            if (segment.SourceEnd <= position)
            {
                continue;
            }

            Segment? cut = position <= segment.SourceStart ? segment : Trim(segment, story, position);
            if (cut is not null)
            {
                result.Add(cut);
            }
        }

        return result;
    }

    // The segment holds the position: keep what is spoken from there, or nothing if only marks are left.
    private static Segment? Trim(Segment segment, string story, int position)
    {
        int textStart = SegmentTextLocator.TextStart(story, segment);
        int skip = Math.Max(0, position - textStart);
        while (skip < segment.Text.Length && char.IsWhiteSpace(segment.Text[skip]))
        {
            skip++;
        }

        if (skip >= segment.Text.Length)
        {
            return null;
        }

        return segment with { Text = segment.Text[skip..], SourceStart = textStart + skip };
    }
}
