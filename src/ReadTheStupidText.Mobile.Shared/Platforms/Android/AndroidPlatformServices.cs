using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>Android's <see cref="IPlatformServices"/>: Supertonic via sherpa-onnx,
/// <c>TextToSpeech</c> as the fallback. OCR lives in the Read The Stupid Text app.</summary>
public sealed class AndroidPlatformServices : IPlatformServices
{
    private readonly IVoiceModelService _model;
    private AndroidSpeechReader? _systemReader;

    public AndroidPlatformServices(IVoiceModelService model) => _model = model;

    public bool SupportsNeuralVoice => true;

    public bool SupportsTextScan => true;

    public ISpeechReader CreateNeuralReader() => new AndroidSupertonicSpeechReader(_model);

    public ISpeechReader CreateFallbackReader() => SystemReader();

    public Task<IReadOnlyCollection<string>?> PhoneVoiceLanguagesAsync() => SystemReader().AvailableLanguagesAsync();

    public void InvalidatePhoneVoices() => _systemReader?.ForgetLanguages();

    // One engine for the reader and the language query, so the warmed engine is never lost.
    private AndroidSpeechReader SystemReader() =>
        _systemReader ??= new AndroidSpeechReader(global::Android.App.Application.Context);
}
