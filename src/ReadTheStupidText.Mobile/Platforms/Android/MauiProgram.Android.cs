using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Mobile;

public static partial class MauiProgram
{
    private static partial void RegisterAppPlatformServices(IServiceCollection services)
    {
        // Slice 52: PaddleOCR from the language's OCR pack, with ML Kit Latin as the fallback.
        services.AddSingleton(_ => new MlKitImageTextExtractor(global::Android.App.Application.Context));
        services.AddSingleton<IPixelImageLoader>(_ => new AndroidPixelImageLoader(global::Android.App.Application.Context));
        services.AddSingleton<LanguageImageTextExtractor>();
        services.AddSingleton<IImageTextExtractor>(sp => sp.GetRequiredService<LanguageImageTextExtractor>());
        services.AddSingleton<IOcrLineExtractor>(sp => sp.GetRequiredService<LanguageImageTextExtractor>());
        services.AddSingleton<OcrPackInstaller>();
        services.AddSingleton<IDocumentScanner, AndroidDocumentScanner>();
    }
}
