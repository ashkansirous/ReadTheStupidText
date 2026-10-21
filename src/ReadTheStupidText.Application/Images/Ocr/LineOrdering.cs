namespace ReadTheStupidText.Application.Images;

/// <summary>
/// Puts detected boxes in reading order. Boxes that overlap vertically and sit closer than
/// <see cref="ColumnGapInHeights"/> median line heights join one row segment; segments that overlap
/// horizontally form a column, and the columns are read left to right (right to left for Arabic and
/// Persian), each from top to bottom. Inside a column, boxes at the same height are one row, read
/// left to right or right to left. A heading that spans the columns merges them into one column,
/// which falls back to plain row-by-row reading.
/// </summary>
public static class LineOrdering
{
    public const float ColumnGapInHeights = 2f;
    private const float RowTolerance = 0.5f;

    public static IReadOnlyList<OcrBox> Order(IEnumerable<OcrBox> boxes, bool rightToLeft)
    {
        List<OcrBox> all = boxes.ToList();
        var ordered = new List<OcrBox>(all.Count);
        foreach (List<OcrBox> column in OrderColumns(Columns(all), rightToLeft))
        {
            ordered.AddRange(OrderRows(column, rightToLeft));
        }

        return ordered;
    }

    private static IEnumerable<List<OcrBox>> OrderColumns(List<List<OcrBox>> columns, bool rightToLeft) =>
        rightToLeft
            ? columns.OrderByDescending(c => c.Max(b => b.Right))
            : columns.OrderBy(c => c.Min(b => b.Left));

    private static List<OcrBox> OrderRows(List<OcrBox> column, bool rightToLeft)
    {
        var ordered = new List<OcrBox>(column.Count);
        foreach (List<OcrBox> row in Rows(column.OrderBy(b => b.CenterY)))
        {
            ordered.AddRange(rightToLeft ? row.OrderByDescending(b => b.Right) : row.OrderBy(b => b.Left));
        }

        return ordered;
    }

    private static List<List<OcrBox>> Rows(IEnumerable<OcrBox> byCentre)
    {
        var rows = new List<List<OcrBox>>();
        foreach (OcrBox box in byCentre)
        {
            if (rows.Count > 0 && SameRow(rows[^1], box))
            {
                rows[^1].Add(box);
            }
            else
            {
                rows.Add([box]);
            }
        }

        return rows;
    }

    private static bool SameRow(List<OcrBox> row, OcrBox box)
    {
        float centre = row.Average(b => b.CenterY);
        float height = Math.Min(row.Average(b => b.Height), box.Height);
        return MathF.Abs(box.CenterY - centre) <= height * RowTolerance;
    }

    private static List<List<OcrBox>> Columns(List<OcrBox> boxes)
    {
        var columns = new List<List<OcrBox>>();
        if (boxes.Count == 0)
        {
            return columns;
        }

        float maxGap = Median(boxes.Select(b => b.Height)) * ColumnGapInHeights;
        foreach (List<OcrBox> segment in Segments(boxes, maxGap).OrderBy(s => s.Min(b => b.Top)))
        {
            List<OcrBox>? column = columns.FirstOrDefault(c => OverlapsHorizontally(c, segment));
            if (column is null)
            {
                columns.Add([.. segment]);
            }
            else
            {
                column.AddRange(segment);
            }
        }

        return columns;
    }

    // A row segment: boxes at the same height with no gap wider than maxGap between neighbours.
    private static List<List<OcrBox>> Segments(List<OcrBox> boxes, float maxGap)
    {
        var segments = new List<List<OcrBox>>();
        foreach (OcrBox box in boxes.OrderBy(b => b.CenterY))
        {
            List<OcrBox>? home = segments
                .Where(s => SameRow(s, box) && Gap(s, box) < maxGap)
                .OrderBy(s => Gap(s, box))
                .FirstOrDefault();
            if (home is null)
            {
                segments.Add([box]);
            }
            else
            {
                home.Add(box);
            }
        }

        return segments;
    }

    // Negative when the box overlaps the segment's horizontal extent.
    private static float Gap(List<OcrBox> segment, OcrBox box) =>
        Math.Max(box.Left - segment.Max(b => b.Right), segment.Min(b => b.Left) - box.Right);

    private static bool OverlapsHorizontally(List<OcrBox> a, List<OcrBox> b) =>
        a.Min(x => x.Left) < b.Max(x => x.Right) && b.Min(x => x.Left) < a.Max(x => x.Right);

    private static float Median(IEnumerable<float> values)
    {
        float[] sorted = values.OrderBy(v => v).ToArray();
        int mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }
}
