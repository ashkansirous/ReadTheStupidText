using System.Text.RegularExpressions;
using ReadTheStupidText.Application.Translation;

namespace ReadTheStupidText.Application.Reading;

/// <summary>A text cut after its first few sentences: the head, the whitespace after it, and the rest.</summary>
public readonly record struct EarlySplit(string Head, string Gap, string Remainder);

/// <summary>
/// What the phone does with its idle time before the tap (Slice 139, Decision 147), decided purely: the head of the
/// original to voice (first 2 sentences, else about 120 characters at a word boundary), the span to translate (first 3
/// sentences, about 300 characters) and the part of its translation to voice (first 2 sentences). Sentences end at Latin,
/// Arabic and Persian, or CJK terminators, and a head never crosses a paragraph break.
/// </summary>
public static partial class EarlyStartPlan
{
    public const int OriginalSentences = 2;
    public const int OriginalMaxChars = 200;
    public const int FallbackChars = 120;
    public const int TranslateSentences = 3;
    public const int TranslateMaxChars = 300;
    public const int VoicedTranslationSentences = 2;

    private const string ParagraphJoin = "\n\n";
    private const string SentenceJoin = " ";
    private const string Terminators = ".!?؟۔。！？";

    // A blank line (two line breaks with only white space between), the paragraph break the chunker reads (the same pattern).
    [GeneratedRegex(@"\r?\n\s*\r?\n")]
    private static partial Regex ParagraphBreak();

    /// <summary>The part of the original the head start voices.</summary>
    public static string OriginalHead(string text) => Cut(text, OriginalSentences, OriginalMaxChars, FallbackChars, wholeFirstSentence: false, alignUnits: true).Head;

    /// <summary>The span to translate early, and what is left of the text after it.</summary>
    public static EarlySplit TranslationSpan(string text) => Cut(text, TranslateSentences, TranslateMaxChars, TranslateMaxChars, wholeFirstSentence: true, alignUnits: false);

    /// <summary>The part of an early translation the head start voices.</summary>
    public static string VoicedTranslation(string translatedHead) =>
        Cut(translatedHead, VoicedTranslationSentences, OriginalMaxChars, FallbackChars, wholeFirstSentence: false, alignUnits: false).Head;

    /// <summary>The translation of a text from the translation of its head and of the rest, keeping the break between them.</summary>
    public static string Join(string translatedHead, string translatedRemainder, string gap)
    {
        if (string.IsNullOrWhiteSpace(translatedRemainder))
        {
            return translatedHead;
        }

        string separator = ParagraphBreak().IsMatch(gap) ? ParagraphJoin : SentenceJoin;
        return $"{translatedHead}{separator}{translatedRemainder.Trim()}";
    }

    // Whole sentences while their count and total length fit, cut where the speech chunker cuts. A first sentence that is too
    // long, or text with no sentence ending at all, is cut at a word boundary for voicing; for translating it is kept whole,
    // because a sentence cut mid-clause translates badly (wholeFirstSentence).
    private static EarlySplit Cut(string text, int maxSentences, int maxChars, int fallbackChars, bool wholeFirstSentence, bool alignUnits)
    {
        string trimmed = (text ?? string.Empty).TrimStart();
        if (trimmed.Length == 0)
        {
            return new EarlySplit(string.Empty, string.Empty, string.Empty);
        }

        Match paragraphEnd = ParagraphBreak().Match(trimmed);
        string region = paragraphEnd.Success ? trimmed[..paragraphEnd.Index] : trimmed;
        List<int> ends = SpeechTextChunker.SentenceEnds(region);
        DropUnfinishedSentence(region, ends);
        int end = SentencesEnd(ends, maxSentences, maxChars);
        if (end <= 0)
        {
            end = wholeFirstSentence ? WholeFirst(region, ends) : WordCut(region, fallbackChars);
        }
        else if (alignUnits)
        {
            end = ToUnitBoundary(trimmed, ends, end);
        }

        return Split(trimmed, end);
    }

    // Where the last whole sentence that fits ends, or 0 when the first alone is too long.
    private static int SentencesEnd(List<int> ends, int maxSentences, int maxChars)
    {
        int end = 0;
        for (int count = 0; count < ends.Count && count < maxSentences && ends[count] <= maxChars; count++)
        {
            end = ends[count];
        }

        return end;
    }

    // The sentence cache keys a unit (SentenceUnits): a sentence shorter than a unit joins the next one, or the previous one
    // when its chunk ends. A head cut after two sentences can end in a short unit that the read's first chunk builds
    // differently (it has the next sentence after it, or peels its head at the same two), and the first unit then keys
    // differently: a cache miss on the first sentence at Play. So the head ends where the read's own first chunk closes a
    // unit at or after the cut: it is then made of the read's exact units.
    private static int ToUnitBoundary(string trimmed, List<int> ends, int end)
    {
        int sentences = ends.IndexOf(end) + 1;
        IReadOnlyList<SpeechChunkPlan> chunks = SpeechChunkPlanner.ForText(trimmed, SpeechTextChunker.HeadPolicy.Cached);
        int covered = 0;
        foreach (int count in SentenceUnits.SentenceCounts(chunks[0].Text))
        {
            covered += count;
            if (covered >= sentences)
            {
                return covered <= ends.Count ? ends[covered - 1] : end;
            }
        }

        return end;
    }

    // A last sentence with no terminator may still be typed or cut: it is never part of a head (a region with no sentence
    // ending at all is left to the word cut).
    private static void DropUnfinishedSentence(string region, List<int> ends)
    {
        if (ends.Count > 0 && !EndsSentence(region))
        {
            ends.RemoveAt(ends.Count - 1);
        }
    }

    // The first sentence however long (the whole region when it has no ending at all).
    private static int WholeFirst(string region, List<int> ends) =>
        ends.Count > 0 ? ends[0] : region.TrimEnd().Length;

    private static bool EndsSentence(string region)
    {
        string trimmed = region.TrimEnd();
        return trimmed.Length > 0 && Terminators.Contains(trimmed[^1]);
    }

    // The whole region when it is short, else up to the limit, back to the last space (unspaced CJK is cut at the limit).
    private static int WordCut(string region, int limit)
    {
        string trimmed = region.TrimEnd();
        if (trimmed.Length <= limit)
        {
            return trimmed.Length;
        }

        int space = trimmed.LastIndexOf(' ', limit);
        return space > 0 ? space : TranslationFlow.Prefix(trimmed, limit).Length;
    }

    private static EarlySplit Split(string text, int end)
    {
        string head = text[..end].TrimEnd();
        string rest = text[head.Length..];
        string remainder = rest.TrimStart();
        return new EarlySplit(head, rest[..(rest.Length - remainder.Length)], remainder);
    }
}
