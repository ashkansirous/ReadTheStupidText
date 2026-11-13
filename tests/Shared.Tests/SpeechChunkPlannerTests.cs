using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class SpeechChunkPlannerTests
{
    private const string Narrator = "supertonic:M2";
    private const string Peter = "supertonic:M1";

    // 'Hello,' he said. "Come here." at known offsets.
    private const string Story = "Peter ran. \"Come here, Cotton Tail!\" Then he stopped.";

    private static string LongText(int sentences) =>
        string.Join(' ', Enumerable.Range(0, sentences).Select(i => $"This is sentence number {i} of the story."));

    [Fact]
    public void Plain_text_chunks_carry_no_voice_and_real_ranges()
    {
        string text = LongText(20);

        IReadOnlyList<SpeechChunkPlan> plan = SpeechChunkPlanner.ForText(text);

        Assert.True(plan.Count > 1);
        Assert.All(plan, c => Assert.Null(c.VoiceId));
        Assert.All(plan, c => Assert.True(c.SourceEnd > c.SourceStart && c.SourceEnd <= text.Length));
    }

    [Fact]
    public void Every_chunk_of_a_segment_carries_the_segments_voice()
    {
        string text = LongText(20);
        var segments = new[] { new VoicedSegment(text, Peter, 0, text.Length, 0) };

        IReadOnlyList<SpeechChunkPlan> plan = SpeechChunkPlanner.ForSegments(segments);

        Assert.True(plan.Count > 1);
        Assert.All(plan, c => Assert.Equal(Peter, c.VoiceId));
    }

    [Fact]
    public void Chunks_tile_the_segment_in_order()
    {
        string text = LongText(20);
        const int offset = 100;
        var segments = new[] { new VoicedSegment(text, Peter, offset, offset + text.Length, offset) };

        IReadOnlyList<SpeechChunkPlan> plan = SpeechChunkPlanner.ForSegments(segments);

        Assert.Equal(offset, plan[0].SourceStart);
        Assert.Equal(offset + text.Length, plan[^1].SourceEnd);
        for (int i = 1; i < plan.Count; i++)
        {
            Assert.True(plan[i].SourceStart >= plan[i - 1].SourceEnd);
        }
    }

    [Fact]
    public void Offsets_are_relative_to_the_full_text_not_the_segment()
    {
        int start = Story.IndexOf("Then", StringComparison.Ordinal);
        string narration = Story[start..];
        var segments = new[] { new VoicedSegment(narration, Narrator, start, Story.Length, start) };

        SpeechChunkPlan chunk = Assert.Single(SpeechChunkPlanner.ForSegments(segments));

        Assert.Equal(narration, Story[chunk.SourceStart..chunk.SourceEnd]);
    }

    [Fact]
    public void A_quote_range_includes_its_marks_and_its_text_sits_inside()
    {
        int open = Story.IndexOf('"');
        int close = Story.LastIndexOf('"', Story.IndexOf("Then", StringComparison.Ordinal));
        string inner = Story[(open + 1)..close];
        var segments = new[] { new VoicedSegment(inner, Peter, open, close + 1, open + 1) };

        SpeechChunkPlan chunk = Assert.Single(SpeechChunkPlanner.ForSegments(segments));

        Assert.Equal(open, chunk.SourceStart);
        Assert.Equal(close + 1, chunk.SourceEnd);
        Assert.Contains(inner, Story[chunk.SourceStart..chunk.SourceEnd]);
    }

    [Fact]
    public void A_long_quote_keeps_inner_chunk_boundaries_inside_the_quote()
    {
        string inner = LongText(12);
        var segments = new[] { new VoicedSegment(inner, Peter, 10, 10 + inner.Length + 2, 11) };

        IReadOnlyList<SpeechChunkPlan> plan = SpeechChunkPlanner.ForSegments(segments);

        Assert.True(plan.Count > 1);
        Assert.All(plan.Skip(1), c => Assert.True(c.SourceStart >= 11));
        Assert.All(plan, c => Assert.True(c.SourceEnd <= 10 + inner.Length + 2));
    }

    [Fact]
    public void Empty_and_whitespace_segments_are_dropped()
    {
        var segments = new[]
        {
            new VoicedSegment("", Narrator, 0, 0, 0),
            new VoicedSegment("  \n ", Narrator, 0, 4, 0),
            new VoicedSegment("Hi.", Peter, 5, 8, 5),
        };

        SpeechChunkPlan chunk = Assert.Single(SpeechChunkPlanner.ForSegments(segments));

        Assert.Equal(Peter, chunk.VoiceId);
    }

    [Fact]
    public void Segments_keep_their_order_and_never_share_a_chunk()
    {
        var segments = new[]
        {
            new VoicedSegment("Peter ran.", Narrator, 0, 10, 0),
            new VoicedSegment("Come here!", Peter, 11, 23, 12),
            new VoicedSegment("Then he stopped.", Narrator, 24, 40, 24),
        };

        IReadOnlyList<SpeechChunkPlan> plan = SpeechChunkPlanner.ForSegments(segments);

        Assert.Equal([Narrator, Peter, Narrator], plan.Select(c => c.VoiceId));
        Assert.Equal(["Peter ran.", "Come here!", "Then he stopped."], plan.Select(c => c.Text));
    }

    [Fact]
    public void Only_the_first_chunk_of_the_whole_plan_is_biased_to_a_short_head()
    {
        string text = LongText(12);
        var segments = new[]
        {
            new VoicedSegment(text, Narrator, 0, text.Length, 0),
            new VoicedSegment(text, Peter, 1000, 1000 + text.Length, 1000),
        };

        IReadOnlyList<SpeechChunkPlan> plan = SpeechChunkPlanner.ForSegments(segments);

        int firstSegmentChunks = SpeechTextChunker.SplitWithRanges(text).Count;
        int unbiasedChunks = SpeechTextChunker.SplitWithRanges(text, biasFirstChunk: false).Count;
        Assert.Equal(firstSegmentChunks + unbiasedChunks, plan.Count);
        Assert.Equal(
            SpeechTextChunker.SplitWithRanges(text, biasFirstChunk: false).Select(c => c.Text),
            plan.Skip(firstSegmentChunks).Select(c => c.Text));
    }

    [Fact]
    public void Inner_chunk_ranges_use_the_explicit_text_start_not_a_width_guess()
    {
        // A quote whose range is wider than its text by more than the opening mark.
        string inner = LongText(12);
        const int sourceStart = 10;
        const int textStart = 13;
        var segments = new[] { new VoicedSegment(inner, Peter, sourceStart, textStart + inner.Length + 1, textStart) };

        IReadOnlyList<SpeechChunkPlan> plan = SpeechChunkPlanner.ForSegments(segments);

        IReadOnlyList<SpeechTextChunker.TextChunk> raw = SpeechTextChunker.SplitWithRanges(inner);
        Assert.Equal(textStart + raw[1].SourceStart, plan[1].SourceStart);
        Assert.Equal(textStart + raw[1].SourceEnd, plan[1].SourceEnd);
        Assert.Equal(sourceStart, plan[0].SourceStart);
        Assert.Equal(textStart + inner.Length + 1, plan[^1].SourceEnd);
    }

    [Fact]
    public void Consecutive_segments_in_the_same_voice_stay_separate_chunks()
    {
        // Merging would need the exact source text between them to keep ranges exact.
        var segments = new[]
        {
            new VoicedSegment("One.", Narrator, 0, 4, 0),
            new VoicedSegment("Two.", Narrator, 5, 9, 5),
        };

        IReadOnlyList<SpeechChunkPlan> plan = SpeechChunkPlanner.ForSegments(segments);

        Assert.Equal([(0, 4), (5, 9)], plan.Select(c => (c.SourceStart, c.SourceEnd)));
    }

    [Fact]
    public void Ranges_are_clamped_and_monotonic_when_text_is_not_an_exact_slice()
    {
        // Text is longer than the source range it claims, and TextStart is past the start.
        string inner = LongText(12);
        const int sourceStart = 10;
        const int sourceEnd = 60;
        var segments = new[] { new VoicedSegment(inner, Peter, sourceStart, sourceEnd, 20) };

        IReadOnlyList<SpeechChunkPlan> plan = SpeechChunkPlanner.ForSegments(segments);

        Assert.True(plan.Count > 1);
        Assert.Equal(sourceStart, plan[0].SourceStart);
        Assert.Equal(sourceEnd, plan[^1].SourceEnd);
        int previousEnd = sourceStart;
        foreach (SpeechChunkPlan c in plan)
        {
            Assert.True(c.SourceStart >= previousEnd);
            Assert.True(c.SourceEnd >= c.SourceStart);
            Assert.True(c.SourceEnd <= sourceEnd);
            previousEnd = c.SourceEnd;
        }
    }

    [Fact]
    public void JoinText_concatenates_segment_texts_with_spaces()
    {
        var segments = new[]
        {
            new VoicedSegment("One.", Narrator, 0, 4, 0),
            new VoicedSegment("Two.", Peter, 5, 9, 5),
        };

        Assert.Equal("One. Two.", VoicedSegment.JoinText(segments));
    }
}
