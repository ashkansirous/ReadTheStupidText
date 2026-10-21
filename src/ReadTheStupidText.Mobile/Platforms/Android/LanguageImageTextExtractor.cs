using Microsoft.ML.OnnxRuntime;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// The Android image reader (Slice 52, Decision 65): the installed OCR pack for the language reads
/// the image with PaddleOCR; with no pack, or if the pack fails to load, ML Kit Latin reads it.
/// ML Kit has no Arabic, Persian or Cyrillic, so for those languages the fallback is flagged
/// <see cref="OcrSource.MlKitPackPending"/> and the page can say the pack is still downloading.
/// <see cref="IImageTextExtractor"/> is the text-only view the Reader page calls; the line boxes
/// stay available through <see cref="IOcrLineExtractor"/> for Slice 63.
/// </summary>
public sealed class LanguageImageTextExtractor : IImageTextExtractor, IOcrLineExtractor, IDisposable
{
    private readonly MlKitImageTextExtractor _mlKit;
    private readonly IVoicePackService _packs;
    private readonly ISettingsStore _settings;
    private readonly IPixelImageLoader _loader;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private PaddleOcrEngine? _engine;
    private OcrModelPaths? _failedToLoad; // a pack that threw while loading: skipped until its folder or version changes

    public LanguageImageTextExtractor(MlKitImageTextExtractor mlKit, IVoicePackService packs, ISettingsStore settings, IPixelImageLoader loader)
    {
        _mlKit = mlKit;
        _packs = packs;
        _settings = settings;
        _loader = loader;
    }

    public async Task<string> ExtractTextAsync(string imagePath)
    {
        string language = CurrentLanguage();
        if (await TryPaddleAsync(imagePath, language, CancellationToken.None).ConfigureAwait(false) is { } lines)
        {
            return new ImageText(lines, OcrSource.PaddleOcr).Text;
        }

        return await _mlKit.ExtractTextAsync(imagePath).ConfigureAwait(false);
    }

    public async Task<ImageText> ExtractLinesAsync(string imagePath, string language, CancellationToken cancellationToken)
    {
        if (await TryPaddleAsync(imagePath, language, cancellationToken).ConfigureAwait(false) is { } lines)
        {
            return new ImageText(lines, OcrSource.PaddleOcr);
        }

        IReadOnlyList<OcrLine> fallback = await _mlKit.ExtractLinesAsync(imagePath).ConfigureAwait(false);
        return new ImageText(fallback, FallbackSource(language));
    }

    public void Dispose()
    {
        _engine?.Dispose();
        _gate.Dispose();
    }

    private string CurrentLanguage() => _settings.Language ?? AppLanguages.Code(AppLanguage.English);

    private static OcrSource FallbackSource(string language) =>
        OcrLanguages.IsLatinScript(language) ? OcrSource.MlKit : OcrSource.MlKitPackPending;

    // Null when no pack serves the language, the pack could not run or found no text: the caller falls back.
    private async Task<IReadOnlyList<OcrLine>?> TryPaddleAsync(string imagePath, string language, CancellationToken cancellationToken)
    {
        OcrModelPaths? paths = InstalledOcrCatalog.Resolve(_packs.GetInstalledOcrPacks(), language);
        if (paths is null || paths == _failedToLoad)
        {
            return null;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IReadOnlyList<OcrLine> lines = await Task.Run(() => ReadWithEngine(paths, imagePath, cancellationToken), cancellationToken).ConfigureAwait(false);
            return lines.Count > 0 ? lines : null; // a blank result is worth a second opinion from ML Kit
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"PaddleOCR pack '{paths.Folder}' failed, using ML Kit: {ex}");
            MarkFailedWhenStructural(paths, ex);
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    // A model that cannot run (wrong output shape or rank, dictionary mismatch, onnxruntime error)
    // will fail on every image, so the pack is skipped until its folder or version changes. A plain
    // I/O or decode error belongs to one image and must not retire the pack.
    private void MarkFailedWhenStructural(OcrModelPaths paths, Exception exception)
    {
        if (exception is InvalidDataException or OnnxRuntimeException)
        {
            _failedToLoad = paths;
        }
    }

    private IReadOnlyList<OcrLine> ReadWithEngine(OcrModelPaths paths, string imagePath, CancellationToken cancellationToken)
    {
        if (_engine?.Paths != paths)
        {
            _engine?.Dispose();
            _engine = null;
            _engine = Load(paths);
        }

        return _engine.Read(imagePath, cancellationToken);
    }

    private PaddleOcrEngine Load(OcrModelPaths paths)
    {
        try
        {
            return new PaddleOcrEngine(paths, _loader);
        }
        catch
        {
            _failedToLoad = paths;
            throw;
        }
    }
}
