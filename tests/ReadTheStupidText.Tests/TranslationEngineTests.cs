using ReadTheStupidText.Infrastructure.Translation;

namespace ReadTheStupidText.Tests;

public class TranslationEngineTests
{
    private static readonly TranslationModelSpec Spec = new("fr", "en", null, DecoderStartTokenId: 9, EosTokenId: 0, PadTokenId: 9, MaxLength: 12);

    [Fact]
    public void Greedy_decoding_follows_the_best_token_until_the_end_token()
    {
        var session = new ScriptedSession(5, 6, 7, Spec.EosTokenId);

        IReadOnlyList<int> ids = GreedyDecoder.Decode(session, Spec, 100, CancellationToken.None);

        Assert.Equal([5, 6, 7], ids);
        Assert.Equal([[9], [9, 5], [9, 5, 6], [9, 5, 6, 7]], session.Prefixes);
    }

    [Fact]
    public void Greedy_decoding_never_picks_the_pad_token()
    {
        // The pad id scores highest everywhere; the next-best token (3) is taken instead.
        var session = new FuncSession(ids => ids.Count < 3 ? Scores(best: 9, runnerUp: 3) : Scores(best: 0));

        IReadOnlyList<int> ids = GreedyDecoder.Decode(session, Spec, 100, CancellationToken.None);

        Assert.Equal([3, 3], ids);
    }

    [Fact]
    public void Greedy_decoding_stops_at_the_length_cap()
    {
        var session = new FuncSession(_ => Scores(best: 4));

        Assert.Equal(3, GreedyDecoder.Decode(session, Spec, 3, CancellationToken.None).Count);
        Assert.Equal(Spec.MaxLength - 1, GreedyDecoder.Decode(session, Spec, 1000, CancellationToken.None).Count);
    }

    [Fact]
    public void Greedy_decoding_honours_cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            GreedyDecoder.Decode(new FuncSession(_ => Scores(best: 4)), Spec, 10, cts.Token));
    }

    [Fact]
    public void The_new_token_cap_grows_with_the_source()
    {
        Assert.True(GreedyDecoder.MaxNewTokensFor(50) > GreedyDecoder.MaxNewTokensFor(5));
        Assert.True(GreedyDecoder.MaxNewTokensFor(0) > 0);
    }

    [Fact]
    public void The_spec_parses_with_and_without_a_target_token()
    {
        const string json = """{"source":"en","target":"zh","targetToken":">>cmn_Hans<<","decoderStartTokenId":65000,"eosTokenId":0,"padTokenId":65000,"maxLength":512}""";

        TranslationModelSpec spec = TranslationModelSpec.Parse(json);

        Assert.Equal(">>cmn_Hans<<", spec.TargetToken);
        Assert.Equal(65000, spec.DecoderStartTokenId);
        Assert.Null(TranslationModelSpec.Parse(json.Replace(">>cmn_Hans<<", "")).TargetToken);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("not json")]
    [InlineData("""{"source":"en","target":"fr","decoderStartTokenId":1,"eosTokenId":0,"padTokenId":1,"maxLength":0}""")]
    public void A_broken_spec_is_rejected(string json) =>
        Assert.Throws<FormatException>(() => TranslationModelSpec.Parse(json));

    [Fact]
    public void Text_splits_into_sentences_in_reading_order()
    {
        IReadOnlyList<string> pieces = TranslationSentences.Split("Bonjour. Comment vas-tu ? Très bien !");

        Assert.Equal(["Bonjour.", "Comment vas-tu ?", "Très bien !"], pieces);
    }

    [Fact]
    public void Cjk_text_without_spaces_still_splits() =>
        Assert.Equal(["你好。", "再见！"], TranslationSentences.Split("你好。再见！"));

    [Fact]
    public void A_long_sentence_is_cut_at_a_space_within_the_limit()
    {
        string sentence = string.Join(' ', Enumerable.Repeat("mot", 40));

        IReadOnlyList<string> pieces = TranslationSentences.Split(sentence, maxChars: 50);

        Assert.True(pieces.Count > 2);
        Assert.All(pieces, p => Assert.True(p.Length <= 50));
        Assert.Equal(sentence, string.Join(' ', pieces));
    }

    [Fact]
    public void Blank_text_has_no_sentences() => Assert.Empty(TranslationSentences.Split("  \n "));

    private static float[] Scores(int best, int runnerUp = -1)
    {
        var scores = new float[10];
        scores[best] = 5;
        if (runnerUp >= 0)
        {
            scores[runnerUp] = 4;
        }

        return scores;
    }

    private sealed class ScriptedSession(params int[] script) : IMarianDecoderSession
    {
        public List<int[]> Prefixes { get; } = [];

        public float[] NextTokenScores(IReadOnlyList<int> decoderIds)
        {
            Prefixes.Add(decoderIds.ToArray());
            return Scores(script[decoderIds.Count - 1]);
        }
    }

    private sealed class FuncSession(Func<IReadOnlyList<int>, float[]> scores) : IMarianDecoderSession
    {
        public float[] NextTokenScores(IReadOnlyList<int> decoderIds) => scores(decoderIds);
    }
}
