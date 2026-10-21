using System.Diagnostics;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Mobile;

namespace DinoReader;

/// <summary>
/// Gets the default voice ready at launch: stores English as the language on first run (the
/// language picker is a later slice), applies the saved speed, then lets the shared installer
/// download or activate the Supertonic voice pack. Until the pack is ready the phone voice reads.
/// </summary>
public sealed class SpeechStartup
{
	private readonly ISettingsStore _settings;
	private readonly ISpeechReader _reader;
	private readonly VoicePackInstaller _installer;

	public SpeechStartup(ISettingsStore settings, ISpeechReader reader, VoicePackInstaller installer)
	{
		_settings = settings;
		_reader = reader;
		_installer = installer;
	}

	public async Task RunAsync()
	{
		try
		{
			_settings.Language ??= AppLanguages.Code(AppLanguage.English);
			_reader.SetSpeed(_settings.Speed);
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
