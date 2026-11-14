namespace DinoReader.Core;

/// <summary>
/// The lines of a block of wrapped text as the platform laid it out (Slice 171): each line's top and bottom (dp, from the top of the text) and the offset of its first character.
/// Read from the real label, so wrapping, mixed scripts and fallback fonts are the screen's own.
/// </summary>
public sealed class TextLines
{
    private readonly double[] _tops;
    private readonly double[] _bottoms;
    private readonly int[] _starts;

    public TextLines(IReadOnlyList<double> tops, IReadOnlyList<double> bottoms, IReadOnlyList<int> starts)
    {
        if (tops.Count != bottoms.Count || tops.Count != starts.Count)
        {
            throw new ArgumentException("Every line needs a top, a bottom and a start.");
        }

        _tops = [.. tops];
        _bottoms = [.. bottoms];
        _starts = [.. starts];
    }

    public int Count => _tops.Length;

    public double Top(int line) => _tops[Math.Clamp(line, 0, Count - 1)];

    public double Bottom(int line) => _bottoms[Math.Clamp(line, 0, Count - 1)];

    /// <summary>The height of the whole text.</summary>
    public double Height => Count == 0 ? 0 : _bottoms[^1] - _tops[0];

    /// <summary>The average line height.</summary>
    public double Pitch => Count == 0 ? 0 : Height / Count;

    /// <summary>The line holding the character at <paramref name="offset"/> (the last line for an offset past the end).</summary>
    public int LineOf(int offset)
    {
        int found = Array.BinarySearch(_starts, offset);
        int line = found >= 0 ? found : ~found - 1;
        return Math.Clamp(line, 0, Math.Max(0, Count - 1));
    }

    /// <summary>The height of the tallest run of <paramref name="count"/> neighbouring lines.</summary>
    public double TallestRun(int count)
    {
        int run = Math.Clamp(count, 1, Math.Max(1, Count));
        double tallest = 0;
        for (int first = 0; first + run <= Count; first++)
        {
            tallest = Math.Max(tallest, _bottoms[first + run - 1] - _tops[first]);
        }

        return tallest;
    }
}
