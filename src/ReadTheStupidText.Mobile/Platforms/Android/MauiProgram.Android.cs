using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Translation;

namespace ReadTheStupidText.Mobile;

public static partial class MauiProgram
{
    private static partial void RegisterAppPlatformServices(IServiceCollection services)
    {
        services.AddSingleton<IImageTextExtractor>(_ => new MlKitImageTextExtractor(global::Android.App.Application.Context));
        services.AddSingleton<IDocumentScanner, AndroidDocumentScanner>();
        services.AddSingleton<ILanguageDetector, MlKitLanguageDetector>();
        services.AddSingleton<ITranslator, MlKitTranslator>();
    }
}
