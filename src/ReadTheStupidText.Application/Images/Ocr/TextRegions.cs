namespace ReadTheStupidText.Application.Images;

/// <summary>A block of neighbouring lines of one page, with the box that holds them and the heuristic's verdict (Slice 79).</summary>
public sealed record TextRegion(IReadOnlyList<OcrLine> Lines, OcrBox Bounds, RegionClass Class);

/// <summary>
/// Tells a picture-book page's story text from the lettering in its illustration (Slice 79). Lines are grouped into
/// regions; the main region is the biggest block of ordinary-sized text; any other region is background when its letters
/// are much bigger or smaller than the main block's, or when it is a line or two far away from it. A page is never emptied.
/// </summary>
public static class TextRegions
{
    public const double MaxGapInHeights = 1.0;
    public const double CentreAlignInHeights = 0.5;
    public const double MainMinHeightRatio = 0.75;
    public const double MainMaxHeightRatio = 1.35;
    public const double BackgroundLargerRatio = 1.5;
    public const double BackgroundSmallerRatio = 0.6;
    public const int MaxIsolatedLines = 2;
    public const double IsolationDistanceInHeights = 3.0;

    public static IReadOnlyList<TextRegion> Classify(OcrReading reading)
    {
        List<List<OcrLine>> groups = Group(reading.Lines);
        if (groups.Count == 0)
        {
            return [];
        }

        double pageHeight = Median(reading.Lines.Select(l => (double)l.Box.Height));
        List<(List<OcrLine> Lines, OcrBox Bounds)> regions = groups.Select(g => (g, BoundsOf(g))).ToList();
        int main = MainIndex(groups, pageHeight);
        double mainHeight = Median(groups[main].Select(l => (double)l.Box.Height));
        return regions
            .Select((r, i) => new TextRegion(r.Lines, r.Bounds, i == main || IsStory(r, regions[main].Bounds, mainHeight) ? RegionClass.Story : RegionClass.Background))
            .ToList();
    }

    // Lines top to bottom; a line joins the region whose last line it follows closely and lines up with.
    private static List<List<OcrLine>> Group(IReadOnlyList<OcrLine> lines)
    {
        var groups = new List<List<OcrLine>>();
        if (lines.Count == 0)
        {
            return groups;
        }

        double height = Median(lines.Select(l => (double)l.Box.Height));
        foreach (OcrLine line in lines.OrderBy(l => l.Box.Top))
        {
            List<OcrLine>? home = groups.FirstOrDefault(g => Joins(g, line, height));
            if (home is null)
            {
                groups.Add([line]);
            }
            else
            {
                home.Add(line);
            }
        }

        return groups;
    }

    private static bool Joins(List<OcrLine> group, OcrLine line, double height)
    {
        OcrBox last = group[^1].Box;
        double gap = line.Box.Top - last.Bottom;
        bool overlaps = line.Box.Left < last.Right && last.Left < line.Box.Right;
        bool centred = Math.Abs(Centre(line.Box) - Centre(last)) <= CentreAlignInHeights * height;
        return gap <= MaxGapInHeights * height && (overlaps || centred);
    }

    private static double Centre(OcrBox box) => (box.Left + box.Right) / 2.0;

    private static int MainIndex(List<List<OcrLine>> groups, double pageHeight)
    {
        int best = -1;
        int bestChars = -1;
        for (int i = 0; i < groups.Count; i++)
        {
            double h = Median(groups[i].Select(l => (double)l.Box.Height));
            int chars = groups[i].Sum(l => l.Text.Length);
            bool ordinary = h >= MainMinHeightRatio * pageHeight && h <= MainMaxHeightRatio * pageHeight;
            if (ordinary && chars > bestChars)
            {
                best = i;
                bestChars = chars;
            }
        }

        // No block of ordinary size: the page keeps its biggest block (rule 5).
        return best >= 0 ? best : Enumerable.Range(0, groups.Count).MaxBy(i => groups[i].Sum(l => l.Text.Length));
    }

    private static bool IsStory((List<OcrLine> Lines, OcrBox Bounds) region, OcrBox main, double mainHeight)
    {
        double h = Median(region.Lines.Select(l => (double)l.Box.Height));
        if (h >= BackgroundLargerRatio * mainHeight || h <= BackgroundSmallerRatio * mainHeight)
        {
            return false;
        }

        bool isolated = region.Lines.Count <= MaxIsolatedLines && Distance(region.Bounds, main) >= IsolationDistanceInHeights * mainHeight;
        return !isolated;
    }

    private static double Distance(OcrBox a, OcrBox b)
    {
        double dy = Math.Max(0, Math.Max(a.Top - b.Bottom, b.Top - a.Bottom));
        double dx = Math.Max(0, Math.Max(a.Left - b.Right, b.Left - a.Right));
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static OcrBox BoundsOf(List<OcrLine> lines) => new(
        lines.Min(l => l.Box.Left), lines.Min(l => l.Box.Top), lines.Max(l => l.Box.Right), lines.Max(l => l.Box.Bottom));

    private static double Median(IEnumerable<double> values)
    {
        double[] sorted = values.OrderBy(v => v).ToArray();
        int mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }
}
