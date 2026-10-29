using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile.Shared.Network;

/// <summary>
/// The one connectivity watcher of the MAUI apps (Slice 182): when the network changes, or the app returns to the
/// foreground (<see cref="NotifyForeground"/>), the stopped pack downloads the coordinator holds start again from
/// their <c>.partial</c> files. Dino's own "start when Wi-Fi arrives" hooks listen to <see cref="NetworkChanged"/>
/// instead of subscribing to <c>Connectivity</c> themselves. A singleton, created at app start.
/// </summary>
public sealed class PackResumeWatcher : IPackResume, IDisposable
{
    private readonly PackResumeCoordinator _coordinator = new();

    private int _started;

    /// <summary>
    /// Begins watching and tells <see cref="PackStatusText"/> how to read "mobile data only". Each app calls it once at start, so the
    /// status line never depends on when the singleton happens to be created. Safe to call again.
    /// </summary>
    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        PackStatusText.CellularOnly = CellularOnly;
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _started, 0) == 1)
        {
            Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
        }
    }

    /// <summary>Raised on any thread after the connectivity changed and the stopped downloads were offered a restart.</summary>
    public event EventHandler? NetworkChanged;

    public void Report(string key, PackRunEnd end, Func<Task<bool>> resume, string? packId = null, bool downloadNow = false, long? epoch = null) =>
        _coordinator.Report(key, end, resume, packId, downloadNow, epoch);

    public long EpochOf(string key) => _coordinator.EpochOf(key);

    public PackResumeState StateOf(string key) => _coordinator.StateOf(key);

    /// <summary>
    /// True when the phone has a connection but only mobile data: a waiting download then says "Waiting for Wi-Fi"
    /// instead of "Waiting for the internet" (Slice 182). No connection at all is not cellular-only.
    /// </summary>
    public static bool CellularOnly()
    {
        try
        {
            NetworkProfile[] profiles = [.. Connectivity.Current.ConnectionProfiles.Select(ToProfile)];
            return profiles.Length > 0 && !StorytellerNetworkPolicy.AllowsAutomaticDownload(profiles);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Connectivity read failed: {ex.Message}");
            return true; // cannot tell: the conservative answer, no automatic restart over a link that may be mobile data
        }
    }

    /// <summary>The app came back to the foreground: stopped downloads get one more try.</summary>
    public void NotifyForeground() => Trigger(PackResumeReason.Foreground);

    // Off the connectivity thread: the trigger reads the packs folder and the subscribers may do real work.
    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e) =>
        _ = Task.Run(() =>
        {
            Trigger(PackResumeReason.Network);
            RaiseNetworkChanged();
        });

    // One failing subscriber never stops the others or the trigger.
    private void RaiseNetworkChanged()
    {
        foreach (Delegate handler in NetworkChanged?.GetInvocationList() ?? [])
        {
            try
            {
                ((EventHandler)handler)(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Network change subscriber failed: {ex.Message}");
            }
        }
    }

    private void Trigger(PackResumeReason reason)
    {
        try
        {
            bool online = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
            IReadOnlyCollection<NetworkProfile> profiles = [.. Connectivity.Current.ConnectionProfiles.Select(ToProfile)];
            int started = _coordinator.Trigger(reason, online, profiles, PartialLength);
            if (started == 0 && _coordinator.TrackedCount > 0)
            {
                PackDownloadLog.ResumeSkipped(reason, online, string.Join('+', profiles));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Pack resume trigger failed: {ex.Message}");
        }
    }

    private static NetworkProfile ToProfile(ConnectionProfile profile) => profile switch
    {
        ConnectionProfile.WiFi => NetworkProfile.WiFi,
        ConnectionProfile.Ethernet => NetworkProfile.Ethernet,
        ConnectionProfile.Cellular => NetworkProfile.Cellular,
        _ => NetworkProfile.Other,
    };

    // The bytes already on disk for the pack, for the log line only.
    private static long PartialLength(string packId)
    {
        try
        {
            return Directory.EnumerateFiles(MobileVoicePackService.PacksRoot())
                .Where(f => f.EndsWith(PackNaming.PartialSuffix, StringComparison.Ordinal) && PackNaming.IsOrphanFileName(Path.GetFileName(f), packId))
                .Select(f => new FileInfo(f).Length)
                .DefaultIfEmpty(0)
                .Max();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
