using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using ReadTheStupidText.Application;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// Android implementation of <see cref="IVoicePackService"/> (Slice 50). Packs live
/// under <c>AppDataDirectory/packs/&lt;id&gt;/</c>. A download streams into
/// <c>&lt;id&gt;-&lt;version&gt;.zip.partial</c> (resumed with an HTTP Range request; keyed by
/// version so a republished pack never resumes onto stale bytes), is sha256-verified,
/// extracted to a scratch folder, validated against its <c>pack.json</c>, and only
/// then moved into place, so a kill at any point never leaves a half-installed pack
/// that <see cref="GetInstalledPath"/> would trust. A verified zip left by a killed
/// install is installed on the next run instead of being downloaded again. The whole
/// pipeline runs off the UI thread; the retry/resume decisions are the pure,
/// unit-tested <see cref="PackRetryPolicy"/>.
/// </summary>
public sealed class MobileVoicePackService : IVoicePackService
{
    private const string PacksFolder = "packs";
    private const string ExtractingSuffix = ".extracting";
    private const string RemovingSuffix = ".removing";
    private const int CopyBufferBytes = 81920;
    private const int FileBufferBytes = 81920;

    private static readonly TimeSpan CatalogTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(60);
    private static readonly HttpClient Http = new();

    public event EventHandler? InstalledPacksChanged;

    private IReadOnlyList<PackInfo>? _catalog;
    private DateTime _catalogFetchedUtc;

    public MobileVoicePackService()
    {
        PackVersionFolders.DeleteOldVersions(PacksRoot()); // app start: nothing holds the folders of old versions yet
    }

