using CoreGraphics;
using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// A <see cref="CGImage"/> drawn to the B, G, R bytes the OCR models expect (Slice 55), scaled to
/// the requested size. The image is drawn onto a white background, so a transparent PNG reads as
/// dark text on paper rather than on black.
/// </summary>
internal static class CgImagePixels
{
    private const int BitsPerComponent = 8;
    private const int BytesPerRgbx = 4;

    public static PixelImage ToPixelImage(CGImage image, int width, int height)
    {
        byte[] rgbx = DrawRgbx(image, width, height);
        byte[] bgr = new byte[width * height * PixelImage.BytesPerPixel];
        for (int pixel = 0; pixel < width * height; pixel++)
        {
            int from = pixel * BytesPerRgbx;
            int to = pixel * PixelImage.BytesPerPixel;
            bgr[to] = rgbx[from + 2];
            bgr[to + 1] = rgbx[from + 1];
            bgr[to + 2] = rgbx[from];
        }

        return new PixelImage(width, height, bgr).Validate();
    }

    // The context owns its pixel memory (a null data pointer); it is copied out before it is
    // disposed. Row 0 of that memory is the top of the drawn image.
    private static byte[] DrawRgbx(CGImage image, int width, int height)
    {
        using CGColorSpace space = CGColorSpace.CreateDeviceRGB();
        using var context = new CGBitmapContext(
            IntPtr.Zero, width, height, BitsPerComponent, width * BytesPerRgbx, space, CGImageAlphaInfo.NoneSkipLast)
        {
            InterpolationQuality = CGInterpolationQuality.High,
        };

        var bounds = new CGRect(0, 0, width, height);
        context.SetFillColor(1, 1, 1, 1);
        context.FillRect(bounds);
        context.DrawImage(bounds, image);

        byte[] rgbx = new byte[width * height * BytesPerRgbx];
        System.Runtime.InteropServices.Marshal.Copy(context.Data, rgbx, 0, rgbx.Length);
        return rgbx;
    }
}
