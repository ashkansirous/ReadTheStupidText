using Sirous.Reading.Application.Activity;
using Sirous.Reading.Application.Documents;
using Sirous.Reading.Application.Input;
using Sirous.Reading.Application.Logging;
using Sirous.Reading.Application.Reading;
using Sirous.Reading.Application.Sanitizing;
using Sirous.Reading.Application.Settings;
using Sirous.Reading.Application.Startup;
using Sirous.Reading.Application.Translation;
using Sirous.Reading.Application.Usage;
using Sirous.Reading.Documents;
using Sirous.Reading.Infrastructure.Logging;
using Sirous.Reading.Infrastructure.Reading;
using Sirous.Reading.Infrastructure.Sanitizing;
using Sirous.Reading.Infrastructure.Settings;
using Sirous.Reading.Infrastructure.Translation;
using Sirous.Reading.Infrastructure.Usage;
using ReadTheStupidText.Windows.Tray.Input;
using ReadTheStupidText.Windows.Tray.Startup;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Net.NetworkInformation;
using Microsoft.UI.Xaml;

namespace ReadTheStupidText_App;

/// <summary>
/// Application entry point. Keeps to provider/DI wiring and window bootstrap;
/// all read-aloud behaviour lives in the Application/Infrastructure layers.
/// </summary>
public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();

        // The stored language drives the UI resources, so it is applied before any window is built (Slice 57).
        UiStrings.ApplyLanguage(Services.GetRequiredService<ISettingsStore>().Language);
    }

    /// <summary>The composition root for the running app.</summary>
    public IServiceProvider Services { get; }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // The neural engine is primary once a downloaded voice pack serves the chosen language (Slice 56);
        // the WinRT reader is the labelled "Windows voice" that reads until then, or by choice.
        // CompositeSpeechReader routes between them, and the catalog lists the pack's voices plus
        // the Windows voice. LocalSettingsStore doubles as the language-change notifier.
        services.AddSingleton<LocalSettingsStore>();
        services.AddSingleton<ISettingsStore>(sp => sp.GetRequiredService<LocalSettingsStore>());
        services.AddSingleton<ILanguageChangeNotifier>(sp => sp.GetRequiredService<LocalSettingsStore>());
        services.AddSingleton<IVoicePackService>(sp => new WindowsVoicePackService(
            PackFolders.PacksRoot(), new HttpClient(), NetworkInterface.GetIsNetworkAvailable, fallbacks: sp.GetRequiredService<IFallbackSink>())); // Slice 273 part C
        services.AddSingleton<IVoiceModelService, PackVoiceModelService>();
        services.AddSingleton<VoicePackSetup>();
        services.AddSingleton<SpeechReader>();
        services.AddSingleton<AudioChunkPaths>();
        services.AddSingleton<SupertonicSpeechReader>();
        services.AddSingleton<CompositeSpeechReader>();
        services.AddSingleton<ISpeechReader>(sp => sp.GetRequiredService<CompositeSpeechReader>());
        services.AddSingleton<IHeadStart>(sp => sp.GetRequiredService<CompositeSpeechReader>());
        services.AddSingleton<IVoiceCatalog, PackVoiceCatalog>();

        services.AddSingleton<IClipboardReader, ClipboardReader>();
        // While an update is required (Slice 217) ReadAloudService itself refuses every read through this gate and the panel reopens on the notice.
        services.AddSingleton<WindowsUpdate>(sp => WindowsUpdate.Create(sp.GetRequiredService<IFallbackSink>()));
        services.AddSingleton<IReadGate>(sp => sp.GetRequiredService<WindowsUpdate>());
        services.AddSingleton<IHotkeyService, GlobalHotkeyService>();
        services.AddSingleton<ISelectionCopier, SelectionCopier>();
        services.AddSingleton<ISelectionMonitor, UiaSelectionMonitor>();
        services.AddSingleton<IClipboardMonitor, ClipboardFormatListener>();
        services.AddSingleton<IForegroundWindow, ForegroundWindowProbe>();
        services.AddSingleton<IStartupService, StartupTaskService>();
        services.AddSingleton<ITextSanitizer, TextSanitizer>();

        services.AddSingleton<PlainTextExtractor>();
        services.AddSingleton<PdfTextExtractor>();
        services.AddSingleton<DocxTextExtractor>();
        services.AddSingleton<IDocumentTextExtractor, CompositeDocumentTextExtractor>();

        // Diagnostic logging: shared log folder, the Serilog system log, and the
        // input-log writer that mirrors the activity log to disk (redacted text).
        services.AddSingleton<LogPaths>();
        services.AddSingleton<ILogFolder>(sp => sp.GetRequiredService<LogPaths>());
        services.AddSingleton<ISystemLog, SerilogSystemLog>();
        services.AddSingleton<ActivityInputLog>();

        // Translate-before-read (Slice 57): packs-backed translator, on-device script detector.
        services.AddSingleton<ITranslator>(sp => PackTranslator.WithInstalledLookup(
            PackFolders.PacksRoot,
            () => sp.GetRequiredService<ISettingsStore>().Language,
            sp.GetRequiredService<IVoicePackService>().GetInstalledPath));
        services.AddSingleton<TranslationPackInstaller>();
        services.AddSingleton<ILanguageDetector, ScriptLanguageDetector>();
        services.AddSingleton<IPackUpdateNetwork, Sirous.Reading.Infrastructure.Translation.WindowsPackUpdateNetwork>();
        services.AddSingleton<ReadTranslator>();

        // Anonymous usage counts (Slice 140): one service, also the IUsageRecorder the read service and the panel's switch use.
        services.AddSingleton(_ => WindowsUsage.Create());
        services.AddSingleton<IUsageRecorder>(sp => sp.GetRequiredService<UsageService>());
        // Slice 273 part B: the shared readers and the translator report fallbacks here; the repeats are queued at the exit flush (StartUsageCounts).
        services.AddSingleton<FallbackReporter>();
        services.AddSingleton<IFallbackSink>(sp => sp.GetRequiredService<FallbackReporter>());

        services.AddSingleton<IActivityLog, ActivityLog>();
        services.AddSingleton<ReadAloudService>();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates the tray-hosting window. It is never activated — ReadTheStupidText lives in
    /// the notification area, not as a visible window.
    /// </summary>
    // Seven days of diagnostic logs is plenty to investigate a report; older
    // day-files are swept on launch so the temp folder stays bounded.
    private static readonly TimeSpan LogRetention = TimeSpan.FromDays(7);

    // Audio-chunk folders are pure transient playback buffers, not a diagnostic
    // record, so they're swept far sooner than logs (Decision 49) — this only
    // catches one orphaned by a read that never reached a terminal state (e.g. the
    // app was killed mid-read); a normal read cleans up its own folder when done.
    private static readonly TimeSpan AudioChunkRetention = TimeSpan.FromDays(1);

    // The longest the exit waits for the last usage send; quitting is never held up by the network.
    private static readonly TimeSpan UsageExitWait = TimeSpan.FromSeconds(2);

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        StartLogging();

        _window = new MainWindow(
            Services.GetRequiredService<ReadAloudService>(),
            Services.GetRequiredService<IHotkeyService>(),
            Services.GetRequiredService<IClipboardMonitor>(),
            Services.GetRequiredService<IStartupService>(),
            Services.GetRequiredService<IActivityLog>(),
            Services.GetRequiredService<ILogFolder>(),
            Services.GetRequiredService<ISettingsStore>(),
            Services.GetRequiredService<VoicePackSetup>(),
            Services.GetRequiredService<ISystemLog>(),
            Services.GetRequiredService<WindowsUpdate>());
        StartVoicePackSetup();
        StartUsageCounts();
        Services.GetRequiredService<WindowsUpdate>().Start(); // Slice 217: the forced-update check, at start and once a day
    }

    // Counts the launch and sends what the last run left queued, off the UI thread; a last send runs as the process exits (Slice 140).
    private void StartUsageCounts()
    {
        var usage = Services.GetRequiredService<UsageService>();
        string? language = UiLanguageTag.Effective(Services.GetRequiredService<ISettingsStore>().Language, CultureInfo.CurrentUICulture.Name);
        _ = usage.StartAsync(context => UsageEvents.AppOpened(context, language, usage.OsMajor));
        var fallbacks = Services.GetRequiredService<FallbackReporter>();
        ReadTheStupidText_App.WindowPlacer.Fallbacks = fallbacks; // Slice 273 part D: window placement fallbacks
        usage.BeforeSend = fallbacks.FlushRepeats; // Slice 273 part B: before each send, as on the phone, so a long-running tray app does not hold its totals until exit
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            fallbacks.FlushRepeats(); // Slice 273 part B: the session totals of fallbacks that happened again
            usage.FlushForExitAsync(UsageExitWait).GetAwaiter().GetResult();
        };
    }

    // First run (no stored language) asks for one in the language window, which starts the download.
    // Every later launch resumes quietly: an installed pack is activated with no network request, a
    // missing one downloads in the background while the Windows voice reads (Slice 56).
    private void StartVoicePackSetup()
    {
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        if (Services.GetRequiredService<ISettingsStore>().Language is null)
        {
            _window!.ShowLanguageWindow();
            return;
        }

        _ = RunVoicePackSetupAsync();
    }

    // A launch (or a Retry) that found no network leaves the setup Offline; the download starts by
    // itself once the network returns. Only an Offline setup restarts: a download in flight, a finished
    // one or a failure that is not about connectivity is left alone. Raised on a thread-pool thread.
    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        if (e.IsAvailable && Services.GetRequiredService<VoicePackSetup>().State == PackSetupState.Offline)
        {
            _ = RunVoicePackSetupAsync();
        }
    }

    private async Task RunVoicePackSetupAsync()
    {
        try
        {
            await Services.GetRequiredService<VoicePackSetup>().StartAsync();
        }
        catch (Exception ex)
        {
            Services.GetRequiredService<ISystemLog>().Error("voice pack setup failed", null, ex);
            Services.GetRequiredService<IFallbackSink>().Report(FallbackArea.Pack, FallbackReason.PackFailed); // Slice 273 part D: the Windows voice reads until the pack is set up
        }
    }

    // Sweeps stale logs, opens the system log, and starts the input-log writer
    // (which subscribes to the activity log) — all before the read pipeline below
    // resolves, so no early read is missed on disk.
    private void StartLogging()
    {
        Services.GetRequiredService<LogPaths>().PurgeOlderThan(LogRetention);
        Services.GetRequiredService<AudioChunkPaths>().PurgeOrphaned(AudioChunkRetention);
        Services.GetRequiredService<ISystemLog>().Info($"{ProductInfo.Name} started");
        Services.GetRequiredService<ActivityInputLog>();
    }
}
