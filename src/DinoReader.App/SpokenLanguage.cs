using DinoReader.Core;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Mobile;

namespace DinoReader;

/// <summary>
/// Points the voice at the language a story is spoken in (Slice 66): the neural reader follows the stored
/// language setting, so opening a story in Farsi activates (or starts downloading) the Farsi voice pack, and
/// the phone voice reads until it is ready. The story's own language, or the grown-up's when it is translated.
/// Only the reading screen switches it; the cast screen never does. A switch made while an install is still
/// running is not lost: a single worker serves the latest wanted language after the current run, once per change.
/// </summary>
public sealed class SpokenLanguage
{
	private readonly ISettingsStore _settings;
	private readonly VoicePackInstaller _installer;
	private readonly StoryTranslator _translator;
	private readonly LatestWinsWorker<string> _worker;

	public SpokenLanguage(ISettingsStore settings, VoicePackInstaller installer, StoryTranslator translator)
	{
		_settings = settings;
		_installer = installer;
		_translator = translator;
		_worker = new LatestWinsWorker<string>(ServeAsync, ex => System.Diagnostics.Debug.WriteLine($"SpokenLanguage: {ex}"));
	}

	/// <summary>The language the voice speaks now (the stored setting, English when none).</summary>
	public string Current => UiLanguage.Resolve(_settings.Language);

	/// <summary>Uses the language <paramref name="story"/> will be spoken in.</summary>
	public void Use(Story story) => Use(_translator.SpokenLanguage(story));

	/// <summary>Uses <paramref name="language"/> for the voice, and starts that language's pack unless it is the one in use and ready.</summary>
	public void Use(string language)
	{
		string code = UiLanguage.Resolve(language);
		bool same = string.Equals(_settings.Language, code, StringComparison.OrdinalIgnoreCase);
		if (same && _installer.State == PackInstallState.Ready)
		{
			return;
		}

		_worker.Use(code);
	}

	// One run per wanted language: store it (the installer starts a run for the change itself), then
	// join that run: StartAsync completes when the run in flight settles, so the language is never
	// reported served while its pack is still resolving.
	private async Task ServeAsync(string code)
	{
		await WhileBusyAsync();
		_settings.Language = code;
		await _installer.StartAsync();
		await WhileBusyAsync();
	}

	// A published state can still be queued for the main thread when StartAsync returns; wait it out.
	private async Task WhileBusyAsync()
	{
		while (_installer.State is PackInstallState.Resolving or PackInstallState.Downloading)
		{
			var changed = new TaskCompletionSource();
			EventHandler handler = (_, _) => changed.TrySetResult();
			_installer.Changed += handler;
			try
			{
				if (_installer.State is PackInstallState.Resolving or PackInstallState.Downloading)
				{
					await changed.Task;
				}
			}
			finally
			{
				_installer.Changed -= handler;
			}
		}
	}
}
