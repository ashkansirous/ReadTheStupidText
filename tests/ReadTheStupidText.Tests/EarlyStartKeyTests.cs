using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

// Slice 139: the early voice and the read must key the first sentence identically, or the head start is wasted.
public class EarlyStartKeyTests
{
    private static string? FirstUnit(string text)
    {
        var chunks = SpeechChunkPlanner.ForText(text, SpeechTextChunker.HeadPolicy.Cached);
        return SentenceUnits.Split(chunks[0].Text)[0];
    }

    // What the early voice prepares: the units of the planned head, as PrepareChunksAsync splits them.
    private static List<string> EarlyUnits(string text)
    {
        string head = EarlyStartPlan.OriginalHead(text);
        string prefix = head.Length > 2000 ? head[..2000] : head;
        return SpeechChunkPlanner.ForText(prefix, SpeechTextChunker.HeadPolicy.Cached).Take(4)
            .SelectMany(c => SentenceUnits.Split(c.Text)).Select(SentenceCacheKey.Normalize).ToList();
    }

    public static IEnumerable<object[]> Texts()
    {
        string filler = " The quick brown fox jumps over the lazy dog again and again until the evening comes.";
        yield return [$"It was a bright cold day in April, and the clocks were striking thirteen. Winston Smith slipped quickly through the glass doors.{filler}{filler}"];
        yield return [$"Yes. It was a bright cold day in April, and the clocks were striking thirteen.{filler}{filler}"];
        yield return [$"Mr. Smith went to Washington. He met a man there.{filler}{filler}"];
        yield return [$"  \n  Chapter 1\n\nIt was a bright cold day in April.{filler}{filler}"];
        yield return [$"Hello there my friend, this is one very long first sentence that keeps going and going without any stop at all until it passes the two hundred character limit for sure, yes indeed it does. Next one.{filler}"];
        yield return [$"Short one. Another short one. A third one is here.{filler}{filler}"];
        yield return [$"Line one of a wrapped\nparagraph that goes on. Second sentence here.{filler}{filler}"];
        yield return [$"Is it? Yes! Really.{filler}{filler}"];
        yield return [$"A first sentence of about eighty characters is right here to test things, ok. Then a second sentence that is long enough to push the total beyond two hundred characters in all.{filler}"];
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void The_first_sentence_has_the_same_cache_text_in_the_early_voice_and_the_read(string text)
    {
        string read = SentenceCacheKey.Normalize(FirstUnit(text)!);
        Assert.Equal(read, EarlyUnits(text)[0]);
    }
}

public class EarlyStartKeyFuzz
{
    [Fact]
    public void Fuzz()
    {
        var rnd = new Random(7);
        string[] words = ["the", "cat", "sat", "on", "a", "mat", "and", "went", "home", "quickly", "happily", "okay"];
        var bad = new List<string>();
        for (int i = 0; i < 4000; i++)
        {
            var sb = new System.Text.StringBuilder();
            int sentences = rnd.Next(2, 12);
            for (int s = 0; s < sentences; s++)
            {
                int n = rnd.Next(1, 30);
                for (int w = 0; w < n; w++) { string word = words[rnd.Next(words.Length)]; sb.Append(w == 0 ? char.ToUpper(word[0]) + word[1..] : word).Append(' '); }
                sb.Length--;
                sb.Append(rnd.Next(10) == 0 ? "? " : "! ");
            }
            string text = sb.ToString();
            var chunks = SpeechChunkPlanner.ForText(text, SpeechTextChunker.HeadPolicy.Cached);
            var read = SentenceUnits.Split(chunks[0].Text).Select(ReadTheStupidText.Application.Reading.SentenceCacheKey.Normalize).ToList();
            string head = EarlyStartPlan.OriginalHead(text);
            var early = SpeechChunkPlanner.ForText(head, SpeechTextChunker.HeadPolicy.Cached).Take(4)
                .SelectMany(c => SentenceUnits.Split(c.Text)).Select(ReadTheStupidText.Application.Reading.SentenceCacheKey.Normalize).ToList();
            if (early.Count > 0 && early[0] != read[0]) { bad.Add($"{early[0]}  ||  {read[0]}  ||  len={text.Length} head=[{head}] text=[{text}]"); }
        }
        Assert.True(bad.Count == 0, bad.Count + " mismatches, e.g.\n" + string.Join("\n", bad.Take(8)));
    }
}
