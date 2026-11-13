using Sirous.Reading.Application.Usage;

namespace ReadTheStupidText.Tests;

/// <summary>Slice 273 part B: a read reports at most one fallback row, chosen inside that read; a new read counts again.</summary>
public sealed class PerReadFallbacksTests
{
    private sealed class RecordingSink : IFallbackSink
    {
        public List<string> Reasons { get; } = [];

        public void Report(string area, string reason, string? language = null) => Reasons.Add(reason);
    }

    [Fact]
    public void A_stream_failure_and_its_whole_text_retry_are_one_row()
    {
        var sink = new RecordingSink();
        var reads = new PerReadFallbacks(sink);

        reads.Report(7, FallbackArea.Speech, FallbackReason.StreamUnavailable);
        reads.Report(7, FallbackArea.Speech, FallbackReason.NeuralUnavailable);

        Assert.Equal([FallbackReason.StreamUnavailable], sink.Reasons);
    }

    [Fact]
    public void Two_separate_failing_reads_are_two_notes_of_the_same_kind()
    {
        var sink = new RecordingSink();
        var reads = new PerReadFallbacks(sink);

        reads.Report(7, FallbackArea.Speech, FallbackReason.StreamUnavailable);
        reads.Report(8, FallbackArea.Speech, FallbackReason.StreamUnavailable);

        Assert.Equal([FallbackReason.StreamUnavailable, FallbackReason.StreamUnavailable], sink.Reasons);
    }

    [Fact]
    public void A_stream_failure_then_a_later_plain_read_failure_notes_both_kinds()
    {
        var sink = new RecordingSink();
        var reads = new PerReadFallbacks(sink);

        reads.Report(7, FallbackArea.Speech, FallbackReason.StreamUnavailable);
        reads.Report(9, FallbackArea.Speech, FallbackReason.NeuralUnavailable);

        Assert.Equal([FallbackReason.StreamUnavailable, FallbackReason.NeuralUnavailable], sink.Reasons);
    }

    [Fact]
    public void A_fallback_counted_where_it_happened_keeps_the_rest_of_that_read_silent()
    {
        var sink = new RecordingSink();
        var reads = new PerReadFallbacks(sink);

        reads.MarkReported(7);
        reads.Report(7, FallbackArea.Speech, FallbackReason.NeuralUnavailable);

        Assert.Empty(sink.Reasons);
    }

    [Fact]
    public void Calls_with_no_read_id_are_each_their_own_read()
    {
        var sink = new RecordingSink();
        var reads = new PerReadFallbacks(sink);

        reads.Report(null, FallbackArea.Speech, FallbackReason.NeuralNotReady);
        reads.Report(null, FallbackArea.Speech, FallbackReason.NeuralNotReady);

        Assert.Equal(2, sink.Reasons.Count);
    }
}
