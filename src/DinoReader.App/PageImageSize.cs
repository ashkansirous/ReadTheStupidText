#if ANDROID
using AndroidExif = Android.Media.ExifInterface;
using AndroidGraphics = Android.Graphics;
#endif
#if IOS
using Foundation;
using ImageIO;
#endif
#if WINDOWS
using Windows.Graphics.Imaging;
using Windows.Storage;
#endif

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

#if ANDROID
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
#elif IOS
	/// <summary>The upright size in pixels, or null when the file is missing or not a picture.</summary>
	public static Size? Read(string path)
	{
		try
		{
			using var url = NSUrl.FromFilename(path);
			using CGImageSource? source = CGImageSource.FromUrl(url);
			var properties = source?.GetProperties(0);
			int width = properties?.PixelWidth ?? 0;
			int height = properties?.PixelHeight ?? 0;
			if (width <= 0 || height <= 0)
			{
				return null;
			}

			int orientation = (int)(properties?.Orientation ?? 0);
			bool sideways = orientation is >= FirstSidewaysOrientation and <= LastSidewaysOrientation;
			return sideways ? new Size(height, width) : new Size(width, height);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Dino Reader could not read the size of {path}: {ex.Message}");
			return null;
		}
	}
#elif WINDOWS
	/// <summary>The upright size in pixels, or null when the file is missing or not a picture.</summary>
	public static Size? Read(string path)
	{
		try
		{
			// Run on the thread pool so waiting on the WinRT call never blocks on the UI thread's context.
			return Task.Run(() => ReadAsync(path)).GetAwaiter().GetResult();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Dino Reader could not read the size of {path}: {ex.Message}");
			return null;
		}
	}

	// The decoder's oriented size already swaps width and height for a sideways picture.
	private static async Task<Size?> ReadAsync(string path)
	{
		StorageFile file = await StorageFile.GetFileFromPathAsync(path);
		using var stream = await file.OpenReadAsync();
		BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
		return decoder.OrientedPixelWidth > 0 && decoder.OrientedPixelHeight > 0
			? new Size(decoder.OrientedPixelWidth, decoder.OrientedPixelHeight)
			: null;
	}
#else
	/// <summary>The upright size in pixels; not available on this platform.</summary>
	public static Size? Read(string path) => null;
#endif
}
