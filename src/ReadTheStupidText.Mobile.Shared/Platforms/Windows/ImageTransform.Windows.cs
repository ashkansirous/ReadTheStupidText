using System.Runtime.InteropServices.WindowsRuntime;
using ReadTheStupidText.Application.Images;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ReadTheStupidText.Mobile;

public static partial class ImageTransform
{
	private const int BytesPerPixel = 4;

	public static partial async Task ApplyAsync(string source, string destination, int quarterTurns, CropRect? crop)
	{
		RejectOverwritingSource(source, destination);
		string path = ScannedText.ToLocalPath(source) ?? throw new IOException($"Not a local image: '{source}'.");
		StorageFile file = await StorageFile.GetFileFromPathAsync(path).AsTask().ConfigureAwait(false);
		using IRandomAccessStream stream = await file.OpenReadAsync().AsTask().ConfigureAwait(false);
		BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream).AsTask().ConfigureAwait(false);
		using SoftwareBitmap upright = await decoder.GetSoftwareBitmapAsync(
			BitmapPixelFormat.Bgra8,
			BitmapAlphaMode.Premultiplied,
			new BitmapTransform(),
			ExifOrientationMode.RespectExifOrientation,
			ColorManagementMode.DoNotColorManage).AsTask().ConfigureAwait(false);

		(byte[] pixels, int width, int height) = ReadPixels(upright);
		(pixels, width, height) = Turn(pixels, width, height, WrapTurns(quarterTurns));
		if (crop is { IsFull: false } rect)
		{
			(pixels, width, height) = Cut(pixels, width, height, rect);
		}

		using SoftwareBitmap result = SoftwareBitmap.CreateCopyFromBuffer(
			pixels.AsBuffer(), BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);
		Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
		await WindowsJpeg.WriteAsync(result, destination).ConfigureAwait(false);
	}

	private static (byte[] Pixels, int Width, int Height) ReadPixels(SoftwareBitmap bitmap)
	{
		byte[] pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * BytesPerPixel];
		bitmap.CopyToBuffer(pixels.AsBuffer());
		return (pixels, bitmap.PixelWidth, bitmap.PixelHeight);
	}

	// Clockwise turns by moving pixels, so the result does not depend on how the encoder orders crop and rotation.
	private static (byte[] Pixels, int Width, int Height) Turn(byte[] pixels, int width, int height, int quarterTurns)
	{
		if (quarterTurns == 0)
		{
			return (pixels, width, height);
		}

		bool swaps = quarterTurns % 2 == 1;
		int outWidth = swaps ? height : width;
		int outHeight = swaps ? width : height;
		byte[] turned = new byte[pixels.Length];
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				(int nx, int ny) = TurnedPosition(x, y, width, height, quarterTurns);
				System.Buffer.BlockCopy(pixels, (y * width + x) * BytesPerPixel, turned, (ny * outWidth + nx) * BytesPerPixel, BytesPerPixel);
			}
		}

		return (turned, outWidth, outHeight);
	}

	private static (int X, int Y) TurnedPosition(int x, int y, int width, int height, int quarterTurns) => quarterTurns switch
	{
		1 => (height - 1 - y, x),
		2 => (width - 1 - x, height - 1 - y),
		_ => (y, width - 1 - x),
	};

	private static (byte[] Pixels, int Width, int Height) Cut(byte[] pixels, int width, int height, CropRect crop)
	{
		(int left, int top, int cutWidth, int cutHeight) = crop.ToPixels(width, height);
		byte[] cut = new byte[cutWidth * cutHeight * BytesPerPixel];
		for (int row = 0; row < cutHeight; row++)
		{
			System.Buffer.BlockCopy(pixels, ((top + row) * width + left) * BytesPerPixel, cut, row * cutWidth * BytesPerPixel, cutWidth * BytesPerPixel);
		}

		return (cut, cutWidth, cutHeight);
	}
}
