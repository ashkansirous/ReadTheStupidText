namespace ReadTheStupidText.Application.Images;

/// <summary>
/// The image <see cref="IPositionedTextExtractor"/> (Slice 63): reads one page image, or several,
/// with the OCR engine for the current language and returns each as a page that keeps its image.
/// A path that fails to read stops the call; callers read pages one at a time to keep going.
/// </summary>
public sealed class OcrPositionedTextExtractor : IPositionedTextExtractor
{
    private readonly IOcrLineExtractor _ocr;
    private readonly Func<string> _language;

    public OcrPositionedTextExtractor(IOcrLineExtractor ocr, Func<string> language)
    {
        _ocr = ocr;
        _language = language;
    }

    public async Task<IReadOnlyList<PositionedPage>> ExtractPagesAsync(string path) =>
        [await ExtractPageAsync(path, 0, CancellationToken.None).ConfigureAwait(false)];

    public async Task<PositionedPage> ExtractPageAsync(string imagePath, int index, CancellationToken cancellationToken)
    {
        ImageText reading = await _ocr.ExtractLinesAsync(imagePath, _language(), cancellationToken).ConfigureAwait(false);
        return OcrPageMapper.ToPage(reading, index, imagePath);
    }
}
