using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile;

public static partial class MauiProgram
{
    // The services only this app needs on iOS (Slice 49): Apple Vision OCR (a stand-in until
    // Slice 55), the VisionKit scanner, and the factory Mobile.Shared uses to build the neural
    // reader (the sherpa-onnx framework is linked by this project).
    private static partial void RegisterAppPlatformServices(IServiceCollection services)
    {
        services.AddSingleton<IImageTextExtractor, VisionImageTextExtractor>();
        services.AddSingleton<IDocumentScanner, VisionKitDocumentScanner>();
        services.AddSingleton<IosNeuralReaderFactory>(provider =>
        {
            var model = provider.GetRequiredService<IVoiceModelService>();
            return () => new IosSupertonicSpeechReader(model);
        });
    }
}
