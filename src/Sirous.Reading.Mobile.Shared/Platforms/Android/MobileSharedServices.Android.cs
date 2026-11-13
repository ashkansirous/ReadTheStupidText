using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sirous.Reading.Application.Images;
using Sirous.Reading.Application.Logging;
using Sirous.Reading.Application.Reading;
using Sirous.Reading.Application.Settings;
using Sirous.Reading.Application.Translation;

namespace Sirous.Reading.Mobile.Shared;

public static partial class MobileSharedServices
{
	private static partial void RegisterPlatformServices(IServiceCollection services)
	{
		services.AddSingleton<IPlatformServices, AndroidPlatformServices>();
		PackDownloadHost.KeepAlive = new AndroidPackKeepAlive(); // Slice 183: big pack downloads keep going behind the lock screen
		PackDownloadHost.CancelAllActive = AndroidPackKeepAlive.CancelAll; // Slice 186: the Debug test hook fires the notification's Cancel
		RegisterScanAndOcr(services);
#if !NO_MLKIT
		// Slice 95: ML Kit translation for both apps (a model is downloaded on first use; Windows and iOS register no engine).
		// Slice 305: not in an app that switched ML Kit off (Dino Reader).
		services.AddSingleton<ITranslator, MlKitTranslator>();
#endif
	}

	// Slice 52 and 63: PaddleOCR from the language's OCR pack with ML Kit Latin as the fallback,
	// the document scanner, and the pieces Dino Reader uses to keep the page pictures.
	private static void RegisterScanAndOcr(IServiceCollection services)
	{
		services.AddSingleton<IPixelImageLoader>(sp => new AndroidPixelImageLoader(global::Android.App.Application.Context, sp.GetService<Sirous.Reading.Application.Usage.IFallbackSink>()));
		services.TryAddSingleton<ISystemLog, AndroidSystemLog>();
		services.AddSingleton<IPdfPageRenderer, AndroidPdfPageRenderer>();
		services.AddSingleton<IPageImageKeeper>(_ => new AndroidPageImageKeeper(global::Android.App.Application.Context));
#if !NO_SPEECH_ENGINES && !NO_MLKIT // Slice 305: the OCR and the scanner need both onnxruntime (PaddleOCR) and ML Kit (the fallback and the scanner); Dino Reader scans nothing
		services.AddSingleton(_ => new MlKitImageTextExtractor(global::Android.App.Application.Context));
		services.AddSingleton<IBuiltInTextRecognizer>(sp => sp.GetRequiredService<MlKitImageTextExtractor>());
		services.AddSingleton<LanguageImageTextExtractor>();
		services.AddSingleton<IImageTextExtractor>(sp => sp.GetRequiredService<LanguageImageTextExtractor>());
		services.AddSingleton<IOcrLineExtractor>(sp => sp.GetRequiredService<LanguageImageTextExtractor>());
		services.AddSingleton<IDocumentScanner, AndroidDocumentScanner>();
		services.AddSingleton(sp => new OcrPositionedTextExtractor(
			sp.GetRequiredService<IOcrLineExtractor>(),
			() => sp.GetRequiredService<ISettingsStore>().Language ?? AppLanguages.Code(AppLanguage.English)));
#endif
	}
}
