using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DinoReader.Core;
using ReadTheStupidText.Application.Documents;
using ReadTheStupidText.Documents;
using ReadTheStupidText.Mobile;

namespace DinoReader;

public static class MauiProgram
{
	// Stories live under AppDataDirectory/stories/<id>/ and never leave the device.
	private const string StoriesFolder = "stories";

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
		builder.Services.AddSingleton<StoryTranslator>();
		builder.Services.AddSingleton<SpokenLanguage>();
		builder.Services.AddSingleton<StoryVoicing>();
		builder.Services.AddSingleton<IStoryLibrary>(_ => new FileStoryLibrary(Path.Combine(FileSystem.AppDataDirectory, StoriesFolder)));
		builder.Services.AddSingleton<GrownUpGate>();
		builder.Services.AddSingleton<ExternalActivityGuard>();
		builder.Services.AddSingleton<StoryActions>();
		AddDocumentExtractors(builder.Services);
		AddPages(builder.Services);

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

	private static void AddDocumentExtractors(IServiceCollection services)
	{
		services.AddSingleton<PlainTextExtractor>();
		services.AddSingleton<PdfTextExtractor>();
		services.AddSingleton<DocxTextExtractor>();
		services.AddSingleton<IDocumentTextExtractor, CompositeDocumentTextExtractor>();
	}

	// Pages are transient: each is opened with an explicit Load(story), never a one-shot
	// Shell hand-off to a cached page.
	private static void AddPages(IServiceCollection services)
	{
		services.AddTransient<GalleryPage>();
		services.AddTransient<ManagePage>();
		services.AddTransient<AddStoryPage>();
		services.AddTransient<EditStoryPage>();
		services.AddTransient<CastPage>();
		services.AddTransient<ReadingPage>();
		services.AddTransient<ReadingSession>();
	}
}
