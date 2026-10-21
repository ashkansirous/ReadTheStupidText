namespace ReadTheStupidText.Application.Images;

/// <summary>
/// Turns one cropped text line into the recognizer's input: scaled to the model's fixed height
/// with the aspect ratio kept (width is free, the network is fully convolutional along it), then
/// normalized to [-1, 1] in the stored B, G, R order.
/// </summary>
public static class RecognitionPreprocessor
{
    public const int DefaultHeight = 48;
    public const int MaxWidthInHeights = 40;
    private const int MinWidth = 8;
    private static readonly float[] Scale = [2f, 2f, 2f];
    private static readonly float[] Offset = [-1f, -1f, -1f];

    public static OcrTensor Prepare(PixelImage line, int height)
    {
        int width = TargetWidth(line.Width, line.Height, height);
        PixelImage resized = ImageResizer.Resize(line, width, height);
        return new OcrTensor(ToNormalizedChw(resized), height, width);
    }

    public static int TargetWidth(int lineWidth, int lineHeight, int height)
    {
        int width = (int)MathF.Ceiling((float)height * lineWidth / lineHeight);
        return Math.Clamp(width, MinWidth, height * MaxWidthInHeights);
    }

    internal static float[] ToNormalizedChw(PixelImage image) => ChwNormalizer.Normalize(image, Scale, Offset);
}
