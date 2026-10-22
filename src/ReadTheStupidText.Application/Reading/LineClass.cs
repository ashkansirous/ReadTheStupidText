using System.Text.RegularExpressions;
using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Application.Reading;

/// <summary>How a line continues into the next one inside a block of text (Slice 76).</summary>
public enum LineJoin
{
    /// <summary>The next line continues the same text: a single space.</summary>
    Space,

    /// <summary>A hyphen-wrapped word ("foot-" + "ball"): no space, the hyphen is dropped.</summary>
    DropHyphen,

    /// <summary>A hyphenated compound ("Anglo-" + "Saxon"): no space, the hyphen stays.</summary>
    KeepHyphen,

    /// <summary>The next line starts a new unit (a list item): a sentence break.</summary>
    Break,

    /// <summary>A paragraph break: a blank line, or a heading on either side.</summary>
    Paragraph,
}

/// <summary>One line of text with its optional position, for <see cref="LineReflow"/>.</summary>
public sealed record ReflowLine(string Text, OcrBox? Box = null);

/// <summary>Medians of one block of lines, the yardsticks for the heading rule.</summary>
internal readonly record struct BlockStats(double MedianHeight, double MedianGap, bool HasGeometry, double[] SortedLengths);

/// <summary>
/// The line classifier behind <see cref="LineReflow"/>: list items, headings and hyphen
/// wraps. Pure; each rule is one method.
/// </summary>
internal static partial class LineClass
{
    internal const double HeadingHeightRatio = 1.25;
    internal const double HeadingGapRatio = 1.5;
    internal const double HeadingLengthRatio = 0.6;
    // A gap below this share of a line's height is box jitter, not a heading gap.
    internal const double MinHeadingGapHeightRatio = 0.5;
    private const string TerminalPunctuation = ".!?…:;,。！？";
    private const string TrailingClosers = "\"'”’)]»";
    private const string SymbolBullets = "•·▪○*☐☑";
    private const string HyphenChars = "-‐";

