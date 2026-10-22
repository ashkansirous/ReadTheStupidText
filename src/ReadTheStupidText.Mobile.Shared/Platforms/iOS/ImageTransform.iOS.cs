using CoreGraphics;
using Foundation;
using ReadTheStupidText.Application.Images;
using UIKit;

namespace ReadTheStupidText.Mobile;

public static partial class ImageTransform
{
	private const float JpegQuality = 0.9f;
	private const int BitsPerComponent = 8;
	private const int FullResolution = int.MaxValue;

	public static partial Task ApplyAsync(string source, string destination, int quarterTurns, CropRect? crop) =>
		Task.Run(() => Apply(source, destination, quarterTurns, crop));

	private static void Apply(string source, string destination, int quarterTurns, CropRect? crop)
	{
		RejectOverwritingSource(source, destination);
		string path = ScannedText.ToLocalPath(source) ?? throw new IOException($"Not a local image: '{source}'.");
		// ImageIO applies the EXIF orientation. The picture is decoded at its own size (not the recognition ceiling), so turning or cropping never shrinks it.
		using CGImage upright = IosPixelImageLoader.DecodeUpright(path, FullResolution);
		using CGImage turned = Turn(upright, WrapTurns(quarterTurns));
		using CGImage cut = Cut(turned, crop);
		using var image = UIImage.FromImage(cut);
		using NSData jpeg = image.AsJPEG(JpegQuality) ?? throw new IOException($"Could not encode '{destination}'.");
		Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
		using var target = NSUrl.FromFilename(destination);
		if (!jpeg.Save(target, NSDataWritingOptions.Atomic, out NSError? error))
		{
			throw new IOException($"Could not write '{destination}': {error?.LocalizedDescription}");
		}
	}

	// Clockwise as seen on screen is a negative angle in CoreGraphics' bottom-left origin.
	private static CGImage Turn(CGImage source, int quarterTurns)
	{
		if (quarterTurns == 0)
		{
			return source;
		}

		int width = (int)source.Width;
		int height = (int)source.Height;
		bool swaps = quarterTurns % 2 == 1;
		int outWidth = swaps ? height : width;
		int outHeight = swaps ? width : height;
		using var space = CGColorSpace.CreateDeviceRGB();
		using var context = new CGBitmapContext(null, outWidth, outHeight, BitsPerComponent, 0, space, CGImageAlphaInfo.PremultipliedLast);
		context.TranslateCTM(outWidth / 2f, outHeight / 2f);
		context.RotateCTM(-quarterTurns * (nfloat)Math.PI / 2);
		context.DrawImage(new CGRect(-width / 2f, -height / 2f, width, height), source);
		return context.ToImage() ?? throw new IOException("Could not turn the image.");
	}

	private static CGImage Cut(CGImage source, CropRect? crop)
	{
		if (crop is not { IsFull: false } rect)
		{
			return source;
		}

		(int x, int y, int width, int height) = rect.ToPixels((int)source.Width, (int)source.Height);
		return source.WithImageInRect(new CGRect(x, y, width, height)) ?? throw new IOException("Could not crop the image.");
	}
}
