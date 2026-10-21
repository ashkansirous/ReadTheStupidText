namespace ReadTheStupidText.Application.Images;

/// <summary>
/// How much to downsample a line's crop when it is decoded from the full-resolution original: the
/// largest power of two that still leaves the line at least <see cref="TargetHeights"/> times the
/// recognizer's input height, so the recognizer always has more detail than it needs to scale down from.
/// </summary>
public static class CropSampling
{
    public const int TargetHeights = 2;

    public static int SampleSize(float lineHeightPixels, int recognitionHeight)
    {
        float target = recognitionHeight * TargetHeights;
        int sample = 1;
        while (lineHeightPixels / (sample * 2) >= target)
        {
            sample *= 2;
        }

        return sample;
    }
}
