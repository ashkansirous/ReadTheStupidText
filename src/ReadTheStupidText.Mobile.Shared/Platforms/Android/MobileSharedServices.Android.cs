using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;

namespace ReadTheStupidText.Mobile;

public static partial class MobileSharedServices
{
	private static partial void RegisterPlatformServices(IServiceCollection services)
	{
		services.AddSingleton<IPlatformServices, AndroidPlatformServices>();
		RegisterScanAndOcr(services);
	}

	// Slice 52 and 63: PaddleOCR from the language's OCR pack with ML Kit Latin as the fallback,
	// the document scanner, and the pieces Dino Reader uses to keep the page pictures.
	private static void RegisterScanAndOcr(IServiceCollection services)
	{
		services.AddSingleton(_ => new MlKitImageTextExtractor(global::Android.App.Application.Context));
		services.AddSingleton<IPixelImageLoader>(_ => new AndroidPixelImageLoader(global::Android.App.Application.Context));
		services.AddSingleton<LanguageImageTextExtractor>();
		services.AddSingleton<IImageTextExtractor>(sp => sp.GetRequiredService<LanguageImageTextExtractor>());
		services.AddSingleton<IOcrLineExtractor>(sp => sp.GetRequiredService<LanguageImageTextExtractor>());
		services.AddSingleton<IDocumentScanner, AndroidDocumentScanner>();
		services.AddSingleton<IPdfPageRenderer, AndroidPdfPageRenderer>();
		services.AddSingleton<IPageImageKeeper>(_ => new AndroidPageImageKeeper(global::Android.App.Application.Context));
		services.AddSingleton(sp => new OcrPositionedTextExtractor(
			sp.GetRequiredService<IOcrLineExtractor>(),
			() => sp.GetRequiredService<ISettingsStore>().Language ?? AppLanguages.Code(AppLanguage.English)));
	}
}
