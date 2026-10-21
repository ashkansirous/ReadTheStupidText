namespace ReadTheStupidText.Application.Images;

/// <summary>
/// Turns an OCR reading into a <see cref="PositionedPage"/> (Slice 63): pixel boxes are divided by
/// the size of the image they were read in. A reading without a size keeps its text but gets empty
/// boxes, so the text path still works.
/// </summary>
public static class OcrPageMapper
{
    public static PositionedPage ToPage(ImageText reading, int index, string? imagePath)
    {
        IReadOnlyList<TextLine> lines = reading.Lines
            .Where(line => !string.IsNullOrWhiteSpace(line.Text))
            .Select(line => ToLine(line, reading.Width, reading.Height))
            .ToList();
        return new PositionedPage(index, imagePath, lines);
    }

    private static TextLine ToLine(OcrLine line, int imageWidth, int imageHeight)
    {
        OcrBox box = line.Box;
        NormalizedRect rect = NormalizedRect.FromBox(box.Left, box.Top, box.Width, box.Height, imageWidth, imageHeight);
        return new TextLine(line.Text.Trim(), rect);
    }
}
