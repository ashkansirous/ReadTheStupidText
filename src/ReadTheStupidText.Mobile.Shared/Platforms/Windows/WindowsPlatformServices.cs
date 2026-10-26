using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Infrastructure.Reading;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// Windows's <see cref="IPlatformServices"/> (Slice 67): the sherpa-onnx Supertonic reader that
/// Read The Stupid Text uses (<see cref="WindowsNeuralReader"/>), with the WinRT
/// <c>SpeechSynthesis</c> reader as the first-run and fallback voice. Text can be read from a
/// picture (Windows.Media.Ocr), but there is no document scanner.
/// </summary>
public sealed class WindowsPlatformServices : IPlatformServices
{
	private readonly IVoiceModelService _model;
	private readonly ISystemLog _log;
	private readonly AudioChunkPaths _audio;

	public WindowsPlatformServices(IVoiceModelService model, ISystemLog log, AudioChunkPaths audio)
	{
		_model = model;
		_log = log;
		_audio = audio;
	}

	public bool SupportsNeuralVoice => true;

	public bool SupportsTextScan => true;

	public ISpeechReader CreateNeuralReader() => new WindowsNeuralReader(new SupertonicSpeechReader(_model, _log, _audio));

	public ISpeechReader CreateFallbackReader() => new SpeechReader();

	// The installed WinRT voices; a failure to list them is an unknown phone, which blocks nothing.
	public Task<IReadOnlyCollection<string>?> PhoneVoiceLanguagesAsync()
	{
		try
		{
			string[] languages = global::Windows.Media.SpeechSynthesis.SpeechSynthesizer.AllVoices
				.Select(v => v.Language)
				.ToArray();
			return Task.FromResult<IReadOnlyCollection<string>?>(languages);
		}
		catch (Exception)
		{
			return Task.FromResult<IReadOnlyCollection<string>?>(null);
		}
	}
}
