using Windows.Graphics.Imaging;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// The one scaling rule for Windows page pictures (Slice 67): the OCR bitmap and the kept picture
/// round their scaled sides the same way, so the OCR boxes line up with the picture shown.
/// </summary>
internal static class WindowsImageScale
{
	/// <summary>A side times the scale, rounded, at least 1.</summary>
	public static uint Scaled(double side, double scale) => (uint)Math.Max(1, Math.Round(side * scale));

	/// <summary>A transform scaling the decoder's image so its longest side is <paramref name="maxSide"/>; a smaller image is untouched.</summary>
	public static BitmapTransform Fit(BitmapDecoder decoder, int maxSide)
	{
		var transform = new BitmapTransform();
		uint longest = Math.Max(decoder.PixelWidth, decoder.PixelHeight);
		if (longest > maxSide)
		{
			double scale = (double)maxSide / longest;
			transform.ScaledWidth = Scaled(decoder.PixelWidth, scale);
			transform.ScaledHeight = Scaled(decoder.PixelHeight, scale);
		}

		return transform;
	}
}
