using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;

namespace ReadTheStupidText.Mobile;

public partial class App : Microsoft.Maui.Controls.Application
{
	private readonly IServiceProvider _services;
	private readonly ISpeechReader _reader;
	private readonly ISettingsStore _settings;
	private readonly VoicePackInstaller _installer;

	public App(IServiceProvider services, ISpeechReader reader, ISettingsStore settings, VoicePackInstaller installer)
	{
		InitializeComponent();

		_services = services;
		_reader = reader;
		_settings = settings;
		_installer = installer;
		_ = InitializeSpeechAsync();
	}

	// First run (no language stored yet) opens the language page; afterwards the Reader.
	protected override Window CreateWindow(IActivationState? activationState)
	{
		Page root = _settings.Language is null
			? _services.GetRequiredService<LanguagePage>()
			: new AppShell();
		return new Window(root);
	}

	/// <summary>Leaves the first-run language page for the Reader.</summary>
	public void ShowMain()
	{
		if (Windows.Count > 0)
		{
			Windows[0].Page = new AppShell();
		}
	}

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

		await _installer.StartAsync();
	}
}
