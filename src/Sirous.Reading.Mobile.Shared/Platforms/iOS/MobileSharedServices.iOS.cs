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
		services.AddSingleton<IPlatformServices, IOSPlatformServices>();
		PackDownloadHost.BodyFetcher = IosBackgroundDownloader.Instance; // Slice 183: pack bodies download in a background URLSession
		RegisterScanAndOcr(services);
#if !NO_SPEECH_ENGINES // Slice 305: Dino Reader links no sherpa-onnx framework, so it registers no neural reader (the phone's own voice reads)
		RegisterNeuralReader(services);
#endif
		// Slice 160: ML Kit translation for both apps, the twin of the Android registration (a model is downloaded on first use).
#if !IOS_SIMULATOR_NO_MLKIT && !NO_MLKIT // the simulator build has no ML Kit (no simulator slice), so no translator and the card hides; Slice 305: nor does an app that switched ML Kit off
		services.AddSingleton<ITranslator, MlKitTranslator>();
#endif
	}

	// Slice 55 and 67: Apple Vision text recognition and the pieces Dino Reader uses to keep the page pictures
	// are always registered; PaddleOCR from the language's OCR pack and the VisionKit scanner only when the
	// app links the speech engines (!NO_SPEECH_ENGINES). The iOS counterpart of the Android registration.
	private static void RegisterScanAndOcr(IServiceCollection services)
	{
		services.AddSingleton<VisionImageTextExtractor>();
		services.AddSingleton<IBuiltInTextRecognizer>(sp => sp.GetRequiredService<VisionImageTextExtractor>());
		services.AddSingleton<IPixelImageLoader, IosPixelImageLoader>();
		services.TryAddSingleton<ISystemLog, IosSystemLog>();
		services.AddSingleton<IPdfPageRenderer, IosPdfPageRenderer>();
		services.AddSingleton<IPageImageKeeper, IosPageImageKeeper>();
#if !NO_SPEECH_ENGINES // Slice 305: PaddleOCR is onnxruntime, which an app without the engines does not link; it scans nothing (Vision stays compiled, unregistered; the VisionKit scanner is not compiled for it, Slice 349)
		services.AddSingleton<LanguageImageTextExtractor>();
		services.AddSingleton<IImageTextExtractor>(sp => sp.GetRequiredService<LanguageImageTextExtractor>());
		services.AddSingleton<IOcrLineExtractor>(sp => sp.GetRequiredService<LanguageImageTextExtractor>());
		services.AddSingleton<IDocumentScanner, VisionKitDocumentScanner>();
		services.AddSingleton(sp => new OcrPositionedTextExtractor(
			sp.GetRequiredService<IOcrLineExtractor>(),
			() => sp.GetRequiredService<ISettingsStore>().Language ?? AppLanguages.Code(AppLanguage.English)));
#endif
	}

	// The neural engine's native framework (SherpaOnnxC.xcframework) is linked by each app
	// project, so an app that does not link it must not read with this reader.
#if !NO_SPEECH_ENGINES
	private static void RegisterNeuralReader(IServiceCollection services)
	{
		services.AddSingleton<IosNeuralReaderFactory>(provider =>
		{
			var model = provider.GetRequiredService<IVoiceModelService>();
			var fallbacks = provider.GetService<Sirous.Reading.Application.Usage.IFallbackSink>(); // Slice 273 part B
			return () => new IosSupertonicSpeechReader(model, fallbacks: fallbacks);
		});
	}
#endif
}
