using System.Text.RegularExpressions;

namespace DinoReader.Core;

/// <summary>
/// The pure parts of a picture + text book (Slice 98, Decision 102): pictures in natural file-name order (<c>1, 2, 10</c>),
/// plain text cut into pages at lines holding only <c>---</c>, and the empty picture slots of a page list filled in order.
/// A page without a picture is not an error: it reads as text. A picture + text page is a <see cref="StoryPage"/> with an
/// <see cref="StoryPage.ImageFile"/> and <see cref="StoryPage.Text"/> but no <see cref="StoryPage.Lines"/>.
/// </summary>
public static partial class PictureTextBook
{
    public const string PageSeparator = "---";

    [GeneratedRegex(@"^[ \t]*---[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex SeparatorLine();

    /// <summary>The most of the visible picture the text panel may cover with the phone upright (Slice 122), its bottom margin included (the drop shadow is not counted).</summary>
    public const double PanelHeightShare = 0.30;

    /// <summary>The tallest the upright text panel may be: <see cref="PanelHeightShare"/> of the visible picture (the AspectFit
    /// rectangle inside the picture area, not the card), where the panel's bottom margin counts toward the share and the letterbox
    /// band under the picture does not cover any of it. The picture area is the card less <paramref name="areaTop"/> above and
    /// <paramref name="areaSide"/> either side; the panel's own place is <see cref="PlacePanel"/>.</summary>
    public static double PanelMaxHeight(double cardWidth, double cardHeight, double pictureAspect, double panelMargin,
        double areaTop = 0, double areaSide = 0)
    {
        double areaWidth = Math.Max(0, cardWidth - 2 * areaSide);
        double areaHeight = Math.Max(0, cardHeight - areaTop);
        if (pictureAspect <= 0)
        {
            // The picture's shape is unknown: take the share of the card height.
            return Math.Max(0, PanelHeightShare * cardHeight - panelMargin);
        }

        double pictureHeight = Math.Min(areaHeight, areaWidth / pictureAspect);
        double bottomBand = (areaHeight - pictureHeight) / 2;
        return Math.Max(0, PanelHeightShare * pictureHeight + bottomBand - panelMargin);
    }

    /// <summary>Where the upright text panel sits (Slice 135): <see cref="BottomMargin"/> above the card's bottom edge and at most <see cref="MaxHeight"/> tall.</summary>
    public readonly record struct PanelPlacement(double BottomMargin, double MaxHeight);

    /// <summary>The empty band under an AspectFit picture centred in the picture area (the card less <paramref name="areaTop"/> and <paramref name="areaSide"/>);
    /// none when the picture's shape is unknown.</summary>
    public static double LetterboxBelow(double cardWidth, double cardHeight, double pictureAspect, double areaTop = 0, double areaSide = 0)
    {
        if (pictureAspect <= 0)
        {
            return 0;
        }

        double areaWidth = Math.Max(0, cardWidth - 2 * areaSide);
        double areaHeight = Math.Max(0, cardHeight - areaTop);
        return (areaHeight - Math.Min(areaHeight, areaWidth / pictureAspect)) / 2;
    }

    /// <summary>The panel's bottom margin and height cap together: its bottom edge is <paramref name="panelMargin"/> above the visible picture's bottom edge (the letterbox band
    /// is added to the margin), and the cap is <see cref="PanelMaxHeight"/> less that band, which it counts as covered.</summary>
    public static PanelPlacement PlacePanel(double cardWidth, double cardHeight, double pictureAspect, double panelMargin, double areaTop = 0, double areaSide = 0)
    {
        double band = LetterboxBelow(cardWidth, cardHeight, pictureAspect, areaTop, areaSide);
        double max = Math.Max(0, PanelMaxHeight(cardWidth, cardHeight, pictureAspect, panelMargin, areaTop, areaSide) - band);
        return new PanelPlacement(panelMargin + band, max);
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex DigitRun();

    /// <summary>The paths sorted by file name with digit runs compared as numbers (<c>2.jpg</c> before <c>10.jpg</c>),
    /// ignoring case; the sort is stable.</summary>
    public static IReadOnlyList<string> SortNaturally(IEnumerable<string> paths) =>
        paths.OrderBy(p => Path.GetFileName(p), NaturalComparer.Instance).ToList();

    /// <summary>Cuts text into pages at each line holding only <c>---</c> (surrounding spaces ignored, CRLF or LF). Pages are
    /// trimmed and empty ones (a leading, doubled or trailing separator) are dropped; text without a separator is one page.</summary>
    public static IReadOnlyList<string> SplitPages(string text)
    {
        string lf = LineBreaks.ToLf(text);
        return SeparatorLine().Split(lf).Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
    }

    /// <summary>The pages an imported text (.txt or typed) becomes: cut at <c>---</c> lines, or the text as it is as one page.</summary>
    public static IReadOnlyList<string> ImportPages(string text) => HasSeparator(text) ? SplitPages(text) : [text];

    /// <summary>Whether a line of the text is a page separator.</summary>
    public static bool HasSeparator(string text) => SeparatorLine().IsMatch(LineBreaks.ToLf(text));

    /// <summary>The slots with each empty one (null) filled, in order, from the pictures in natural file-name order. A slot
    /// that already holds a picture is left alone; pictures left over are ignored.</summary>
    public static IReadOnlyList<string?> FillEmptySlots(IReadOnlyList<string?> slots, IEnumerable<string> pictures)
    {
        Queue<string> queue = new(SortNaturally(pictures));
        var filled = new List<string?>(slots.Count);
        foreach (string? slot in slots)
        {
            filled.Add(slot ?? (queue.Count > 0 ? queue.Dequeue() : null));
        }

        return filled;
    }

    /// <summary>Whether the draft is a picture + text page: a picture the grown-up chose for a page of text, never read by OCR.</summary>
    public static bool IsPictureText(PageDraft page) =>
        page.HasPicture && page.Ocr is { HasText: false } && !string.IsNullOrWhiteSpace(page.Text);

    /// <summary>The text page with <paramref name="pictureFile"/> beside it. The empty reading marks it as read, so no OCR runs on
    /// the picture and the page keeps its text and no line boxes.</summary>
    public static PageDraft WithPicture(PageDraft page, string pictureFile) =>
        page with
        {
            SourceFile = pictureFile,
            RotationQuarterTurns = 0,
            Crop = null,
            TransformedFile = null,
            Ocr = new PageReading([]),
            EditedText = page.Text,
        };

    /// <summary>Whether the story page is a picture + text page (a picture and its own text, no recognised lines).</summary>
    public static bool IsPictureText(StoryPage page) =>
        page.ImageFile is not null && !string.IsNullOrWhiteSpace(page.Text) && page.Lines is not { Count: > 0 };

    /// <summary>Whether the pages are drawn side by side: at least one is a picture + text page. A book that mixes pages stays in
    /// this layout (a picture-only page shows its picture, a text-only page its text); a book whose pictures all carry line boxes
    /// or all lack text keeps the scanned-page layout.</summary>
    public static bool IsPictureTextBook(IReadOnlyList<StoryPage> pages) => pages.Any(IsPictureText);

    private sealed class NaturalComparer : IComparer<string?>
    {
        public static readonly NaturalComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            string left = x ?? string.Empty;
            string right = y ?? string.Empty;
            int at = 0;
            int other = 0;
            while (at < left.Length && other < right.Length)
            {
                int result = CompareNext(left, ref at, right, ref other);
                if (result != 0)
                {
                    return result;
                }
            }

            return (left.Length - at).CompareTo(right.Length - other);
        }

        private static int CompareNext(string left, ref int at, string right, ref int other)
        {
            if (char.IsDigit(left[at]) && char.IsDigit(right[other]))
            {
                return CompareNumbers(left, ref at, right, ref other);
            }

            int result = char.ToUpperInvariant(left[at]).CompareTo(char.ToUpperInvariant(right[other]));
            at++;
            other++;
            return result;
        }

        private static int CompareNumbers(string left, ref int at, string right, ref int other)
        {
            string a = DigitRun().Match(left, at).Value.TrimStart('0');
            string b = DigitRun().Match(right, other).Value.TrimStart('0');
            at += DigitRun().Match(left, at).Length;
            other += DigitRun().Match(right, other).Length;
            int byLength = a.Length.CompareTo(b.Length);
            return byLength != 0 ? byLength : string.CompareOrdinal(a, b);
        }
    }
}
