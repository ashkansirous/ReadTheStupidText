using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile;

/// <summary>Android's <see cref="IPlatformServices"/>: Supertonic via sherpa-onnx,
/// <c>TextToSpeech</c> as the fallback, and ML Kit for OCR.</summary>
public sealed class AndroidPlatformServices : IPlatformServices
{
    private readonly IVoiceModelService _model;

    public AndroidPlatformServices(IVoiceModelService model) => _model = model;

    public ISpeechReader CreateNeuralReader() => new AndroidSupertonicSpeechReader(_model);

    public ISpeechReader CreateFallbackReader() => new AndroidSpeechReader(global::Android.App.Application.Context);

    public IImageTextExtractor CreateImageTextExtractor() =>
        new MlKitImageTextExtractor(global::Android.App.Application.Context);
}
