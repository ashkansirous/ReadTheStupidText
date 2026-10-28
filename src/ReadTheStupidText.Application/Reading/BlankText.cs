using System.Globalization;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Whether text has nothing to speak (Slice 176): empty, whitespace, or only invisible format and control characters
/// (zero-width space, BOM, soft hyphen, direction marks). One definition for the chunker and every read entry.
/// </summary>
public static class BlankText
{
    public static bool IsBlank([System.Diagnostics.CodeAnalysis.NotNullWhen(false)] string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        foreach (char c in text)
        {
            if (!IsInvisible(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether one character is whitespace or an invisible format/control character.</summary>
    public static bool IsInvisible(char c) =>
        char.IsWhiteSpace(c) || CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.Format or UnicodeCategory.Control;
}
