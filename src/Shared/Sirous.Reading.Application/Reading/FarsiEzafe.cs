using Sirous.Reading.Domain.Reading;

namespace Sirous.Reading.Application.Reading;

/// <summary>
/// Steers the ezafe (the linking -e between a noun and what follows, written with no mark) of the Piper Farsi voices
/// where espeak-ng guesses it wrong (Slice 342). A small word-pair table, applied to the text sent to the engine only:
/// the display text and its offsets never change (the engine-side string, which may be longer by the added kasras and commas, is never mapped back). Pure; text with no table pair comes back as the same instance.
/// </summary>
internal static class FarsiEzafe
{
    private const char Kasra = 'ِ';
    private const char ZeroWidthNonJoiner = '‌';
    private const char Comma = '،';
    private const string ClauseEndMarks = ".!?؟،؛:…";
    private const string ClosingMarks = "»\"')]”’";

    // First word -> the second words after which the first needs its ezafe. The probe (Slice 342) proved the kasra makes
    // espeak write the "e" ("hal shoma" becomes "hale shoma"); no mark removes an extra ezafe, so there are no suppress
    // pairs: the extra-ezafe pairs of Slice 343 ("sobh yek", "daftar kharid", "rooz sobh") get a pause, see CommaPairs.
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ForcePairs =
        new Dictionary<string, IReadOnlyList<string>>
        {
            ["حال"] = ["شما"],
        };

    // First word -> the second words before which a comma goes after it (Slice 343, third listen): the model adds an extra
    // ezafe there ("sobhe yek", "rooze sobh", "daftare kharid") and the comma, an Arabic comma, stopped it for all three
    // clips. Exactly these pairs, nothing else.
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> CommaPairs =
        new Dictionary<string, IReadOnlyList<string>>
        {
            ["صبح"] = ["یک"],
            ["روز"] = ["صبح"],
            ["دفتر"] = ["خرید"],
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
        foreach ((int end, char mark) in MatchedFirstWords(text))
        {
            result ??= new System.Text.StringBuilder(text.Length + 4);
            result.Append(text, copied, end - copied).Append(mark);
            copied = end;
        }

        if (result is null)
        {
            return text;
        }

        return result.Append(text, copied, text.Length - copied).ToString();
    }

    // Each (end index of the first word, the mark to put there) whose next word is a table partner.
    private static IEnumerable<(int End, char Mark)> MatchedFirstWords(string text)
    {
        List<(int Start, int End)> words = Words(text);
        for (int i = 0; i + 1 < words.Count; i++)
        {
            string first = text[words[i].Start..words[i].End];
            string second = text[words[i + 1].Start..words[i + 1].End];
            if (OnlySpaceBetween(text, words[i].End, words[i + 1].Start) && MarkFor(first, second) is char mark
                && (mark != Comma || !HasLineBreak(text, words[i].End, words[i + 1].Start))
                && (!IsVerbOnlyPair(first, second) || ClauseEndsAt(text, words[i + 1].End)))
            {
                yield return (words[i].End, mark);
            }
        }
    }

    private static char? MarkFor(string first, string second)
    {
        if (InTable(ForcePairs, first, second))
        {
            return Kasra;
        }

        return InTable(CommaPairs, first, second) ? Comma : null;
    }

    // "daftar kharid" is the verb "bought" only when "kharid" ends the clause; before anything else it can be the noun
    // phrase "daftar-e kharid" (shopping notebook), whose ezafe is right and stays untouched (Slice 343, review pass 2).
    private static bool IsVerbOnlyPair(string first, string second) => first == "دفتر" && second == "خرید";

    // True when, after spaces and closing marks (» " ' ) ] and the curly quotes), a clause end follows: . ! ? ؟ ، ؛ : … a line break or the end of the text.
    private static bool ClauseEndsAt(string text, int index)
    {
        for (int i = index; i < text.Length; i++)
        {
            char c = text[i];
            if (c is '\n' or '\r' || ClauseEndMarks.Contains(c))
            {
                return true;
            }

            if (!(char.IsWhiteSpace(c) || ClosingMarks.Contains(c)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool InTable(IReadOnlyDictionary<string, IReadOnlyList<string>> table, string first, string second) =>
        table.TryGetValue(first, out IReadOnlyList<string>? partners) && partners.Contains(second);

    // A comma is a pause, so it never crosses a line or paragraph break (the kasra may).
    private static bool HasLineBreak(string text, int from, int to) =>
        text.AsSpan(from, to - from).IndexOfAny('\n', '\r') >= 0;

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
