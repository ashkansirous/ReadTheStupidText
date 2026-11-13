using DinoReader.Core;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader;

/// <summary>
/// The grown-up's reading speed (Slice 150, Decision 155), kept under Dino's own key because the shared store cannot tell a missing
/// value from a stored one: nothing stored gives 1x (Decision 172), a stored value is clamped to 0.8x-1.2x. Slice 278 B: pre-made clips and recordings
/// are played at it (the clip player takes the rate for each play), so the setting stays; there is no speech engine to apply it to.
/// </summary>
public static class ReadingSpeedPreference
{
	private const string Key = "dino.reading.speed";

	/// <summary>Slice 372: a speed the Debug test hook sets for this app run only (never saved, gone at the next start). It may go up to 2x, past Dino's 1.2x range, so a check can play a book quickly without touching the grown-up's saved speed.</summary>
	internal static PlaybackRate? TestOverride { get; set; }

	/// <summary>The speed the reader plays clips at: the run-only test override when one is set, else the saved speed. Only playback reads this; Settings, the slider and the backup export use <see cref="Load"/> (the saved value).</summary>
	public static PlaybackRate LoadForPlayback() => TestOverride ?? Load();

	/// <summary>The saved speed (never the run-only test override).</summary>
	public static PlaybackRate Load()
	{
		try
		{
			return ReadingSpeed.Clamp(Preferences.Default.Get(Key, ReadingSpeed.Default));
		}
		catch (InvalidCastException)
		{
			// no-fallback: a value of another type under this key is treated as missing, which is the normal default speed, not a degraded path.
			return ReadingSpeed.DefaultRate;
		}
	}

	public static void Save(PlaybackRate speed) => Preferences.Default.Set(Key, speed.Value);
}
