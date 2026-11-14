using Microsoft.Extensions.DependencyInjection;
using Sirous.Reading.Application.Reading;
using Sirous.Reading.Application.Settings;
using Sirous.Reading.Application.Usage;

namespace Sirous.Reading.Mobile.Shared;

/// <summary>
/// Registers everything both MAUI apps share (Slice 58): the voice pack/model services, the
/// installer, the settings store and the speech reader. On Android it also registers the
/// scanner and OCR stack (Slice 63): the document scanner, PaddleOCR with the ML Kit fallback
/// (as <c>IImageTextExtractor</c> and <c>IOcrLineExtractor</c>), the PDF page renderer and the
/// page image keeper. Each app's <c>MauiProgram</c> calls this once and adds only its own pages
/// and services.
/// </summary>
public static partial class MobileSharedServices
{
	public static MauiAppBuilder AddMobileSharedServices(this MauiAppBuilder builder)
	{
		RegisterPlatformServices(builder.Services);
		ReportScannerFallbacks(builder.Services);
		builder.Services.AddSingleton<IVoicePackService, MobileVoicePackService>();
		builder.Services.AddSingleton<IVoiceModelService, MobileVoiceModelService>();
		builder.Services.AddSingleton<IPackUpdateNetwork, MauiPackUpdateNetwork>();
		builder.Services.AddSingleton<Network.PackResumeWatcher>();
		builder.Services.AddSingleton<IPackResume>(sp => sp.GetRequiredService<Network.PackResumeWatcher>());
		builder.Services.AddSingleton<VoicePackInstaller>();
		builder.Services.AddSingleton<MobilePreferencesSettingsStore>();
		builder.Services.AddSingleton<ISettingsStore>(sp => sp.GetRequiredService<MobilePreferencesSettingsStore>());
		builder.Services.AddSingleton<ILanguageChangeNotifier>(sp => sp.GetRequiredService<MobilePreferencesSettingsStore>());
		builder.Services.AddSingleton<ISpeechReader>(CreateSpeechReader);
		return builder;
	}

	// Each platform's Platforms/<OS>/ folder implements this and registers its
	// IPlatformServices. The method is required (it has an accessibility modifier), so a target
	// framework listed in the csproj without a Platforms/<OS> implementation fails to compile
	// instead of failing at runtime (Slice 44, Decision 69).
	private static partial void RegisterPlatformServices(IServiceCollection services);

	// Slice 273 part C: the platform's document scanner (Android and iOS register one; Windows none) is wrapped so a scanner that cannot start is counted as a fallback.
	private static void ReportScannerFallbacks(IServiceCollection services) => DocumentScannerRegistration.ReportFallbacks(services);

	private static ISpeechReader CreateSpeechReader(IServiceProvider services)
	{
		var platform = services.GetRequiredService<IPlatformServices>();
#if NO_SPEECH_ENGINES
		// Slice 305: an app without the engines (Dino Reader on Android and iOS) only has the phone's own voice; the Debug test hooks still resolve a reader. The switch is never defined for the Windows head (Mobile.Shared.csproj).
		return platform.CreateFallbackReader();
#else
		return new CompositeSpeechReader(
			platform.CreateNeuralReader(),
			platform.CreateFallbackReader(),
			services.GetRequiredService<IVoiceModelService>(),
			services.GetService<IFallbackSink>()); // Slice 273 part B: the app's sink, or none
#endif
	}
}
