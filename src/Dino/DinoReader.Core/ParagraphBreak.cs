namespace DinoReader.Core;

/// <summary>Paragraph breaks: a blank line (two line feeds with only blanks between).</summary>
internal static class ParagraphBreak
{
    public static bool IsAt(string text, int i)
    {
        if (text[i] != '\n') return false;
        int j = i + 1;
        while (j < text.Length && text[j] != '\n' && char.IsWhiteSpace(text[j])) j++;
        return j < text.Length && text[j] == '\n';
    }

    public static bool Between(string text, int start, int end)
    {
        for (int i = start; i < end && i < text.Length; i++)
            if (IsAt(text, i)) return true;
        return false;
    }
}
