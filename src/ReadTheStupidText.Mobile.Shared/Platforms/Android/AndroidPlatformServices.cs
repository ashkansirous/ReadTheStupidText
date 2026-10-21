using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile;

/// <summary>Android's <see cref="IPlatformServices"/>: Supertonic via sherpa-onnx,
/// <c>TextToSpeech</c> as the fallback. OCR lives in the Read The Stupid Text app.</summary>
public sealed class AndroidPlatformServices : IPlatformServices
{
    private readonly IVoiceModelService _model;

    public AndroidPlatformServices(IVoiceModelService model) => _model = model;

    public bool SupportsNeuralVoice => true;

    public bool SupportsTextScan => true;

    public ISpeechReader CreateNeuralReader() => new AndroidSupertonicSpeechReader(_model);

    public ISpeechReader CreateFallbackReader() => new AndroidSpeechReader(global::Android.App.Application.Context);
}
