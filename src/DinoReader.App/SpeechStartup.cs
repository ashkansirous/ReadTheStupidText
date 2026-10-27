using System.Diagnostics;
using DinoReader.Core;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Mobile.Shared;

namespace DinoReader;

/// <summary>
/// Gets the voice ready at launch: applies the saved speed, then lets the shared installer
/// download or activate the voice pack of the stored language. It no longer picks English by itself
/// (Slice 93): a fresh install has no language until the first-run page stores the grown-up's choice.
/// Until the pack is ready the phone voice reads. Then the storyteller pack (Slice 115) starts downloading by itself when missing.
/// </summary>
public sealed class SpeechStartup
{
	private readonly ISpeechReader _reader;
	private readonly VoicePackInstaller _installer;
	private readonly StorytellerDefault _storyteller;

	public SpeechStartup(ISpeechReader reader, VoicePackInstaller installer, StorytellerDefault storyteller)
	{
		_storyteller = storyteller;
		_reader = reader;
		_installer = installer;
	}

	public async Task RunAsync()
	{
		try
		{
			_reader.SetSpeed(ReadingSpeedPreference.Load());
			// The storyteller pack is Dino's default voice (Slice 115): fetched on Wi-Fi when missing, never throws. Started first so a slow or failing installer cannot hold it back.
			_ = _storyteller.StartAsync();
			await _installer.StartAsync();
		}
		catch (Exception ex)
		{
			// Fire-and-forget from DinoApp: an escaped exception would go unobserved, so log it
			// and publish a terminal Failed state (tap to retry) for the UI.
			Debug.WriteLine($"Dino Reader speech startup failed: {ex}");
			_installer.ReportFailed();
		}
	}
}

/// <summary>
/// The grown-up's reading speed (Slice 150, Decision 155), kept under Dino's own key because the shared store cannot tell a missing
/// value from a stored one: nothing stored gives 0.8x, a stored value is clamped to 0.5x-1.2x.
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
