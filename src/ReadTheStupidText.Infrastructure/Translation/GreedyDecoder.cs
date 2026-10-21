namespace ReadTheStupidText.Infrastructure.Translation;

/// <summary>One encoded sentence plus the ability to ask the decoder for the next-token scores.</summary>
public interface IMarianDecoderSession
{
    /// <summary>The scores over the vocabulary for the token that follows <paramref name="decoderIds"/>.</summary>
    float[] NextTokenScores(IReadOnlyList<int> decoderIds);
}

/// <summary>
/// Greedy autoregressive decoding for a Marian model (Slice 75): start from the decoder start token,
/// repeatedly take the best-scoring token (never the pad token) until the end-of-sentence token or the
/// length cap. Pure, so it is tested against a fake session without any model.
/// </summary>
public static class GreedyDecoder
{
    /// <summary>The generated token ids, without the start token and without the end token.</summary>
    public static IReadOnlyList<int> Decode(IMarianDecoderSession session, TranslationModelSpec spec, int maxNewTokens, CancellationToken cancellationToken)
    {
        var ids = new List<int> { spec.DecoderStartTokenId };
        int limit = Math.Min(maxNewTokens, spec.MaxLength - 1);
        for (int step = 0; step < limit; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int next = ArgMax(session.NextTokenScores(ids), spec.PadTokenId);
            if (next == spec.EosTokenId)
            {
                break;
            }

            ids.Add(next);
        }

        return ids.Skip(1).ToList();
    }

    /// <summary>The best token other than <paramref name="forbidden"/>.</summary>
    internal static int ArgMax(float[] scores, int forbidden)
    {
        int best = -1;
        for (int i = 0; i < scores.Length; i++)
        {
            if (i != forbidden && (best < 0 || scores[i] > scores[best]))
            {
                best = i;
            }
        }

        return best;
    }

    /// <summary>A cap on new tokens for a source of <paramref name="sourceLength"/> tokens: translations rarely exceed about twice the source.</summary>
    public static int MaxNewTokensFor(int sourceLength) => sourceLength * 2 + 16;
}
