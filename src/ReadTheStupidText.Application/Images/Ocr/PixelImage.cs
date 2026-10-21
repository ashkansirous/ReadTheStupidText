namespace ReadTheStupidText.Application.Images;

/// <summary>
/// A decoded image: three bytes per pixel in B, G, R order (the order PaddleOCR's models were
/// trained on), rows top to bottom with no padding.
/// </summary>
public sealed record PixelImage(int Width, int Height, byte[] Bgr)
{
    public const int BytesPerPixel = 3;

    public PixelImage Validate()
    {
        if (Width <= 0 || Height <= 0 || Bgr.Length != Width * Height * BytesPerPixel)
        {
            throw new ArgumentException("The pixel buffer does not match the image size.");
        }

        return this;
    }
}
