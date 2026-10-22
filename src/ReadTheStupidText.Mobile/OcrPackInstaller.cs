using System.Collections.Concurrent;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Downloads the OCR pack of the chosen language in the background, after the voice pack
/// (Slice 52, Decision 65). It is silent on purpose: until the pack arrives scans still work through
/// ML Kit, and a failure (offline, hash mismatch) is simply retried the next time the app starts
/// (the app calls <see cref="EnsureForCurrentLanguageAsync"/> at every start, whatever the voice
/// installer does), so no screen needs a state for it. The same start-up check installs a newer
/// version of an installed pack when the catalogue lists one; the old pack stays in use until the
/// new one has verified. Voice downloads go first: a run waits, after a short delay that lets the
/// voice installer begin, until the voice installer is no longer Resolving or Downloading. One download runs at a time; a
/// language change during a run is picked up by a second run for the new language when it ends, and
/// a language change while idle starts one straight away. The folder scan and the catalog fetch both
/// run off the calling (UI) thread, and the catalog reuses the voice installer's recent fetch.
/// </summary>
public sealed class OcrPackInstaller
{
    private static readonly TimeSpan CatalogMaxAge = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan VoiceHeadStart = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan VoicePollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan VoiceMaxWait = TimeSpan.FromMinutes(30);

    private readonly IVoicePackService _packs;
    private readonly ISettingsStore _settings;
    private readonly VoicePackInstaller _voice;
    private int _running;
    private readonly ConcurrentDictionary<string, DownloadFlight> _flights = new(StringComparer.OrdinalIgnoreCase); // one download per pack id, shared by the background run and a person waiting (Slice 87)

    public OcrPackInstaller(IVoicePackService packs, ISettingsStore settings, ILanguageChangeNotifier languages, VoicePackInstaller voice)
    {
        _packs = packs;
        _settings = settings;
        _voice = voice;
        languages.LanguageChanged += (_, _) => _ = EnsureForCurrentLanguageAsync();
    }

    public async Task EnsureForCurrentLanguageAsync()
    {
        if (Interlocked.Exchange(ref _running, 1) == 1)
        {
            return;
        }

        string? handled = _settings.Language;
        try
        {
            await Task.Run(() => RunAsync(handled)).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }

        if (LanguageChangedSince(handled))
        {
            await EnsureForCurrentLanguageAsync().ConfigureAwait(false);
        }
    }

    private bool LanguageChangedSince(string? handled) =>
        _settings.Language is { } current && !string.Equals(current, handled, StringComparison.OrdinalIgnoreCase);

    private async Task RunAsync(string? language)
    {
        if (language is null)
        {
            return;
        }

        try
        {
            await WaitForVoiceDownloadsAsync().ConfigureAwait(false);
            await InstallAsync(language).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OCR pack install for '{language}' failed: {ex.Message}");
        }
    }

    // The voice pack is the one the user is waiting for, so OCR never competes with it for bandwidth.
    private async Task WaitForVoiceDownloadsAsync()
    {
        await Task.Delay(VoiceHeadStart).ConfigureAwait(false);
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (_voice.State is PackInstallState.Resolving or PackInstallState.Downloading && waited.Elapsed < VoiceMaxWait)
        {
            await Task.Delay(VoicePollInterval).ConfigureAwait(false);
        }
    }

    private async Task InstallAsync(string language)
    {
        PackDownloadResult result = await InstallAsync(language, null, CancellationToken.None).ConfigureAwait(false);
        if (result != PackDownloadResult.Ok)
        {
            System.Diagnostics.Debug.WriteLine($"OCR pack for '{language}': {result}");
        }
    }

    /// <summary>
    /// Installs the OCR pack of a language the person picked for a scanned page (Slice 87), reporting
    /// 0..1 progress. It runs now, not behind the voice download, because the person is waiting on it,
    /// and it joins a download of the same pack already under way instead of starting a second one.
    /// Ok also means the pack was already current.
    /// </summary>
    public Task<PackDownloadResult> EnsureForLanguageAsync(string language, IProgress<double> progress, CancellationToken cancellationToken) =>
        Task.Run(() => InstallAsync(language, progress, cancellationToken), cancellationToken);

