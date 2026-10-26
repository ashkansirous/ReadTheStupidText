namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// A crop rectangle in fractions of the picture (all values 0..1, origin top-left), like <c>NormalizedRect</c>, so it is valid
/// at any resolution (Slice 78). <see cref="Normalised"/> makes any rectangle usable; <see cref="ToPixels"/> turns it into the
/// pixel rectangle a platform decoder crops. Pure, so it is unit-tested.
/// </summary>
public readonly record struct CropRect(double Left, double Top, double Width, double Height)
{
	/// <summary>The smallest side a crop may have, as a fraction of the picture.</summary>
	public const double MinimumSide = 0.05;

	/// <summary>The whole picture.</summary>
	public static CropRect Full { get; } = new(0, 0, 1, 1);

	/// <summary>Whether this rectangle covers the whole picture (so cropping is a no-op).</summary>
	public bool IsFull => Left <= 0 && Top <= 0 && Width >= 1 && Height >= 1;

	/// <summary>The rectangle moved and sized to lie inside 0..1 with sides of at least <see cref="MinimumSide"/>.</summary>
	public CropRect Normalised()
	{
		double width = Math.Clamp(double.IsNaN(Width) ? 1 : Width, MinimumSide, 1);
		double height = Math.Clamp(double.IsNaN(Height) ? 1 : Height, MinimumSide, 1);
		double left = Math.Clamp(double.IsNaN(Left) ? 0 : Left, 0, 1 - width);
		double top = Math.Clamp(double.IsNaN(Top) ? 0 : Top, 0, 1 - height);
		return new CropRect(left, top, width, height);
	}

	/// <summary>The pixel rectangle on a picture of the given size: inside the picture, at least one pixel on each side.</summary>
	public (int X, int Y, int Width, int Height) ToPixels(int pictureWidth, int pictureHeight)
	{
		CropRect rect = Normalised();
		int x = Math.Clamp((int)Math.Round(rect.Left * pictureWidth), 0, Math.Max(pictureWidth - 1, 0));
		int y = Math.Clamp((int)Math.Round(rect.Top * pictureHeight), 0, Math.Max(pictureHeight - 1, 0));
		int width = Math.Clamp((int)Math.Round(rect.Width * pictureWidth), 1, Math.Max(pictureWidth - x, 1));
		int height = Math.Clamp((int)Math.Round(rect.Height * pictureHeight), 1, Math.Max(pictureHeight - y, 1));
		return (x, y, width, height);
	}
}
