namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// The reading direction of a piece of text, from its first strong character (Slice 85). The app
/// layout never mirrors; only text views follow their own content with this.
/// </summary>
public static class TextDirection
{
    /// <summary>True when the first letter is from a right-to-left script; empty or letterless text is left-to-right.</summary>
    public static bool IsRightToLeft(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        foreach (char c in text)
        {
            if (!char.IsLetter(c))
            {
                continue;
            }

            return IsRightToLeftLetter(c);
        }

        return false;
    }

    /// <summary>The direction of content text; empty text takes the UI language's direction instead.</summary>
    public static bool Resolve(string? text, bool uiRightToLeft) =>
        string.IsNullOrEmpty(text) ? uiRightToLeft : IsRightToLeft(text);

    private static bool IsRightToLeftLetter(char c) =>
        c is >= '֐' and <= 'ࣿ' // Hebrew, Arabic, Syriac, Thaana, NKo, Arabic extended
            or >= 'יִ' and <= '﷿' // presentation forms A
            or >= 'ﹰ' and <= '﻿'; // presentation forms B
}
