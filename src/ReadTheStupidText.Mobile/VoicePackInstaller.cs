using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;

namespace ReadTheStupidText.Mobile;

/// <summary>What the UI shows about the first-run voice pack download.</summary>
public enum PackInstallState
{
    Idle,
    Resolving,
    Downloading,
    Ready,
    Unavailable,
    Offline,
    HashMismatch,
    InvalidPack,
    Failed,
}

/// <summary>
/// Runs the "chosen language -> catalog -> voice pack -> neural voice" flow once at a
/// time and publishes its state, so the language page and the Reader show the same
/// progress while the phone voice keeps reading (Slice 50, Decision 61). A singleton:
/// the download outlives the page that started it.
/// </summary>
public sealed class VoicePackInstaller
{
    private readonly IVoicePackService _packs;
    private readonly IVoiceModelService _model;
    private readonly ISpeechReader _reader;
    private readonly ISettingsStore _settings;
    private readonly IPlatformServices _platform;
    private int _running;

    public VoicePackInstaller(IVoicePackService packs, IVoiceModelService model, ISpeechReader reader, ISettingsStore settings, IPlatformServices platform)
    {
        _packs = packs;
        _model = model;
        _reader = reader;
        _settings = settings;
        _platform = platform;
    }

    public PackInstallState State { get; private set; } = PackInstallState.Idle;

    /// <summary>Download progress 0..1 while <see cref="State"/> is Downloading.</summary>
    public double Progress { get; private set; }

    public event EventHandler? Changed;

    public bool CanRetry => State is PackInstallState.Offline or PackInstallState.HashMismatch or PackInstallState.Failed;

    // An installed pack is activated whatever the stored language (it needs no network
    // and no language); only a missing pack needs the language to choose one. A call
    // while a run is in flight is ignored, but re-publishes the current state so the
    // caller's UI keeps showing the in-flight progress instead of a dead tap.
    public async Task StartAsync()
    {
        if (Interlocked.Exchange(ref _running, 1) == 1)
        {
            Publish(State, Progress);
            return;
        }

        PackInstallState? failure;
        try
        {
            failure = await RunAsync();
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }

        // Published only after _running is cleared, so a Retry tapped the moment a
        // failure shows can never be swallowed by the run that just ended.
        if (failure is { } state)
        {
            Publish(state);
        }
    }

    // Returns the failure state to publish, or null when nothing failed.
    private async Task<PackInstallState?> RunAsync()
    {
        // A platform with no neural engine never fetches the catalog or the pack; the
        // phone voice reads, which the Unavailable state already says.
        if (!_platform.SupportsNeuralVoice)
        {
            return PackInstallState.Unavailable;
        }

        if (_packs.GetInstalledPath(PackConstants.SupertonicPackId) is not null)
        {
            await ActivateInstalledAsync();
            return null;
        }

        if (_settings.Language is not { } language)
        {
            return null;
        }

        Publish(PackInstallState.Resolving);
        (PackInfo? pack, PackInstallState? resolveFailure) = await ResolvePackAsync(language);
        if (pack is null)
        {
            return resolveFailure;
        }

        PackInstallState? downloadFailure = await DownloadPackAsync(pack);
        if (downloadFailure is null)
        {
            await ActivateInstalledAsync();
        }

        return downloadFailure;
    }

    private async Task<(PackInfo? Pack, PackInstallState? Failure)> ResolvePackAsync(string language)
    {
        PackCatalogResult catalog = await _packs.GetCatalogAsync(CancellationToken.None);
        if (catalog.Status != PackDownloadResult.Ok)
        {
            return (null, ToState(catalog.Status));
        }

        PackInfo? pack = VoicePackSelector.Select(catalog.Packs, language);
        return pack is null ? (null, PackInstallState.Unavailable) : (pack, null);
    }

    private async Task<PackInstallState?> DownloadPackAsync(PackInfo pack)
    {
        Publish(PackInstallState.Downloading, 0);
        var progress = new Progress<double>(PublishProgress);
        PackDownloadResult result = await _packs.DownloadAsync(pack.Id, progress, CancellationToken.None);
        return result == PackDownloadResult.Ok ? null : ToState(result);
    }

    private async Task ActivateInstalledAsync()
    {
        await _model.InitializeAsync();
        Publish(PackInstallState.Ready);
        await _reader.WarmUpAsync();
    }

    private static PackInstallState ToState(PackDownloadResult result) => result switch
    {
        PackDownloadResult.Offline => PackInstallState.Offline,
        PackDownloadResult.HashMismatch => PackInstallState.HashMismatch,
        PackDownloadResult.InvalidPack => PackInstallState.InvalidPack,
        _ => PackInstallState.Failed,
    };

    // Called from the download pipeline's background thread too: state and the
    // Changed event always land on the main thread, so UI handlers can touch views.
    private void Publish(PackInstallState state, double progress = 0)
    {
        if (MainThread.IsMainThread)
        {
            Apply(state, progress);
            return;
        }

        MainThread.BeginInvokeOnMainThread(() => Apply(state, progress));
    }

    // A late progress tick must not drag a finished or failed state back to Downloading.
    private void PublishProgress(double progress)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (State == PackInstallState.Downloading)
            {
                Apply(PackInstallState.Downloading, progress);
            }
        });
    }

    private void Apply(PackInstallState state, double progress)
    {
        State = state;
        Progress = progress;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
