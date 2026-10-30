using System.Collections.Concurrent;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Mobile.Resources.Strings;
using ReadTheStupidText.Mobile.Shared;

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
    private const string HeldMarker = "OCRPACK held-voice-cancelled";
    private static readonly TimeSpan CatalogMaxAge = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan VoiceHeadStart = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan VoicePollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan VoiceMaxWait = TimeSpan.FromMinutes(30);

    private readonly IVoicePackService _packs;
    private readonly ISettingsStore _settings;
    private readonly VoicePackInstaller _voice;
    private readonly IPackResume? _resume;
    private int _running;
    private string? _heldLanguage; // Slice 186: the language whose scan pack is held back by a voice cancel; started once the voice pack is Ready
    private string? _cancelledLanguage; // the background install the user cancelled from the notification, reported once the guard is clear
    private (string Language, PackDownloadResult Result)? _stopped; // how the background install ended, reported once the guard is clear
    private readonly ConcurrentDictionary<string, DownloadFlight> _flights = new(StringComparer.OrdinalIgnoreCase); // one download per pack id, shared by the background run and a person waiting (Slice 87)

    public OcrPackInstaller(IVoicePackService packs, ISettingsStore settings, ILanguageChangeNotifier languages, VoicePackInstaller voice, IPackResume? resume = null)
    {
        _resume = resume;
        _packs = packs;
        _settings = settings;
        _voice = voice;
        languages.LanguageChanged += (_, _) => _ = EnsureForCurrentLanguageAsync();
        voice.Changed += (_, _) => StartIfHoldLifted();
        UseLocalizedDownloadNotification();
    }

    // Slice 183: this installer is created at every start, so it sets the (localized) text of the download notification once.
    private static void UseLocalizedDownloadNotification()
    {
        PackDownloadHost.NotificationText = fraction => AppResources.Format(AppResources.PackStatus_Downloading, fraction.ToString("P0", System.Globalization.CultureInfo.CurrentUICulture));
        PackDownloadHost.CancelLabel = () => AppResources.Common_Cancel;
        PackDownloadHost.AskNotification = () => PackNotificationAsk.AskOnceAsync(ShowNotificationSheetAsync); // a download that did not start from the first-run page (translation, scan)
    }

    // Throws when no page is up (an automatic resume in the background); the ask then skips and tries again at the next download.
    private static Task<bool> ShowNotificationSheetAsync() => MainThread.InvokeOnMainThreadAsync(() =>
    {
        Page page = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page ?? throw new InvalidOperationException("No page to ask on.");
        return page.DisplayAlertAsync(AppResources.Notify_Title, AppResources.Notify_Body, AppResources.Notify_Continue, AppResources.Notify_NotNow);
    });

    // Slice 186: a retried voice pack that reaches Ready lifts the hold; the held scan pack then follows.
    private void StartIfHoldLifted()
    {
        string? held = Volatile.Read(ref _heldLanguage);
        if (held is null || _voice.State != PackInstallState.Ready || _voice.IsUserCancelled(held))
        {
            return;
        }

        if (!string.Equals(_settings.Language, held, StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.CompareExchange(ref _heldLanguage, null, held); // stale: the language moved on, its own run decides
            return;
        }

        if (Interlocked.CompareExchange(ref _heldLanguage, null, held) == held)
        {
            _ = EnsureForCurrentLanguageAsync();
        }
    }

    public async Task EnsureForCurrentLanguageAsync() => await EnsureBackgroundAsync(null).ConfigureAwait(false);

    // forcedLanguage: an automatic restart reruns the language it was registered with, not whatever is set by then (Slice 182).
    // True when this call took the guard and ran; false when another run holds it.
    private async Task<bool> EnsureBackgroundAsync(string? forcedLanguage)
    {
        if (Interlocked.Exchange(ref _running, 1) == 1)
        {
            return false;
        }

        string? handled = forcedLanguage ?? _settings.Language;
        (string Language, PackDownloadResult Result)? stopped;
        try
        {
            await Task.Run(() => RunAsync(handled)).ConfigureAwait(false);
        }
        finally
        {
            stopped = _stopped;
            _stopped = null;
            Volatile.Write(ref _running, 0);
        }

        ReportEnd(stopped); // after the guard clears, so an immediate restart is never swallowed

        if (LanguageChangedSince(handled))
        {
            await EnsureForCurrentLanguageAsync().ConfigureAwait(false);
        }

        return true;
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
            if (!string.Equals(_settings.Language, language, StringComparison.OrdinalIgnoreCase))
            {
                return; // the language changed during the wait: the restart in the caller handles the new one
            }

            if (_voice.IsUserCancelled(language))
            {
                LogHeld(language); // Slice 186: the user cancelled the voice pack; no scan pack download by itself (opening Scan still offers it)
                Volatile.Write(ref _heldLanguage, language);
                _stopped = (language, PackDownloadResult.Ok); // reported Finished, so an older "ocr:<lang>" resume entry is forgotten
                return;
            }

            await InstallAsync(language).ConfigureAwait(false);
        }
        catch (PackCancelledByNotificationException)
        {
            // Slice 183: Cancel on the download notification is final; reported as Cancelled, never retried.
            _cancelledLanguage = language;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OCR pack install for '{language}' failed: {ex.Message}");
            _stopped = (language, PackDownloadResult.Failed);
        }
    }

    private static void LogHeld(string language)
    {
        string line = $"{HeldMarker} {language}";
        System.Diagnostics.Debug.WriteLine(line);
#if DEBUG
        Shared.Testing.TestHookLog.Write(line);
#endif
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

        _stopped = (language, result);
    }

    // Slice 182: the run reports its end once; Offline or Failed starts again by itself when the network is back or the app returns.
    private void ReportEnd((string Language, PackDownloadResult Result)? stopped)
    {
        if (Interlocked.Exchange(ref _cancelledLanguage, null) is { } cancelled)
        {
            _resume?.Report($"ocr:{cancelled}", PackRunEnd.Cancelled, () => Task.FromResult(false));
            return;
        }

        if (stopped is { } s)
        {
            _resume?.Report($"ocr:{s.Language}", IPackResume.EndOf(s.Result), () => ResumeAsync(s.Language));
        }
    }

    // False while another run (maybe for another language) holds the guard; the entry stays and the next trigger offers it again.
    // A run that finds the pack already current ends Ok, which reports Finished and clears the entry.
    private Task<bool> ResumeAsync(string language) => EnsureBackgroundAsync(language);

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
            catch (OperationCanceledException ex) when (ex is not PackCancelledByNotificationException && !cancellationToken.IsCancellationRequested)
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
