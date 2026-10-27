using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Core;

/// <summary>
/// Dino Reader's own reading-speed range (Slice 150, Decision 155): 0.5x to 1.2x in 0.05 steps, 0.8x until a grown-up
/// chooses. Narrower than the shared <see cref="PlaybackRate"/> (0.5-2.0), which RTST keeps; nothing above 1.2x can reach the reader.
/// </summary>
public static class ReadingSpeed
{
	public const double Min = 0.5;
	public const double Max = 1.2;
	public const double Default = 0.8;
	public const double Step = 0.05;

	/// <summary>The nearest step inside Dino's range; a value that is not a number gives the default.</summary>
	public static PlaybackRate Clamp(double value) =>
		double.IsNaN(value) ? new PlaybackRate(Default) : new PlaybackRate(Math.Clamp(value, Min, Max));

	/// <summary>The default rate, for a fresh install.</summary>
	public static PlaybackRate DefaultRate => new(Default);
}
