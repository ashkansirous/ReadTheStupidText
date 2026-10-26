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
    private static readonly string[] Words = ["the", "cat", "sat", "on", "a", "mat", "and", "went", "home", "quickly", "happily", "okay"];

    private static string RandomText(Random rnd, bool paragraphs)
    {
        var sb = new System.Text.StringBuilder();
        int sentences = rnd.Next(2, 12);
        for (int s = 0; s < sentences; s++)
        {
            int n = rnd.Next(1, 30);
            for (int w = 0; w < n; w++)
            {
                string word = Words[rnd.Next(Words.Length)];
                sb.Append(w == 0 ? char.ToUpper(word[0]) + word[1..] : word).Append(' ');
            }

            sb.Length--;
            sb.Append(rnd.Next(10) == 0 ? "?" : "!");
            sb.Append(paragraphs && rnd.Next(4) == 0 ? "\n\n" : " ");
        }

        return sb.ToString();
    }

    private static List<string> Units(string text) =>
        SpeechChunkPlanner.ForText(text, SpeechTextChunker.HeadPolicy.Cached).Take(4)
            .SelectMany(c => SentenceUnits.Split(c.Text)).Select(SentenceCacheKey.Normalize).ToList();

    private static string? Mismatch(string head, string full)
    {
        List<string> early = Units(head);
        string read = Units(full)[0];
        return head.Length > 0 && early.Count > 0 && early[0] != read ? $"{early[0]}  ||  {read}  ||  text=[{full}]" : null;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_original_head_keys_its_first_sentence_as_the_read_does(bool paragraphs)
    {
        var rnd = new Random(7);
        List<string> bad = Enumerable.Range(0, 4000).Select(_ => RandomText(rnd, paragraphs))
            .Select(t => Mismatch(EarlyStartPlan.OriginalHead(t), t)).OfType<string>().ToList();

        Assert.True(bad.Count == 0, Describe(bad));
    }

    private static string Describe(List<string> bad) => bad.Count + " mismatches, e.g. " + string.Join(Environment.NewLine, bad.Take(5));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_voiced_translated_head_keys_its_first_sentence_as_the_read_of_the_whole_translation_does(bool paragraphs)
    {
        var rnd = new Random(11);
        var bad = new List<string>();
        for (int i = 0; i < 4000; i++)
        {
            string text = RandomText(rnd, paragraphs);
            EarlySplit split = EarlyStartPlan.TranslationSpan(text); // the translation is taken to be the text itself
            string full = EarlyStartPlan.Join(split.Head, split.Remainder, split.Gap);
            if (Mismatch(EarlyStartPlan.VoicedTranslation(split.Head, split.Remainder.Length > 0), full) is { } m)
            {
                bad.Add(m);
            }
        }

        Assert.True(bad.Count == 0, Describe(bad));
    }
}