    public async Task<PackCatalogResult> GetCatalogAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(CatalogTimeout);
            string json = await Http.GetStringAsync(PackConstants.CatalogUrl, timeout.Token).ConfigureAwait(false);
            _catalog = PackCatalogParser.Parse(json);
            PackDownloadHost.NoteCatalog(_catalog); // Slice 208: the notifications name a pack's language and size
            _catalogFetchedUtc = DateTime.UtcNow;
            return new PackCatalogResult(PackDownloadResult.Ok, _catalog);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return PackCatalogResult.Failure(ClassifyFailure(ex));
        }
    }

    public Task<PackCatalogResult> GetCatalogCachedAsync(TimeSpan maxAge, CancellationToken cancellationToken)
    {
        IReadOnlyList<PackInfo>? cached = _catalog;
        bool fresh = cached is not null && DateTime.UtcNow - _catalogFetchedUtc <= maxAge;
        return fresh ? Task.FromResult(new PackCatalogResult(PackDownloadResult.Ok, cached!)) : GetCatalogAsync(cancellationToken);
    }

    public string? GetInstalledPath(string packId) => LoadInstalled(packId)?.Folder;

    // Every complete pack folder under the packs root. Scratch (.extracting) folders and
    // anything that is not a valid pack id are skipped, as is a folder whose manifest
    // names a different pack.
    public IReadOnlyList<InstalledVoicePack> GetInstalledVoicePacks() => InstalledPacks(PackKind.Voice);

    // OCR packs (Slice 52) are installed, validated and listed exactly like voice packs.
    public IReadOnlyList<InstalledVoicePack> GetInstalledOcrPacks() => InstalledPacks(PackKind.Ocr);

    // Dino's storyteller packs (Slice 114): listed apart from the voice packs so the text reader never sees one.
    public IReadOnlyList<InstalledVoicePack> GetInstalledStorytellerPacks() => InstalledPacks(PackKind.Storyteller);

    public bool SupportsStorytellerPacks => true;

    public IReadOnlyList<InstalledVoicePack> GetInstalledVoiceAndStorytellerPacks() =>
        InstalledPacks(kind => kind is PackKind.Voice or PackKind.Storyteller);

    // Removes the whole pack (every version) and raises InstalledPacksChanged. The folder is first renamed out of the way: a rename fails as
    // a whole when a loaded model still holds a file (Windows), leaving the pack intact, where a half-finished delete would leave it broken.
    // Only the cast screen calls it, behind the grown-up gate, after it stopped the reader.
    public bool Uninstall(string packId)
    {
        if (!PackNaming.IsValidId(packId))
        {
            return false;
        }

        string folder = Path.Combine(PacksRoot(), packId);
        if (!Directory.Exists(folder))
        {
            return false;
        }

        string removing = folder + RemovingSuffix;
        try
        {
            DeleteQuietly(removing);
            Directory.Move(folder, removing);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"Could not remove pack '{packId}': {ex.Message}");
            return false;
        }

        InstalledPacksChanged?.Invoke(this, EventArgs.Empty);
        DeleteQuietly(removing);
        return true;
    }

    // The renamed folder is not a pack id, so no scan sees it; a delete that fails is retried by the next removal of this pack.
    private static void DeleteQuietly(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"Could not delete '{folder}': {ex.Message}");
        }
    }

    private static IReadOnlyList<InstalledVoicePack> InstalledPacks(PackKind kind) => InstalledPacks(k => k == kind);

    private static IReadOnlyList<InstalledVoicePack> InstalledPacks(Func<PackKind, bool> wanted) =>
        Directory.EnumerateDirectories(PacksRoot())
            .Select(folder => Path.GetFileName(folder))
            .Where(PackNaming.IsValidId)
            .Select(LoadInstalled)
            .OfType<InstalledVoicePack>()
            .Where(p => wanted(p.Manifest.Kind))
            .ToList();

    private static InstalledVoicePack? LoadInstalled(string packId)
    {
        string folder = PackVersionFolders.Resolve(Path.Combine(PacksRoot(), packId));
        string manifestPath = Path.Combine(folder, PackConstants.ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            PackManifest manifest = PackManifest.Parse(File.ReadAllText(manifestPath));
            bool complete = manifest.Id == packId
                && manifest.MissingFiles(f => File.Exists(Path.Combine(folder, f))).Count == 0
                && manifest.BrokenTokenFoldersIn(folder).Count == 0; // Slice 201: a pack sherpa would abort on is not installed
            LogBrokenTokens(packId, manifest, folder);
            return complete ? new InstalledVoicePack(folder, manifest) : null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An unreadable pack is skipped, not fatal: the others stay usable.
            System.Diagnostics.Debug.WriteLine($"Skipping voice pack '{packId}': {ex.Message}");
            return null;
        }
    }

    public async Task<PackDownloadResult> DownloadAsync(string packId, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        using IDisposable gate = await PackDownloadGate.EnterAsync(packId, cancellationToken).ConfigureAwait(false); // one run per pack (Slice 173)
        if (LoadInstalled(packId) is { } installed && !UpgradeAvailable(installed))
        {
            PackDownloadHost.ClearPaused(packId); // already there: a notice left from before has nothing to ask any more
            progress?.Report(1);
            return PackDownloadResult.Ok;
        }

        return await RunWithKeepAliveAsync(packId, progress, cancellationToken).ConfigureAwait(false);
    }

    // Slice 183: a foreground service (Android) holds the process and the network while the phone is locked. The user's Cancel on its notification
    // also stops this run; the caller's own token stays what ends a run the caller asked to stop.
    private async Task<PackDownloadResult> RunWithKeepAliveAsync(string packId, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        using var notificationCancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var cancelListener = new NotificationCancelListener(packId, notificationCancel);
        if (!PackResumeScope.IsAutomatic)
        {
            _ = PackDownloadHost.AskNotification?.Invoke(packId); // never awaited: the download does not wait for the question; never asked for an automatic restart
        }

        using IPackKeepAliveScope? keepAlive = BeginKeepAlive(packId);
        double? lastFraction = null;
        var scoped = new InlineProgress(value =>
        {
            lastFraction = value;
            keepAlive?.Progress(value);
            progress?.Report(value);
        });

        PackDownloadHost.ClearPaused(packId); // Slice 208: this run is the resume the "Download paused" notice asked for
        // Hash, extract and copy are heavy: keep all of it off the UI thread.
        try
        {
            PackDownloadResult result = await Task.Run(() => DownloadOffUiThreadAsync(packId, scoped, notificationCancel.Token), notificationCancel.Token).ConfigureAwait(false);
            SettlePausedNotice(packId, result, lastFraction);
            return result;
        }
        catch (OperationCanceledException) when (notificationCancel.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            PackDownloadHost.ClearPaused(packId);
            throw new PackCancelledByNotificationException(packId); // the caller's token did not fire: it was the notification's Cancel
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            PackDownloadHost.ClearPaused(packId); // the person stopped it: nothing is paused
            throw;
        }
        catch (Exception ex) when (IsResumableFailure(ex))
        {
            PackDownloadHost.Paused(packId, lastFraction); // a network stop or an HttpClient timeout; the platform decides whether a notice is due (background only)
            throw;
        }
        catch (Exception)
        {
            PackDownloadHost.ClearPaused(packId); // nothing the person can finish by tapping: no notice
            throw;
        }
    }

    private static bool IsResumableFailure(Exception ex) =>
        ex is OperationCanceledException or HttpRequestException or System.Net.Sockets.SocketException or TimeoutException;

    // Slice 208: the notice is decided where the run ends. Finished or a failure nothing can resume clears it; a stop that waiting or the network can
    // end (Offline, Failed) posts it, and the platform posts only when the app is in the background (Android: after the time limit stopped the service).
    private static void SettlePausedNotice(string packId, PackDownloadResult result, double? lastFraction)
    {
        if (PackResumePolicy.IsResumableResult(result))
        {
            PackDownloadHost.Paused(packId, lastFraction);
            return;
        }

        PackDownloadHost.ClearPaused(packId);
    }

    /// <summary>
    /// Launch scan for half-finished downloads (Slice 208). This service owns the pack files, so the scan asks it, under the gate that starts every
    /// download, whether a pack is in flight or queued: such a pack is never touched. A partial nothing wants is deleted under that gate (a delete and a
    /// start cannot interleave), after the wanted set is read again, together with the iOS resume sidecar; a partial that is not clearly the
    /// catalog's version of a wanted pack is kept. Nothing is downloaded here: <see cref="StalePartialScan.Resumable"/> lists the wanted packs whose partial
    /// the caller's normal start entry finishes (Wi-Fi rule, consent and the person's cancel apply there). Needs the catalog (none: every partial stays).
    /// Never throws.
    /// </summary>
    /// <param name="languages">Asked again just before each delete: the languages the settings want right now.</param>
    public async Task<StalePartialScan> ScanStalePartialsAsync(Func<IReadOnlyCollection<string>> languages, CancellationToken cancellationToken)
    {
        try
        {
            List<FoundPartial> partials = FindPartials();
            if (partials.Count == 0)
            {
                return StalePartialScan.Empty;
            }

            PackCatalogResult catalog = await GetCatalogCachedAsync(StalePartialCatalogAge, cancellationToken).ConfigureAwait(false);
            if (catalog.Status != PackDownloadResult.Ok)
            {
                return StalePartialScan.Empty;
            }

            IReadOnlyList<StalePartialDecision> decisions = DecideAll(partials, catalog.Packs, languages());
            int deleted = await DeleteUnwantedAsync(decisions, catalog.Packs, languages, cancellationToken).ConfigureAwait(false);
            string[] resumable = [.. decisions.Where(d => d.Action == StalePartialAction.Resume).Select(d => d.PackId)];
            foreach (string packId in resumable)
            {
                PackDownloadHost.LogService(StalePartialResumeMarker, packId);
            }

            return new StalePartialScan(deleted, resumable);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Debug.WriteLine($"Stale partial scan failed: {ex.Message}");
            return StalePartialScan.Empty;
        }
    }

    private static readonly TimeSpan StalePartialCatalogAge = TimeSpan.FromHours(6);

    private static IReadOnlyList<StalePartialDecision> DecideAll(IEnumerable<FoundPartial> found, IReadOnlyList<PackInfo> packs, IReadOnlyCollection<string> languages)
    {
        // The wanted packs are exactly the ones the app's own start picks for each language (VoicePackSelector, OcrPackSelector): one source of truth, so a
        // pack the start would never resume is not "wanted" and a selected pack is never deleted. A translation pair, Dino's storyteller voice and a scan
        // pack of a language not asked for here (a scanned page can be in any language) are not decided by the settings seen here: their partial is kept.
        IReadOnlySet<string> wanted = languages
            .SelectMany(l => new[] { VoicePackSelector.Select(packs, l), OcrPackSelector.Select(packs, l) })
            .OfType<PackInfo>().Select(p => p.Id).ToHashSet();
        IReadOnlySet<string> undetermined = packs
            .Where(p => p.Kind is PackKind.Translation or PackKind.Storyteller || (p.Kind == PackKind.Ocr && !wanted.Contains(p.Id)))
            .Select(p => p.Id).ToHashSet();
        IReadOnlyDictionary<string, string> versions = packs.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First().Version);
        return StalePartialPolicy.Decide(found, wanted, versions, undetermined);
    }

    private static List<FoundPartial> FindPartials()
    {
        var found = new List<FoundPartial>();
        try
        {
            foreach (string file in Directory.EnumerateFiles(PacksRoot()).Where(f => f.EndsWith(PackNaming.PartialSuffix, StringComparison.Ordinal)))
            {
                found.Add(new FoundPartial(Path.GetFileName(file), new FileInfo(file).Length));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Could not list partial downloads: {ex.Message}");
        }

        return found;
    }

    private static async Task<int> DeleteUnwantedAsync(
        IEnumerable<StalePartialDecision> decisions, IReadOnlyList<PackInfo> packs, Func<IReadOnlyCollection<string>> languages, CancellationToken cancellationToken)
    {
        int deleted = 0;
        foreach (StalePartialDecision decision in decisions.Where(d => d.Action == StalePartialAction.Delete))
        {
            if (await TryDeleteIdleAsync(decision, packs, languages).ConfigureAwait(false))
            {
                deleted++;
            }
        }

        return deleted;
    }

    // The pack is taken through the same per-pack gate every download enters: a pack in flight (or queued ahead of us) holds it, so we leave it alone, and once
    // we hold it nothing can start this pack until the file is gone. Under the gate the file and the wanted set are read again.
    private static async Task<bool> TryDeleteIdleAsync(
        StalePartialDecision decision, IReadOnlyList<PackInfo> packs, Func<IReadOnlyCollection<string>> languages)
    {
        IDisposable? gate = PackDownloadGate.TryEnter(decision.PackId);
        if (gate is null)
        {
            return false; // in flight or held by another caller: not ours to delete
        }

        using (gate)
        {
            return await DeleteHeldAsync(decision, packs, languages).ConfigureAwait(false);
        }
    }

    private static async Task<bool> DeleteHeldAsync(StalePartialDecision decision, IReadOnlyList<PackInfo> packs, Func<IReadOnlyCollection<string>> languages)
    {
        string path = Path.Combine(PacksRoot(), decision.FileName);
        try
        {
            if (!File.Exists(path) || await PackDownloadHost.HasLiveTransferAsync(path).ConfigureAwait(false))
            {
                return false; // gone already, or a background transfer (iOS) still writes it
            }

            var fresh = new FoundPartial(decision.FileName, new FileInfo(path).Length);
            if (DecideAll([fresh], packs, languages()) is not [{ Action: StalePartialAction.Delete }])
            {
                return false; // the settings or the file changed since the scan began
            }

            File.Delete(path);
            PackDownloadHost.DiscardTransferLeftovers(path);
            PackDownloadHost.LogService(StalePartialDeleteMarker, decision.PackId);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Could not delete the partial of '{decision.PackId}': {ex.Message}");
            return false;
        }
    }

    internal const string StalePartialResumeMarker = "stale-partial resume";
    internal const string StalePartialDeleteMarker = "stale-partial delete";

    // Subscribes to the notification's Cancel for one pack; disposing it unsubscribes, whatever way the run ends.
    private sealed class NotificationCancelListener : IDisposable
    {
        private readonly string _packId;
        private readonly CancellationTokenSource _cancel;

        public NotificationCancelListener(string packId, CancellationTokenSource cancel)
        {
            _packId = packId;
            _cancel = cancel;
            PackDownloadHost.CancelRequested += OnCancelRequested;
        }

        public void Dispose() => PackDownloadHost.CancelRequested -= OnCancelRequested;

        private void OnCancelRequested(string id)
        {
            if (id != _packId)
            {
                return;
            }

            try
            {
                _cancel.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The run already ended and disposed its source: nothing left to cancel.
            }
        }
    }

    // Null where the platform has no keep-alive. The caller's token also stops when the user taps Cancel on the notification.
    private static IPackKeepAliveScope? BeginKeepAlive(string packId)
    {
        if (PackDownloadHost.KeepAlive is not { } host)
        {
            return null;
        }

        try
        {
            IPackKeepAliveScope scope = host.Begin(packId);
            PackDownloadHost.LogService(PackDownloadHost.StartAction, packId);
            return new LoggedScope(scope, packId);
        }
        catch (Exception ex)
        {
            PackDownloadHost.LogService("start-refused", ex.GetType().Name); // best effort: the download runs without a keep-alive
            return null;
        }
    }

    private sealed class LoggedScope(IPackKeepAliveScope inner, string packId) : IPackKeepAliveScope
    {
        public void Progress(double fraction) => inner.Progress(fraction);

        public void Dispose()
        {
            inner.Dispose();
            PackDownloadHost.LogService(PackDownloadHost.StopAction, packId);
        }
    }

    // An installed pack is re-downloaded only when the catalog we last fetched lists a newer version
    // of it. The old pack stays in use until the new one has verified and been installed.
    private bool UpgradeAvailable(InstalledVoicePack installed) =>
        _catalog?.FirstOrDefault(p => p.Id == installed.Manifest.Id) is { } listed
        && PackVersions.IsNewer(listed.Version, installed.Manifest.Version);

    private async Task<PackDownloadResult> DownloadOffUiThreadAsync(string packId, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        PackCatalogResult catalog = _catalog is null
            ? await GetCatalogAsync(cancellationToken).ConfigureAwait(false)
            : new PackCatalogResult(PackDownloadResult.Ok, _catalog);
        if (catalog.Status != PackDownloadResult.Ok)
        {
            return catalog.Status;
        }

        PackInfo? pack = catalog.Packs.FirstOrDefault(p => p.Id == packId);
        if (pack is null)
        {
            return PackDownloadResult.Failed;
        }

        PackDownloadResult result = await DownloadWithRetriesAsync(pack, progress, cancellationToken).ConfigureAwait(false);
        if (result == PackDownloadResult.Ok)
        {
            InstalledPacksChanged?.Invoke(this, EventArgs.Empty);
        }

        return result;
    }

    private static async Task<PackDownloadResult> DownloadWithRetriesAsync(PackInfo pack, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        long started = Stopwatch.GetTimestamp();
        for (int attempt = 1; ; attempt++)
        {
            PackDownloadResult result = await TryDownloadOnceAsync(pack, progress, cancellationToken).ConfigureAwait(false);
            if (!PackRetryPolicy.ShouldRetry(result, attempt))
            {
                LogOutcome(pack, attempt, result, started);
                return result;
            }

            TimeSpan backoff = PackRetryPolicy.Backoff(attempt);
            PackDownloadLog.Retry(pack.Id, attempt, result, backoff);
            await Task.Delay(backoff, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void LogOutcome(PackInfo pack, int attempt, PackDownloadResult result, long started)
    {
        if (result == PackDownloadResult.Ok)
        {
            PackDownloadLog.Done(pack.Id, pack.SizeBytes, Stopwatch.GetElapsedTime(started));
        }
        else
        {
            PackDownloadLog.Failed(pack.Id, attempt, result);
        }
    }

    private static async Task<PackDownloadResult> TryDownloadOnceAsync(PackInfo pack, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        string zipPath = ZipPath(pack);
        try
        {
            if (!await HasVerifiedZipAsync(pack, zipPath, cancellationToken).ConfigureAwait(false)
                && await FetchZipAsync(pack, zipPath, progress, cancellationToken).ConfigureAwait(false) is { } failure)
            {
                return failure;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return ClassifyFailure(ex);
        }

        return InstallVerifiedZip(pack, zipPath, progress, cancellationToken);
    }

    // A verified zip survives only if an earlier run was killed between verify and install.
    private static async Task<bool> HasVerifiedZipAsync(PackInfo pack, string zipPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(zipPath))
        {
            return false;
        }

        if (await HashMatchesAsync(zipPath, pack.Sha256, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        File.Delete(zipPath);
        return false;
    }

    // Returns null when the verified zip is in place, otherwise the failure.
    private static async Task<PackDownloadResult?> FetchZipAsync(PackInfo pack, string zipPath, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (Connectivity.Current.NetworkAccess == Microsoft.Maui.Networking.NetworkAccess.None)
        {
            return PackDownloadResult.Offline;
        }

        string partial = zipPath + PackNaming.PartialSuffix;
        await DownloadBodyAsync(pack, partial, progress, cancellationToken).ConfigureAwait(false);
        if (!await HashMatchesAsync(partial, pack.Sha256, cancellationToken).ConfigureAwait(false))
        {
            File.Delete(partial);
            return PackDownloadResult.HashMismatch;
        }

        File.Move(partial, zipPath, overwrite: true);
        return null;
    }

    private static PackDownloadResult InstallVerifiedZip(PackInfo pack, string zipPath, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        try
        {
            Install(pack, zipPath, cancellationToken);
            DeleteOrphans(pack, cancellationToken);
            progress?.Report(1);
            return PackDownloadResult.Ok;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return PackErrorClassifier.ClassifyInstall(ex);
        }
    }

    // Offline wins only when the OS says there is no network at all at failure time
    // (Android's handler surfaces Java exceptions that don't always look like sockets).
    // ConstrainedInternet (captive portal) and Local still have a route, so they fall
    // through to the exception classifier and a transient failure is retried; Unknown
    // means the OS cannot tell, so the exception itself is the better evidence.
    private static PackDownloadResult ClassifyFailure(Exception exception)
    {
        if (Connectivity.Current.NetworkAccess == Microsoft.Maui.Networking.NetworkAccess.None)
        {
            return PackDownloadResult.Offline;
        }

        return PackErrorClassifier.Classify(exception);
    }

    private static async Task DownloadBodyAsync(PackInfo pack, string partial, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        long existing = File.Exists(partial) ? new FileInfo(partial).Length : 0;
        long offset = PackRetryPolicy.ResumeOffset(existing, pack.SizeBytes);
        if (offset == 0 && existing > 0)
        {
            File.Delete(partial);
        }

        if (offset == pack.SizeBytes)
        {
            return; // body already complete; only verification is left
        }

        PackDownloadLog.Start(pack.Id, offset);

        if (PackDownloadHost.BodyFetcher is { } fetcher)
        {
            await fetcher.FetchAsync(pack, partial, offset, progress, cancellationToken).ConfigureAwait(false);
            return;
        }

        using GuardedResponse response = await StallGuardedRead.SendAsync(Http, () => BuildBodyRequest(pack, offset), StallGuardedRead.HeaderTimeout(StallTimeout), cancellationToken, new DownloadTag(pack.Id, offset)).ConfigureAwait(false);
        await StreamBodyAsync(response, pack, partial, offset, progress, cancellationToken).ConfigureAwait(false);
    }

    private static HttpRequestMessage BuildBodyRequest(PackInfo pack, long offset)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, pack.Url);
        if (offset > 0)
        {
            PackDownloadLog.Range(pack.Id, offset);
            request.Headers.Range = new RangeHeaderValue(offset, null);
        }

        return request;
    }

    private static async Task StreamBodyAsync(GuardedResponse guarded, PackInfo pack, string partial, long offset, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = guarded.Message;
        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            File.Delete(partial); // stale partial: the next attempt starts clean
            throw new IOException("The partial download no longer matches the pack.");
        }

        response.EnsureSuccessStatusCode();
        bool resumed = response.StatusCode == HttpStatusCode.PartialContent;
        long written = resumed ? offset : 0; // a 200 means the server ignored Range: restart
        await using Stream body = await guarded.OpenBodyAsync(StallTimeout, cancellationToken, new DownloadTag(pack.Id, written)).ConfigureAwait(false);
        await using var file = new FileStream(partial, resumed ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, FileBufferBytes, useAsync: true);
        await CopyWithStallGuardAsync(body, file, pack.SizeBytes, written, progress, cancellationToken).ConfigureAwait(false);
    }

    private static async Task CopyWithStallGuardAsync(Stream source, Stream destination, long totalBytes, long written, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[CopyBufferBytes];
        int lastPercent = -1;
#if DEBUG
        bool throttled = PackDownloadThrottle.IsOn();
#endif
        while (true)
        {
            int read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
#if DEBUG
            if (throttled)
            {
                await PackDownloadThrottle.PauseAsync(read, cancellationToken).ConfigureAwait(false);
            }
#endif
            written += read;
            lastPercent = ReportWhenPercentChanges(progress, written, totalBytes, lastPercent);
        }
    }

    private static int ReportWhenPercentChanges(IProgress<double>? progress, long written, long totalBytes, int lastPercent)
    {
        int percent = (int)Math.Min(100, written * 100 / totalBytes);
        if (percent != lastPercent)
        {
            progress?.Report(percent / 100.0);
        }

        return percent;
    }

    private static async Task<bool> HashMatchesAsync(string path, string expectedSha256, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileBufferBytes, useAsync: true);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return string.Equals(expectedSha256.Trim(), Convert.ToHexString(hash), StringComparison.OrdinalIgnoreCase);
    }

    private static void Install(PackInfo pack, string verifiedZip, CancellationToken cancellationToken)
    {
        string scratch = Path.Combine(PacksRoot(), pack.Id + ExtractingSuffix);
        DeleteDirectoryIfExists(scratch);
        try
        {
            ZipFile.ExtractToDirectory(verifiedZip, scratch);
            cancellationToken.ThrowIfCancellationRequested();
            string payload = LocatePayload(scratch);
            ValidatePayload(pack, payload);
            PackVersionFolders.Install(payload, Path.Combine(PacksRoot(), pack.Id), pack.Version);
        }
        finally
        {
            // A flat zip's payload IS the scratch folder and was just moved away.
            DeleteDirectoryIfExists(scratch);
        }
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    // Removes this pack's zips and partials (any version) once it is installed.
    // Matching is exact (<id>-<version>.zip[.partial]) so another pack whose id
    // merely starts with this one's is never touched.
    private static void DeleteOrphans(PackInfo pack, CancellationToken cancellationToken)
    {
        foreach (string file in Directory.EnumerateFiles(PacksRoot()))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (PackNaming.IsOrphanFileName(Path.GetFileName(file), pack.Id))
            {
                File.Delete(file);
            }
        }
    }

    // The zip has one top-level folder holding pack.json; accept a flat zip too.
    private static string LocatePayload(string scratch)
    {
        string[] folders = Directory.GetDirectories(scratch);
        bool nested = folders.Length == 1 && File.Exists(Path.Combine(folders[0], PackConstants.ManifestFileName));
        return nested ? folders[0] : scratch;
    }

    // Debug-only marker (Slice 201): a device check can tell "pack rejected for its tokens.txt" from "pack missing".
    private static void LogBrokenTokens(string packId, PackManifest manifest, string folder)
    {
        if (manifest.BrokenTokenFoldersIn(folder).Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"RTST_PACK_TOKENS_REJECTED pack={packId} version={manifest.Version}");
        }
    }

    private static void ValidatePayload(PackInfo pack, string payload)
    {
        string manifestPath = Path.Combine(payload, PackConstants.ManifestFileName);
        PackManifest manifest = PackManifest.Parse(File.ReadAllText(manifestPath));
        if (manifest.Id != pack.Id || manifest.MissingFiles(f => File.Exists(Path.Combine(payload, f))).Count > 0)
        {
            throw new InvalidDataException("The downloaded pack does not match its manifest.");
        }

        if (manifest.BrokenTokenFoldersIn(payload).Count > 0)
        {
            throw new InvalidDataException("The downloaded pack has a tokens.txt the speech engine cannot read.");
        }
    }

    /// <summary>The folder every pack lives in (created if needed); also read by the iOS backup exclusion.</summary>
    internal static string PacksRoot()
    {
#if WINDOWS
        // Windows (Slice 67): the package's LocalCacheFolder, never the cloud-backed LocalFolder, as the
        // Read The Stupid Text app keeps its packs (Slice 56).
        string root = Path.Combine(FileSystem.Current.CacheDirectory, PacksFolder);
#else
        string root = Path.Combine(FileSystem.Current.AppDataDirectory, PacksFolder);
#endif
        Directory.CreateDirectory(root);
        return root;
    }

    // Keyed by id AND version so a republished pack never resumes onto stale bytes.
    private static string ZipPath(PackInfo pack)
    {
        return Path.Combine(PacksRoot(), PackNaming.ZipFileName(pack.Id, pack.Version));
    }
}
