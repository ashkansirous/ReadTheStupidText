using Microsoft.Extensions.DependencyInjection;
using Sirous.Reading.Application.Images;
using Sirous.Reading.Application.Logging;
using Sirous.Reading.Application.Reading;
using Sirous.Reading.Application.Settings;
using Sirous.Reading.Infrastructure.Logging;
using Sirous.Reading.Infrastructure.Reading;

namespace Sirous.Reading.Mobile.Shared;

public static partial class MobileSharedServices
{
	private static partial void RegisterPlatformServices(IServiceCollection services)
	{
		services.AddSingleton<IPlatformServices, WindowsPlatformServices>();
		RegisterDiagnostics(services);
		RegisterScanAndOcr(services);
	}

	// The engine's diagnostics and its audio folders, as the Windows app registers them (Infrastructure).
	private static void RegisterDiagnostics(IServiceCollection services)
	{
		services.AddSingleton<LogPaths>();
		services.AddSingleton<ILogFolder>(sp => sp.GetRequiredService<LogPaths>());
		services.AddSingleton<ISystemLog, SerilogSystemLog>();
		services.AddSingleton<AudioChunkPaths>();
	}

	// Slice 67: pictures are read with Windows.Media.Ocr, and the pieces Dino Reader uses to keep page
	// pictures. No IDocumentScanner is registered, so the Scan tile is inert on Windows.
	private static void RegisterScanAndOcr(IServiceCollection services)
	{
		services.AddSingleton<WindowsOcrTextExtractor>();
		services.AddSingleton<IImageTextExtractor>(sp => sp.GetRequiredService<WindowsOcrTextExtractor>());
		services.AddSingleton<IOcrLineExtractor>(sp => sp.GetRequiredService<WindowsOcrTextExtractor>());
		services.AddSingleton<IPdfPageRenderer, WindowsPdfPageRenderer>();
		services.AddSingleton<IPageImageKeeper, WindowsPageImageKeeper>();
		services.AddSingleton(sp => new OcrPositionedTextExtractor(
			sp.GetRequiredService<IOcrLineExtractor>(),
			() => sp.GetRequiredService<ISettingsStore>().Language ?? AppLanguages.Code(AppLanguage.English)));
	}
}
