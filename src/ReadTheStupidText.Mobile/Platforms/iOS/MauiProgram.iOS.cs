using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Translation;

namespace ReadTheStupidText.Mobile;

public static partial class MauiProgram
{
    // The services only this app needs on iOS: the OCR pack installer and the language detector.
    // The scanner, OCR engines, PDF renderer and neural reader factory come from Mobile.Shared
    // (Slice 67). No ITranslator is registered: Apple's Translation framework is Swift-only and
    // ML Kit has no .NET iOS binding we can verify here, so the translate card stays hidden on iOS
    // (see the Slice 55 decisions).
    private static partial void RegisterAppPlatformServices(IServiceCollection services)
    {
        services.AddSingleton<OcrPackInstaller>();
        services.AddSingleton<ILanguageDetector, NlLanguageDetector>();
    }
}
