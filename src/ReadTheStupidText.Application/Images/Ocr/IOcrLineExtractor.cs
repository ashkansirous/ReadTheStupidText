namespace ReadTheStupidText.Application.Images;

/// <summary>
/// An image reader that keeps the line boxes. <see cref="IImageTextExtractor"/> is the text-only
/// view of the same engine; Slice 63 uses the geometry.
/// </summary>
public interface IOcrLineExtractor
{
    Task<ImageText> ExtractLinesAsync(string imagePath, string language, CancellationToken cancellationToken);
}
