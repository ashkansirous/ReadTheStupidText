using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// iOS's <see cref="IPlatformServices"/> for the first TestFlight build (Slice 48):
/// <c>AVSpeechSynthesizer</c> for the fallback reader and, until Slice 49 links
/// sherpa-onnx for iOS, for the "neural" slot too. The OCR slot is a stub that says so.
/// </summary>
public sealed class IOSPlatformServices : IPlatformServices
{
    private AvSpeechReader? _reader;

    // No Supertonic engine on iOS until Slice 49 links sherpa-onnx, so the voice pack is
    // never downloaded (VoicePackInstaller checks this) and the model never becomes ready.
    public bool SupportsNeuralVoice => false;

    // No Vision-based extractor yet, so the Reader hides Scan.
    public bool SupportsTextScan => false;

    // Never routed to while SupportsNeuralVoice is false (the model is not ready). Both slots
    // share ONE reader so there is a single AVSpeechSynthesizer; CompositeSpeechReader skips
    // the second Dispose of a shared instance.
    public ISpeechReader CreateNeuralReader() => _reader ??= new AvSpeechReader();

    public ISpeechReader CreateFallbackReader() => _reader ??= new AvSpeechReader();

    public IImageTextExtractor CreateImageTextExtractor() => new UnavailableImageTextExtractor();
}
