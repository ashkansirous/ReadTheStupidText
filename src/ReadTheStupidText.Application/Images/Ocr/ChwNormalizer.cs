namespace ReadTheStupidText.Application.Images;

/// <summary>
/// The shared last step of both OCR preprocessors: stored B, G, R bytes (HWC) become a planar
/// float tensor (CHW), each value mapped as <c>byte / 255 * scale[c] + offset[c]</c>.
/// </summary>
public static class ChwNormalizer
{
    public static float[] Normalize(PixelImage image, IReadOnlyList<float> scale, IReadOnlyList<float> offset)
    {
        int plane = image.Width * image.Height;
        float[] data = new float[PixelImage.BytesPerPixel * plane];
        for (int i = 0; i < plane; i++)
        {
            for (int c = 0; c < PixelImage.BytesPerPixel; c++)
            {
                data[c * plane + i] = image.Bgr[i * PixelImage.BytesPerPixel + c] / 255f * scale[c] + offset[c];
            }
        }

        return data;
    }
}
