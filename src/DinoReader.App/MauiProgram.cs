using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReadTheStupidText.Mobile;

namespace DinoReader;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<DinoApp>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("Fredoka.ttf", DinoFonts.Ui);
				fonts.AddFont("Andika-Regular.ttf", DinoFonts.Reading);
				fonts.AddFont("Andika-Bold.ttf", DinoFonts.ReadingBold);
				fonts.AddFont("Vazirmatn.ttf", DinoFonts.Persian);
			});

		builder.AddMobileSharedServices();
		builder.Services.AddSingleton<SpeechStartup>();
		builder.Services.AddSingleton<StoryVoicing>();
		builder.Services.AddTransient<MainPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
