using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DinoReader.Core;
using ReadTheStupidText.Application.Documents;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Documents;
using ReadTheStupidText.Mobile.Shared;
#if DEBUG
using ReadTheStupidText.Mobile.Shared.Testing;
#endif

namespace DinoReader;

public static class MauiProgram
{
	// Dino asks before a translation pack downloads (Slice 159, Decision 164): the engine is created with auto-download off, whoever resolves it first.
	private static void AskBeforePackDownloads(IServiceCollection services)
	{
		ServiceDescriptor? original = services.LastOrDefault(d => d.ServiceType == typeof(ReadTheStupidText.Application.Translation.ITranslator));
		if (original?.ImplementationType is not { } engineType)
		{
			return;
		}

		services.Remove(original);
		services.AddSingleton<ReadTheStupidText.Application.Translation.ITranslator>(sp =>
		{
			var engine = (ReadTheStupidText.Application.Translation.ITranslator)ActivatorUtilities.CreateInstance(sp, engineType);
			if (engine is ITranslationPacks packs)
			{
				packs.AutoDownload = false;
			}

			return engine;
		});
	}

	// Stories live under AppDataDirectory/stories/<id>/ and never leave the device.
	private const string StoriesFolder = "stories";

	// Rendered PDF pages wait here (cache) until a story keeps them.
	private const string PdfPagesFolder = "pdf-pages";

#if DEBUG
	// Slice 162: the iOS URL scheme of the Debug-only scripted-flow hooks (Android listens for a broadcast instead).
	private const string TestHookScheme = "dino-test";
#endif

	public static MauiApp CreateMauiApp()
	{
		// Before anything reads the culture (services, resources, the first page), including the UI thread's own.
		UiCulture.Apply(UiCulture.Resolve(LanguageChoice.Preferred()));
		TextFlowHandlers.Register();
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
		AskBeforePackDownloads(builder.Services);
		builder.Services.AddSingleton<SpeechStartup>();
		builder.Services.AddSingleton<StoryTranslator>();
		builder.Services.AddSingleton<SpokenLanguage>();
		builder.Services.AddSingleton<NarratorChoice>();
		// Registered here only (Slice 114): with it the voice model service also looks at the installed storyteller packs, so Dino's English books can read with Kokoro.
		builder.Services.AddSingleton<StorytellerVoices>();
		builder.Services.AddSingleton<StoryVoicing>();
		// The storyteller pack downloads by itself and re-casts every book when it lands (Slice 115).
		builder.Services.AddSingleton<StorytellerDefault>();
		builder.Services.AddSingleton<IStoryLibrary>(_ => new FileStoryLibrary(Path.Combine(FileSystem.AppDataDirectory, StoriesFolder)));
		builder.Services.AddSingleton<GrownUpGate>();
		builder.Services.AddSingleton<ExternalActivityGuard>();
		builder.Services.AddSingleton<StoryActions>();
		builder.Services.AddTransient<IMicRecorder, MicRecorder>();
		builder.Services.AddTransient<IRecordingPlayer, RecordingPlayer>();
		builder.Services.AddSingleton<StarterStories>();
		builder.Services.AddSingleton<StagedBookUpdates>();
		builder.Services.AddSingleton<StoreClient>();
		builder.Services.AddSingleton<AppConfigClient>();
		builder.Services.AddSingleton<UsageCounter>();
		builder.Services.AddSingleton<StoryLanguagePacks>();
		AddDocumentExtractors(builder.Services);
		AddPages(builder.Services);

#if DEBUG
		builder.Logging.AddDebug();
		builder.AddTestHooks(TestHookScheme);
#endif

		MauiApp app = builder.Build();
		// Stories still carry their language's narrator on disk, for older builds that require the field (Slice 109).
		StoryJson.LegacyNarratorOf = language => app.Services.GetRequiredService<StoryVoicing>().LegacyNarratorOf(language);
		// Counts left from an earlier session go out now (Slice 133); the counter does nothing when counting is off.
		_ = app.Services.GetRequiredService<UsageCounter>().FlushAsync();
#if DEBUG
		TestHooks.Install(app.Services, TestHookScheme, code => (Microsoft.Maui.Controls.Application.Current as DinoApp)?.CompleteFirstRun(code) ?? false);
#endif
		return app;
	}

	private static void AddDocumentExtractors(IServiceCollection services)
	{
		services.AddSingleton<PlainTextExtractor>();
		services.AddSingleton<PdfTextExtractor>();
		services.AddSingleton<DocxTextExtractor>();
		services.AddSingleton<IDocumentTextExtractor, CompositeDocumentTextExtractor>();
		services.AddSingleton(sp => new RenderedPdfTextExtractor(new PdfPositionedTextExtractor(),
			sp.GetRequiredService<IPdfPageRenderer>(), sp.GetRequiredService<OcrPositionedTextExtractor>(),
			Path.Combine(FileSystem.CacheDirectory, PdfPagesFolder)));
		services.AddSingleton(sp => new StoryImport(sp.GetRequiredService<IPageImageKeeper>()));
	}

	// Pages are transient: each is opened with an explicit Load(story), never a one-shot
	// Shell hand-off to a cached page.
	private static void AddPages(IServiceCollection services)
	{
		services.AddTransient<GalleryPage>();
		services.AddTransient<StorePage>();
		services.AddTransient<UpdatePage>();
		services.AddTransient<BackupPage>();
		services.AddTransient<LanguagePage>();
		services.AddTransient<NarratorPage>();
		services.AddTransient<NarratorLanguagesPage>();
		services.AddTransient<SettingsPage>();
		services.AddTransient<StoryLanguagesPage>();
		services.AddTransient<ReadingSpeedPage>();
		services.AddTransient<AnimalSoundsPage>();
		services.AddTransient<UsageCountsPage>();
		services.AddTransient<AboutPage>();
		services.AddTransient<FirstRunPage>();
		services.AddTransient<IntroWizardPage>();
		services.AddSingleton<GrownUpMode>();
		services.AddTransient<StoryWizardPage>();
		services.AddTransient<CastPage>();
		services.AddTransient<RecordPage>();
		services.AddTransient<ReadingPage>();
		services.AddTransient<ReadingSession>();
	}
}
