namespace ReadTheStupidText.Application.Images;

/// <summary>Pure bilinear resize and crop on <see cref="PixelImage"/>.</summary>
public static class ImageResizer
{
    public static PixelImage Resize(PixelImage source, int width, int height)
    {
        byte[] output = new byte[width * height * PixelImage.BytesPerPixel];
        float scaleX = (float)source.Width / width;
        float scaleY = (float)source.Height / height;
        for (int y = 0; y < height; y++)
        {
            float sourceY = Math.Clamp((y + 0.5f) * scaleY - 0.5f, 0, source.Height - 1);
            for (int x = 0; x < width; x++)
            {
                float sourceX = Math.Clamp((x + 0.5f) * scaleX - 0.5f, 0, source.Width - 1);
                WritePixel(source, sourceX, sourceY, output, (y * width + x) * PixelImage.BytesPerPixel);
            }
        }

        return new PixelImage(width, height, output);
    }

    /// <summary>Copies the region, clamped to the image; null when nothing is left of it.</summary>
    public static PixelImage? Crop(PixelImage source, OcrBox box)
    {
        int left = Math.Clamp((int)MathF.Floor(box.Left), 0, source.Width);
        int top = Math.Clamp((int)MathF.Floor(box.Top), 0, source.Height);
        int right = Math.Clamp((int)MathF.Ceiling(box.Right), 0, source.Width);
        int bottom = Math.Clamp((int)MathF.Ceiling(box.Bottom), 0, source.Height);
        if (right <= left || bottom <= top)
        {
            return null;
        }

        int width = right - left;
        byte[] output = new byte[width * (bottom - top) * PixelImage.BytesPerPixel];
        for (int y = top; y < bottom; y++)
        {
            int from = (y * source.Width + left) * PixelImage.BytesPerPixel;
            Buffer.BlockCopy(source.Bgr, from, output, (y - top) * width * PixelImage.BytesPerPixel, width * PixelImage.BytesPerPixel);
        }

        return new PixelImage(width, bottom - top, output);
    }

    /// <summary>Rotates 90 degrees counter-clockwise, like numpy's <c>rot90</c> (what PaddleOCR does to a tall crop).</summary>
    public static PixelImage RotateCounterClockwise(PixelImage source)
    {
        int width = source.Height;
        int height = source.Width;
        byte[] output = new byte[width * height * PixelImage.BytesPerPixel];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int from = (x * source.Width + (source.Width - 1 - y)) * PixelImage.BytesPerPixel;
                Buffer.BlockCopy(source.Bgr, from, output, (y * width + x) * PixelImage.BytesPerPixel, PixelImage.BytesPerPixel);
            }
        }

        return new PixelImage(width, height, output);
    }

    private static void WritePixel(PixelImage source, float x, float y, byte[] output, int offset)
    {
        int x0 = (int)x;
        int y0 = (int)y;
        int x1 = Math.Min(x0 + 1, source.Width - 1);
        int y1 = Math.Min(y0 + 1, source.Height - 1);
        float fx = x - x0;
        float fy = y - y0;
        for (int c = 0; c < PixelImage.BytesPerPixel; c++)
        {
            float top = Lerp(Sample(source, x0, y0, c), Sample(source, x1, y0, c), fx);
            float bottom = Lerp(Sample(source, x0, y1, c), Sample(source, x1, y1, c), fx);
            output[offset + c] = (byte)Math.Clamp(MathF.Round(Lerp(top, bottom, fy)), 0, 255);
        }
    }

    private static float Sample(PixelImage image, int x, int y, int channel) =>
        image.Bgr[(y * image.Width + x) * PixelImage.BytesPerPixel + channel];

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
