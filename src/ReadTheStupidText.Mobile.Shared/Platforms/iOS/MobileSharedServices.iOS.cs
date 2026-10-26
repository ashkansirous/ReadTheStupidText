using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;

namespace ReadTheStupidText.Mobile.Shared;

public static partial class MobileSharedServices
{
	private static partial void RegisterPlatformServices(IServiceCollection services)
	{
		services.AddSingleton<IPlatformServices, IOSPlatformServices>();
		RegisterScanAndOcr(services);
		RegisterNeuralReader(services);
	}

	// Slice 55 and 67: PaddleOCR from the language's OCR pack with Apple Vision as the fallback,
	// the VisionKit scanner, and the pieces Dino Reader uses to keep the page pictures. The iOS
	// counterpart of the Android registration.
	private static void RegisterScanAndOcr(IServiceCollection services)
	{
		services.AddSingleton<VisionImageTextExtractor>();
		services.AddSingleton<IBuiltInTextRecognizer>(sp => sp.GetRequiredService<VisionImageTextExtractor>());
		services.AddSingleton<IPixelImageLoader, IosPixelImageLoader>();
		services.TryAddSingleton<ISystemLog, IosSystemLog>();
		services.AddSingleton<LanguageImageTextExtractor>();
		services.AddSingleton<IImageTextExtractor>(sp => sp.GetRequiredService<LanguageImageTextExtractor>());
		services.AddSingleton<IOcrLineExtractor>(sp => sp.GetRequiredService<LanguageImageTextExtractor>());
		services.AddSingleton<IDocumentScanner, VisionKitDocumentScanner>();
		services.AddSingleton<IPdfPageRenderer, IosPdfPageRenderer>();
		services.AddSingleton<IPageImageKeeper, IosPageImageKeeper>();
		services.AddSingleton(sp => new OcrPositionedTextExtractor(
			sp.GetRequiredService<IOcrLineExtractor>(),
			() => sp.GetRequiredService<ISettingsStore>().Language ?? AppLanguages.Code(AppLanguage.English)));
	}

	// The neural engine's native framework (SherpaOnnxC.xcframework) is linked by each app
	// project, so an app that does not link it must not read with this reader.
	private static void RegisterNeuralReader(IServiceCollection services)
	{
		services.AddSingleton<IosNeuralReaderFactory>(provider =>
		{
			var model = provider.GetRequiredService<IVoiceModelService>();
			return () => new IosSupertonicSpeechReader(model);
		});
	}
}
