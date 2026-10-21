using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class ReadChunkMatchTests
{
    [Fact]
    public void A_chunk_of_the_same_text_matches_even_when_whitespace_was_normalized()
    {
        const string text = "First  line.\nSecond line.";
        var chunk = new ReadChunk(0, 1, "First line. Second line.", 0, text.Length);

        Assert.True(chunk.MatchesSource(text));
    }

    [Fact]
    public void A_late_chunk_of_an_earlier_read_does_not_match_the_next_reads_text()
    {
        var late = new ReadChunk(1, 3, "Hello there.", 20, 32);

        Assert.False(late.MatchesSource("A completely different read of about forty chars."));
    }

    [Fact]
    public void A_range_beyond_the_text_never_matches()
    {
        Assert.False(new ReadChunk(0, 1, "abc", 0, 99).MatchesSource("abc"));
    }
}
