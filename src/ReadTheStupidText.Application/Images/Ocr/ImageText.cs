namespace ReadTheStupidText.Application.Images;

/// <summary>
/// The result of reading an image: the lines in reading order with their boxes, and which engine
/// read them. <see cref="Text"/> is derived from the lines, so the geometry is never discarded.
/// <see cref="Width"/> and <see cref="Height"/> are the pixel size of the image the boxes are in
/// (0 when the engine did not report it), so a box can be normalized (Slice 63).
/// </summary>
public sealed record ImageText(IReadOnlyList<OcrLine> Lines, OcrSource Source, int Width = 0, int Height = 0)
{
    private const string LineSeparator = "\n";

    public string Text => string.Join(LineSeparator, Lines.Select(l => l.Text));
}
