using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>Writes a bitmap as a JPEG file (Slice 67): the one encoder the PDF renderer and the page image keeper share.</summary>
internal static class WindowsJpeg
{
	private const float Quality = 0.85f;

	public static async Task WriteAsync(SoftwareBitmap bitmap, string path)
	{
		using var stream = new InMemoryRandomAccessStream();
		var properties = new BitmapPropertySet
		{
			{ "ImageQuality", new BitmapTypedValue(Quality, Windows.Foundation.PropertyType.Single) },
		};
		BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream, properties).AsTask().ConfigureAwait(false);
		using SoftwareBitmap opaque = Opaque(bitmap);
		encoder.SetSoftwareBitmap(opaque);
		await encoder.FlushAsync().AsTask().ConfigureAwait(false);

		stream.Seek(0);
		await using FileStream output = File.Create(path);
		await stream.AsStreamForRead().CopyToAsync(output).ConfigureAwait(false);
	}

	// The JPEG encoder takes an opaque Bgra8 image; a copy keeps the caller's bitmap untouched.
	private static SoftwareBitmap Opaque(SoftwareBitmap bitmap) =>
		SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
}
