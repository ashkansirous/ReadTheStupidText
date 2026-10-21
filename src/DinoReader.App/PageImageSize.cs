using AndroidExif = Android.Media.ExifInterface;
using AndroidGraphics = Android.Graphics;

namespace DinoReader;

/// <summary>
/// The size a page picture is shown at, read from the file's header without decoding it: width and
/// height swap for a picture stored sideways and marked rotated (EXIF orientation 5 to 8), because the
/// image view turns it upright and the boxes are in the upright picture's coordinates (Slice 64).
/// </summary>
public static class PageImageSize
{
	private const int FirstSidewaysOrientation = 5;
	private const int LastSidewaysOrientation = 8;

	/// <summary>The upright size in pixels, or null when the file is missing or not a picture.</summary>
	public static Size? Read(string path)
	{
		try
		{
			using var options = new AndroidGraphics.BitmapFactory.Options { InJustDecodeBounds = true };
			AndroidGraphics.BitmapFactory.DecodeFile(path, options);
			if (options.OutWidth <= 0 || options.OutHeight <= 0)
			{
				return null;
			}

			return IsSideways(path) ? new Size(options.OutHeight, options.OutWidth) : new Size(options.OutWidth, options.OutHeight);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Dino Reader could not read the size of {path}: {ex.Message}");
			return null;
		}
	}

	private static bool IsSideways(string path)
	{
		using var exif = new AndroidExif(path);
		int orientation = exif.GetAttributeInt(AndroidExif.TagOrientation, 1);
		return orientation is >= FirstSidewaysOrientation and <= LastSidewaysOrientation;
	}
}
