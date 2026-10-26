using System.Diagnostics;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
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
	private readonly ISettingsStore _settings;
	private readonly ISpeechReader _reader;
	private readonly VoicePackInstaller _installer;
	private readonly StorytellerDefault _storyteller;

	public SpeechStartup(ISettingsStore settings, ISpeechReader reader, VoicePackInstaller installer, StorytellerDefault storyteller)
	{
		_storyteller = storyteller;
		_settings = settings;
		_reader = reader;
		_installer = installer;
	}

	public async Task RunAsync()
	{
		try
		{
			_reader.SetSpeed(_settings.Speed);
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
