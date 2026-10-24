using System.Text.RegularExpressions;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Application.Translation;

/// <summary>
/// The pure decisions around translating before a read (Slice 54): whether to translate at all, how
/// to read a detector's tag, and the chunk-translate-rejoin loop. Platform engines plug in through
/// <see cref="ITranslator"/> and <see cref="ILanguageDetector"/>, so iOS and Windows reuse all of it.
/// </summary>
public static partial class TranslationFlow
{
    /// <summary>Below this the detector's guess is treated as "unknown" (ML Kit's own default threshold).</summary>
    public const float MinimumConfidence = 0.5f;

    /// <summary>A paragraph longer than this is split at sentence ends before translating.</summary>
    public const int MaxPieceLength = 2000;

    /// <summary>How much of the text the language detector sees.</summary>
    public const int DetectionSampleLength = 1000;

    private const string UndeterminedTag = "und";
    private const char SubtagSeparator = '-';
    private const string MacroNorwegianTag = "no";
    private const string BokmalTag = "nb";
    private const string ParagraphJoin = "\n\n";
    private const string SentenceJoin = " ";

    // Zero-width split after a sentence terminator (Latin, Arabic/Persian question mark and full stop,
    // CJK full-width forms), so CJK text with no spaces still splits and nothing is lost.
    [GeneratedRegex(@"(?<=[.!?\u061F\u06D4\u3002\uFF01\uFF1F])")]
    private static partial Regex SentenceEnd();

    /// <summary>
    /// The one sentence splitter (Slice 75): the text cut after each terminator, with the whitespace between
    /// sentences left on the pieces so they concatenate back to the text. Callers trim and drop blanks as they need.
    /// </summary>
    public static string[] SplitSentences(string text) => SentenceEnd().Split(text);

    /// <summary>Translate only when the toggle is on, both languages are known and they differ.</summary>
    public static bool ShouldTranslate(bool enabled, string? source, string? target) =>
        enabled
        && !string.IsNullOrEmpty(source)
        && !string.IsNullOrEmpty(target)
        && !string.Equals(source, target, StringComparison.OrdinalIgnoreCase);

    /// <summary>The lower-case primary language code of a BCP-47 tag ("zh-Latn" becomes "zh", the macrolanguage "no" becomes "nb"), or null for blank or undetermined.</summary>
    public static string? NormalizeTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        string primary = tag.Split(SubtagSeparator)[0].Trim().ToLowerInvariant();
        if (primary.Length == 0 || primary == UndeterminedTag)
        {
            return null;
        }

        return primary == MacroNorwegianTag ? BokmalTag : primary;
    }

    /// <summary>The detected language's code, or null when there is no guess or it is not confident enough.</summary>
    public static string? SourceFromDetection(DetectedLanguage? detected) =>
        detected is { } d && d.Confidence >= MinimumConfidence ? NormalizeTag(d.Tag) : null;

    /// <summary>Whether the engine can translate between these two languages.</summary>
    public static bool IsPairSupported(IReadOnlyCollection<string> supported, string source, string target) =>
        supported.Contains(source, StringComparer.OrdinalIgnoreCase)
        && supported.Contains(target, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Translates <paramref name="text"/> paragraph by paragraph (the speech chunker's paragraph split;
    /// ML Kit takes whole paragraphs) and rejoins it, keeping paragraph breaks. A paragraph over
    /// <see cref="MaxPieceLength"/> is split at sentence ends. Paragraphs run one after another, in
    /// order (the engine serialises anyway); pieces that translate to nothing are dropped. Any failure
    /// surfaces as itself; only the caller's own token being cancelled throws a cancellation.
    /// </summary>
    public static async Task<string> TranslateAsync(ITranslator translator, string text, string source, string target, CancellationToken cancellationToken)
    {
        var translated = new List<string>();
        foreach (string paragraph in SpeechTextChunker.SplitParagraphs(text))
        {
            string result = await TranslateParagraphAsync(translator, paragraph, source, target, cancellationToken);
            if (result.Length > 0)
            {
                translated.Add(result);
            }
        }

        return string.Join(ParagraphJoin, translated);
    }

    /// <summary>At most <paramref name="max"/> characters of <paramref name="text"/>, never cutting a surrogate pair.</summary>
    public static string Prefix(string text, int max)
    {
        if (text.Length <= max)
        {
            return text;
        }

        int length = char.IsHighSurrogate(text[max - 1]) ? max - 1 : max;
        return text[..length];
    }

    /// <summary>The bounded prefix handed to the language detector.</summary>
    public static string DetectionSample(string text) => Prefix(text, DetectionSampleLength);

    private static async Task<string> TranslateParagraphAsync(ITranslator translator, string paragraph, string source, string target, CancellationToken cancellationToken)
    {
        var pieces = new List<string>();
        foreach (string piece in SplitLongParagraph(paragraph))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string translated = (await translator.TranslateAsync(piece, source, target, cancellationToken)).Trim();
            if (translated.Length > 0)
            {
                pieces.Add(translated);
            }
        }

        return string.Join(SentenceJoin, pieces);
    }

    /// <summary>The paragraph whole, or, when very long, runs of whole sentences each within the limit (one longer sentence stays alone).</summary>
    internal static IEnumerable<string> SplitLongParagraph(string paragraph)
    {
        if (paragraph.Length <= MaxPieceLength)
        {
            yield return paragraph;
            yield break;
        }

        var run = new System.Text.StringBuilder();
        foreach (string sentence in SplitSentences(paragraph))
        {
            if (run.Length > 0 && run.Length + sentence.Length > MaxPieceLength)
            {
                yield return run.ToString();
                run.Clear();
            }

            run.Append(sentence);
        }

        if (run.Length > 0)
        {
            yield return run.ToString();
        }
    }
}
