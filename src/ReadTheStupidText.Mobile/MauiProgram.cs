using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReadTheStupidText.Application.Documents;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Documents;

namespace ReadTheStupidText.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<IVoiceModelService, MobileVoiceModelService>();
		builder.Services.AddSingleton<ISettingsStore, MobilePreferencesSettingsStore>();
		builder.Services.AddSingleton<ISpeechReader>(CreateSpeechReader);
		builder.Services.AddSingleton<IImageTextExtractor>(CreateImageTextExtractor);
		builder.Services.AddSingleton<PlainTextExtractor>();
		builder.Services.AddSingleton<PdfTextExtractor>();
		builder.Services.AddSingleton<DocxTextExtractor>();
		builder.Services.AddSingleton<IDocumentTextExtractor, CompositeDocumentTextExtractor>();
		builder.Services.AddSingleton<PendingScanResult>();
		builder.Services.AddTransient<ReaderPage>();
		builder.Services.AddTransient<VoicePickerPage>();
		builder.Services.AddTransient<ScanPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

	private static ISpeechReader CreateSpeechReader(IServiceProvider services)
	{
#if ANDROID
		var neural = new AndroidSupertonicSpeechReader(services.GetRequiredService<IVoiceModelService>());
		var fallback = new AndroidSpeechReader(global::Android.App.Application.Context);
		return new CompositeSpeechReader(neural, fallback, services.GetRequiredService<IVoiceModelService>());
#else
		throw new PlatformNotSupportedException("Only Android is implemented so far (Decision 37).");
#endif
	}

	private static IImageTextExtractor CreateImageTextExtractor(IServiceProvider services)
	{
#if ANDROID
		return new MlKitImageTextExtractor(global::Android.App.Application.Context);
#else
		throw new PlatformNotSupportedException("Only Android is implemented so far (Decision 37).");
#endif
	}
}
