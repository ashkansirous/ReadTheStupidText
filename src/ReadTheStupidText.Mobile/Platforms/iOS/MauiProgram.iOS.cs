using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Mobile;

public static partial class MauiProgram
{
    // iOS has no document scanner (Slice 45 is Android) and its OCR is a stub until Slice 49.
    private static partial void RegisterAppPlatformServices(IServiceCollection services)
    {
        services.AddSingleton<IImageTextExtractor, UnavailableImageTextExtractor>();
    }
}
