using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Sirous.Reading.Application.Usage;
using ReadTheStupidText.Mobile;
using Sirous.Reading.Mobile.Shared;

namespace ReadTheStupidText.Tests;

/// <summary>Slice 273 part C: packs, OCR, documents, store, config and UI language report through the shared <see cref="IFallbackSink"/>.</summary>
public sealed class FallbackPartCTests
{
    private sealed class RecordingSink : IFallbackSink
    {
        public List<(string Area, string Reason, string? Language)> Reports { get; } = [];

        public void Report(string area, string reason, string? language = null) => Reports.Add((area, reason, language));
    }

    private sealed class FakeScanner(Exception? failure, IReadOnlyList<string>? pages = null) : IDocumentScanner
    {
        public Task<IReadOnlyList<string>> ScanAsync() =>
            failure is null ? Task.FromResult(pages ?? []) : Task.FromException<IReadOnlyList<string>>(failure);

        public Task PrepareAsync() => Task.CompletedTask;
    }

    [Theory]
    [InlineData("pack:pack_failed", "pack", "pack_failed")]
    [InlineData("ocr:ocr_pack_failed", "ocr", "ocr_pack_failed")]
    [InlineData("config:config_cached", "config", "config_cached")]
    [InlineData("", "speech", "neural_not_ready")]
    [InlineData("   ", "speech", "neural_not_ready")]
    [InlineData("nocolon", "nocolon", "")]
    public void The_test_hook_kind_is_split_into_area_and_reason(string kind, string area, string reason) =>
        Assert.Equal((area, reason), FallbackKinds.Split(kind));

