using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;

namespace ReadTheStupidText.Mobile;

public partial class App : Microsoft.Maui.Controls.Application
{
	private readonly ISpeechReader _reader;
	private readonly ISettingsStore _settings;
	private readonly IVoiceModelService _model;

	public App(ISpeechReader reader, ISettingsStore settings, IVoiceModelService model)
	{
		InitializeComponent();

		_reader = reader;
		_settings = settings;
		_model = model;
		_ = InitializeSpeechAsync();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}

	// Applies the persisted speed/voice immediately (cheap, no model needed),
	// then locates the neural model (a one-time ~139 MB copy out of the package
	// on first launch — see MobileVoiceModelService) and warms the engine, so
	// the first real read pays as little cold-start cost as possible.
	private async Task InitializeSpeechAsync()
	{
		_reader.SetSpeed(_settings.Speed);
		if (_settings.VoiceId is { } voiceId)
		{
			_reader.SetVoice(voiceId);
		}

		await _model.InitializeAsync();
		await _reader.WarmUpAsync();
	}
}
