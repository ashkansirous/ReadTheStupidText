using System.Text.RegularExpressions;

namespace DinoReader.Core;

/// <summary>The marks a spoken Farsi or Arabic line carries on top of its letters (Slice 154): vowel signs U+064B-U+065F, dagger alef U+0670, tatweel U+0640 and Quranic marks U+06D6-U+06ED.</summary>
public static partial class ArabicMarks
{
    private const char FirstMark = '\u0640';
    private const char LastMark = '\u06ED';

    /// <summary><paramref name="text"/> without those marks; text with none of them is returned as it is, without a scan by the pattern.</summary>
    public static string Strip(string text) =>
        text.AsSpan().IndexOfAnyInRange(FirstMark, LastMark) < 0 ? text : Marks().Replace(text, "");

    [GeneratedRegex("[\u0640\u064B-\u065F\u0670\u06D6-\u06ED]")]
    private static partial Regex Marks();
}
