using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// Windows implementation of <see cref="IVoicePackService"/> (Slice 56), mirroring the Android
/// <c>MobileVoicePackService</c>. Packs live under <c>&lt;packsRoot&gt;\&lt;id&gt;\</c>. A download streams
/// into <c>&lt;id&gt;-&lt;version&gt;.zip.partial</c> (resumed with an HTTP Range request; keyed by version so a
/// republished pack never resumes onto stale bytes), is sha256-verified, extracted to a scratch folder,
/// validated against its <c>pack.json</c> and only then moved into place, so a kill at any point never
/// leaves a half-installed pack that <see cref="GetInstalledPath"/> would trust. The retry/resume
/// decisions are the pure, unit-tested <see cref="PackRetryPolicy"/>. The root, HTTP client and
/// connectivity probe are injected so the service is testable without package identity or a network.
/// </summary>
public sealed class WindowsVoicePackService : IVoicePackService
{
    private const string ExtractingSuffix = ".extracting";
    private const int BufferBytes = 81920;

    private static readonly TimeSpan CatalogTimeout = TimeSpan.FromSeconds(30);

    private readonly string _packsRoot;
    private readonly HttpClient _http;
    private readonly Func<bool> _isNetworkAvailable;
    private readonly string _catalogUrl;
    private readonly TimeSpan _stallTimeout;
    private readonly Func<int, TimeSpan> _backoff;
    private IReadOnlyList<PackInfo>? _catalog;
    private DateTime _catalogFetchedUtc;

    public WindowsVoicePackService(
        string packsRoot, HttpClient http, Func<bool> isNetworkAvailable,
        string catalogUrl = PackConstants.CatalogUrl, TimeSpan? stallTimeout = null, Func<int, TimeSpan>? backoff = null)
    {
        _packsRoot = packsRoot;
        _http = http;
        _isNetworkAvailable = isNetworkAvailable;
        _catalogUrl = catalogUrl;
        _stallTimeout = stallTimeout ?? TimeSpan.FromSeconds(60);
        _backoff = backoff ?? PackRetryPolicy.Backoff;
        PackVersionFolders.DeleteOldVersions(packsRoot); // app start: nothing holds the folders of old versions yet
    }

    public async Task<PackCatalogResult> GetCatalogAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(CatalogTimeout);
            string json = await _http.GetStringAsync(_catalogUrl, timeout.Token).ConfigureAwait(false);
            _catalog = PackCatalogParser.Parse(json);
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

    // Every complete pack folder under the packs root. Scratch (.extracting) folders and anything
    // that is not a valid pack id are skipped, as is a folder whose manifest names another pack.
    public IReadOnlyList<InstalledVoicePack> GetInstalledVoicePacks() => InstalledPacks(PackKind.Voice);

    // OCR packs (Slice 52) are installed, validated and listed exactly like voice packs.
    public IReadOnlyList<InstalledVoicePack> GetInstalledOcrPacks() => InstalledPacks(PackKind.Ocr);

    private IReadOnlyList<InstalledVoicePack> InstalledPacks(PackKind kind) =>
        Directory.EnumerateDirectories(PacksRoot())
            .Select(folder => Path.GetFileName(folder))
            .Where(PackNaming.IsValidId)
            .Select(LoadInstalled)
            .OfType<InstalledVoicePack>()
            .Where(p => p.Manifest.Kind == kind)
            .ToList();

