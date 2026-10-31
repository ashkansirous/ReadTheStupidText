using System.Diagnostics;
using ReadTheStupidText.Application.AppConfig;
using ReadTheStupidText.Application.Usage;
using ReadTheStupidText.Mobile.Shared.Usage;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// The phone's forced update (Slice 217, port of Dino Reader's Slice 132): reads the platform's <c>minVersion</c> / <c>latestVersion</c> from the
/// same remote config as the usage switch, through its own <see cref="RemoteConfigClient{TConfig}"/> (the shared cache key, so a copy kept by
/// either is used by both). Below <c>minVersion</c> the whole app is the <see cref="UpdatePage"/>; below <c>latestVersion</c> only the dismissible
/// card on the reader page shows. A missing or bad config, an offline start with no kept copy, a Debug build and an unstamped version never
/// update. One shared instance, so the pages need no registration.
/// </summary>
public sealed class AppUpdate
{
    private const string CacheKey = "rtst.appconfig.json";
    private const string DismissedKey = "rtst.update.dismissedFor";
    private const string LogName = "Read The Stupid Text";
    private const string PlayPage = "https://play.google.com/store/apps/details?id=uk.sirous.readthestupidtext";
    private const string AppStorePage = "https://apps.apple.com/app/id6825052929"; // App Store Connect app record (SKU readthestupidtext)

    /// <summary>The one instance the pages and the app share.</summary>
    public static AppUpdate Shared { get; } = new();

    private readonly RemoteConfigClient<UsageConfig> _client = new(new RemoteConfigOptions<UsageConfig>(
        RtstUsage.BaseAddress, CacheKey, LogName, UsageConfig.Default, UsageConfig.Parse, UsageConfig.TryParseServed));

    private readonly bool _buildMayUpdate = UpdateDecision.BuildMayUpdate(new UsageFacts(IsDebug, UsageEnvironment.InstalledVersion, false, false));
    private UsageConfig? _testConfig; // Debug hook only: replaces the fetched config, so a scripted check never touches the live file

    private AppUpdate()
    {
        _client.Changed += (_, _) => RaiseChanged();
    }

    /// <summary>Raised on the main thread when the update state may have changed (a fetch brought a different config, or a Debug test config was set).</summary>
    public event EventHandler? Changed;

    public static AppPlatform ThisPlatform =>
        DeviceInfo.Platform == DevicePlatform.iOS ? AppPlatform.Ios : AppPlatform.Android;

    /// <summary>What the config in force asks of this build.</summary>
    public UpdateState State =>
        UpdateDecision.Evaluate(_testConfig ?? _client.Current, ThisPlatform, UsageEnvironment.InstalledVersion, _buildMayUpdate || _testConfig is not null);

    /// <summary>True when the quiet "A new version is ready" card should show: a newer release exists and the person has not closed the card for it.</summary>
    public bool ShowAvailableCard => State == UpdateState.Available && !UpdateDecision.IsDismissed(ReadDismissed(), Config, ThisPlatform);

    private UsageConfig Config => _testConfig ?? _client.Current;

    private static bool IsDebug
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }

    /// <summary>Fetches the config and keeps it (at most every 15 minutes). Never throws.</summary>
    public Task RefreshAsync() => _client.RefreshAsync();

    /// <summary>Hides the card until the next <c>latestVersion</c>.</summary>
    public void DismissAvailable()
    {
        try
        {
            Preferences.Default.Set(DismissedKey, Config.For(ThisPlatform).LatestVersion);
        }
        catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException)
        {
            Debug.WriteLine($"{LogName} could not remember the closed update card: {ex.Message}");
        }

        RaiseChanged();
    }

    /// <summary>Opens this platform's store page; never throws.</summary>
    public static async Task OpenStoreAsync()
    {
        string page = ThisPlatform == AppPlatform.Ios ? AppStorePage : PlayPage;
        try
        {
            await Launcher.Default.TryOpenAsync(new Uri(page));
        }
        catch (Exception ex) when (ex is InvalidOperationException or FeatureNotSupportedException)
        {
            Debug.WriteLine($"{LogName} could not open the store: {ex.Message}");
        }
    }

    private static string? ReadDismissed()
    {
        try
        {
            return Preferences.Default.Get(DismissedKey, (string?)null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException)
        {
            return null;
        }
    }

    // A handler that throws must not escape into the main-thread dispatcher.
    private void RaiseChanged()
    {
        try
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{LogName}: an update change handler failed: {ex.Message}");
        }
    }

#if DEBUG
    /// <summary>Debug test hook (Slice 217): sets a built-in test config for this platform, or clears it with null; raises <see cref="Changed"/>.</summary>
    internal void SetTestConfig(UsageConfig? config)
    {
        _testConfig = config;
        ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"UPDATE state={State} card={ShowAvailableCard}");
        RaiseChanged();
    }
#endif
}
