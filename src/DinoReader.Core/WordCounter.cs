using System.Text.RegularExpressions;

namespace DinoReader.Core;

internal static partial class WordCounter
{
    [GeneratedRegex(@"[\p{L}\p{N}]+(?:['’]\p{L}+)?")]
    private static partial Regex WordPattern();

    public static int Count(string text) => WordPattern().Count(text);
}