    private InstalledVoicePack? LoadInstalled(string packId)
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
                && manifest.MissingFiles(f => File.Exists(Path.Combine(folder, f))).Count == 0;
            return complete ? new InstalledVoicePack(folder, manifest) : null;
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
        {
            // An unreadable pack is skipped, not fatal: the others stay usable.
            return null;
        }
    }

    // A pack that is installed is kept unless the catalogue lists a newer version of it; an update
    // that cannot be fetched leaves the installed pack in place.
    public async Task<PackDownloadResult> DownloadAsync(string packId, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        InstalledVoicePack? installed = LoadInstalled(packId);

        // Hash, extract and copy are heavy: keep all of it off the calling (UI) thread.
        return await Task.Run(() => DownloadOffCallerAsync(packId, installed, progress, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    private async Task<PackDownloadResult> DownloadOffCallerAsync(string packId, InstalledVoicePack? installed, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        PackCatalogResult catalog = _catalog is null
            ? await GetCatalogAsync(cancellationToken).ConfigureAwait(false)
            : new PackCatalogResult(PackDownloadResult.Ok, _catalog);
        if (catalog.Status != PackDownloadResult.Ok)
        {
            return KeepInstalledOr(installed, progress, catalog.Status);
        }

        PackInfo? pack = catalog.Packs.FirstOrDefault(p => p.Id == packId);
        if (installed is not null && (pack is null || !PackVersions.IsNewer(pack.Version, installed.Manifest.Version)))
        {
            progress?.Report(1);
            return PackDownloadResult.Ok; // current (or no longer listed): nothing to fetch
        }

        return pack is null
            ? PackDownloadResult.Failed
            : await DownloadWithRetriesAsync(pack, progress, cancellationToken).ConfigureAwait(false);
    }

    // With no catalogue to compare against, an installed pack is still good.
    private static PackDownloadResult KeepInstalledOr(InstalledVoicePack? installed, IProgress<double>? progress, PackDownloadResult failure)
    {
        if (installed is null)
        {
            return failure;
        }

        progress?.Report(1);
        return PackDownloadResult.Ok;
    }

    private async Task<PackDownloadResult> DownloadWithRetriesAsync(PackInfo pack, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            PackDownloadResult result = await TryDownloadOnceAsync(pack, progress, cancellationToken).ConfigureAwait(false);
            if (!PackRetryPolicy.ShouldRetry(result, attempt))
            {
                return result;
            }

            await Task.Delay(_backoff(attempt), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<PackDownloadResult> TryDownloadOnceAsync(PackInfo pack, IProgress<double>? progress, CancellationToken cancellationToken)
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
    private async Task<PackDownloadResult?> FetchZipAsync(PackInfo pack, string zipPath, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (!_isNetworkAvailable())
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

    private PackDownloadResult InstallVerifiedZip(PackInfo pack, string zipPath, IProgress<double>? progress, CancellationToken cancellationToken)
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

    // Offline wins when the OS reports no network at failure time; otherwise the exception
    // itself decides, so a transient failure on a live network is retried.
    private PackDownloadResult ClassifyFailure(Exception exception) =>
        _isNetworkAvailable() ? PackErrorClassifier.Classify(exception) : PackDownloadResult.Offline;

    private async Task DownloadBodyAsync(PackInfo pack, string partial, IProgress<double>? progress, CancellationToken cancellationToken)
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

        using HttpResponseMessage response = await OpenBodyAsync(pack, partial, offset, cancellationToken).ConfigureAwait(false);
        await StreamBodyAsync(response, pack, partial, offset, progress, cancellationToken).ConfigureAwait(false);
    }

    // Asks for the body from the offset. A 206 whose Content-Range does not start at that offset cannot
    // be appended to the partial file, so it is discarded and the whole file requested again.
    private async Task<HttpResponseMessage> OpenBodyAsync(PackInfo pack, string partial, long offset, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await SendBodyRequestAsync(pack, offset, cancellationToken).ConfigureAwait(false);
        if (offset == 0 || response.StatusCode != HttpStatusCode.PartialContent || response.Content.Headers.ContentRange?.From == offset)
        {
            return response;
        }

        response.Dispose();
        File.Delete(partial);
        return await SendBodyRequestAsync(pack, 0, cancellationToken).ConfigureAwait(false);
    }

    private Task<HttpResponseMessage> SendBodyRequestAsync(PackInfo pack, long offset, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, pack.Url);
        if (offset > 0)
        {
            request.Headers.Range = new RangeHeaderValue(offset, null);
        }

        return _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private async Task StreamBodyAsync(HttpResponseMessage response, PackInfo pack, string partial, long offset, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            File.Delete(partial); // stale partial: the next attempt starts clean
            throw new IOException("The partial download no longer matches the pack.");
        }

        response.EnsureSuccessStatusCode();
        bool resumed = response.StatusCode == HttpStatusCode.PartialContent && offset > 0;
        long written = resumed ? offset : 0; // a 200 means the server ignored Range: restart
        if (resumed)
        {
            progress?.Report(Math.Min(100, written * 100 / Math.Max(1, pack.SizeBytes)) / 100.0); // show where the resume starts
        }

        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var file = new FileStream(partial, resumed ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, BufferBytes, useAsync: true);
        await CopyWithStallGuardAsync(body, file, pack.SizeBytes, written, progress, cancellationToken).ConfigureAwait(false);
    }

    private async Task CopyWithStallGuardAsync(Stream source, Stream destination, long totalBytes, long written, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[BufferBytes];
        using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        int lastPercent = -1;
        while (true)
        {
            stall.CancelAfter(_stallTimeout); // re-arms the one stall timer for this read
            int read = await source.ReadAsync(buffer, stall.Token).ConfigureAwait(false);
            if (read == 0)
            {
                return;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            written += read;
            lastPercent = ReportWhenPercentChanges(progress, written, totalBytes, lastPercent);
        }
    }

    private static int ReportWhenPercentChanges(IProgress<double>? progress, long written, long totalBytes, int lastPercent)
    {
        int percent = (int)Math.Min(100, written * 100 / Math.Max(1, totalBytes));
        if (percent != lastPercent)
        {
            progress?.Report(percent / 100.0);
        }

        return percent;
    }

    private static async Task<bool> HashMatchesAsync(string path, string expectedSha256, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferBytes, useAsync: true);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return string.Equals(expectedSha256.Trim(), Convert.ToHexString(hash), StringComparison.OrdinalIgnoreCase);
    }

    private void Install(PackInfo pack, string verifiedZip, CancellationToken cancellationToken)
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

    // Removes this pack's zips and partials (any version) once it is installed. Matching is exact
    // (id-version.zip and .zip.partial) so another pack whose id merely starts with this one is untouched.
    private void DeleteOrphans(PackInfo pack, CancellationToken cancellationToken)
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

    private static void ValidatePayload(PackInfo pack, string payload)
    {
        string manifestPath = Path.Combine(payload, PackConstants.ManifestFileName);
        PackManifest manifest = PackManifest.Parse(File.ReadAllText(manifestPath));
        if (manifest.Id != pack.Id || manifest.MissingFiles(f => File.Exists(Path.Combine(payload, f))).Count > 0)
        {
            throw new InvalidDataException("The downloaded pack does not match its manifest.");
        }
    }

    private string PacksRoot()
    {
        Directory.CreateDirectory(_packsRoot);
        return _packsRoot;
    }

    // Keyed by id AND version so a republished pack never resumes onto stale bytes.
    private string ZipPath(PackInfo pack) => Path.Combine(PacksRoot(), PackNaming.ZipFileName(pack.Id, pack.Version));
}
