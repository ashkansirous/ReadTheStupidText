using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using ReadTheStupidText.Application.AppConfig;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Usage;
using ReadTheStupidText.Infrastructure.Usage;
using Windows.ApplicationModel;
using Windows.Storage;

namespace ReadTheStupidText_App;

/// <summary>
/// The tray app's forced update (Slice 217, mock frames W3 and W4): the remote config's <c>windows</c> versions, checked at start and once a day
/// (<see cref="UpdateConfigService"/>, whether or not the usage switch is on). Below <c>minVersion</c> reading is paused and the control panel shows
/// the "Update needed" card; below <c>latestVersion</c> it shows a quiet bar once the person opens it. Debug builds and unstamped versions never
/// update, and a scripted check sets <see cref="UpdateConfigService.TestConfigVariable"/> to use its own config instead of the live file.
/// <see cref="Changed"/> and <see cref="ReadBlocked"/> are raised on the UI thread.
/// </summary>
public sealed class WindowsUpdate : IReadGate
{
    private const string StoreProductId = "9NGT1BN1H92V";
    private const string DismissedKey = "UpdateDismissedFor";
    private const string FallbackVersion = "0.0.0";
    private static readonly TimeSpan AutomaticReopenGap = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan CheckEvery = TimeSpan.FromHours(24);
    private static readonly TimeSpan[] StartRetryDelays =
        { TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(20) };
    private static readonly TimeSpan RetryCap = TimeSpan.FromMinutes(30);

    private readonly UpdateConfigService _service;

    // Null if the singleton was first resolved off the UI thread; Start() (called on the UI thread) fills it in.
    private DispatcherQueue? _dispatcher = DispatcherQueue.GetForCurrentThread();
    private DispatcherQueueTimer? _daily;
    private readonly object _reopenGate = new();
    private DateTimeOffset _lastAutomaticReopen = DateTimeOffset.MinValue;

    public WindowsUpdate(UpdateConfigService service)
    {
        _service = service;
        _service.Changed += (_, _) => PostToUi(RaiseChanged);
    }

    // Runs on the UI thread; when no dispatcher could be found (off-thread resolve before Start) it runs inline rather than being lost.
    private void PostToUi(Action action)
    {
        if (_dispatcher is null)
        {
            action();
            return;
        }

        _dispatcher.TryEnqueue(() => action());
    }

    /// <summary>Raised when the update state may have changed (a fetch brought a different config, or the note was closed).</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when a read was refused because an update is required (the hotkey, a selection, a copy or a play click): the panel opens.</summary>
    public event EventHandler? ReadBlocked;

    /// <summary>True while the installed version is below the config's <c>minVersion</c>: reading is paused.</summary>
    public bool Required => _service.State == UpdateState.Required;

    /// <summary>True when a newer release exists and the person has not closed the bar for it.</summary>
    public bool ShowAvailableBar => _service.State == UpdateState.Available && !UpdateDecision.IsDismissed(ReadDismissed(), _service.Config, AppPlatform.Windows);

    /// <summary>The service over the usage counts' file store and Read The Stupid Text's own host, for this build.</summary>
    public static WindowsUpdate Create()
    {
        string version = InstalledVersion();
        var service = new UpdateConfigService(
            new UsageFileStore(), new UsageHttpTransport(), AppPlatform.Windows, version, UpdateDecision.BuildMayUpdate(Facts(version)), TestConfig());
        return new WindowsUpdate(service);
    }

    // Only a Debug build reads the test config variable; a shipped build always uses the live config.
    private static string? TestConfig()
    {
#if DEBUG
        return Environment.GetEnvironmentVariable(UpdateConfigService.TestConfigVariable);
#else
        return null;
#endif
    }

    /// <summary>Checks now (retrying a few times if the network is not up yet), then once a day while the app runs. Never throws.</summary>
    public void Start()
    {
        _dispatcher ??= DispatcherQueue.GetForCurrentThread();
        _ = RefreshWithRetryAsync();
        _daily = _dispatcher?.CreateTimer();
        if (_daily is null)
        {
            System.Diagnostics.Debug.WriteLine("Update check: no UI dispatcher, the daily check is off.");
            return;
        }

        _daily.Interval = CheckEvery;
        _daily.IsRepeating = true;
        _daily.Tick += (_, _) => _ = RefreshAsync();
        _daily.Start();
    }

    /// <summary>
    /// Called by a read trigger that was refused; opens the panel on the notice. A selection or copy that fires on its own
    /// (<paramref name="automatic"/>) reopens it at most once per <c>AutomaticReopenGap</c>, so a closed panel does not steal focus on every Ctrl+C.
    /// </summary>
    public void NotifyReadBlocked(bool automatic = false)
    {
        if (automatic && !AutomaticReopenDue())
        {
            return;
        }

        PostToUi(() => ReadBlocked?.Invoke(this, EventArgs.Empty));
    }

    bool IReadGate.IsBlocked => Required;

    void IReadGate.ReadRefused(bool automatic) => NotifyReadBlocked(automatic);

    private bool AutomaticReopenDue()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        lock (_reopenGate)
        {
            if (now - _lastAutomaticReopen < AutomaticReopenGap)
            {
                return false;
            }

            _lastAutomaticReopen = now;
            return true;
        }
    }

    /// <summary>Hides the bar until the next <c>latestVersion</c>.</summary>
    public void DismissAvailable()
    {
        try
        {
            ApplicationData.Current.LocalSettings.Values[DismissedKey] = _service.LatestVersion;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Update bar could not be remembered as closed: {ex.Message}");
        }

        RaiseChanged();
    }

    /// <summary>Opens this app's Microsoft Store page; never throws.</summary>
    public static async Task OpenStoreAsync()
    {
        try
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri($"ms-windows-store://pdp/?productid={StoreProductId}"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"The Microsoft Store page could not be opened: {ex.Message}");
        }
    }

    // A start before the network is up must not wait a day for its next try: retry after 1, 2 and 5 minutes until one fetch has worked.
    // The delay doubles-ish up to RetryCap and then stays there, so a start that is online late is still checked within half an hour.
    private async Task RefreshWithRetryAsync()
    {
        await RefreshAsync();
        for (int attempt = 0; !_service.HasFetched; attempt++)
        {
            TimeSpan delay = attempt < StartRetryDelays.Length ? StartRetryDelays[attempt] : RetryCap;
            await Task.Delay(delay);
            await RefreshAsync();
        }
    }

    private async Task RefreshAsync()
    {
        await _service.RefreshAsync();
        System.Diagnostics.Debug.WriteLine($"Update check: {_service.State}");
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private static string? ReadDismissed() =>
        ApplicationData.Current.LocalSettings.Values.TryGetValue(DismissedKey, out object? value) ? value as string : null;

    // The package version without its trailing revision (the Store keeps it 0); a build with no package identity reports "0.0.0", which is unstamped.
    private static string InstalledVersion()
    {
        try
        {
            PackageVersion version = Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }
        catch (InvalidOperationException)
        {
            return FallbackVersion;
        }
    }

    private static UsageFacts Facts(string version)
    {
#if DEBUG
        const bool isDebug = true;
#else
        const bool isDebug = false;
#endif
        return new UsageFacts(isDebug, version, false, false);
    }
}
