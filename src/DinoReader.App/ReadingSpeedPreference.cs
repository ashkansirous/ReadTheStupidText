using DinoReader.Core;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader;

/// <summary>
/// The grown-up's reading speed (Slice 150, Decision 155), kept under Dino's own key because the shared store cannot tell a missing
/// value from a stored one: nothing stored gives 0.8x, a stored value is clamped to 0.5x-1.2x. Slice 278 B: pre-made clips and recordings
/// are played at it (the clip player takes the rate for each play), so the setting stays; there is no speech engine to apply it to.
/// </summary>
public static class ReadingSpeedPreference
{
	private const string Key = "dino.reading.speed";

	public static PlaybackRate Load()
	{
		try
		{
			return ReadingSpeed.Clamp(Preferences.Default.Get(Key, ReadingSpeed.Default));
		}
		catch (InvalidCastException)
		{
			// A value of another type under this key: treat it as missing.
			return ReadingSpeed.DefaultRate;
		}
	}

	public static void Save(PlaybackRate speed) => Preferences.Default.Set(Key, speed.Value);
}
