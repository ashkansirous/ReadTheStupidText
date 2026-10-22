using System.Text;
using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Application.Reading;

/// <summary>What a reflowed unit is: a paragraph, a heading, or one list item.</summary>
public enum UnitKind
{
    Paragraph,
    Heading,
    ListItem,
}

/// <summary>One unit of reading text: wrapped lines joined, or a heading / list item on its own.</summary>
public sealed record ReflowUnit(string Text, UnitKind Kind);

/// <summary>
/// The newline positions of a text (indices of '\n') where a sentence must end, and the subset
/// that sit next to a heading. Every other newline is a soft wrap.
/// </summary>
public sealed record LineBreakMap(
    IReadOnlySet<int> SentenceBreaks, IReadOnlySet<int> HeadingBreaks, IReadOnlySet<int>? HyphenDrops = null,
    IReadOnlySet<int>? HeadingPairs = null)
{
    public static LineBreakMap Empty { get; } = new(new HashSet<int>(), new HashSet<int>(), new HashSet<int>());

    /// <summary>The newlines whose line ends in a hyphen that reflow drops ("foot-" line break "ball").</summary>
    public IReadOnlySet<int> DroppedHyphenBreaks { get; } = HyphenDrops ?? new HashSet<int>();

    /// <summary>The newlines between two consecutive headings (a title and its subtitle).</summary>
    public IReadOnlySet<int> HeadingPairBreaks { get; } = HeadingPairs ?? new HashSet<int>();
}

/// <summary>
/// Read-time reflow of hard-wrapped text (Slice 76): a single newline is not a sentence end.
/// Wrapped lines become one sentence, hyphenated words are rejoined, blank lines are
/// paragraphs, and headings and list items stay units of their own even without terminal
/// punctuation. Stored text is never changed; callers apply this where text is split.
/// </summary>
public static class LineReflow
{
    private const string UnitSeparator = "\n\n";

    private readonly record struct Analysis(LineJoin[] Joins, bool[] Headings);

    /// <summary>The text with soft wraps joined; units are separated by a blank line.</summary>
    public static string ReflowText(string text)
    {
        return JoinUnits(FromPlainText(text));
    }

    /// <summary>The units' text, separated by a blank line (the paragraph break the chunker reads).</summary>
    public static string JoinUnits(IEnumerable<ReflowUnit> units)
    {
        return string.Join(UnitSeparator, units.Select(u => u.Text));
    }

    public static IReadOnlyList<ReflowUnit> FromPlainText(string text)
    {
        List<ReflowLine> lines = text.Split('\n').Select(l => new ReflowLine(l.TrimEnd('\r'))).ToList();
        return Reflow(lines);
    }

    public static IReadOnlyList<ReflowUnit> Reflow(IReadOnlyList<ReflowLine> lines)
    {
        Analysis analysis = Analyze(lines);
        var units = new List<ReflowUnit>();
        var current = new StringBuilder();
        UnitKind kind = UnitKind.Paragraph;
        int previous = -2;
        for (int i = 0; i < lines.Count; i++)
        {
            string text = lines[i].Text.Trim();
            if (text.Length == 0)
            {
                continue;
            }

            LineJoin join = previous == i - 1 ? analysis.Joins[previous] : LineJoin.Paragraph;
            if (join is LineJoin.Break or LineJoin.Paragraph)
            {
                Flush(units, current, kind);
                kind = KindOf(lines[i].Text, analysis.Headings[i]);
            }

            AppendLine(current, text, join);
            previous = i;
        }

        Flush(units, current, kind);
        return units;
    }

    /// <summary>
    /// The newlines of <paramref name="text"/> that end a sentence. <paramref name="boxes"/>, when
    /// given, are the boxes of the text's lines in order (null entries for lines without one).
    /// </summary>
    public static LineBreakMap BreakMap(string text, IReadOnlyList<OcrBox?>? boxes = null)
    {
        if (!text.Contains('\n'))
        {
            return LineBreakMap.Empty;
        }

        string[] raw = text.Split('\n');
        // A box list that does not line up with the text cannot be trusted: read the text without geometry.
        if (boxes is not null && boxes.Count != raw.Length)
        {
            boxes = null;
        }

        var lines = new List<ReflowLine>(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            lines.Add(new ReflowLine(raw[i], boxes?[i]));
        }

        return ToMap(raw, Analyze(lines));
    }

