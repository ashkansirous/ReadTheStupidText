using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

// Slice 139: the early voice and the read must key the first sentence identically, or the head start is wasted. The hint is
// explicit and scoped (no process-wide state): a read without a hint plans exactly as before.
public class EarlyStartKeyTests
{
    private static readonly SpeechTextChunker.HeadPolicy Cached = SpeechTextChunker.HeadPolicy.Cached;

    private const string Filler = " The quick brown fox jumps over the lazy dog again and again until the evening comes.";

    internal static string[] GoldenTexts() =>
    [
        "Short text.",
        $"It was a bright cold day in April, and the clocks were striking thirteen. Winston Smith slipped quickly through the glass doors.{Filler}{Filler}",
        $"Mr. Smith went to Washington. He met a man there.{Filler}{Filler}",
        $"  \n  Chapter 1\n\nIt was a bright cold day in April.{Filler}{Filler}",
        "Hello there my friend, this is one very long first sentence that keeps going and going without any stop at all until it passes the two hundred character limit for sure, yes indeed it does. Next one.",
        "今天天气非常好，我们一起去公园玩耍。我们在公园里开心地跑步和唱歌。然后我们一起回家吃好吃的晚饭，大家都很开心。明天我们还要去海边看日出，然后在沙滩上玩一整天，直到太阳下山。",
    ];

    private static string Normalize(string text) => LineReflow.ReflowText(text).Trim();

    // The units the sentence cache is asked for, in order, normalized as the cache keys them.
    private static List<string> Units(string text, string? hint) =>
        SpeechChunkPlanner.ForText(text, Cached, firstChunkHint: hint).Take(4)
            .SelectMany(c => SentenceUnits.Split(c.Text)).Select(SentenceCacheKey.Normalize).ToList();

    // Whether the chunker would take the head as the first chunk of the full text, and if it does, whether the early voice's
    // units (the head planned alone) are the first units of the read. A head it does not accept is not forced: null mismatch.
    internal static (bool Accepted, string? Mismatch) Check(string head, string full)
    {
        string hint = Normalize(head);
        if (!SpeechTextChunker.AcceptsHint(Normalize(full), hint, Cached))
        {
            return (false, null);
        }

        List<string> early = Units(head, null);
        List<string> read = Units(full, hint).Take(early.Count).ToList();
        string? mismatch = early.SequenceEqual(read) ? null : $"[{string.Join(" | ", early)}]  ||  [{string.Join(" | ", read)}]  ||  text=[{full}]";
        return (true, mismatch);
    }

