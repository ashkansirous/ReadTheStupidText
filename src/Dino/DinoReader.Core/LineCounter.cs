using System.Text.RegularExpressions;

namespace DinoReader.Core;

/// <summary>Where a <see cref="LineCounter"/> stands: lines of finished paragraphs, lines of the open one, and the open line's width.</summary>
public readonly record struct LineState(int ClosedLines, int ParagraphLines, double LineWidth);

/// <summary>
/// Counts the lines a text takes under the same greedy wrap as <see cref="LineWrapper"/>, fed in
/// order so a pager adds one sentence at a time and carries the last partial line instead of
/// wrapping the whole prefix again. Every word is measured once (and remembered), a space is one
/// measured width, and a line break starts a new paragraph (a blank one counts a line).
/// </summary>
public sealed partial class LineCounter(double width, ITextMeasurer measurer)
{
    private readonly double _space = measurer.Width(" ");
    private readonly Dictionary<string, double> _widths = [];

    [GeneratedRegex(@"\S+")]
    private static partial Regex WordPattern();

    /// <summary>The position to roll back to when a trial addition does not fit.</summary>
    public LineState State { get; set; }

    /// <summary>The lines used so far, counting the open line (a counter with nothing in it is one empty line).</summary>
    public int Lines => State.ClosedLines + Math.Max(1, State.ParagraphLines);

    public void Reset() => State = default;

    public void Add(string text)
    {
        LineState state = State;
        int from = 0;
        while (from <= text.Length)
        {
            int newline = text.IndexOf('\n', from);
            int end = newline < 0 ? text.Length : newline;
            state = AddWords(state, text, from, end);
            if (newline < 0)
            {
                break;
            }

            state = new LineState(state.ClosedLines + Math.Max(1, state.ParagraphLines), 0, 0);
            from = newline + 1;
        }

        State = state;
    }

    private LineState AddWords(LineState state, string text, int from, int end)
    {
        foreach (ValueMatch match in WordPattern().EnumerateMatches(text.AsSpan(from, end - from)))
        {
            state = Place(state, WidthOf(text.Substring(from + match.Index, match.Length)));
        }

        return state;
    }

    private LineState Place(LineState state, double word)
    {
        if (state.ParagraphLines == 0)
        {
            return state with { ParagraphLines = 1, LineWidth = word };
        }

        double joined = state.LineWidth + _space + word;
        return joined > width
            ? state with { ParagraphLines = state.ParagraphLines + 1, LineWidth = word }
            : state with { LineWidth = joined };
    }

    private double WidthOf(string word)
    {
        if (!_widths.TryGetValue(word, out double w))
        {
            w = measurer.Width(word);
            _widths[word] = w;
        }

        return w;
    }
}
