using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;

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
		builder.Services.AddTransient<TypePage>();
		builder.Services.AddTransient<VoicePickerPage>();

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
}