    public static IEnumerable<object[]> Texts()
    {
        string filler = Filler;
        yield return [$"It was a bright cold day in April, and the clocks were striking thirteen. Winston Smith slipped quickly through the glass doors.{filler}{filler}"];
        yield return [$"Yes. It was a bright cold day in April, and the clocks were striking thirteen.{filler}{filler}"];
        yield return [$"Mr. Smith went to Washington. He met a man there.{filler}{filler}"];
        yield return [$"  \n  Chapter 1\n\nIt was a bright cold day in April.{filler}{filler}"];
        yield return [$"Short one. Another short one. A third one is here.{filler}{filler}"];
        yield return [$"Line one of a wrapped\nparagraph that goes on. Second sentence here.{filler}{filler}"];
        yield return [$"Is it? Yes! Really.{filler}{filler}"];
        yield return ["Je suis allee a Paris et j'ai vu la tour Eiffel hier soir avec ma soeur. Oui! Nous avons mange des crepes. Puis nous sommes rentrees a la maison tres tard dans la nuit, fatiguees mais contentes. Fin."];
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void The_early_voice_units_are_the_first_units_of_the_read(string text)
    {
        (bool accepted, string? mismatch) = Check(EarlyStartPlan.OriginalHead(text), text);

        Assert.True(accepted);
        Assert.Null(mismatch);
    }

    [Fact]
    public void A_hint_is_the_first_chunk_of_a_read_that_starts_with_it()
    {
        const string head = "Je suis allee a Paris hier soir avec ma soeur. Oui!";
        string text = head + " Nous avons mange des crepes tres bonnes. Puis nous sommes rentrees a la maison tres tard dans la nuit, fatiguees mais contentes. Fin.";

        Assert.Equal(head, SpeechChunkPlanner.ForText(text, Cached, firstChunkHint: head)[0].Text);
    }

    [Fact]
    public void Without_a_hint_the_plan_is_the_plain_split()
    {
        foreach (string text in GoldenTexts())
        {
            Assert.Equal(SpeechTextChunker.Split(text, true, Cached), SpeechTextChunker.Split(text, true, Cached, firstChunkHint: null));
        }
    }

    [Theory]
    [InlineData("Alphabet soup is nice tod")]
    [InlineData("Alphabet soup")]
    public void A_hint_that_does_not_end_a_sentence_changes_nothing(string hint)
    {
        const string text = "Alphabet soup is nice today. Another sentence here that is long enough to matter at all, honestly. And a third.";

        Assert.Equal(SpeechTextChunker.Split(text, true, Cached), SpeechTextChunker.Split(text, true, Cached, hint));
    }

    [Fact]
    public void A_hint_that_ends_a_sentence_is_the_first_chunk()
    {
        const string text = "Alphabet soup is nice today. Another sentence here that is long enough to matter at all, honestly. And a third.";

        Assert.Equal("Alphabet soup is nice today.", SpeechTextChunker.Split(text, true, Cached, "Alphabet soup is nice today.")[0]);
    }

    [Fact]
    public void A_hint_over_the_size_limits_changes_nothing()
    {
        string tooLong = string.Join(' ', Enumerable.Repeat("word", 45)) + ".";
        const string tooMany = "One is here. Two is here. Three is here.";
        string text = $"{tooLong} Next sentence follows here.{Filler}";
        string many = $"{tooMany}{Filler}{Filler}";

        Assert.Equal(SpeechTextChunker.Split(text, true, Cached), SpeechTextChunker.Split(text, true, Cached, tooLong));
        Assert.Equal(SpeechTextChunker.Split(many, true, Cached), SpeechTextChunker.Split(many, true, Cached, tooMany));
    }

    [Fact]
    public void A_hint_over_the_default_policys_cap_changes_nothing()
    {
        const string head = "A first sentence of about eighty characters is right here to test things, ok. Another one is here, and it is a good deal longer.";
        string text = head + Filler + Filler;

        Assert.Equal(SpeechTextChunker.Split(text), SpeechTextChunker.Split(text, true, null, head));
    }

    [Fact]
    public void Hints_are_scoped_to_a_voice_and_taken_once()
    {
        var hints = new FirstChunkHints();
        const string head = "Hello there my friend. How are you today?";
        string text = head + Filler + Filler;
        string voiceA = FirstChunkHints.ScopeOf("F1", null);
        string voiceB = FirstChunkHints.ScopeOf("M1", null);

        Assert.True(hints.Remember(head, voiceA, Cached));

        Assert.Null(hints.Take(text, voiceB));
        Assert.Equal(head, hints.Take(text, voiceA));
        Assert.Null(hints.Take(text, voiceA));
    }

    [Fact]
    public void A_language_pack_is_part_of_the_scope()
    {
        var english = new VoiceModelPaths("root", Language: "en");
        var french = new VoiceModelPaths("root", Language: "fr");

        Assert.NotEqual(FirstChunkHints.ScopeOf("F1", english), FirstChunkHints.ScopeOf("F1", french));
        Assert.NotEqual(FirstChunkHints.ScopeOf("F1", english), FirstChunkHints.ScopeOf("F1", null));
    }

    [Fact]
    public void A_text_that_does_not_start_with_the_head_takes_nothing_and_keeps_it()
    {
        var hints = new FirstChunkHints();
        const string head = "Hello there my friend. How are you today?";
        string scope = FirstChunkHints.ScopeOf(null, null);
        hints.Remember(head, scope, Cached);

        Assert.Null(hints.Take("Goodbye for now. See you later." + Filler, scope));
        Assert.Equal(head, hints.Take(head + Filler, scope));
    }

    [Fact]
    public void Only_a_head_the_chunker_would_accept_is_remembered()
    {
        var hints = new FirstChunkHints();
        string scope = FirstChunkHints.ScopeOf(null, null);

        Assert.True(hints.Remember("A heading with no full stop", scope, Cached)); // a whole paragraph is a sentence boundary
        Assert.False(hints.Remember("One is here. Two is here. Three is here.", scope, Cached));
        Assert.False(hints.Remember(new string('x', 250) + ".", scope, Cached));
        Assert.False(hints.Remember("   ", scope, Cached));
    }

    [Fact]
    public void The_newest_few_heads_are_kept()
    {
        var hints = new FirstChunkHints();
        string scope = FirstChunkHints.ScopeOf(null, null);
        for (int i = 0; i < 6; i++)
        {
            hints.Remember($"Head number {i} is here.", scope, Cached);
        }

        Assert.Null(hints.Take("Head number 0 is here." + Filler, scope));
        Assert.Null(hints.Take("Head number 1 is here." + Filler, scope));
        Assert.NotNull(hints.Take("Head number 5 is here." + Filler, scope));
    }
}

// Golden: what the Windows and Dino readers plan today. They pass no hint, so a change here is a change to those reads.
public class ChunkingWithoutAHintIsUnchangedTests
{
    [Fact]
    public void Golden_plans_for_the_default_and_cached_policies()
    {
        string[] texts = EarlyStartKeyTests.GoldenTexts();
        for (int i = 0; i < texts.Length; i++)
        {
            Assert.Equal(GoldenPlans.Default[i], SpeechTextChunker.Split(texts[i]));
            Assert.Equal(GoldenPlans.Cached[i], SpeechTextChunker.Split(texts[i], true, SpeechTextChunker.HeadPolicy.Cached));
            Assert.Equal(GoldenPlans.Unbiased[i], SpeechTextChunker.Split(texts[i], false));
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
        var bad = new List<string>();
        int accepted = 0;
        for (int i = 0; i < 4000; i++)
        {
            string text = RandomText(rnd, paragraphs);
            (bool ok, string? mismatch) = EarlyStartKeyTests.Check(EarlyStartPlan.OriginalHead(text), text);
            accepted += ok ? 1 : 0;
            if (mismatch is not null)
            {
                bad.Add(mismatch);
            }
        }

        Assert.True(bad.Count == 0, Describe(bad));
        Assert.True(accepted > 3000, $"only {accepted} of 4000 heads were taken as the first chunk");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_voiced_translated_head_units_are_the_first_units_of_the_read_of_the_whole_translation(bool paragraphs)
    {
        var rnd = new Random(11);
        var bad = new List<string>();
        int accepted = 0;
        for (int i = 0; i < 4000; i++)
        {
            string text = RandomText(rnd, paragraphs);
            EarlySplit split = EarlyStartPlan.TranslationSpan(text); // the translation is taken to be the text itself
            string full = EarlyStartPlan.Join(split.Head, split.Remainder, split.Gap);
            (bool ok, string? mismatch) = EarlyStartKeyTests.Check(EarlyStartPlan.VoicedTranslation(split.Head), full);
            accepted += ok ? 1 : 0;
            if (mismatch is not null)
            {
                bad.Add(mismatch);
            }
        }

        Assert.True(bad.Count == 0, Describe(bad));
        Assert.True(accepted > 3000, $"only {accepted} of 4000 heads were taken as the first chunk");
    }

    // Any prefix of any text, taken as a hint: either the plan is exactly the plain split (not accepted), or the hint is the
    // first chunk and the chunks still spell the whole text.
    [Fact]
    public void Any_prefix_as_a_hint_is_ignored_or_a_clean_first_chunk()
    {
        var rnd = new Random(23);
        var cached = SpeechTextChunker.HeadPolicy.Cached;
        for (int i = 0; i < 1500; i++)
        {
            string text = RandomText(rnd, paragraphs: i % 2 == 0);
            string trimmed = LineReflow.ReflowText(text).Trim();
            string hint = trimmed[..rnd.Next(0, trimmed.Length + 1)].TrimEnd();

            IReadOnlyList<string> with = SpeechTextChunker.Split(text, true, cached, hint);

            if (SpeechTextChunker.AcceptsHint(trimmed, hint, cached))
            {
                Assert.Equal(hint, with[0]);
                Assert.Equal(Squash(trimmed), Squash(string.Concat(with)));
            }
            else
            {
                Assert.Equal(SpeechTextChunker.Split(text, true, cached), with);
            }
        }
    }

    private static string Squash(string text) => string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
}
