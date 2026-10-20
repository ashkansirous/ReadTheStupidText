using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReadTheStupidText.Application.Documents;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Documents;

namespace ReadTheStupidText.Mobile;

public static partial class MauiProgram
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

		RegisterPlatformServices(builder.Services);
		builder.Services.AddSingleton<IVoicePackService, MobileVoicePackService>();
		builder.Services.AddSingleton<IVoiceModelService, MobileVoiceModelService>();
		builder.Services.AddSingleton<VoicePackInstaller>();
		builder.Services.AddSingleton<ISettingsStore, MobilePreferencesSettingsStore>();
		builder.Services.AddSingleton<ISpeechReader>(CreateSpeechReader);
		builder.Services.AddSingleton<IImageTextExtractor>(CreateImageTextExtractor);
		builder.Services.AddSingleton<PlainTextExtractor>();
		builder.Services.AddSingleton<PdfTextExtractor>();
		builder.Services.AddSingleton<DocxTextExtractor>();
		builder.Services.AddSingleton<IDocumentTextExtractor, CompositeDocumentTextExtractor>();
		builder.Services.AddTransient<LanguagePage>();
		builder.Services.AddTransient<ReaderPage>();
		builder.Services.AddTransient<VoicePickerPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

	// Each platform's Platforms/<OS>/ folder implements this and registers its
	// IPlatformServices. The method is required (it has an accessibility modifier), so a target
	// framework listed in the csproj without a Platforms/<OS> implementation fails to compile
	// instead of failing at runtime. Platforms/Android and Platforms/iOS each implement it.
	private static partial void RegisterPlatformServices(IServiceCollection services);

	private static ISpeechReader CreateSpeechReader(IServiceProvider services)
	{
		var platform = services.GetRequiredService<IPlatformServices>();
		return new CompositeSpeechReader(
			platform.CreateNeuralReader(),
			platform.CreateFallbackReader(),
			services.GetRequiredService<IVoiceModelService>());
	}

	private static IImageTextExtractor CreateImageTextExtractor(IServiceProvider services) =>
		services.GetRequiredService<IPlatformServices>().CreateImageTextExtractor();
}
