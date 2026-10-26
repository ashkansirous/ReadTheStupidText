using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

// Slice 139: the early voice and the read must key the first sentence identically, or the head start is wasted.
public class EarlyStartKeyTests
{
    // The units the sentence cache is asked for, in order, normalized as the cache keys them.
    internal static List<string> Units(string text) =>
        SpeechChunkPlanner.ForText(text, SpeechTextChunker.HeadPolicy.Cached).Take(4)
            .SelectMany(c => SentenceUnits.Split(c.Text)).Select(SentenceCacheKey.Normalize).ToList();

    // The early voice stores the units of the head it voiced (registering it first, as EarlyStartVoice does); the read of the
    // whole text then starts with the same head as its first chunk, so its first units are exactly those.
    internal static string? Mismatch(string head, string full)
    {
        if (head.Length == 0)
        {
            return null;
        }

        EarlyFirstChunks.Register(head);
        try
        {
            List<string> early = Units(head);
            List<string> read = Units(full).Take(early.Count).ToList();
            return early.SequenceEqual(read) ? null : $"[{string.Join(" | ", early)}]  ||  [{string.Join(" | ", read)}]  ||  text=[{full}]";
        }
        finally
        {
            EarlyFirstChunks.Remove(head);
        }
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
        yield return ["Je suis allee a Paris et j'ai vu la tour Eiffel hier soir avec ma soeur. Oui! Nous avons mange des crepes. Puis nous sommes rentrees a la maison tres tard dans la nuit, fatiguees mais contentes. Fin."];
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void The_early_voice_units_are_the_first_units_of_the_read(string text)
    {
        Assert.Null(Mismatch(EarlyStartPlan.OriginalHead(text), text));
    }

    [Fact]
    public void A_registered_head_is_the_first_chunk_of_a_read_that_starts_with_it()
    {
        const string head = "Je suis allee a Paris hier soir avec ma soeur. Oui!";
        string text = head + " Nous avons mange des crepes tres bonnes. Puis nous sommes rentrees a la maison tres tard dans la nuit, fatiguees mais contentes. Fin.";
        EarlyFirstChunks.Register(head);
        try
        {
            Assert.Equal(head, SpeechChunkPlanner.ForText(text, SpeechTextChunker.HeadPolicy.Cached)[0].Text);
        }
        finally
        {
            EarlyFirstChunks.Remove(head);
        }
    }

    [Fact]
    public void A_head_that_is_not_a_whole_word_prefix_changes_nothing()
    {
        const string text = "Alphabet soup is nice today. Another sentence here that is long enough to matter at all, honestly.";
        List<string> without = SpeechTextChunker.Split(text).ToList();
        EarlyFirstChunks.Register("Alphabet soup is nice tod");
        try
        {
            Assert.Equal(without, SpeechTextChunker.Split(text).ToList());
        }
        finally
        {
            EarlyFirstChunks.Remove("Alphabet soup is nice tod");
        }
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

    private static string Describe(List<string> bad) => bad.Count + " mismatches, e.g. " + string.Join(Environment.NewLine, bad.Take(5));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_original_head_units_are_the_first_units_of_the_read(bool paragraphs)
    {
        var rnd = new Random(7);
        List<string> bad = Enumerable.Range(0, 4000).Select(_ => RandomText(rnd, paragraphs))
            .Select(t => EarlyStartKeyTests.Mismatch(EarlyStartPlan.OriginalHead(t), t)).OfType<string>().ToList();

        Assert.True(bad.Count == 0, Describe(bad));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_voiced_translated_head_units_are_the_first_units_of_the_read_of_the_whole_translation(bool paragraphs)
    {
        var rnd = new Random(11);
        var bad = new List<string>();
        for (int i = 0; i < 4000; i++)
        {
            string text = RandomText(rnd, paragraphs);
            EarlySplit split = EarlyStartPlan.TranslationSpan(text); // the translation is taken to be the text itself
            string full = EarlyStartPlan.Join(split.Head, split.Remainder, split.Gap);
            if (EarlyStartKeyTests.Mismatch(EarlyStartPlan.VoicedTranslation(split.Head), full) is { } m)
            {
                bad.Add(m);
            }
        }

        Assert.True(bad.Count == 0, Describe(bad));
    }
}
