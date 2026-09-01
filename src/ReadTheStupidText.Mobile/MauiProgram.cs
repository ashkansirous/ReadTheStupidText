using Microsoft.Extensions.Logging;
using ReadTheStupidText.Application.Reading;

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

		builder.Services.AddSingleton<ISpeechReader>(CreateSpeechReader);
		builder.Services.AddTransient<TypePage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

	private static ISpeechReader CreateSpeechReader(IServiceProvider services)
	{
#if ANDROID
		return new AndroidSpeechReader(global::Android.App.Application.Context);
#else
		throw new PlatformNotSupportedException("Only Android is implemented so far (Decision 37).");
#endif
	}
}
