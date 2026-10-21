using System.Text;
using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Documents;

/// <summary>
/// Groups a page's words into lines (Slice 63): words whose vertical centre falls inside the
/// current line's vertical span share the line; each line reads left-to-right, or right-to-left
/// when most of its letters are in an RTL script. Words are not split by column, so a multi-column
/// page merges across columns - accepted for single-column books.
/// </summary>
public static class WordLineGrouper
{
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
        double rowTop = 0, rowBottom = 0;
        foreach (PositionedWord word in words.Where(w => !string.IsNullOrWhiteSpace(w.Text)).OrderBy(w => w.CenterY))
        {
            if (rows.Count > 0 && word.CenterY >= rowTop && word.CenterY <= rowBottom)
            {
                rows[^1].Add(word);
                rowTop = Math.Min(rowTop, word.Top);
                rowBottom = Math.Max(rowBottom, word.Bottom);
                continue;
            }

            rows.Add([word]);
            rowTop = word.Top;
            rowBottom = word.Bottom;
        }

        return rows;
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
