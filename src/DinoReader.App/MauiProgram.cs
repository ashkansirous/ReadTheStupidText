using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DinoReader.Core;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Crashes;
using ReadTheStupidText.Mobile.Shared;
using ReadTheStupidText.Mobile.Shared.Crashes;
#if DEBUG
using ReadTheStupidText.Mobile.Shared.Testing;
#endif

namespace DinoReader;

public static class MauiProgram
{
	// Stories live under AppDataDirectory/stories/<id>/ and never leave the device.
	private const string StoriesFolder = "stories";
	private const string AudioPackFolder = "audio-packs";

#if DEBUG
	// Slice 162: the iOS URL scheme of the Debug-only scripted-flow hooks (Android listens for a broadcast instead).
	private const string TestHookScheme = "dino-test";
#endif

	public static MauiApp CreateMauiApp()
	{
		// Slice 227: first of all, so even a crash during start-up leaves a pending report; it follows the grown-up's usage switch.
		CrashReporter.Start(CrashApp.Dino, UsageCounter.EnabledKey);
		// Before anything reads the culture (services, resources, the first page), including the UI thread's own.
		System.Globalization.CultureInfo preferredCulture = LanguageChoice.Preferred();
		bool deviceLanguageUnsupported = !UiLanguage.IsSupported(preferredCulture.Name); // Slice 273 part C: reported once the sink exists, below
		UiCulture.Apply(UiCulture.Resolve(preferredCulture));
		TextFlowHandlers.Register();
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<DinoApp>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("Fredoka.ttf", DinoFonts.Ui);
				fonts.AddFont("Fredoka-Medium.ttf", DinoFonts.UiMedium);
				fonts.AddFont("Andika-Regular.ttf", DinoFonts.Reading);
				fonts.AddFont("Andika-Bold.ttf", DinoFonts.ReadingBold);
				fonts.AddFont("Vazirmatn.ttf", DinoFonts.Persian);
			});

		// Slice 278 part B: this registers the shared speech reader and voice services too, but nothing in Dino ever resolves them (ReadingSessionNeverAsksForASpeechEngineTests scans the sources for that).
		builder.AddMobileSharedServices();
		builder.Services.AddSingleton<ILanguageChangeNotifier, InertLanguageNotifier>(); // replaces the shared one (last registration wins): a language change must never start a voice pack download in Dino
		builder.Services.AddSingleton<StoryTranslator>();
		builder.Services.AddSingleton<StoryVoicing>();
		StoryJson.MainLanguageOf = AppLanguageForMigration; // Slice 286: books recorded before recordings had a language are filed under the app language when first read
		builder.Services.AddSingleton<IStoryLibrary>(_ => new FileStoryLibrary(Path.Combine(FileSystem.AppDataDirectory, StoriesFolder)));
		builder.Services.AddSingleton<GrownUpGate>();
		builder.Services.AddSingleton<ExternalActivityGuard>();
		builder.Services.AddSingleton<StoryActions>();
		builder.Services.AddTransient<IMicRecorder, MicRecorder>();
		builder.Services.AddTransient<IRecordingPlayer, RecordingPlayer>();
		builder.Services.AddSingleton<OldVoicePacksCleanup>(); // Slice 278 part B review: deletes the old voice packs once, started at launch
		builder.Services.AddSingleton<OwnBooksCleanup>(); // Slice 278 A3: deletes the families' own books once, started by GalleryPage.OnAppearing, once per app run
		builder.Services.AddSingleton<StarterStories>();
		builder.Services.AddSingleton<StagedBookUpdates>();
		builder.Services.AddSingleton(services => CreateAudioPackStore(services));
		builder.Services.AddSingleton<StoreClient>();
		builder.Services.AddSingleton<AppConfigClient>();
		builder.Services.AddSingleton<UsageCounter>();
		builder.Services.AddSingleton<ReadTheStupidText.Application.Usage.IFallbackSink, DinoFallbackSink>(); // Slice 273 part B: the reading session and the app's own code report fallbacks through it
		builder.Services.AddSingleton<ShelfAudioPacks>(); // Slice 262: the one owner of the side languages' pre-made audio for the shelf books
		AddPages(builder.Services);

#if DEBUG
		builder.Logging.AddDebug();
		builder.AddTestHooks(TestHookScheme);
