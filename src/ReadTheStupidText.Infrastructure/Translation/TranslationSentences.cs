using ReadTheStupidText.Application.Translation;

namespace ReadTheStupidText.Infrastructure.Translation;

/// <summary>
/// Splits text into the sentence-sized pieces a Marian model translates (Slice 75), in reading order, so the
/// first sentences are translated first. A sentence longer than the limit is cut at the last space or
/// comma before it. Pure.
/// </summary>
public static class TranslationSentences
{
    /// <summary>Characters per piece; at about 4 characters per token this stays well under the model's 512-token window.</summary>
    public const int MaxPieceChars = 400;

    private static readonly char[] SoftBreaks = [' ', ',', '،', '、', '，'];

    public static IReadOnlyList<string> Split(string text, int maxChars = MaxPieceChars)
    {
        var pieces = new List<string>();
        foreach (string sentence in TranslationFlow.SplitSentences(text))
        {
            string trimmed = sentence.Trim();
            if (trimmed.Length > 0)
            {
                pieces.AddRange(CutLong(trimmed, maxChars));
            }
        }

        return pieces;
    }

    private static IEnumerable<string> CutLong(string sentence, int maxChars)
    {
        string rest = sentence;
        while (rest.Length > maxChars)
        {
            int cut = rest.LastIndexOfAny(SoftBreaks, maxChars - 1);
            cut = cut <= 0 ? maxChars : cut + 1;
            if (char.IsHighSurrogate(rest[cut - 1]))
            {
                cut--;
            }

            yield return rest[..cut].Trim();
            rest = rest[cut..].Trim();
        }

        if (rest.Length > 0)
        {
            yield return rest;
        }
    }
}
