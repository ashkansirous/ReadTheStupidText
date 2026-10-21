using ReadTheStupidText.Application.Images;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// <see cref="IPageImageKeeper"/> for Windows (Slice 67): decodes the page upright (its EXIF
/// orientation applied) with the long edge capped at <see cref="IPageImageKeeper.MaxLongEdge"/>,
/// and writes it as a JPEG.
/// </summary>
public sealed class WindowsPageImageKeeper : IPageImageKeeper
{
	public async Task KeepAsync(string location, string destinationPath)
	{
		string source = ScannedText.ToLocalPath(location)
			?? throw new IOException($"Not a local image: '{location}'.");
		RejectOverwritingSource(source, destinationPath);

		StorageFile file = await StorageFile.GetFileFromPathAsync(source).AsTask().ConfigureAwait(false);
		using IRandomAccessStream stream = await file.OpenReadAsync().AsTask().ConfigureAwait(false);
		BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream).AsTask().ConfigureAwait(false);
		using SoftwareBitmap upright = await decoder.GetSoftwareBitmapAsync(
			BitmapPixelFormat.Bgra8,
			BitmapAlphaMode.Premultiplied,
			WindowsImageScale.Fit(decoder, IPageImageKeeper.MaxLongEdge),
			ExifOrientationMode.RespectExifOrientation,
			ColorManagementMode.DoNotColorManage).AsTask().ConfigureAwait(false);

		Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
		await WindowsJpeg.WriteAsync(upright, destinationPath).ConfigureAwait(false);
	}

	// The kept copy must be a different file from the source, which the caller deletes afterwards.
	private static void RejectOverwritingSource(string source, string destinationPath)
	{
		if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException("The kept page image must not be the scanned source file.", nameof(destinationPath));
		}
	}
}
