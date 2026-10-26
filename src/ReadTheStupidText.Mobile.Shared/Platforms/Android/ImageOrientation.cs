using Android.Graphics;
using Android.Media;
using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// EXIF orientation as a bitmap transform. The matrices are the standard ones for all eight values
/// (rotations and the mirrored Flip, Transpose and Transverse), so a photo is upright whatever
/// the camera wrote.
/// </summary>
internal static class ImageOrientation
{
    /// <summary>The matrix that turns a stored image upright, or null when it already is.</summary>
    public static Matrix? MatrixFor(int exifOrientation)
    {
        var matrix = new Matrix();
        switch (exifOrientation)
        {
            case (int)Orientation.FlipHorizontal:
                matrix.SetScale(-1, 1);
                break;
            case (int)Orientation.Rotate180:
                matrix.SetRotate(180);
                break;
            case (int)Orientation.FlipVertical:
                matrix.SetScale(1, -1);
                break;
            case (int)Orientation.Transpose:
                matrix.SetRotate(90);
                matrix.PostScale(-1, 1);
                break;
            case (int)Orientation.Rotate90:
                matrix.SetRotate(90);
                break;
            case (int)Orientation.Transverse:
                matrix.SetRotate(-90);
                matrix.PostScale(-1, 1);
                break;
            case (int)Orientation.Rotate270:
                matrix.SetRotate(-90);
                break;
            default:
                matrix.Dispose();
                return null;
        }

        return matrix;
    }

    /// <summary>True when upright width and height are the stored height and width.</summary>
    public static bool SwapsAxes(int exifOrientation) =>
        exifOrientation is (int)Orientation.Transpose or (int)Orientation.Rotate90
            or (int)Orientation.Transverse or (int)Orientation.Rotate270;

    /// <summary>The upright bitmap: the source itself when no transform is needed, otherwise a new one the caller disposes.</summary>
    public static Bitmap Apply(Bitmap source, int exifOrientation)
    {
        using Matrix? matrix = MatrixFor(exifOrientation);
        return matrix is null
            ? source
            : Bitmap.CreateBitmap(source, 0, 0, source.Width, source.Height, matrix, true)!;
    }
}

/// <summary>Bitmap to the B, G, R bytes the OCR models expect, one row at a time.</summary>
internal static class BitmapPixels
{
    public static PixelImage ToPixelImage(Bitmap bitmap)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;
        byte[] bgr = new byte[width * height * PixelImage.BytesPerPixel];
        int[] row = new int[width];
        for (int y = 0; y < height; y++)
        {
            bitmap.GetPixels(row, 0, width, 0, y, width, 1);
            WriteRow(row, bgr, y * width * PixelImage.BytesPerPixel);
        }

        return new PixelImage(width, height, bgr).Validate();
    }

    private static void WriteRow(int[] argb, byte[] bgr, int offset)
    {
        for (int x = 0; x < argb.Length; x++)
        {
            int pixel = argb[x];
            int at = offset + x * PixelImage.BytesPerPixel;
            bgr[at] = (byte)(pixel & 0xFF);
            bgr[at + 1] = (byte)((pixel >> 8) & 0xFF);
            bgr[at + 2] = (byte)((pixel >> 16) & 0xFF);
        }
    }
}
