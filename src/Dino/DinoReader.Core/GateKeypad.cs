using System.Globalization;

namespace DinoReader.Core;

/// <summary>The grown-up gate's number pad (Slice 164): what a key press does to the typed answer.</summary>
public static class GateKeypad
{
    public const int MaxAnswerDigits = 2;
    public const string BackspaceKey = "⌫";
    public const string OkKey = "OK";

    public static bool IsSubmit(string key) => key == OkKey;

    /// <summary>The answer after a digit or backspace key; OK leaves it unchanged (the caller submits).</summary>
    public static string Press(string answer, string key)
    {
        if (key == OkKey)
        {
            return answer;
        }

        if (key == BackspaceKey)
        {
            return answer.Length > 0 ? answer[..^1] : answer;
        }

        return answer.Length < MaxAnswerDigits ? answer + key : answer;
    }

    /// <summary>
    /// The six answer keys of the card (Slice 363, mock G2): the right answer and five others within 3 of it, all different, in a random order.
    /// A tap on one is an answer like a typed one, so a wrong key rolls a new sum.
    /// </summary>
    public static IReadOnlyList<int> AnswerChoices(int answer, Random random)
    {
        List<int> wrong = [-3, -2, -1, 1, 2, 3];
        Shuffle(wrong, random);
        List<int> choices = [answer, .. wrong.Take(AnswerKeyCount - 1).Select(offset => answer + offset)];
        Shuffle(choices, random);
        return choices;
    }

    public const int AnswerKeyCount = 6;

    private static void Shuffle(List<int> items, Random random)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}

/// <summary>A caption with one word drawn as a key cap (Slice 363): "hold the Space bar" shows "Space bar" as a key, "Type it, then Enter" shows "Enter".</summary>
public readonly record struct CaptionParts(string Before, string Key, string After)
{
    private const char LastLatinLetter = 'ɏ'; // U+024F

    /// <summary>
    /// The caption cut around the first <paramref name="key"/> in it, found ignoring case and by the current culture; the key is the caption's own spelling of it
    /// ("barra espaciadora" for the key "Barra espaciadora"), and a Latin word the caption ends differently ("mellemrumstasten") is taken whole.
    /// A caption with no such word comes back whole in <see cref="Before"/>, with an empty key.
    /// </summary>
    public static CaptionParts Split(string caption, string key)
    {
        int matchLength = 0;
        int at = string.IsNullOrEmpty(key) ? -1 : CultureInfo.CurrentCulture.CompareInfo.IndexOf(caption, key, CompareOptions.IgnoreCase, out matchLength);
        if (at < 0)
        {
            return new CaptionParts(caption, string.Empty, string.Empty);
        }

        int end = at + matchLength;
        while (end < caption.Length && char.IsLetter(caption[end]) && caption[end] <= LastLatinLetter)
        {
            end++;
        }

        return new CaptionParts(caption[..at], caption[at..end], caption[end..]);
    }
}
