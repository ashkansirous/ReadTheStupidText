using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.AppConfig;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Mobile.Shared;

namespace ReadTheStupidText.Mobile;

public partial class App : Microsoft.Maui.Controls.Application
{
	private readonly IServiceProvider _services;
	private readonly ISpeechReader _reader;
	private readonly ISettingsStore _settings;
	private readonly VoicePackInstaller _installer;
	private readonly OcrPackInstaller? _ocrPacks;

	public App(IServiceProvider services, ISpeechReader reader, ISettingsStore settings, VoicePackInstaller installer, OcrPackInstaller? ocrPacks = null)
	{
		// Before InitializeComponent: pages read their strings while they are inflated.
		UiCulture.Apply(UiCulture.Resolve(settings.Language, CultureInfo.CurrentUICulture));
		InitializeComponent();

		_services = services;
		_reader = reader;
		_settings = settings;
		_installer = installer;
		_ocrPacks = ocrPacks;
		_installer.Changed += OnInstallerChanged;
#if DEBUG
		_installer.DebugMarker = Shared.Testing.TestHookLog.Write; // Slice 204: the held marker the scripted check asserts on
		UpdateTestHook.Install(); // Slice 217: chained after the commands registered before it
#endif
		AppUpdate.Shared.Changed += (_, _) => ShowUpdateIfRequired(); // raised on the main thread
		_ = AppUpdate.Shared.RefreshAsync();
		_ = InitializeSpeechAsync();
		// OCR pack (Slice 52): checked at every start, on its own; it waits for any voice download itself.
		if (!UpdateRequired)
		{
			_ = _ocrPacks?.EnsureForCurrentLanguageAsync();
		}
	}

	// True while the "Update needed" page must hold the app: no pack download or read starts behind it (Slice 217).
	private static bool UpdateRequired => AppUpdate.Shared.State == UpdateState.Required;

	// First run (no language stored yet) opens the language page; afterwards the Reader.
	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(RootPage());
		window.Resumed += (_, _) => InvalidatePhoneVoices();
		window.Resumed += (_, _) => _ = AppUpdate.Shared.RefreshAsync();
		var resume = _services.GetRequiredService<ReadTheStupidText.Mobile.Shared.Network.PackResumeWatcher>(); // Slice 182: created and started with the window, not when a page first asks for it
		resume.Start();
		window.Resumed += (_, _) => resume.NotifyForeground();
		return window;
	}

	// A build below the config's minVersion opens on the "Update needed" page and nothing else (Slice 217); otherwise the first-run page or the Reader.
	private Page? _pageBeforeUpdate;

	private Page RootPage()
	{
		if (AppUpdate.Shared.State == UpdateState.Required)
		{
			return new UpdatePage();
		}

		return _settings.Language is null ? _services.GetRequiredService<LanguagePage>() : new AppShell();
	}

	// A fetched config can raise minVersion while the app is open: the update page then replaces everything, and a read in progress stops.
	// When the requirement goes away again (only a Debug test config can do that) the app comes back as it opens.
	private void ShowUpdateIfRequired()
	{
		if (Windows.FirstOrDefault() is not { } window)
		{
			return;
		}

		bool required = AppUpdate.Shared.State == UpdateState.Required;
		if (required && window.Page is not UpdatePage)
		{
			_reader.Stop();
			_pageBeforeUpdate = window.Page; // kept so a lifted requirement brings this very page back, not a second shell with doubled handlers
			window.Page = new UpdatePage();
		}
		else if (!required && window.Page is UpdatePage)
		{
			window.Page = _pageBeforeUpdate ?? RootPage();
			_pageBeforeUpdate = null;
		}
	}

	// Voice data may have been installed in system settings while the app was away.
	private void InvalidatePhoneVoices() =>
		PlatformVoiceAvailability.Invalidate(_services.GetRequiredService<IPlatformServices>());

	// A finished pack install changes what can read; ask the phone again.
	private void OnInstallerChanged(object? sender, EventArgs e)
	{
		if (_installer.State == PackInstallState.Ready)
		{
			MainThread.BeginInvokeOnMainThread(InvalidatePhoneVoices);
		}
	}

	/// <summary>Rebuilds the root in the current language: leaves the first-run page, or re-applies a language change.</summary>
	public void ShowMain()
	{
		if (Windows.Count == 0)
		{
			return;
		}

		if (Windows[0].Page is UpdatePage)
		{
			_pageBeforeUpdate = new AppShell(); // rebuilt in the new language, so lifting the requirement does not bring back the stale page
			return;
		}

		Windows[0].Page = new AppShell();
	}

#if DEBUG
	/// <summary>Debug test hook (Slice 162): finishes first run for <paramref name="code"/> as the language page's Continue and Start do; false when there is no window to hand over.</summary>
	public bool CompleteFirstRun(string code)
	{
		// Acts only while a first-run page is showing: no window yet means retry, the shell already up means nothing to do.
		if (Windows.FirstOrDefault()?.Page is null or AppShell)
		{
			return false;
		}

		_settings.Language = code;
		UiCulture.Apply(UiCulture.Resolve(code, CultureInfo.CurrentUICulture));
		_ = _installer.StartAsync(PackStartReason.User);
		ShowMain();
		return true;
	}
#endif

	// Applies the persisted speed/voice immediately (cheap, no model needed), then
	// lets the installer resume or finish the voice pack for the chosen language
	// (Slice 50). Until the neural pack is ready the phone voice reads.
	private async Task InitializeSpeechAsync()
	{
		_reader.SetSpeed(_settings.Speed);
		if (_settings.VoiceId is { } voiceId)
		{
			_reader.SetVoice(voiceId);
		}

		if (UpdateRequired)
		{
			return; // the pack download waits until the app is updated (Slice 217)
		}

		await _installer.StartAsync(PackStartReason.Automatic); // Slice 204: a download the user stopped stays stopped
	}
}
