using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Translation;

namespace ReadTheStupidText.Mobile;

public static partial class MauiProgram
{
    // The services only this app needs on iOS: PaddleOCR from the language's OCR pack with Apple
    // Vision as the fallback (Slice 55), the VisionKit scanner, the language detector, and the
    // factory Mobile.Shared uses to build the neural reader (the sherpa-onnx framework is linked
    // by this project). No ITranslator is registered: Apple's Translation framework is
    // Swift-only and ML Kit has no .NET iOS binding we can verify here, so the translate card
    // stays hidden on iOS (see the Slice 55 decisions).
    private static partial void RegisterAppPlatformServices(IServiceCollection services)
    {
        services.AddSingleton<VisionImageTextExtractor>();
        services.AddSingleton<IPixelImageLoader, IosPixelImageLoader>();
        services.AddSingleton<IosLanguageImageTextExtractor>();
        services.AddSingleton<IImageTextExtractor>(sp => sp.GetRequiredService<IosLanguageImageTextExtractor>());
        services.AddSingleton<IOcrLineExtractor>(sp => sp.GetRequiredService<IosLanguageImageTextExtractor>());
        services.AddSingleton<OcrPackInstaller>();
        services.AddSingleton<IDocumentScanner, VisionKitDocumentScanner>();
        services.AddSingleton<ILanguageDetector, NlLanguageDetector>();
        services.AddSingleton<IosNeuralReaderFactory>(provider =>
        {
            var model = provider.GetRequiredService<IVoiceModelService>();
            return () => new IosSupertonicSpeechReader(model);
        });
    }
}
