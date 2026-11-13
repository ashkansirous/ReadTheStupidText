using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Steers the ezafe (the linking -e between a noun and what follows, written with no mark) of the Piper Farsi voices
/// where espeak-ng guesses it wrong (Slice 342). A small word-pair table, applied to the text sent to the engine only:
/// the text shown and the highlight offsets never change. Pure; text with no table pair comes back as the same instance.
/// </summary>
internal static class FarsiEzafe
{
    private const char Kasra = 'ِ';
    private const char ZeroWidthNonJoiner = '‌';

    // First word -> the second words after which the first needs its ezafe. The probe (Slice 342) proved the kasra makes
    // espeak write the "e" ("hal shoma" becomes "hale shoma"); no mark was found that removes an extra ezafe, so there are
    // no suppress pairs (Slice 343's "sobh yek" and "daftar kharid" stay open).
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ForcePairs =
        new Dictionary<string, IReadOnlyList<string>>
        {
            ["حال"] = ["شما"],
        };

    /// <summary>The text for the engine of <paramref name="model"/>: <see cref="Apply"/> for the Farsi Piper model, else unchanged.</summary>
    public static string ForModel(VoiceModelPaths model, string text) =>
        model.Kind == VoiceEngineKind.Vits && model.Language == VoiceChoices.PersianLanguageCode ? Apply(text) : text;

    public static string Apply(string text)
    {
        if (text.Length == 0)
        {
            return text;
        }

        System.Text.StringBuilder? result = null;
        int copied = 0;
        foreach ((int end, _) in MatchedFirstWords(text))
        {
            result ??= new System.Text.StringBuilder(text.Length + 4);
            result.Append(text, copied, end - copied).Append(Kasra);
            copied = end;
        }

        if (result is null)
        {
            return text;
        }

        return result.Append(text, copied, text.Length - copied).ToString();
    }

    // Each (end index of the first word, the word) whose next word is a forced partner.
    private static IEnumerable<(int End, string Word)> MatchedFirstWords(string text)
    {
        List<(int Start, int End)> words = Words(text);
        for (int i = 0; i + 1 < words.Count; i++)
        {
            string first = text[words[i].Start..words[i].End];
            string second = text[words[i + 1].Start..words[i + 1].End];
            if (ForcePairs.TryGetValue(first, out IReadOnlyList<string>? partners)
                && partners.Contains(second)
                && OnlySpaceBetween(text, words[i].End, words[i + 1].Start))
            {
                yield return (words[i].End, first);
            }
        }
    }

    // Any whitespace run (space, no-break space, tab, newline) counts as the gap; the separator is kept as written.
    private static bool OnlySpaceBetween(string text, int from, int to) =>
        to > from && text.AsSpan(from, to - from).Trim().IsEmpty;

    // Runs of letters; a ZWNJ (half space) stays inside its word, so a word with a half space is one word.
    private static List<(int Start, int End)> Words(string text)
    {
        var words = new List<(int, int)>();
        int start = -1;
        for (int i = 0; i <= text.Length; i++)
        {
            bool inWord = i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == ZeroWidthNonJoiner || char.GetUnicodeCategory(text[i]) == System.Globalization.UnicodeCategory.NonSpacingMark);
            if (inWord && start < 0)
            {
                start = i;
            }
            else if (!inWord && start >= 0)
            {
                words.Add((start, i));
                start = -1;
            }
        }

        return words;
    }
}