    /// <summary>
    /// The OCR-pack languages a scanned page could be in and that are not installed yet, with the
    /// catalog's fetch status so an offline scan can tell "nothing to download" from "could not look".
    /// </summary>
    public async Task<PageLanguageOffer> OfferedLanguagesAsync(IEnumerable<OcrSource> sources, TextScript? pageScript, CancellationToken cancellationToken)
    {
        if (!PageLanguagePrompt.NeedsPack(sources))
        {
            return new PageLanguageOffer(PackDownloadResult.Ok, []);
        }

        PackCatalogResult catalog = await _packs.GetCatalogCachedAsync(CatalogMaxAge, cancellationToken).ConfigureAwait(false);

        return catalog.Status == PackDownloadResult.Ok
            ? new PageLanguageOffer(catalog.Status, PageLanguagePrompt.Offer(sources, pageScript, catalog.Packs, _packs.GetInstalledOcrPacks()))
            : new PageLanguageOffer(catalog.Status, []);
    }

    private async Task<PackDownloadResult> InstallAsync(string language, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        InstalledVoicePack? installed = PackSelection.Serving(_packs.GetInstalledOcrPacks(), PackKind.Ocr, language).FirstOrDefault();
        PackCatalogResult catalog = await _packs.GetCatalogCachedAsync(CatalogMaxAge, cancellationToken).ConfigureAwait(false);
        if (catalog.Status != PackDownloadResult.Ok)
        {
            return catalog.Status;
        }

        if (OcrPackSelector.Select(catalog.Packs, language) is not { } pack)
        {
            return PackDownloadResult.InvalidPack; // no pack serves this language: trying again cannot help
        }

        return PackVersions.ShouldInstall(pack, installed)
            ? await DownloadSharedAsync(pack.Id, progress, cancellationToken).ConfigureAwait(false)
            : PackDownloadResult.Ok;
    }

    // The first caller for a pack downloads it; later callers wait for that result and share its progress.
    // A joiner whose own token fires just stops waiting; if the downloader was cancelled instead, the
    // joiner starts the download itself.
    private async Task<PackDownloadResult> DownloadSharedAsync(string packId, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var created = new DownloadFlight();
            DownloadFlight flight = _flights.GetOrAdd(packId, created);
            flight.Listen(progress);
            try
            {
                return ReferenceEquals(flight, created)
                    ? await RunFlightAsync(packId, created, cancellationToken).ConfigureAwait(false)
                    : await flight.Result.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // The downloader we joined gave up; loop and take over.
            }
            finally
            {
                flight.Unlisten(progress);
            }
        }
    }

    private async Task<PackDownloadResult> RunFlightAsync(string packId, DownloadFlight flight, CancellationToken cancellationToken)
    {
        try
        {
            PackDownloadResult result = await _packs.DownloadAsync(packId, flight, cancellationToken).ConfigureAwait(false);
            System.Diagnostics.Debug.WriteLine($"OCR pack '{packId}': {result}");
            _flights.TryRemove(packId, out _);
            flight.Complete(result);
            return result;
        }
        catch (Exception ex)
        {
            _flights.TryRemove(packId, out _);
            flight.Fail(ex);
            throw;
        }
    }

    private sealed class DownloadFlight : IProgress<double>
    {
        private readonly TaskCompletionSource<PackDownloadResult> _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _gate = new();
        private readonly List<IProgress<double>> _listeners = [];
        private double? _last;

        public Task<PackDownloadResult> Result => _done.Task;

        // A listener that joins mid-download is told the last value at once.
        public void Listen(IProgress<double>? listener)
        {
            if (listener is null)
            {
                return;
            }

            double? last;
            lock (_gate)
            {
                _listeners.Add(listener);
                last = _last;
            }

            if (last is { } value)
            {
                listener.Report(value);
            }
        }

        public void Unlisten(IProgress<double>? listener)
        {
            if (listener is null)
            {
                return;
            }

            lock (_gate)
            {
                _listeners.Remove(listener);
            }
        }

        public void Report(double value)
        {
            IProgress<double>[] snapshot;
            lock (_gate)
            {
                _last = value;
                snapshot = _listeners.ToArray();
            }

            foreach (IProgress<double> listener in snapshot)
            {
                listener.Report(value);
            }
        }

        public void Complete(PackDownloadResult result) => _done.TrySetResult(result);

        public void Fail(Exception exception)
        {
            if (exception is OperationCanceledException)
            {
                _done.TrySetCanceled();
                return;
            }

            _done.TrySetException(exception);
        }
    }
}
