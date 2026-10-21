using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Translation;

namespace ReadTheStupidText.Mobile;

public static partial class MauiProgram
{
    private static partial void RegisterAppPlatformServices(IServiceCollection services)
    {
        // The scanner and the OCR engines are registered by Mobile.Shared (Slice 63).
        services.AddSingleton<OcrPackInstaller>();
        services.AddSingleton<ILanguageDetector, MlKitLanguageDetector>();
        services.AddSingleton<ITranslator, MlKitTranslator>();
    }
}
