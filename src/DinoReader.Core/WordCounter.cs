using System.Text.RegularExpressions;

namespace DinoReader.Core;

internal static partial class WordCounter
{
    private const int IdeographsPerWord = 2;

    [GeneratedRegex(@"[\p{L}\p{N}]+(?:['’]\p{L}+)?")]
    private static partial Regex WordPattern();

    // Han and kana are written without spaces, so a run of them is not one word.
    [GeneratedRegex(@"[぀-ヿ㐀-䶿一-鿿]")]
    private static partial Regex IdeographPattern();

    /// <summary>Words in the text; a Chinese or Japanese character counts as half a word (two characters, one word).</summary>
    public static int Count(string text)
    {
        int ideographs = IdeographPattern().Count(text);
        if (ideographs == 0) return WordPattern().Count(text);
        string spaced = IdeographPattern().Replace(text, " ");
        return WordPattern().Count(spaced) + (ideographs + IdeographsPerWord - 1) / IdeographsPerWord;
    }
}
