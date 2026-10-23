using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Infrastructure.Reading;

namespace ReadTheStupidText.Tests;

public sealed class WindowsVoicePackServiceTests : IDisposable
{
    private const string CatalogUrl = "https://test.invalid/packs.json";
    private const string PackUrl = "https://test.invalid/supertonic-3-1.zip";
    private const string PackId = "supertonic-3";
    private const string PayloadFile = "model.onnx";

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("rtst-packs-");

    public void Dispose() => _root.Delete(recursive: true);

    private static byte[] BuildZip(string manifestId = PackId, bool includePayload = true, string version = "1")
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, $"{PackId}/pack.json", $$"""
                { "id": "{{manifestId}}", "kind": "voice", "language": "en", "version": "{{version}}",
                  "engine": "sherpa-onnx", "files": ["{{PayloadFile}}"] }
                """);
            if (includePayload)
            {
                Add(zip, $"{PackId}/{PayloadFile}", new string('x', 4096));
            }
        }

        return buffer.ToArray();
    }

    private static void Add(ZipArchive zip, string name, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open());
        writer.Write(content);
    }

    private static string Sha256Of(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static string CatalogJson(byte[] zip, string? sha = null, string version = "1") => $$"""
        [ { "id": "{{PackId}}", "language": "en", "kind": "voice", "version": "{{version}}",
            "sizeBytes": {{zip.Length}}, "sha256": "{{sha ?? Sha256Of(zip)}}", "license": "Apache-2.0", "url": "{{PackUrl}}" } ]
        """;

    private WindowsVoicePackService Service(FakeServer server, Func<bool>? network = null) =>
        new(_root.FullName, new HttpClient(server), network ?? (() => true), CatalogUrl,
            stallTimeout: TimeSpan.FromSeconds(5), backoff: _ => TimeSpan.Zero);

    [Fact]
    public async Task Download_installs_the_pack_and_leaves_no_zip_behind()
    {
        byte[] zip = BuildZip();
        var server = new FakeServer(CatalogJson(zip), zip);
        var progress = new List<double>();

        PackDownloadResult result = await Service(server).DownloadAsync(PackId, new Progress<double>(progress.Add), CancellationToken.None);

        Assert.Equal(PackDownloadResult.Ok, result);
        var service = Service(server);
        Assert.NotNull(service.GetInstalledPath(PackId));
        Assert.Equal(PackId, Assert.Single(service.GetInstalledVoicePacks()).Manifest.Id);
        Assert.Empty(Directory.EnumerateFiles(_root.FullName));
        Assert.Empty(Directory.EnumerateDirectories(_root.FullName, "*.extracting"));
    }

    [Fact]
    public async Task Download_resumes_a_partial_file_with_a_range_request()
    {
        byte[] zip = BuildZip();
        int already = zip.Length / 3;
        File.WriteAllBytes(Path.Combine(_root.FullName, "supertonic-3-1.zip.partial"), zip[..already]);
        var server = new FakeServer(CatalogJson(zip), zip);

        PackDownloadResult result = await Service(server).DownloadAsync(PackId, null, CancellationToken.None);

        Assert.Equal(PackDownloadResult.Ok, result);
        Assert.Equal(already, server.LastRangeStart);
    }

    [Fact]
    public async Task A_server_that_ignores_range_restarts_the_file()
    {
        byte[] zip = BuildZip();
        File.WriteAllBytes(Path.Combine(_root.FullName, "supertonic-3-1.zip.partial"), zip[..100]);
        var server = new FakeServer(CatalogJson(zip), zip) { HonourRange = false };

        PackDownloadResult result = await Service(server).DownloadAsync(PackId, null, CancellationToken.None);

        Assert.Equal(PackDownloadResult.Ok, result);
    }

    [Fact]
    public async Task A_wrong_hash_is_rejected_after_one_clean_redownload()
    {
        byte[] zip = BuildZip();
        var server = new FakeServer(CatalogJson(zip, sha: new string('0', 64)), zip);
        var service = Service(server);

        PackDownloadResult result = await service.DownloadAsync(PackId, null, CancellationToken.None);

        Assert.Equal(PackDownloadResult.HashMismatch, result);
        Assert.Equal(PackRetryPolicy.MaxHashMismatchAttempts, server.PackRequests);
        Assert.Null(service.GetInstalledPath(PackId));
        Assert.Empty(Directory.EnumerateFiles(_root.FullName));
    }

    [Fact]
    public async Task A_pack_whose_manifest_names_another_pack_is_invalid_and_not_retried()
    {
        byte[] zip = BuildZip(manifestId: "other-pack");
        var server = new FakeServer(CatalogJson(zip), zip);

        PackDownloadResult result = await Service(server).DownloadAsync(PackId, null, CancellationToken.None);

        Assert.Equal(PackDownloadResult.InvalidPack, result);
        Assert.Equal(1, server.PackRequests);
    }

    [Fact]
    public async Task A_pack_missing_a_listed_file_is_invalid()
    {
        byte[] zip = BuildZip(includePayload: false);
        var server = new FakeServer(CatalogJson(zip), zip);

        PackDownloadResult result = await Service(server).DownloadAsync(PackId, null, CancellationToken.None);

        Assert.Equal(PackDownloadResult.InvalidPack, result);
        Assert.Null(Service(server).GetInstalledPath(PackId));
    }

    [Fact]
    public async Task Offline_stops_at_once_without_a_download_attempt()
    {
        byte[] zip = BuildZip();
        var server = new FakeServer(CatalogJson(zip), zip) { FailCatalog = true };

        PackCatalogResult catalog = await Service(server, network: () => false).GetCatalogAsync(CancellationToken.None);

        Assert.Equal(PackDownloadResult.Offline, catalog.Status);
        Assert.Equal(0, server.PackRequests);
    }

    [Fact]
    public async Task A_name_resolution_failure_on_a_live_network_is_offline()
    {
        var server = new FakeServer("[]", []) { FailCatalog = true };

        PackCatalogResult catalog = await Service(server).GetCatalogAsync(CancellationToken.None);

        Assert.Equal(PackDownloadResult.Offline, catalog.Status);
    }

    [Fact]
    public async Task A_transient_server_error_is_retried_then_succeeds()
    {
        byte[] zip = BuildZip();
        var server = new FakeServer(CatalogJson(zip), zip) { FailFirstPackRequests = 1 };

        PackDownloadResult result = await Service(server).DownloadAsync(PackId, null, CancellationToken.None);

        Assert.Equal(PackDownloadResult.Ok, result);
        Assert.Equal(2, server.PackRequests);
    }

    [Fact]
    public async Task An_installed_pack_needs_no_network_request()
    {
        byte[] zip = BuildZip();
        var server = new FakeServer(CatalogJson(zip), zip);
        await Service(server).DownloadAsync(PackId, null, CancellationToken.None);
        var second = new FakeServer(CatalogJson(zip), zip);

        PackDownloadResult result = await Service(second).DownloadAsync(PackId, null, CancellationToken.None);

        Assert.Equal(PackDownloadResult.Ok, result);
        Assert.Equal(0, second.PackRequests); // current: only the catalogue is consulted
    }

    [Fact]
    public async Task An_installed_pack_stays_usable_when_the_catalogue_cannot_be_reached()
    {
        byte[] zip = BuildZip();
        await Service(new FakeServer(CatalogJson(zip), zip)).DownloadAsync(PackId, null, CancellationToken.None);
        var offline = new FakeServer("[]", []) { FailCatalog = true };

        PackDownloadResult result = await Service(offline).DownloadAsync(PackId, null, CancellationToken.None);

        Assert.Equal(PackDownloadResult.Ok, result);
        Assert.NotNull(Service(offline).GetInstalledPath(PackId));
    }

    [Fact]
    public async Task A_newer_catalogue_version_replaces_the_installed_pack()
    {
        byte[] v1 = BuildZip();
        await Service(new FakeServer(CatalogJson(v1), v1)).DownloadAsync(PackId, null, CancellationToken.None);
        byte[] v2 = BuildZip(version: "2");
        var server = new FakeServer(CatalogJson(v2, version: "2"), v2);

        PackDownloadResult result = await Service(server).DownloadAsync(PackId, null, CancellationToken.None);

        Assert.Equal(PackDownloadResult.Ok, result);
        Assert.Equal(1, server.PackRequests);
        Assert.Equal("2", Assert.Single(Service(server).GetInstalledVoicePacks()).Manifest.Version);
        Assert.Equal([Path.Combine(_root.FullName, PackId)], Directory.EnumerateDirectories(_root.FullName)); // no .extracting left
    }

    [Fact]
    public async Task An_update_installs_next_to_a_pack_whose_files_are_open_and_the_old_version_goes_at_the_next_start()
    {
        byte[] v1 = BuildZip();
        await Service(new FakeServer(CatalogJson(v1), v1)).DownloadAsync(PackId, null, CancellationToken.None);
        string oldFolder = Service(new FakeServer("[]", [])).GetInstalledPath(PackId)!;
        byte[] v2 = BuildZip(version: "2");
        var server = new FakeServer(CatalogJson(v2, version: "2"), v2);
        PackDownloadResult result;
        string newFolder;
        using (File.OpenRead(Path.Combine(oldFolder, PayloadFile))) // the engine holding the model
        {
            var service = Service(server);
            result = await service.DownloadAsync(PackId, null, CancellationToken.None);
            newFolder = service.GetInstalledPath(PackId)!;
        }

        Assert.Equal(PackDownloadResult.Ok, result);
        Assert.NotEqual(oldFolder, newFolder);
        Assert.True(Directory.Exists(oldFolder)); // nothing was renamed or moved under the reader
        Assert.Equal(newFolder, Service(server).GetInstalledPath(PackId)); // a restart; the old folder is deleted
        Assert.False(Directory.Exists(oldFolder));
    }

    [Fact]
    public async Task A_failed_update_keeps_the_installed_pack()
    {
        byte[] v1 = BuildZip();
        await Service(new FakeServer(CatalogJson(v1), v1)).DownloadAsync(PackId, null, CancellationToken.None);
        byte[] v2 = BuildZip(version: "2");
        var server = new FakeServer(CatalogJson(v2, sha: new string('0', 64), version: "2"), v2);

        PackDownloadResult result = await Service(server).DownloadAsync(PackId, null, CancellationToken.None);

        Assert.Equal(PackDownloadResult.HashMismatch, result);
        Assert.Equal("1", Assert.Single(Service(server).GetInstalledVoicePacks()).Manifest.Version);
    }

    [Fact]
    public async Task A_resumed_download_reports_its_starting_fraction_at_once()
    {
        byte[] zip = BuildZip();
        int already = zip.Length / 2;
        File.WriteAllBytes(Path.Combine(_root.FullName, "supertonic-3-1.zip.partial"), zip[..already]);
        var progress = new SyncProgress();

        await Service(new FakeServer(CatalogJson(zip), zip)).DownloadAsync(PackId, progress, CancellationToken.None);

        Assert.NotEmpty(progress.Values);

        Assert.True(progress.Values[0] >= 0.45, $"first report was {progress.Values[0]}");
    }

    [Fact]
    public async Task A_partial_response_that_starts_elsewhere_restarts_from_zero_without_appending()
    {
        byte[] zip = BuildZip();
        File.WriteAllBytes(Path.Combine(_root.FullName, "supertonic-3-1.zip.partial"), zip[..100]);
        var server = new FakeServer(CatalogJson(zip), zip) { MisplacedContentRange = true };

        PackDownloadResult result = await Service(server).DownloadAsync(PackId, null, CancellationToken.None);

        Assert.Equal(PackDownloadResult.Ok, result);
        Assert.Equal(2, server.PackRequests); // the ranged request, then a clean full one
    }

    [Fact]
    public void A_half_installed_folder_is_not_trusted()
    {
        Directory.CreateDirectory(Path.Combine(_root.FullName, PackId));
        File.WriteAllText(Path.Combine(_root.FullName, PackId, "pack.json"),
            """{ "id": "supertonic-3", "kind": "voice", "language": "en", "version": "1", "engine": "sherpa-onnx", "files": ["model.onnx"] }""");

        var service = Service(new FakeServer("[]", []));

        Assert.Null(service.GetInstalledPath(PackId));
        Assert.Empty(service.GetInstalledVoicePacks());
    }

    private sealed class SyncProgress : IProgress<double>
    {
        public List<double> Values { get; } = [];

        public void Report(double value) => Values.Add(value);
    }

    private sealed class FakeServer(string catalog, byte[] zip) : HttpMessageHandler
    {
        public bool HonourRange { get; set; } = true;
        public bool FailCatalog { get; set; }
        public bool MisplacedContentRange { get; set; }
        public int FailFirstPackRequests { get; set; }
        public int PackRequests { get; private set; }
        public int TotalRequests { get; private set; }
        public long? LastRangeStart { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            TotalRequests++;
            if (request.RequestUri!.AbsoluteUri == CatalogUrl)
            {
                return FailCatalog ? throw new HttpRequestException(HttpRequestError.NameResolutionError, "no dns") : Respond(new StringContent(catalog));
            }

            PackRequests++;
            if (PackRequests <= FailFirstPackRequests)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }

            return Task.FromResult(ServePack(request));
        }

        private static Task<HttpResponseMessage> Respond(HttpContent content) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });

        private HttpResponseMessage ServePack(HttpRequestMessage request)
        {
            long? start = request.Headers.Range?.Ranges.First().From;
            LastRangeStart = start;
            if (start is null || !HonourRange)
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(zip) };
            }

            long from = MisplacedContentRange ? 0 : start.Value;
            var content = new ByteArrayContent(zip[(int)from..]);
            content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(from, zip.Length - 1, zip.Length);
            return new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = content };
        }
    }
}
