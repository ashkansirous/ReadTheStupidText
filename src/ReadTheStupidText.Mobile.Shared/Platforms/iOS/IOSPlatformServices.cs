using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// iOS's <see cref="IPlatformServices"/> (Slices 48-49): <c>AVSpeechSynthesizer</c> is the
/// fallback reader; the neural reader comes from the app's <see cref="IosNeuralReaderFactory"/>
/// when it registers one (Mobile.Shared itself does not link sherpa-onnx on iOS).
/// </summary>
public sealed class IOSPlatformServices : IPlatformServices
{
    private readonly IosNeuralReaderFactory? _neuralFactory;
    private readonly bool _hasTextExtractor;
    private AvSpeechReader? _systemReader;

    // Both are optional: DI passes the default (null) for a service the app did not register.
    public IOSPlatformServices(IosNeuralReaderFactory? neuralFactory = null, IImageTextExtractor? ocr = null)
    {
        _neuralFactory = neuralFactory;
        _hasTextExtractor = ocr is not null;
    }

    // Without a factory the voice pack is never downloaded (VoicePackInstaller checks this)
    // and the model never becomes ready.
    public bool SupportsNeuralVoice => _neuralFactory is not null;

    // Only an app that registered an OCR engine scans; the others hide the Scan action.
    public bool SupportsTextScan => _hasTextExtractor;

    // With no neural engine both slots share ONE reader so there is a single
    // AVSpeechSynthesizer; CompositeSpeechReader skips the second Dispose of a shared instance.
    public ISpeechReader CreateNeuralReader() => _neuralFactory?.Invoke() ?? SystemReader();

    public ISpeechReader CreateFallbackReader() => SystemReader();

    public Task<IReadOnlyCollection<string>?> PhoneVoiceLanguagesAsync() =>
        Task.FromResult<IReadOnlyCollection<string>?>(AvSpeechReader.InstalledLanguages());

    private AvSpeechReader SystemReader() => _systemReader ??= new AvSpeechReader();
}
