namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// One chunk of a read, ready for synthesis: its text, the voice that speaks it (null: the
/// reader's currently selected voice, as for a plain read) and the span of the read's full
/// text it covers.
/// </summary>
internal readonly record struct SpeechChunkPlan(
    string Text, string? VoiceId, int SourceStart, int SourceEnd, IVoicedAudioTransform? Transform = null);

/// <summary>
/// Turns text or voiced segments into the ordered chunks an engine synthesizes. Pure
/// bookkeeping over <see cref="SpeechTextChunker"/>: each segment is chunked on its own
/// (so a chunk never spans two voices; consecutive same-voice segments are not merged
/// because their exact source ranges can't be rebuilt without the full text), empty
/// segments are dropped, and every chunk's range is expressed against the full story
/// text, not its segment.
/// </summary>
internal static class SpeechChunkPlanner
{
    public static IReadOnlyList<SpeechChunkPlan> ForText(
        string text, SpeechTextChunker.HeadPolicy? head = null, bool biasFirstChunk = true) =>
        SpeechTextChunker.SplitWithRanges(text, biasFirstChunk, head)
            .Select(c => new SpeechChunkPlan(c.Text, null, c.SourceStart, c.SourceEnd))
            .ToList();

    public static IReadOnlyList<SpeechChunkPlan> ForSegments(
        IReadOnlyList<VoicedSegment> segments, SpeechTextChunker.HeadPolicy? head = null)
    {
        var plan = new List<SpeechChunkPlan>();
        foreach (VoicedSegment segment in segments)
        {
            if (!string.IsNullOrWhiteSpace(segment.Text))
            {
                // Only the first chunk of the whole read is biased to a short head: later
                // segments are not what the listener waits on.
                plan.AddRange(ForSegment(segment, biasFirstChunk: plan.Count == 0, head));
            }
        }

        return plan;
    }

    // The chunks tile the segment: the first starts at the segment's start and the last
    // ends at its end (so a quote's chunks cover its quote marks), and the boundaries in
    // between are the chunker's word positions shifted by where the text begins and clamped to the segment's range, so ranges stay
    // ordered and non-overlapping even when Text is not an exact slice of the source.
    private static List<SpeechChunkPlan> ForSegment(
        VoicedSegment segment, bool biasFirstChunk, SpeechTextChunker.HeadPolicy? head)
    {
        IReadOnlyList<SpeechTextChunker.TextChunk> chunks =
            SpeechTextChunker.SplitWithRanges(segment.Text, biasFirstChunk, head);
        var result = new List<SpeechChunkPlan>(chunks.Count);
        int cursor = segment.SourceStart;
        for (int i = 0; i < chunks.Count; i++)
        {
            int rawStart = i == 0 ? segment.SourceStart : segment.TextStart + chunks[i].SourceStart;
            int rawEnd = i == chunks.Count - 1 ? segment.SourceEnd : segment.TextStart + chunks[i].SourceEnd;
            int start = Math.Clamp(Math.Max(rawStart, cursor), segment.SourceStart, segment.SourceEnd);
            int end = Math.Clamp(rawEnd, start, segment.SourceEnd);
            result.Add(new SpeechChunkPlan(chunks[i].Text, segment.VoiceId, start, end, segment.Transform));
            cursor = end;
        }

        return result;
    }
}
