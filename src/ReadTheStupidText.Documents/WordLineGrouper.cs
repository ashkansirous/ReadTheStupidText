using System.Text;
using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Documents;

/// <summary>
/// Groups a page's words into lines (Slice 63): words whose vertical extent overlaps the row's
/// median word band by at least 40% share the line; each line reads left-to-right, or right-to-left
/// when most of its letters are in an RTL script. Words are not split by column, so a multi-column
/// page merges across columns - accepted for single-column books.
/// </summary>
public static class WordLineGrouper
{
    private const double MinBandOverlap = 0.4;

    public static IReadOnlyList<TextLine> Group(
        IReadOnlyList<PositionedWord> words, double pageWidth, double pageHeight)
    {
        var lines = new List<TextLine>();
        foreach (List<PositionedWord> row in GroupRows(words))
        {
            lines.Add(ToLine(row, pageWidth, pageHeight));
        }

        return lines;
    }

    private static List<List<PositionedWord>> GroupRows(IReadOnlyList<PositionedWord> words)
    {
        var rows = new List<List<PositionedWord>>();
        foreach (PositionedWord word in words.Where(w => !string.IsNullOrWhiteSpace(w.Text)).OrderBy(w => w.CenterY))
        {
            if (rows.Count > 0 && SitsOnRow(rows[^1], word))
            {
                rows[^1].Add(word);
                continue;
            }

            rows.Add([word]);
        }

        return rows;
    }

    // A word joins the row when its vertical extent overlaps the row's typical word band by at
    // least MinBandOverlap of the smaller of the two heights, so a superscript, a descender or a
    // small inline word (all partly outside a strict band) still joins. The band is the median top
    // and bottom of the row's words; a row of one or two words has no stable median, so its shortest
    // word stands in for it (a drop cap spanning several lines is the tall one there).
    private static bool SitsOnRow(List<PositionedWord> row, PositionedWord word)
    {
        (double bandTop, double bandBottom) = Band(row);
        double overlap = Math.Min(word.Bottom, bandBottom) - Math.Max(word.Top, bandTop);
        double smaller = Math.Min(word.Height, bandBottom - bandTop);
        return smaller > 0
            ? overlap >= smaller * MinBandOverlap
            : word.CenterY >= bandTop && word.CenterY <= bandBottom;
    }

    private static (double Top, double Bottom) Band(List<PositionedWord> row)
    {
        if (row.Count <= 2)
        {
            PositionedWord shortest = row.OrderBy(w => w.Height).First();
            return (shortest.Top, shortest.Bottom);
        }

        return (LowerMedian(row.Select(w => w.Top)), LowerMedian(row.Select(w => w.Bottom)));
    }

    private static double LowerMedian(IEnumerable<double> values)
    {
        double[] sorted = values.OrderBy(v => v).ToArray();
        return sorted[(sorted.Length - 1) / 2];
    }

    private static TextLine ToLine(List<PositionedWord> row, double pageWidth, double pageHeight)
    {
        string joined = string.Join(' ', Order(row).Select(w => w.Text.Trim()));
        double left = row.Min(w => w.Left);
        double top = row.Min(w => w.Top);
        double width = row.Max(w => w.Right) - left;
        double height = row.Max(w => w.Bottom) - top;
        return new TextLine(joined, NormalizedRect.FromBox(left, top, width, height, pageWidth, pageHeight));
    }

    private static IEnumerable<PositionedWord> Order(List<PositionedWord> row) =>
        IsRightToLeft(row) ? row.OrderByDescending(w => w.Left) : row.OrderBy(w => w.Left);

    private static bool IsRightToLeft(List<PositionedWord> row)
    {
        int rtl = 0, letters = 0;
        foreach (Rune rune in row.SelectMany(w => w.Text.EnumerateRunes()).Where(r => Rune.IsLetter(r)))
        {
            letters++;
            if (IsRtlCodePoint(rune.Value))
            {
                rtl++;
            }
        }

        return letters > 0 && rtl * 2 > letters;
    }

    // Hebrew, Arabic, Syriac, Thaana, NKo and the Arabic presentation forms.
    private static bool IsRtlCodePoint(int cp) =>
        cp is >= 0x0590 and <= 0x08FF or >= 0xFB1D and <= 0xFDFF or >= 0xFE70 and <= 0xFEFF;
}
