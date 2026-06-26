using ReadTheStupidText.Infrastructure.Reading;

namespace ReadTheStupidText.Tests;

public class SpeechTextChunkerTests
{
    // Mirrors SpeechTextChunker.TargetChunkMax (private): no chunk should greatly
    // exceed this, and a single word is the only thing allowed past it.
    private const int TargetChunkMax = 240;

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
}
