using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ReadTheStupidText.Application.Images;

/// <summary>
/// PaddleOCR's Arabic-script recognizers emit a line in visual (left-to-right on screen) order.
/// This restores logical order the way PaddleOCR's own <c>pred_reverse</c> does: the line is
/// reversed, but runs of Latin letters, digits (including Arabic-Indic and Persian ones) and the punctuation <c>: * . / % + -</c> and spaces
/// keep their internal order so a number or a Latin word is not mirrored.
/// </summary>
public static partial class RightToLeftText
{
    [GeneratedRegex("[a-zA-Z0-9\u0660-\u0669\u06F0-\u06F9 :*./%+-]")]
    private static partial Regex KeepsOrder();

    public static string ToLogicalOrder(string visual)
    {
        var parts = new List<string>();
        var run = new StringBuilder();
        foreach (string element in TextElements(visual))
        {
            if (KeepsOrder().IsMatch(element))
            {
                run.Append(element);
                continue;
            }

            FlushRun(parts, run);
            parts.Add(element);
        }

        FlushRun(parts, run);
        parts.Reverse();
        return string.Concat(parts);
    }

    private static void FlushRun(List<string> parts, StringBuilder run)
    {
        if (run.Length > 0)
        {
            parts.Add(run.ToString());
            run.Clear();
        }
    }

    private static IEnumerable<string> TextElements(string text)
    {
        TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            yield return (string)enumerator.Current;
        }
    }
}