    private static LineBreakMap ToMap(string[] raw, Analysis analysis)
    {
        var breaks = new HashSet<int>();
        var headingBreaks = new HashSet<int>();
        var hyphenDrops = new HashSet<int>();
        var headingPairs = new HashSet<int>();
        int offset = 0;
        for (int i = 0; i < analysis.Joins.Length; i++)
        {
            offset += raw[i].Length;
            if (analysis.Joins[i] is LineJoin.Break or LineJoin.Paragraph)
            {
                breaks.Add(offset);
            }

            if (analysis.Joins[i] is LineJoin.DropHyphen)
            {
                hyphenDrops.Add(offset);
            }

            if (analysis.Headings[i] != analysis.Headings[i + 1])
            {
                headingBreaks.Add(offset);
            }
            else if (analysis.Headings[i])
            {
                headingPairs.Add(offset);
            }

            offset++;
        }

        return new LineBreakMap(breaks, headingBreaks, hyphenDrops, headingPairs);
    }

    // Joins (length lines - 1): blank lines and headings give paragraph breaks, list items
    // sentence breaks, hyphen wraps and soft wraps joins.
    private static Analysis Analyze(IReadOnlyList<ReflowLine> lines)
    {
        var joins = new LineJoin[Math.Max(lines.Count - 1, 0)];
        Array.Fill(joins, LineJoin.Paragraph);
        var headings = new bool[lines.Count];
        int start = 0;
        while (start < lines.Count)
        {
            if (IsBlank(lines[start]))
            {
                start++;
                continue;
            }

            int end = start;
            while (end < lines.Count && !IsBlank(lines[end]))
            {
                end++;
            }

            AnalyzeBlock(lines, start, end, joins, headings);
            start = end;
        }

        return new Analysis(joins, headings);
    }

    private static void AnalyzeBlock(IReadOnlyList<ReflowLine> lines, int start, int end, LineJoin[] joins, bool[] headings)
    {
        List<ReflowLine> block = lines.Skip(start).Take(end - start).ToList();
        BlockStats stats = StatsOf(block);
        for (int i = 0; i < block.Count; i++)
        {
            headings[start + i] = LineClass.IsHeading(block, i, stats);
        }

        for (int i = 0; i + 1 < block.Count; i++)
        {
            joins[start + i] = JoinBetween(block, i, headings[start + i], headings[start + i + 1]);
        }
    }

    private static LineJoin JoinBetween(List<ReflowLine> block, int i, bool headingHere, bool headingNext)
    {
        if (headingHere && headingNext)
        {
            return LineJoin.Break; // a title and its subtitle are two sentences
        }

        if (headingHere || headingNext)
        {
            return LineJoin.Paragraph;
        }

        bool nextIsItem = i + 2 < block.Count && LineClass.IsListItem(block[i + 2].Text);
        if (LineClass.StartsListItem(block[i + 1].Text, block[i].Text, headingHere, nextIsItem))
        {
            return LineJoin.Break;
        }

        return LineClass.IsHyphenWrap(block[i].Text, block[i + 1].Text, out LineJoin wrap) ? wrap : LineJoin.Space;
    }

    private static BlockStats StatsOf(List<ReflowLine> block)
    {
        double[] lengths = block.Select(l => (double)l.Text.Trim().Length).OrderBy(v => v).ToArray();
        if (block.Count == 0 || block.Any(l => l.Box is null))
        {
            return new BlockStats(0, 0, false, lengths);
        }

        double height = LineStats.Median(block.Select(l => (double)l.Box!.Value.Height));
        var gaps = new List<double>();
        for (int i = 0; i + 1 < block.Count; i++)
        {
            gaps.Add(block[i + 1].Box!.Value.Top - block[i].Box!.Value.Bottom);
        }

        return new BlockStats(height, LineStats.Median(gaps), true, lengths);
    }

    private static UnitKind KindOf(string line, bool heading)
    {
        if (heading)
        {
            return UnitKind.Heading;
        }

        return LineClass.IsListItem(line) ? UnitKind.ListItem : UnitKind.Paragraph;
    }

    private static void AppendLine(StringBuilder builder, string text, LineJoin join)
    {
        switch (join)
        {
            case LineJoin.DropHyphen:
                builder.Length--;
                builder.Append(text);
                break;
            case LineJoin.KeepHyphen:
                builder.Append(text);
                break;
            case LineJoin.Space when builder.Length > 0:
                builder.Append(' ').Append(text);
                break;
            default:
                builder.Append(text);
                break;
        }
    }

    private static void Flush(List<ReflowUnit> units, StringBuilder current, UnitKind kind)
    {
        if (current.Length > 0)
        {
            units.Add(new ReflowUnit(current.ToString(), kind));
            current.Clear();
        }
    }

    private static bool IsBlank(ReflowLine line)
    {
        return string.IsNullOrWhiteSpace(line.Text);
    }
}
