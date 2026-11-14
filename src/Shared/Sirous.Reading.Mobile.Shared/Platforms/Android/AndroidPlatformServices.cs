using Sirous.Reading.Application.Reading;
using Sirous.Reading.Application.Usage;

namespace Sirous.Reading.Mobile.Shared;

/// <summary>Android's <see cref="IPlatformServices"/>: Supertonic via sherpa-onnx (an app built with the speech engines),
/// <c>TextToSpeech</c> as the fallback, and the one reader when it is built without them (Dino Reader, Slice 305).
/// Scan and OCR support follows the same switches (<c>NO_SPEECH_ENGINES</c>, <c>NO_MLKIT</c>).</summary>
public sealed class AndroidPlatformServices : IPlatformServices
{
#if !NO_SPEECH_ENGINES
    private readonly IVoiceModelService _model; // only the Supertonic reader uses it
#endif
    private AndroidSpeechReader? _systemReader;

    private readonly IFallbackSink? _fallbacks;

    // The sink is optional: DI passes null for an app that registered none (Slice 273 part B).
    public AndroidPlatformServices(IVoiceModelService model, IFallbackSink? fallbacks = null)
    {
#if !NO_SPEECH_ENGINES
        _model = model;
#endif
        _fallbacks = fallbacks;
    }

#if NO_SPEECH_ENGINES
    // Slice 305: no sherpa-onnx in this app, so no neural voice and the one reader is the phone's own.
    public bool SupportsNeuralVoice => false;

    public ISpeechReader CreateNeuralReader() => SystemReader();
#else
    public bool SupportsNeuralVoice => true;

    public ISpeechReader CreateNeuralReader() => new AndroidSupertonicSpeechReader(_model, fallbacks: _fallbacks);
#endif

#if NO_SPEECH_ENGINES || NO_MLKIT
    // Slice 305: the scanner and the OCR are registered only with both onnxruntime and ML Kit (MobileSharedServices.Android.cs).
    public bool SupportsTextScan => false;
#else
    public bool SupportsTextScan => true;
#endif

    public ISpeechReader CreateFallbackReader() => SystemReader();

    public Task<IReadOnlyCollection<string>?> PhoneVoiceLanguagesAsync() => SystemReader().AvailableLanguagesAsync();

    public void InvalidatePhoneVoices() => _systemReader?.ForgetLanguages();

    // One engine for the reader and the language query, so the warmed engine is never lost.
    private AndroidSpeechReader SystemReader() =>
        _systemReader ??= new AndroidSpeechReader(global::Android.App.Application.Context, _fallbacks);
}
