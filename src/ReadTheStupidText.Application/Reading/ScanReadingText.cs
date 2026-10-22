using System.Text;
using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// The text for a scan (Slice 86): <see cref="ImageText.Text"/> with every newline kept, plus a blank line
/// where the line geometry says a heading starts or ends, so the plain-text reader (which only sees the
/// text) pauses there. Without boxes, or when the boxes do not line up with the text, it is the text unchanged.
/// </summary>
public static class ScanReadingText
{
    private const char LineSeparator = '\n';

    public static string From(ImageText image)
    {
        string text = image.Text;
        IReadOnlyList<OcrBox?>? boxes = BoxesOfTextLines(image.Lines, text);
        if (boxes is null)
        {
            return text;
        }

        LineBreakMap map = LineReflow.BreakMap(text, boxes);
        var offsets = new HashSet<int>(map.HeadingBreaks);
        offsets.UnionWith(map.HeadingPairBreaks);
        return offsets.Count == 0 ? text : InsertBlankLines(text, offsets);
    }

    // One box per line of the text (null for a blank line or a line without a box), or null when there is no
    // geometry to use or the text's non-blank lines are not exactly the recognized lines, in order: such a
    // mapping cannot be trusted. Whitespace-only recognized lines are skipped.
    private static IReadOnlyList<OcrBox?>? BoxesOfTextLines(IReadOnlyList<OcrLine> recognized, string text)
    {
        List<(string Text, OcrBox? Box)> lines = recognized
            .Where(l => !string.IsNullOrWhiteSpace(l.Text))
            .Select(l => (l.Text.Trim(), BoxOf(l)))
            .ToList();
        if (!lines.Any(l => l.Box is not null))
        {
            return null;
        }

        return Align(text.Split(LineSeparator), lines);
    }

    // The engines report "no box" as the default box; this is the one place that reads it.
    private static OcrBox? BoxOf(OcrLine line)
    {
        return line.Box == default ? null : line.Box;
    }

    private static List<OcrBox?>? Align(string[] rawLines, List<(string Text, OcrBox? Box)> lines)
    {
        var boxes = new List<OcrBox?>(rawLines.Length);
        int next = 0;
        foreach (string raw in rawLines)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                boxes.Add(null);
                continue;
            }

            if (next >= lines.Count || lines[next].Text != raw.Trim())
            {
                return null;
            }

            boxes.Add(lines[next++].Box);
        }

        return next == lines.Count ? boxes : null;
    }

    // Walks the lines once: after line i add a blank line when its newline is a heading break and neither
    // neighbouring line is already blank (a blank or whitespace-only line, which includes a lone "\r").
    private static string InsertBlankLines(string text, IReadOnlySet<int> newlineOffsets)
    {
        string[] lines = text.Split(LineSeparator);
        var result = new StringBuilder(text.Length + newlineOffsets.Count);
        int offset = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            result.Append(lines[i]);
            offset += lines[i].Length;
            if (i + 1 < lines.Length)
            {
                result.Append(LineSeparator);
                if (newlineOffsets.Contains(offset) && !IsBlank(lines[i]) && !IsBlank(lines[i + 1]))
                {
                    result.Append(LineSeparator);
                }
            }

            offset++;
        }

        return result.ToString();
    }

    private static bool IsBlank(string line)
    {
        return string.IsNullOrWhiteSpace(line);
    }
}
