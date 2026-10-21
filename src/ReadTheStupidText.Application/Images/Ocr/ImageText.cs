namespace ReadTheStupidText.Application.Images;

/// <summary>
/// The result of reading an image: the lines in reading order with their boxes, and which engine
/// read them. <see cref="Text"/> is derived from the lines, so the geometry is never discarded.
/// </summary>
public sealed record ImageText(IReadOnlyList<OcrLine> Lines, OcrSource Source)
{
    private const string LineSeparator = "\n";

    public string Text => string.Join(LineSeparator, Lines.Select(l => l.Text));
}
