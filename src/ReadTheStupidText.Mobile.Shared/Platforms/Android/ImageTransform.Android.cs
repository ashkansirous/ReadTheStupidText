using Android.Graphics;

namespace ReadTheStupidText.Mobile;

public static partial class ImageTransform
{
	private const int JpegQuality = 90;
	private const float DegreesPerQuarterTurn = 90f;

	public static partial Task ApplyAsync(string source, string destination, int quarterTurns, CropRect? crop) =>
		Task.Run(() => Apply(source, destination, quarterTurns, crop));

	private static void Apply(string source, string destination, int quarterTurns, CropRect? crop)
	{
		RejectOverwritingSource(source, destination);
		Android.Content.Context context = Android.App.Application.Context;
		using Bitmap decoded = Decode(context, source);
		using Bitmap upright = ImageOrientation.Apply(decoded, AndroidImageSource.ReadOrientation(context, source));
		using Bitmap turned = Turn(upright, WrapTurns(quarterTurns));
		using Bitmap cut = Cut(turned, crop);
		Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
		using FileStream output = File.Create(destination);
		cut.Compress(Bitmap.CompressFormat.Jpeg!, JpegQuality, output);
	}

	private static Bitmap Decode(Android.Content.Context context, string source)
	{
		using Stream stream = AndroidImageSource.Open(context, source);
		return BitmapFactory.DecodeStream(stream) ?? throw new IOException($"Could not decode the image '{source}'.");
	}

	// The source itself when nothing turns, as ImageOrientation.Apply does; callers dispose both.
	private static Bitmap Turn(Bitmap source, int quarterTurns)
	{
		if (quarterTurns == 0)
		{
			return source;
		}

		using var matrix = new Matrix();
		matrix.SetRotate(quarterTurns * DegreesPerQuarterTurn);
		return Bitmap.CreateBitmap(source, 0, 0, source.Width, source.Height, matrix, true)!;
	}

	private static Bitmap Cut(Bitmap source, CropRect? crop)
	{
		if (crop is not { IsFull: false } rect)
		{
			return source;
		}

		(int x, int y, int width, int height) = rect.ToPixels(source.Width, source.Height);
		return Bitmap.CreateBitmap(source, x, y, width, height)!;
	}
}
