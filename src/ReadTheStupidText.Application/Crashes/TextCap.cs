namespace ReadTheStupidText.Application.Crashes;

/// <summary>
/// Cuts text to a number of UTF-8 bytes without splitting a character (Slice 227). Cheap enough for a crash handler: the text is first cut by character
/// count (a character is at least one byte, so no more than <c>maxBytes</c> characters can fit), then walked once; there is no re-counting loop.
/// </summary>
public static class TextCap
{
    private const int LoneSurrogateBytes = 3; // UTF-8 writes the replacement character for it
    private const int SurrogatePairBytes = 4;

    /// <summary>The longest start of <paramref name="text"/> that fits in <paramref name="maxBytes"/> UTF-8 bytes, cut on a whole character.</summary>
    public static string Head(string? text, int maxBytes)
    {
        if (string.IsNullOrEmpty(text) || maxBytes <= 0)
        {
            return string.Empty;
        }

        string window = text.Length > maxBytes ? text[..maxBytes] : text;
        int bytes = 0;
        int index = 0;
        while (index < window.Length)
        {
            int width = WidthAt(window, index);
            int size = BytesOf(window, index, width);
            if (bytes + size > maxBytes)
            {
                break;
            }

            bytes += size;
            index += width;
        }

        return index == text.Length ? text : window[..index];
    }

    /// <summary>The longest end of <paramref name="text"/> that fits in <paramref name="maxBytes"/> UTF-8 bytes (the newest part of a log), cut on a whole character.</summary>
    public static string Tail(string? text, int maxBytes)
    {
        if (string.IsNullOrEmpty(text) || maxBytes <= 0)
        {
            return string.Empty;
        }

        string window = text.Length > maxBytes ? text[^maxBytes..] : text;
        int bytes = 0;
        int index = window.Length;
        while (index > 0)
        {
            int width = WidthBefore(window, index);
            int size = BytesOf(window, index - width, width);
            if (bytes + size > maxBytes)
            {
                break;
            }

            bytes += size;
            index -= width;
        }

        return index == 0 && window.Length == text.Length ? text : window[index..];
    }

    // The characters (1, or 2 for a surrogate pair) that start at index.
    private static int WidthAt(string text, int index) =>
        char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]) ? 2 : 1;

    // The characters that end just before index.
    private static int WidthBefore(string text, int index) =>
        char.IsLowSurrogate(text[index - 1]) && index >= 2 && char.IsHighSurrogate(text[index - 2]) ? 2 : 1;

    private static int BytesOf(string text, int index, int width)
    {
        if (width == 2)
        {
            return SurrogatePairBytes;
        }

        char c = text[index];
        return c switch
        {
            < '\u0080' => 1,
            < 'ࠀ' => 2,
            _ when char.IsSurrogate(c) => LoneSurrogateBytes,
            _ => 3,
        };
    }
}
