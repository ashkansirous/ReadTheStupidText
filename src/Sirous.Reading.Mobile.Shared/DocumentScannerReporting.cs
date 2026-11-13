using Sirous.Reading.Application.Usage;

namespace Sirous.Reading.Mobile.Shared;

/// <summary>
/// Wraps the platform's <see cref="IDocumentScanner"/> (Slice 273 part C) so a scanner that could not start (<see cref="DocumentScannerStartFailedException"/>, or one that can never
/// start here, <see cref="DocumentScannerUnavailableException"/>) is counted as an <c>ocr</c> / <c>scanner_start_failed</c> fallback, on Android and iOS alike, before the exception
/// goes on to the page that offers the photo route. The scanner's own behaviour, its exceptions and its results are unchanged.
/// </summary>
public sealed class DocumentScannerReporting(IDocumentScanner inner, IFallbackSink? fallbacks = null) : IDocumentScanner
{
    private readonly IFallbackSink _fallbacks = fallbacks ?? NoFallbackSink.Instance;

    public Task<IReadOnlyList<string>> ScanAsync() => ReportingAsync(inner.ScanAsync);

    public Task<IReadOnlyList<string>> ScanAsync(int maxPages) => ReportingAsync(() => inner.ScanAsync(maxPages));

    public Task PrepareAsync() => inner.PrepareAsync();

    private async Task<IReadOnlyList<string>> ReportingAsync(Func<Task<IReadOnlyList<string>>> scan)
    {
        try
        {
            return await scan().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is DocumentScannerStartFailedException or DocumentScannerUnavailableException)
        {
            _fallbacks.Report(FallbackArea.Ocr, FallbackReason.ScannerStartFailed);
            throw;
        }
    }
}
