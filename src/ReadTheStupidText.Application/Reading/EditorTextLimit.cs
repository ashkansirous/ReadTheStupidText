namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// How much text the phone's editor is given (Slice 148). A 36 MB text file crashed the review page with an <c>OutOfMemoryError</c> inside the
/// native <c>EditText.setText</c>, so a longer text is cut to <see cref="MaxCharacters"/> (at a line or word break near the end, never inside a
/// surrogate pair) and the page says so. Pure, so it is unit-tested.
/// </summary>
public static class EditorTextLimit
{
    /// <summary>The most characters the editor holds: about four hours of speech, and far below what the native editor can take.</summary>
    public const int MaxCharacters = 300_000;

    private const int BreakSearchWindow = 1_000;

    /// <summary>The text to show and whether it was cut. A text within <paramref name="max"/> comes back as it is.</summary>
    public static (string Text, bool Cut) Cap(string text, int max = MaxCharacters)
    {
        if (text.Length <= max)
        {
            return (text, false);
        }

        if (max <= 0)
        {
            return (string.Empty, true);
        }

        int end = BreakBefore(text, max);
        return (text[..end].TrimEnd(), true);
    }

    // The last line or word break within the final window before max; the hard limit when there is none. Never splits a surrogate pair.
    private static int BreakBefore(string text, int max)
    {
        int floor = Math.Max(0, max - BreakSearchWindow);
        for (int i = max; i > floor; i--)
        {
            if (char.IsWhiteSpace(text[i - 1]))
            {
                return i;
            }
        }

        return char.IsHighSurrogate(text[max - 1]) ? max - 1 : max;
    }
}
