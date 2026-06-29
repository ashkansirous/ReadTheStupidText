using ReadTheStupidText.Application.Activity;
using ReadTheStupidText.Application.Input;
using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Sanitizing;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Application.Startup;
using ReadTheStupidText.Infrastructure.Input;
using ReadTheStupidText.Infrastructure.Logging;
using ReadTheStupidText.Infrastructure.Reading;
using ReadTheStupidText.Infrastructure.Sanitizing;
using ReadTheStupidText.Infrastructure.Settings;
using ReadTheStupidText.Infrastructure.Startup;
using Microsoft.Extensions.DependencyInjection;
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
    }

    /// <summary>The composition root for the running app.</summary>
    public IServiceProvider Services { get; }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Neural (Supertonic) is the primary engine; the WinRT reader is the
        // fallback used only while the model downloads. CompositeSpeechReader
        // routes between them, and the catalog exposes the neural voices once ready.
        services.AddSingleton<IVoiceModelService, SupertonicModelService>();
        services.AddSingleton<SpeechReader>();
        services.AddSingleton<SupertonicSpeechReader>();
        services.AddSingleton<ISpeechReader, CompositeSpeechReader>();
        services.AddSingleton<IVoiceCatalog, NeuralVoiceCatalog>();

        services.AddSingleton<IClipboardReader, ClipboardReader>();
        services.AddSingleton<IHotkeyService, GlobalHotkeyService>();
        services.AddSingleton<ISelectionCopier, SelectionCopier>();
        services.AddSingleton<ISelectionMonitor, UiaSelectionMonitor>();
        services.AddSingleton<IClipboardMonitor, ClipboardFormatListener>();
        services.AddSingleton<IForegroundWindow, ForegroundWindowProbe>();
        services.AddSingleton<IStartupService, StartupTaskService>();
        services.AddSingleton<ISettingsStore, LocalSettingsStore>();
        services.AddSingleton<ITextSanitizer, TextSanitizer>();

        // Diagnostic logging: shared log folder, the Serilog system log, and the
        // input-log writer that mirrors the activity log to disk (redacted text).
        services.AddSingleton<LogPaths>();
        services.AddSingleton<ILogFolder>(sp => sp.GetRequiredService<LogPaths>());
        services.AddSingleton<ISystemLog, SerilogSystemLog>();
        services.AddSingleton<ActivityInputLog>();

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

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        StartLogging();

        _window = new MainWindow(
            Services.GetRequiredService<ReadAloudService>(),
            Services.GetRequiredService<IHotkeyService>(),
            Services.GetRequiredService<IClipboardMonitor>(),
            Services.GetRequiredService<IStartupService>(),
            Services.GetRequiredService<IActivityLog>(),
            Services.GetRequiredService<ILogFolder>());
    }

    // Sweeps stale logs, opens the system log, and starts the input-log writer
    // (which subscribes to the activity log) — all before the read pipeline below
    // resolves, so no early read is missed on disk.
    private void StartLogging()
    {
        Services.GetRequiredService<LogPaths>().PurgeOlderThan(LogRetention);
        Services.GetRequiredService<ISystemLog>().Info("Read The Stupid Text started");
        Services.GetRequiredService<ActivityInputLog>();
    }
}
