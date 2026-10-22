using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Application.Images;

/// <summary>
/// Lines read from one image plus the pixel size of the image their boxes are in (Slice 63).
/// <see cref="FullText"/> is the engine's own text with its block and paragraph breaks, when it has one.
/// </summary>
public sealed record OcrReading(IReadOnlyList<OcrLine> Lines, int Width, int Height, string? FullText = null)
{
    /// <summary>
    /// The text to read aloud (Slice 76): wrapped lines joined into sentences, headings and list
    /// items kept as their own units, using the line boxes. The stored text keeps its newlines.
    /// </summary>
    public string ReflowedText()
    {
        List<ReflowLine> lines = Lines.Select(l => new ReflowLine(l.Text, l.Box)).ToList();
        return LineReflow.JoinUnits(LineReflow.Reflow(lines));
    }
}
