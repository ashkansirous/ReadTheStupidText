using Microsoft.Extensions.DependencyInjection;

namespace ReadTheStupidText.Mobile;

public static partial class MauiProgram
{
    private static partial void RegisterPlatformServices(IServiceCollection services)
    {
        services.AddSingleton<IPlatformServices, AndroidPlatformServices>();
        services.AddSingleton<IDocumentScanner, AndroidDocumentScanner>();
    }
}
