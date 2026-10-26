using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReadTheStupidText.Application.Documents;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Application.Usage;
using ReadTheStupidText.Documents;
using ReadTheStupidText.Mobile.Shared;
using ReadTheStupidText.Mobile.Shared.Usage;

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

		builder.AddMobileSharedServices();
		RegisterAppPlatformServices(builder.Services);
		builder.Services.AddSingleton<IHeadStart>(sp => sp.GetRequiredService<ISpeechReader>() as IHeadStart ?? new NoHeadStart());
		builder.Services.AddSingleton<PlainTextExtractor>();
		builder.Services.AddSingleton<PdfTextExtractor>();
		builder.Services.AddSingleton<DocxTextExtractor>();
		builder.Services.AddSingleton<IDocumentTextExtractor, CompositeDocumentTextExtractor>();
		builder.Services.AddSingleton<EditorDraft>();
		builder.Services.AddSingleton<ReviewHandOff>();
		builder.Services.AddSingleton<RtstUsage>();
		builder.Services.AddTransient<LanguagePage>();
		builder.Services.AddTransient<ReaderPage>();
		builder.Services.AddTransient<VoicePickerPage>();
		builder.Services.AddTransient<SettingsPage>();
		builder.Services.AddTransient<CapturePage>();
		builder.Services.AddTransient<ReviewPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		MauiApp app = builder.Build();
		StartUsageCounts(app.Services);
		return app;
	}

	// Anonymous counts (Slice 137): the config fetch, then one app_opened per launch and whatever an earlier session left queued.
	// RtstUsage does nothing while the Settings switch or the remote config says off.
	private static void StartUsageCounts(IServiceProvider services)
	{
		var usage = services.GetRequiredService<RtstUsage>();
		var settings = services.GetRequiredService<ISettingsStore>();
		string language = AppLanguages.Code(AppLanguageSetting.Current(settings));
		_ = usage.StartAsync(context => UsageEvents.AppOpened(context, language, UsageEnvironment.OsMajor));
	}

	// Each platform's Platforms/<OS>/ folder implements this for the services only this app
	// needs (the shared engines are registered by MobileSharedServices). The method is required,
	// so a target framework without a Platforms/<OS> implementation fails to compile.
	private static partial void RegisterAppPlatformServices(IServiceCollection services);
}
