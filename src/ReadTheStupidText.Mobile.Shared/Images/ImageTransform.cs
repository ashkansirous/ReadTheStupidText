namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// Rotates (by quarter turns) and crops a picture into a new JPEG file (Slice 78). The source is never written to, so undoing
/// an edit is just pointing back at the source. The source's EXIF orientation is applied first (Slice 63 found photos that
/// arrive sideways), then the rotation, then the crop, which is given in the rotated picture's own 0..1 coordinates. Each platform
/// supplies <see cref="ApplyAsync"/> (Platforms/*/ImageTransform.*.cs).
/// </summary>
public static partial class ImageTransform
{
	public const int QuarterTurnsInCircle = 4;

	/// <summary>Writes <paramref name="source"/> turned clockwise by <paramref name="quarterTurns"/> and then cut to <paramref name="crop"/>
	/// (null for no crop) to <paramref name="destination"/>. The destination must be a different file from the source.</summary>
	public static partial Task ApplyAsync(string source, string destination, int quarterTurns, CropRect? crop);

	/// <summary>The rotation, as clockwise quarter turns in 0..3.</summary>
	public static int WrapTurns(int quarterTurns) =>
		((quarterTurns % QuarterTurnsInCircle) + QuarterTurnsInCircle) % QuarterTurnsInCircle;

	/// <summary>Rotation only.</summary>
	public static Task RotateAsync(string source, string destination, int quarterTurns) =>
		ApplyAsync(source, destination, quarterTurns, null);

	/// <summary>Crop only.</summary>
	public static Task CropAsync(string source, string destination, CropRect crop) =>
		ApplyAsync(source, destination, 0, crop);

	private static void RejectOverwritingSource(string source, string destination)
	{
		if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException("The transformed picture must not overwrite its source.", nameof(destination));
		}
	}
}
