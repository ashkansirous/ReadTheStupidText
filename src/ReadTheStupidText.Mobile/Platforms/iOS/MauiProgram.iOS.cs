using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Translation;

namespace ReadTheStupidText.Mobile;

public static partial class MauiProgram
{
    // The services only this app needs on iOS: the OCR pack installer and the language detector.
    // The scanner, OCR engines, PDF renderer, neural reader factory and the ML Kit translator come from
    // Mobile.Shared (Slices 67 and 160), so the translate card shows on iOS as it does on Android.
    private static partial void RegisterAppPlatformServices(IServiceCollection services)
    {
        services.AddSingleton<OcrPackInstaller>();
        services.AddSingleton<ILanguageDetector, NlLanguageDetector>();
    }
}
