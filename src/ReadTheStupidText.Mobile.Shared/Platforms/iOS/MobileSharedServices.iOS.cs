using Microsoft.Extensions.DependencyInjection;

namespace ReadTheStupidText.Mobile;

public static partial class MobileSharedServices
{
	private static partial void RegisterPlatformServices(IServiceCollection services)
	{
		services.AddSingleton<IPlatformServices, IOSPlatformServices>();
	}
}
