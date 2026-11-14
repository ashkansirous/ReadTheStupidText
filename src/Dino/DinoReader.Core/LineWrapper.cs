using System.Text.RegularExpressions;

namespace DinoReader.Core;

/// <summary>A word on a wrapped line and where it starts (an index into the text that was wrapped, plus the base offset given).</summary>
public readonly record struct WordAt(string Text, int Start);

/// <summary>Greedy word wrap: the lines a paragraph needs at a given width. A word wider than the
/// width stays alone on its line.</summary>
public static partial class LineWrapper
{
    [GeneratedRegex(@"\S+")]
    private static partial Regex WordPattern();

    public static IReadOnlyList<string> Wrap(string text, double width, ITextMeasurer measurer) =>
        WrapWords(text, 0, width, measurer).Select(Join).ToList();

    /// <summary>The lines of one paragraph as words with their positions (<paramref name="baseOffset"/> is added to each).</summary>
    public static IReadOnlyList<IReadOnlyList<WordAt>> WrapWords(string text, int baseOffset, double width, ITextMeasurer measurer) =>
        WrapWords(text, baseOffset, width, measurer, measurer.Width(" "));

    // Each word is measured once; a line's width is its words plus one measured space between them.
    private static IReadOnlyList<IReadOnlyList<WordAt>> WrapWords(string text, int baseOffset, double width,
        ITextMeasurer measurer, double space)
    {
        var lines = new List<IReadOnlyList<WordAt>>();
        var current = new List<WordAt>();
        double lineWidth = 0;
        foreach (Match match in WordPattern().Matches(text))
        {
            double word = measurer.Width(match.Value);
            if (current.Count > 0 && lineWidth + space + word > width)
            {
                lines.Add(current);
                current = [];
            }

            lineWidth = current.Count > 0 ? lineWidth + space + word : word;
            current.Add(new WordAt(match.Value, baseOffset + match.Index));
        }

        if (current.Count > 0)
        {
            lines.Add(current);
        }

        return lines;
    }

    /// <summary>
    /// The lines of a text where every line break starts a new line; a blank line is an empty list of
    /// words so the gap between paragraphs is kept.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<WordAt>> WrapParagraphs(string text, int baseOffset, double width, ITextMeasurer measurer)
    {
        var lines = new List<IReadOnlyList<WordAt>>();
        double space = measurer.Width(" ");
        int offset = 0;
        foreach (string paragraph in text.Split('\n'))
        {
            IReadOnlyList<IReadOnlyList<WordAt>> wrapped = WrapWords(paragraph, baseOffset + offset, width, measurer, space);
            if (wrapped.Count == 0)
            {
                lines.Add([]);
            }
            else
            {
                lines.AddRange(wrapped);
            }

            offset += paragraph.Length + 1;
        }

        return lines;
    }

    /// <summary>How many lines a text takes when each line break starts a new line (a blank line counts as one).</summary>
    public static int CountLines(string text, double width, ITextMeasurer measurer)
    {
        var counter = new LineCounter(width, measurer);
        counter.Add(text);
        return counter.Lines;
    }

    private static string Join(IReadOnlyList<WordAt> line) => string.Join(' ', line.Select(w => w.Text));
}