    private static readonly char[] CloserChars = TrailingClosers.ToCharArray();

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "of", "and", "or", "to", "in", "for", "on", "with", "at", "by", "from", "is", "as", "vs",
    };

    public static bool IsListItem(string line)
    {
        bool matches = ListMarker().IsMatch(line);
        return matches;
    }

    // A symbol bullet always opens an item. A dash or a numeral also opens a wrapped sentence
    // ("...on page\n10. Then"), so those count only after a finished sentence, a heading or another item.
    public static bool StartsListItem(string line, string? previous, bool previousIsHeading, bool nextIsListItem = false)
    {
        if (!IsListItem(line))
        {
            return false;
        }

        string marker = line.TrimStart();
        if (previous is null || previousIsHeading || IsSymbolBullet(marker[0]) || IsListItem(previous) || nextIsListItem)
        {
            return true;
        }

        return HasTerminalPunctuation(previous);
    }

    private static bool IsSymbolBullet(char c)
    {
        return SymbolBullets.Contains(c);
    }

    public static bool IsHyphenWrap(string line, string next, out LineJoin join)
    {
        join = LineJoin.Space;
        string end = line.TrimEnd();
        string start = next.TrimStart();
        if (end.Length < 2 || start.Length == 0 || !HyphenChars.Contains(end[^1]) || !char.IsLetter(end[^2]))
        {
            return false;
        }

        if (!char.IsLetter(start[0]))
        {
            return false;
        }

        join = char.IsLower(start[0]) ? LineJoin.DropHyphen : LineJoin.KeepHyphen;
        return true;
    }

    public static bool HasTerminalPunctuation(string line)
    {
        string text = line.TrimEnd().TrimEnd(CloserChars);
        return text.Length > 0 && TerminalPunctuation.Contains(text[^1]);
    }

    public static bool IsHeading(IReadOnlyList<ReflowLine> block, int index, BlockStats stats)
    {
        string text = block[index].Text.Trim();
        if (text.Length == 0 || HasTerminalPunctuation(text) || IsListItem(text))
        {
            return false;
        }

        return stats.HasGeometry ? IsGeometryHeading(block, index, stats) : IsPlainHeading(block, index, stats);
    }

    private static bool IsGeometryHeading(IReadOnlyList<ReflowLine> block, int index, BlockStats stats)
    {
        if (index + 1 < block.Count && StartsLowerCase(block[index + 1].Text))
        {
            return false; // a mid-sentence wrap, however tall or spaced its box is
        }

        OcrBox box = block[index].Box!.Value;
        if (stats.MedianHeight > 0 && box.Height >= HeadingHeightRatio * stats.MedianHeight)
        {
            return true;
        }

        if (index + 1 >= block.Count || stats.MedianGap <= 0)
        {
            return false;
        }

        double gap = block[index + 1].Box!.Value.Top - box.Bottom;
        return gap >= Math.Max(HeadingGapRatio * stats.MedianGap, MinHeadingGapHeightRatio * stats.MedianHeight);
    }

    private static bool StartsLowerCase(string text)
    {
        string trimmed = text.TrimStart();
        return trimmed.Length > 0 && char.IsLower(trimmed[0]);
    }

    private static bool IsPlainHeading(IReadOnlyList<ReflowLine> block, int index, BlockStats stats)
    {
        string text = block[index].Text.Trim();
        if (index + 1 >= block.Count || !StartsNewUnit(block[index + 1].Text))
        {
            return false;
        }

        // A heading follows a finished sentence (or opens the block), never a wrapped line.
        if (index > 0 && !HasTerminalPunctuation(block[index - 1].Text))
        {
            return false;
        }

        double others = LineStats.MedianWithout(stats.SortedLengths, text.Length);
        return others > 0 && text.Length <= HeadingLengthRatio * others && IsTitleOrCaps(text);
    }

    // The line after a heading starts upper-case, with a digit, or with a list marker.
    private static bool StartsNewUnit(string next)
    {
        string text = next.TrimStart();
        return text.Length > 0 && (char.IsUpper(text[0]) || char.IsDigit(text[0]) || IsListItem(text));
    }

    private static bool IsTitleOrCaps(string text)
    {
        string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string[] content = words.Where(w => !StopWords.Contains(w)).ToArray();
        if (content.Length == 0)
        {
            return false;
        }

        bool allCaps = !text.Any(char.IsLower) && text.Any(char.IsUpper);
        return allCaps || content.All(w => char.IsUpper(w[0]) || char.IsDigit(w[0]));
    }

    // Bullets and checkboxes, "1." / "12)" / "a)" / "(i)" enumerators. A bare ASCII hyphen needs a space after it; en and em dashes open dialogue, never a list.
    [GeneratedRegex(@"^\s*(?:[•·▪○*\-]\s+|[☐☑]|\d{1,3}[.)]\s+|[a-z]\)\s+|\((?:[ivxlcdm]+|\d{1,3}|[a-z])\)\s+)")]
    private static partial Regex ListMarker();
}

internal static class LineStats
{
    public static double Median(IEnumerable<double> values)
    {
        double[] sorted = values.OrderBy(v => v).ToArray();
        if (sorted.Length == 0)
        {
            return 0;
        }

        int mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }

    /// <summary>The median of an ascending array with one occurrence of <paramref name="removed"/> left out.</summary>
    public static double MedianWithout(double[] sorted, double removed)
    {
        int skip = Array.BinarySearch(sorted, removed);
        int count = sorted.Length - 1;
        if (skip < 0 || count <= 0)
        {
            return 0;
        }

        double At(int k) => sorted[k < skip ? k : k + 1];
        int mid = count / 2;
        return count % 2 == 1 ? At(mid) : (At(mid - 1) + At(mid)) / 2;
    }
}
