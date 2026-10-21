using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile;

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
    private const int CopyBufferBytes = 81920;
    private const int FileBufferBytes = 81920;

    private static readonly TimeSpan CatalogTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(60);
    private static readonly HttpClient Http = new();

    private IReadOnlyList<PackInfo>? _catalog;

    public async Task<PackCatalogResult> GetCatalogAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(CatalogTimeout);
            string json = await Http.GetStringAsync(PackConstants.CatalogUrl, timeout.Token).ConfigureAwait(false);
            _catalog = PackCatalogParser.Parse(json);
            return new PackCatalogResult(PackDownloadResult.Ok, _catalog);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return PackCatalogResult.Failure(ClassifyFailure(ex));
        }
    }

    public string? GetInstalledPath(string packId) => LoadInstalled(packId)?.Folder;

    // Every complete pack folder under the packs root. Scratch (.extracting) folders and
    // anything that is not a valid pack id are skipped, as is a folder whose manifest
    // names a different pack.
    public IReadOnlyList<InstalledVoicePack> GetInstalledVoicePacks() =>
        Directory.EnumerateDirectories(PacksRoot())
            .Select(folder => Path.GetFileName(folder))
            .Where(PackNaming.IsValidId)
            .Select(LoadInstalled)
            .OfType<InstalledVoicePack>()
            .Where(p => p.Manifest.Kind == PackKind.Voice)
            .ToList();

    private static InstalledVoicePack? LoadInstalled(string packId)
    {
        string folder = Path.Combine(PacksRoot(), packId);
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
        if (GetInstalledPath(packId) is not null)
        {
            progress?.Report(1);
            return PackDownloadResult.Ok;
        }

        // Hash, extract and copy are heavy: keep all of it off the UI thread.
        return await Task.Run(() => DownloadOffUiThreadAsync(packId, progress, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

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
        return pack is null
            ? PackDownloadResult.Failed
            : await DownloadWithRetriesAsync(pack, progress, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<PackDownloadResult> DownloadWithRetriesAsync(PackInfo pack, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            PackDownloadResult result = await TryDownloadOnceAsync(pack, progress, cancellationToken).ConfigureAwait(false);
            if (!PackRetryPolicy.ShouldRetry(result, attempt))
            {
                return result;
            }

            await Task.Delay(PackRetryPolicy.Backoff(attempt), cancellationToken).ConfigureAwait(false);
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
        if (Connectivity.Current.NetworkAccess == NetworkAccess.None)
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
        if (Connectivity.Current.NetworkAccess == NetworkAccess.None)
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

        using var request = new HttpRequestMessage(HttpMethod.Get, pack.Url);
        if (offset > 0)
        {
            request.Headers.Range = new RangeHeaderValue(offset, null);
        }

        using HttpResponseMessage response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await StreamBodyAsync(response, pack, partial, offset, progress, cancellationToken).ConfigureAwait(false);
    }

    private static async Task StreamBodyAsync(HttpResponseMessage response, PackInfo pack, string partial, long offset, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            File.Delete(partial); // stale partial: the next attempt starts clean
            throw new IOException("The partial download no longer matches the pack.");
        }

        response.EnsureSuccessStatusCode();
        bool resumed = response.StatusCode == HttpStatusCode.PartialContent;
        long written = resumed ? offset : 0; // a 200 means the server ignored Range: restart
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var file = new FileStream(partial, resumed ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, FileBufferBytes, useAsync: true);
        await CopyWithStallGuardAsync(body, file, pack.SizeBytes, written, progress, cancellationToken).ConfigureAwait(false);
    }

    private static async Task CopyWithStallGuardAsync(Stream source, Stream destination, long totalBytes, long written, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[CopyBufferBytes];
        using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        int lastPercent = -1;
        while (true)
        {
            stall.CancelAfter(StallTimeout); // re-arms the one stall timer for this read
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
            MovePayloadIntoPlace(payload, Path.Combine(PacksRoot(), pack.Id));
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

    private static void ValidatePayload(PackInfo pack, string payload)
    {
        string manifestPath = Path.Combine(payload, PackConstants.ManifestFileName);
        PackManifest manifest = PackManifest.Parse(File.ReadAllText(manifestPath));
        if (manifest.Id != pack.Id || manifest.MissingFiles(f => File.Exists(Path.Combine(payload, f))).Count > 0)
        {
            throw new InvalidDataException("The downloaded pack does not match its manifest.");
        }
    }

    private static void MovePayloadIntoPlace(string payload, string target)
    {
        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }

        Directory.Move(payload, target);
    }

    private static string PacksRoot()
    {
        string root = Path.Combine(FileSystem.Current.AppDataDirectory, PacksFolder);
        Directory.CreateDirectory(root);
        return root;
    }

    // Keyed by id AND version so a republished pack never resumes onto stale bytes.
    private static string ZipPath(PackInfo pack)
    {
        return Path.Combine(PacksRoot(), PackNaming.ZipFileName(pack.Id, pack.Version));
    }
}
