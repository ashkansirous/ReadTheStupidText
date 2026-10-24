using System.Net;
using System.Net.Sockets;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class VoicePackTests
{
    private const string CatalogJson = """
        [
          { "id": "supertonic-3", "language": "en, fr,es", "kind": "voice", "version": "1",
            "sizeBytes": 1000, "sha256": "ABC", "license": "Apache-2.0", "url": "https://x/s.zip" },
          { "id": "ocr-fa", "language": "fa", "kind": "ocr", "version": "1",
            "sizeBytes": 10, "sha256": "d", "license": "MIT", "url": "https://x/o.zip" },
          { "id": "future", "language": "en", "kind": "hologram", "version": "1",
            "sizeBytes": 10, "sha256": "d", "license": "MIT", "url": "https://x/f.zip" }
        ]
        """;

    [Fact]
    public void Catalog_parses_known_kinds_and_skips_unknown_ones()
    {
        IReadOnlyList<PackInfo> packs = PackCatalogParser.Parse(CatalogJson);

        Assert.Equal(2, packs.Count);
        Assert.Equal(PackKind.Voice, packs[0].Kind);
        Assert.Equal(["en", "fr", "es"], packs[0].Languages);
        Assert.Equal(1000, packs[0].SizeBytes);
        Assert.Equal(PackKind.Ocr, packs[1].Kind);
    }

    [Theory]
    [InlineData("../evil", "1")]
    [InlineData("a/b", "1")]
    [InlineData("Upper", "1")]
    [InlineData("ok", "1/../2")]
    [InlineData("ok", "1-2")]
    [InlineData("ok", "")]
    public void Catalog_skips_entries_with_unsafe_id_or_version(string id, string version)
    {
        string json = $$"""
            [ { "id": "{{id}}", "language": "en", "kind": "voice", "version": "{{version}}",
                "sizeBytes": 10, "sha256": "d", "license": "MIT", "url": "https://x/s.zip" } ]
            """;

        Assert.Empty(PackCatalogParser.Parse(json));
    }

    [Theory]
    [InlineData("supertonic-3-1.zip", true)]
    [InlineData("supertonic-3-1.2.zip.partial", true)]
    [InlineData("supertonic-3-xx-1.zip", false)]
    [InlineData("supertonic-3-xx-1.zip.partial", false)]
    [InlineData("supertonic-3-.zip", false)]
    [InlineData("supertonic-3.zip", false)]
    [InlineData("supertonic-3-1.txt", false)]
    [InlineData("other-1.zip", false)]
    public void Orphan_matching_is_exact_to_the_pack_id(string fileName, bool expected)
    {
        Assert.Equal(expected, PackNaming.IsOrphanFileName(fileName, "supertonic-3"));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""[ { "id": "x", "kind": "voice" } ]""")]
    public void Catalog_rejects_malformed_input(string json)
    {
        Assert.Throws<FormatException>(() => PackCatalogParser.Parse(json));
    }

    [Fact]
    public void Selector_picks_the_voice_pack_serving_the_language()
    {
        IReadOnlyList<PackInfo> packs = PackCatalogParser.Parse(CatalogJson);

        Assert.Equal("supertonic-3", VoicePackSelector.Select(packs, "FR")?.Id);
        Assert.Null(VoicePackSelector.Select(packs, "fa"));
    }

    [Fact]
    public void Selector_prefers_the_most_specific_pack()
    {
        var wide = new PackInfo("wide", ["en", "fr"], PackKind.Voice, "1", 1, "h", "l", "u");
        var narrow = new PackInfo("narrow", ["en"], PackKind.Voice, "1", 1, "h", "l", "u");

        Assert.Equal("narrow", VoicePackSelector.Select([wide, narrow], "en")?.Id);
    }

    [Fact]
    public void Manifest_parses_and_reports_missing_files()
    {
        const string json = """
            { "id": "supertonic-3", "kind": "voice", "language": "en", "version": "1",
              "engine": "sherpa-onnx", "files": ["a.onnx", "b.bin"] }
            """;

        PackManifest manifest = PackManifest.Parse(json);

        Assert.Equal(["b.bin"], manifest.MissingFiles(f => f == "a.onnx"));
        Assert.Empty(manifest.MissingFiles(_ => true));
    }

    [Fact]
    public void Manifest_without_files_is_rejected()
    {
        Assert.Throws<FormatException>(() => PackManifest.Parse("""{ "id": "x" }"""));
    }

    [Theory]
    [InlineData(0, 100, 0)]      // nothing downloaded
    [InlineData(40, 100, 40)]    // resume mid-way
    [InlineData(100, 100, 100)]  // complete: only verify
    [InlineData(101, 100, 0)]    // larger than the pack: corrupt, start over
    public void Resume_offset_restarts_when_partial_is_unusable(long partial, long size, long expected)
    {
        Assert.Equal(expected, PackRetryPolicy.ResumeOffset(partial, size));
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(10, 30)]  // capped
    [InlineData(100, 30)]
    public void Backoff_doubles_up_to_the_cap(int attempt, int seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), PackRetryPolicy.Backoff(attempt));
    }

    [Theory]
    [InlineData(PackDownloadResult.Ok, 1, false)]
    [InlineData(PackDownloadResult.Offline, 1, false)]
    [InlineData(PackDownloadResult.InvalidPack, 1, false)]
    [InlineData(PackDownloadResult.Failed, 1, true)]
    [InlineData(PackDownloadResult.Failed, PackRetryPolicy.MaxAttempts, false)]
    [InlineData(PackDownloadResult.HashMismatch, 1, true)]
    [InlineData(PackDownloadResult.HashMismatch, PackRetryPolicy.MaxHashMismatchAttempts, false)]
    public void Retry_policy_per_outcome(PackDownloadResult result, int attempt, bool retry)
    {
        Assert.Equal(retry, PackRetryPolicy.ShouldRetry(result, attempt));
    }

    [Fact]
    public void Connectivity_failures_classify_as_offline()
    {
        var socket = new HttpRequestException("x", new SocketException());
        var plain = new HttpRequestException("x", null, HttpStatusCode.InternalServerError);
        var notFound = new HttpRequestException("x", null, HttpStatusCode.NotFound);

        Assert.Equal(PackDownloadResult.Offline, PackErrorClassifier.Classify(socket));
        Assert.Equal(PackDownloadResult.Offline, PackErrorClassifier.Classify(new WebException("x")));
        Assert.Equal(PackDownloadResult.Failed, PackErrorClassifier.Classify(plain));
        Assert.Equal(PackDownloadResult.Failed, PackErrorClassifier.Classify(notFound));
        Assert.Equal(PackDownloadResult.Failed, PackErrorClassifier.Classify(new IOException()));
    }

    [Fact]
    public void Android_java_network_exceptions_classify_as_offline_by_type_name()
    {
        Assert.Equal(PackDownloadResult.Offline, PackErrorClassifier.Classify(new HttpRequestException("x", new Java.Net.UnknownHostException())));
        Assert.Equal(PackDownloadResult.Offline, PackErrorClassifier.Classify(new HttpRequestException("x", new Java.Net.ConnectException())));
        Assert.Equal(PackDownloadResult.Offline, PackErrorClassifier.Classify(new HttpRequestException("x", new Java.Net.SocketTimeoutException())));
        Assert.Equal(PackDownloadResult.Offline, PackErrorClassifier.Classify(new HttpRequestException("x", new Java.IO.IOException())));
        Assert.Equal(PackDownloadResult.Offline, PackErrorClassifier.Classify(new HttpRequestException("x", new Java.Net.DerivedHostException())));
    }

    [Fact]
    public void Java_io_exception_alone_is_not_a_network_failure()
    {
        Assert.Equal(PackDownloadResult.Failed, PackErrorClassifier.Classify(new Java.IO.IOException()));
    }

    [Theory]
    [MemberData(nameof(InstallFailures))]
    public void Install_failures_classify_by_determinism(Exception exception, PackDownloadResult expected)
    {
        Assert.Equal(expected, PackErrorClassifier.ClassifyInstall(exception));
    }

    public static TheoryData<Exception, PackDownloadResult> InstallFailures => new()
    {
        { new InvalidDataException(), PackDownloadResult.InvalidPack },
        { new FormatException(), PackDownloadResult.InvalidPack },
        { new FileNotFoundException(), PackDownloadResult.InvalidPack },
        { new DirectoryNotFoundException(), PackDownloadResult.InvalidPack },
        { new IOException("disk full"), PackDownloadResult.Failed },
    };

    [Theory]
    [InlineData("en", true, AppLanguage.English)]
    [InlineData("FA", true, AppLanguage.Persian)]
    [InlineData("SV", true, AppLanguage.Swedish)]
    [InlineData("nb", true, AppLanguage.Norwegian)]
    [InlineData("nl", true, AppLanguage.Dutch)]
    [InlineData("da", true, AppLanguage.Danish)]
    [InlineData("fi", true, AppLanguage.Finnish)]
    [InlineData("xx", false, default(AppLanguage))]
    [InlineData(null, false, default(AppLanguage))]
    public void Language_codes_round_trip(string? code, bool ok, AppLanguage expected)
    {
        Assert.Equal(ok, AppLanguages.TryParse(code, out AppLanguage language));
        Assert.Equal(expected, language);
    }

    [Fact]
    public void Every_language_has_a_unique_code()
    {
        string[] codes = Enum.GetValues<AppLanguage>().Select(AppLanguages.Code).ToArray();

        Assert.Equal(14, codes.Length);
        Assert.Equal(codes.Length, codes.Distinct().Count());
    }
}
