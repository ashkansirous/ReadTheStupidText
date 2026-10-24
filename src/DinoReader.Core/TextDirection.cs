namespace DinoReader.Core;

/// <summary>Which way a piece of text runs, from its first strong letter (Slice 96, Decision 100). Pure, so it is unit-tested.</summary>
public static class TextDirection
{
    public static bool StartsRightToLeft(string? text)
    {
        foreach (char c in text ?? string.Empty)
        {
            if (char.IsLetter(c))
            {
                return IsRightToLeftLetter(c);
            }
        }

        return false;
    }

    private static bool IsRightToLeftLetter(char c) =>
        c is (>= '֐' and <= 'ࣿ') or (>= 'יִ' and <= '﷿') or (>= 'ﹰ' and <= 'ﻼ');
}