#endif

		MauiApp app = builder.Build();
		WireConfigFallbacks(app.Services); // before the first config fetch starts (FlushAsync and BeginCrashReports below), so a fast failure is not lost
		// Counts left from an earlier session go out now (Slice 133); the counter does nothing when counting is off.
		_ = app.Services.GetRequiredService<UsageCounter>().FlushAsync();
		BeginCrashReports(app.Services.GetRequiredService<AppConfigClient>());
		if (deviceLanguageUnsupported)
		{
			app.Services.GetRequiredService<ReadTheStupidText.Application.Usage.IFallbackSink>().Report(
				ReadTheStupidText.Application.Usage.FallbackArea.UiLanguage, ReadTheStupidText.Application.Usage.FallbackReason.UiLanguageUnsupported);
		}

		string sessionId = app.Services.GetRequiredService<UsageCounter>().SessionId; // Slice 250: captured now, so the crash path never touches the container
		CrashReporter.SessionProvider = () => sessionId; // a crash or problem report names the launch's usage session
#if DEBUG
		TestHooks.Install(app.Services, TestHookScheme, code => (Microsoft.Maui.Controls.Application.Current as DinoApp)?.CompleteFirstRun(code) ?? false);
		CrashTestHook.Install(); // Slice 227: after the app's own Debug commands, so it chains onto them
#endif
		return app;
	}

	// Slice 273 part C: the config client reports "last good copy used" / "default used" through the app's sink. The sink only queues a count (it never fetches the config),
	// so reporting from the client's own failure path cannot loop. Wired after the build because the sink depends on the counter, which depends on the config client.
	// The app's real language, or null when the preference store cannot be read yet: the book is then read without migrating its older recordings (Slice 286).
	private static string? AppLanguageForMigration()
	{
		try
		{
			return LanguageChoice.Current();
		}
		catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException)
		{
			// no-fallback: the older recordings stay unfiled and the book is migrated on a later read
			return null;
		}
	}

	private static void WireConfigFallbacks(IServiceProvider services)
	{
		var sink = services.GetRequiredService<ReadTheStupidText.Application.Usage.IFallbackSink>();
		services.GetRequiredService<AppConfigClient>().FallbackReported = reason => sink.Report(ReadTheStupidText.Application.Usage.FallbackArea.Config, reason);
	}

	// Slice 267: every pack download is counted (book id, language, closed outcome) and, through AudioPackLog, kept in the rolling log; no title or file name is carried.
	private static AudioPackStore CreateAudioPackStore(IServiceProvider services)
	{
		var store = new AudioPackStore(Path.Combine(FileSystem.AppDataDirectory, AudioPackFolder), FileSystem.CacheDirectory, new StoreAudioPackFetcher(), AudioPackLog.Write);
		var counter = services.GetRequiredService<UsageCounter>(); // resolved here, once, not inside the handler: a circular dependency shows at start-up, not at the first download
		store.Outcome += (bookId, language, detail) =>
		{
			AudioPackLog.Write($"pack {bookId}/{language}: {detail}");
			counter.RecordShelfBook(UsageEventName.AudioPackDownload, bookId, language, detail);
		};
		return store;
	}

	// Slice 227: pending crash reports are posted a little after start-up, once the remote config says crashReports.enabled (default off).
	private static void BeginCrashReports(AppConfigClient config) =>
		CrashReporter.BeginSending(new CrashSendOptions(StoreClient.BaseAddress, () => config.Current.CrashReportsEnabled, () => config.HasCopy, () => config.RefreshAsync()));

	// Pages are transient: each is opened with an explicit Load(story), never a one-shot
	// Shell hand-off to a cached page.
	private static void AddPages(IServiceCollection services)
	{
		services.AddTransient<GalleryPage>();
		services.AddTransient<StorePage>();
		services.AddTransient<UpdatePage>();
		services.AddTransient<BackupPage>();
		services.AddTransient<LanguagePage>();
		services.AddTransient<SettingsPage>();
		services.AddTransient<StoryLanguagesPage>();
		services.AddTransient<ReadingSpeedPage>();
		services.AddTransient<AnimalSoundsPage>();
		services.AddTransient<UsageCountsPage>();
		services.AddTransient<AboutPage>();
		services.AddTransient<FirstRunPage>();
		services.AddTransient<FirstBooksPage>();
		services.AddTransient<IntroWizardPage>();
		services.AddSingleton<GrownUpMode>();
		services.AddTransient<RecordPage>();
		services.AddTransient<RecordIntroPage>();
		services.AddTransient<ReadingPage>();
		services.AddTransient<ReadingSession>();
	}
}
