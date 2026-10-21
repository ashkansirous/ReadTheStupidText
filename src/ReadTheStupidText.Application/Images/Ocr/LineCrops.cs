namespace ReadTheStupidText.Application.Images;

/// <summary>
/// Shapes a line crop for the recognizer: a tall crop is turned on its side (PaddleOCR does the same
/// when height / width is at least 1.5), and a line wider than the recognizer's width cap is cut into
/// pieces rather than squashed.
/// </summary>
public static class LineCrops
{
    public const float TallRatio = 1.5f;

    public static PixelImage ToHorizontal(PixelImage crop) =>
        crop.Height > crop.Width * TallRatio ? ImageResizer.RotateCounterClockwise(crop) : crop;

    /// <summary>The crop itself when it fits the width cap at the recognizer height, else left-to-right pieces that do.</summary>
    public static IReadOnlyList<PixelImage> Split(PixelImage crop, int recognitionHeight)
    {
        float scaledWidth = (float)recognitionHeight * crop.Width / crop.Height;
        int maxWidth = recognitionHeight * RecognitionPreprocessor.MaxWidthInHeights;
        if (scaledWidth <= maxWidth)
        {
            return [crop];
        }

        int count = (int)MathF.Ceiling(scaledWidth / maxWidth);
        return Pieces(crop, count);
    }

    private static List<PixelImage> Pieces(PixelImage crop, int count)
    {
        var pieces = new List<PixelImage>(count);
        for (int i = 0; i < count; i++)
        {
            float left = (float)crop.Width * i / count;
            float right = (float)crop.Width * (i + 1) / count;
            if (ImageResizer.Crop(crop, new OcrBox(left, 0, right, crop.Height)) is { } piece)
            {
                pieces.Add(piece);
            }
        }

        return pieces;
    }
}
