namespace ReadTheStupidText.Application.Images;

/// <summary>
/// Turns a page into the detector's input (PaddleOCR DB): the longest side is capped at
/// <see cref="MaxSide"/>, both sides become multiples of 32 (the network downsamples by 32), and
/// pixels are normalized with the ImageNet mean/std in the stored B, G, R order. The scale factors
/// map the detector's output back to the original image.
/// </summary>
public static class DetectionPreprocessor
{
    public const int MaxSide = 1280;
    private const int Stride = 32;
    private static readonly float[] Mean = [0.485f, 0.456f, 0.406f];
    private static readonly float[] Std = [0.229f, 0.224f, 0.225f];
    private static readonly float[] Scale = Std.Select(s => 1f / s).ToArray();
    private static readonly float[] Offset = Mean.Select((m, c) => -m / Std[c]).ToArray();

    public static (OcrTensor Tensor, float ScaleX, float ScaleY) Prepare(PixelImage image)
    {
        (int width, int height) = TargetSize(image.Width, image.Height);
        PixelImage resized = ImageResizer.Resize(image, width, height);
        float[] data = ToNormalizedChw(resized);
        return (new OcrTensor(data, height, width), (float)width / image.Width, (float)height / image.Height);
    }

    public static (int Width, int Height) TargetSize(int width, int height)
    {
        float ratio = Math.Max(width, height) > MaxSide ? (float)MaxSide / Math.Max(width, height) : 1f;
        return (RoundToStride(width * ratio), RoundToStride(height * ratio));
    }

    private static int RoundToStride(float value) =>
        Math.Max(Stride, (int)MathF.Round(value / Stride) * Stride);

    internal static float[] ToNormalizedChw(PixelImage image) => ChwNormalizer.Normalize(image, Scale, Offset);
}