    [Fact]
    public void Every_part_C_area_and_reason_is_on_the_closed_lists()
    {
        string[] areas = [FallbackArea.Pack, FallbackArea.Ocr, FallbackArea.Document, FallbackArea.Store, FallbackArea.Config, FallbackArea.UiLanguage];
        string[] reasons =
        [
            FallbackReason.PackSyncFailed, FallbackReason.StorytellerFailed, FallbackReason.RecastFailed, FallbackReason.CatalogueFailed,
            FallbackReason.OcrBuiltinNoPack, FallbackReason.OcrBuiltinFailed, FallbackReason.OcrPlatformFallback, FallbackReason.OcrPackFailed, FallbackReason.ScannerStartFailed,
            FallbackReason.PdfImportFailed, FallbackReason.StoreUnreachable, FallbackReason.UpdateCheckFailed, FallbackReason.ConfigCached, FallbackReason.ConfigDefault, FallbackReason.UiLanguageUnsupported,
        ];

        Assert.All(areas, area => Assert.Contains(area, FallbackArea.All));
        Assert.All(reasons, reason => Assert.Contains(reason, FallbackReason.All));
        Assert.Equal(FallbackReason.All.Count, FallbackReason.All.Distinct().Count());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_scanner_that_cannot_start_is_counted_and_the_exception_still_reaches_the_page(bool unavailable)
    {
        var sink = new RecordingSink();
        Exception failure = unavailable ? new DocumentScannerUnavailableException() : new DocumentScannerStartFailedException();
        IDocumentScanner scanner = new DocumentScannerReporting(new FakeScanner(failure), sink);

        Exception thrown = await Assert.ThrowsAnyAsync<Exception>(() => scanner.ScanAsync());

        Assert.Same(failure, thrown);
        Assert.Equal(("ocr", "scanner_start_failed", null), Assert.Single(sink.Reports));
    }

    [Fact]
    public async Task A_scanner_that_works_or_is_still_preparing_reports_nothing()
    {
        var sink = new RecordingSink();
        var ok = new DocumentScannerReporting(new FakeScanner(null, ["a.jpg"]), sink);
        var preparing = new DocumentScannerReporting(new FakeScanner(new DocumentScannerPreparingException()), sink);

        Assert.Equal(["a.jpg"], await ok.ScanAsync());
        await Assert.ThrowsAsync<DocumentScannerPreparingException>(() => preparing.ScanAsync());

        Assert.Empty(sink.Reports);
    }

    [Fact]
    public async Task The_scanner_wrapper_without_a_sink_still_passes_the_failure_on()
    {
        var scanner = new DocumentScannerReporting(new FakeScanner(new DocumentScannerStartFailedException()));

        await Assert.ThrowsAsync<DocumentScannerStartFailedException>(() => scanner.ScanAsync(1));
    }

    [Fact]
    public void An_unsupported_stored_and_device_language_falls_back_to_english_and_Resolve_stays_pure()
    {
        var swahili = CultureInfo.GetCultureInfo("sw");

        Assert.True(UiCulture.FallsBackToEnglish(null, swahili));
        Assert.Equal("en", UiCulture.Resolve(null, swahili).TwoLetterISOLanguageName);
    }

    [Fact]
    public void Repeated_failed_fetches_hold_one_entry_per_kind()
    {
        var sent = new RecordingSink();
        var hold = new Sirous.Reading.Mobile.Shared.Usage.ConfigFallbackHold(() => false, sent.Report);

        Assert.True(hold.TryHold(FallbackArea.Config, FallbackReason.ConfigDefault, null));
        Assert.True(hold.TryHold(FallbackArea.Config, FallbackReason.ConfigDefault, null));
        Assert.True(hold.TryHold(FallbackArea.Config, FallbackReason.ConfigDefault, null));
        hold.Release();

        Assert.Single(sent.Reports);
    }

    [Fact]
    public void Config_and_ui_language_fallbacks_wait_for_a_real_config_then_go_out_in_order()
    {
        bool configHeld = false;
        var sent = new RecordingSink();
        var hold = new Sirous.Reading.Mobile.Shared.Usage.ConfigFallbackHold(() => configHeld, sent.Report);

        Assert.True(hold.TryHold(FallbackArea.Config, FallbackReason.ConfigDefault, null));
        Assert.True(hold.TryHold(FallbackArea.UiLanguage, FallbackReason.UiLanguageUnsupported, null));
        Assert.False(hold.TryHold(FallbackArea.Pack, FallbackReason.PackFailed, "de")); // other areas never wait
        Assert.Empty(sent.Reports);

        configHeld = true;
        hold.Release();
        hold.Release(); // nothing is sent twice

        Assert.Equal([(FallbackArea.Config, FallbackReason.ConfigDefault, (string?)null), (FallbackArea.UiLanguage, FallbackReason.UiLanguageUnsupported, null)], sent.Reports);
        Assert.False(hold.TryHold(FallbackArea.Config, FallbackReason.ConfigCached, null)); // a config is held now: report at once
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Transient)]
    [InlineData(ServiceLifetime.Scoped)]
    public void The_scanner_wrapper_keeps_the_original_lifetime(ServiceLifetime lifetime)
    {
        IServiceCollection services = new ServiceCollection();
        services.Add(ServiceDescriptor.Describe(typeof(IDocumentScanner), _ => new FakeScanner(null), lifetime));

        DocumentScannerRegistration.ReportFallbacks(services);

        Assert.Equal(lifetime, services.Single(d => d.ServiceType == typeof(IDocumentScanner) && !d.IsKeyedService).Lifetime);
        Assert.Equal(lifetime, services.Single(d => d.IsKeyedService).Lifetime);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var first = scope.ServiceProvider.GetRequiredService<IDocumentScanner>();
        var second = scope.ServiceProvider.GetRequiredService<IDocumentScanner>();
        Assert.IsType<DocumentScannerReporting>(first);
        Assert.Equal(lifetime != ServiceLifetime.Transient, ReferenceEquals(first, second));
    }

    [Fact]
    public void A_supported_stored_or_device_language_is_not_a_fallback()
    {
        Assert.False(UiCulture.FallsBackToEnglish("fa", CultureInfo.GetCultureInfo("sw")));
        Assert.False(UiCulture.FallsBackToEnglish(null, CultureInfo.GetCultureInfo("fr")));
        Assert.False(UiCulture.FallsBackToEnglish(null, CultureInfo.GetCultureInfo("pt-BR")));
    }
}
