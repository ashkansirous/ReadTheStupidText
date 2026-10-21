namespace ReadTheStupidText.Application.Images;

/// <summary>An axis-aligned box in the pixel coordinates of the image that was read.</summary>
public readonly record struct OcrBox(float Left, float Top, float Right, float Bottom)
{
    public float Width => Right - Left;

    public float Height => Bottom - Top;

    public float CenterY => (Top + Bottom) / 2;

    /// <summary>
    /// A box from a rectangle normalised to 0..1 and measured from the image's bottom-left corner
    /// (Apple Vision's convention), in the image's top-left pixel coordinates.
    /// </summary>
    public static OcrBox FromNormalizedBottomLeft(double x, double y, double width, double height, float imageWidth, float imageHeight) =>
        new(
            (float)x * imageWidth,
            (1 - (float)(y + height)) * imageHeight,
            (float)(x + width) * imageWidth,
            (1 - (float)y) * imageHeight);
}
