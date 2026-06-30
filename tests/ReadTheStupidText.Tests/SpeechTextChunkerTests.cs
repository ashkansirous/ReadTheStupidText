using ReadTheStupidText.Infrastructure.Reading;

namespace ReadTheStupidText.Tests;

public class SpeechTextChunkerTests
{
    // Mirrors SpeechTextChunker.TargetChunkMax (private): no chunk should greatly
    // exceed this, and a single word is the only thing allowed past it.
    private const int TargetChunkMax = 240;

    // Mirrors SpeechTextChunker.FirstChunkMax (private): the first chunk to play is
    // kept short so time-to-first-audio is a fast synthesis.
    private const int FirstChunkMax = 120;

    [Fact]
    public void Short_text_stays_a_single_trimmed_chunk()
    {
        IReadOnlyList<string> chunks = SpeechTextChunker.Split("  Hello there.  ");

        Assert.Single(chunks);
        Assert.Equal("Hello there.", chunks[0]);
    }

    [Fact]
    public void Whitespace_only_returns_a_single_chunk()
    {
        Assert.Single(SpeechTextChunker.Split("   \n  "));
    }

    [Fact]
    public void Long_text_splits_into_multiple_chunks_each_within_target()
    {
        string sentence = "This is a moderately long sentence that carries some weight. ";
        string text = string.Concat(Enumerable.Repeat(sentence, 12)); // ~720 chars

        IReadOnlyList<string> chunks = SpeechTextChunker.Split(text);

        Assert.True(chunks.Count > 1, "long text should split into several chunks");
        Assert.All(chunks, c => Assert.True(c.Length <= TargetChunkMax, $"chunk over target: {c.Length}"));
        Assert.All(chunks, c => Assert.False(string.IsNullOrWhiteSpace(c)));
    }

    [Fact]
    public void Paragraph_boundaries_start_new_chunks()
    {
        string p1 = string.Concat(Enumerable.Repeat("Alpha sentence here. ", 8)).Trim();
        string p2 = string.Concat(Enumerable.Repeat("Beta sentence here. ", 8)).Trim();
        string text = p1 + "\n\n" + p2;

        IReadOnlyList<string> chunks = SpeechTextChunker.Split(text);

        // No chunk should straddle the paragraph break (mix Alpha and Beta).
        Assert.DoesNotContain(chunks, c => c.Contains("Alpha") && c.Contains("Beta"));
    }

    [Fact]
    public void First_chunk_of_a_long_read_is_just_the_leading_sentence()
    {
        // One paragraph of several sentences, over SingleChunkMax so it splits.
        string text = "First short sentence. " +
            string.Concat(Enumerable.Repeat("Then more padding sentence here. ", 8));

        IReadOnlyList<string> chunks = SpeechTextChunker.Split(text);

        Assert.True(chunks.Count > 1, "long text should split");
        Assert.Equal("First short sentence.", chunks[0]);
    }

    [Fact]
    public void A_single_oversized_sentence_is_hard_wrapped_on_words()
    {
        string longSentence = string.Concat(Enumerable.Repeat("word ", 80)).Trim(); // ~400 chars, no . ! ?

        IReadOnlyList<string> chunks = SpeechTextChunker.Split(longSentence);

        Assert.True(chunks.Count > 1, "an oversized word-only sentence should wrap");
        Assert.All(chunks, c => Assert.True(c.Length <= TargetChunkMax, $"chunk over target: {c.Length}"));
    }

    // A faithful facsimile of read #54 from input-20260630.log — a 707-char clipboard
    // read whose first audio did not start until 5865 ms (the "~5 second" read). The
    // text is a copied diagnostic-log line + PR description: its leading run has no
    // sentence-ending ".!?<space>" for ~240 chars (the ":" / "|" / "·" separators and
    // "11:32.251" don't break, since SentenceBreak needs punctuation *followed by*
    // whitespace), so the first "sentence" is itself oversized and gets hard-wrapped.
    private const string SlowRead54 =
        "a phone number:11:32.251 51 Hotkey Read None Chrome | Slice 21: daily on-disk " +
        "diagnostic logs + open-logs button by ashkansirous Pull Request 123 " +
        "ashkansirous/ReadTheStupidText - Google Chrome 5338 5338 Adds two per-day " +
        "diagnostic files under the package TemporaryFolder logs: a Serilog rolling " +
        "system log carrying every id-correlated action and exception with fixed " +
        "Info Debug Warning Error levels, and an append-only input log the " +
        "ActivityInputLog writer adds one TSV row per activity-state transition to so " +
        "the two files join on the id.";

    [Fact]
    public void Slow_read_54_first_chunk_is_a_short_head_for_fast_first_audio()
    {
        IReadOnlyList<string> chunks = SpeechTextChunker.Split(SlowRead54);

        // Regression for the ~5 s latency: before the fix, chunk[0] was ~235 chars
        // because the leading "sentence" had no early ".!?<space>" boundary and was
        // hard-wrapped near the target, so the first synthesis (which gates
        // time-to-first-audio) was large. The first-chunk bias now peels a short head
        // regardless of punctuation, so the first synthesis is fast.
        Assert.True(chunks.Count > 1, "long text should split");
        Assert.True(
            chunks[0].Length <= FirstChunkMax,
            $"first chunk should be a short head; got {chunks[0].Length} chars");
        Assert.False(string.IsNullOrWhiteSpace(chunks[0]));
    }
}
