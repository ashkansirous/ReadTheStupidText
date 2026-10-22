using System.Runtime.ExceptionServices;
using Microsoft.ML.OnnxRuntime;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// The image reader on Android and iOS (Slice 52, Decision 65; shared in Slice 55). The page's script
/// picks the engine, never the app language (Slice 84, Decision 91): the platform's built-in
/// recognizer (ML Kit Latin on Android, Apple Vision on iOS) reads first and a mostly-Latin result is
/// final once it holds enough letters; otherwise the installed non-Latin OCR packs read with PaddleOCR
/// (<see cref="OcrPackChoice"/> decides the winner and when to stop), ordered right-to-left only for a
/// right-to-left script. A failing built-in recognizer does not stop the packs. The language
/// argument is only a hint (which pack goes first, what the fallback is flagged for). When no pack
/// wins and the built-in recognizer does not cover the page's script, the fallback is flagged
/// <see cref="OcrSource.MlKitPackPending"/> (the value means "built-in recognizer, pack still
/// pending", whichever OS provides it) and the page can say the pack is still downloading.
/// <see cref="IImageTextExtractor"/> is the text-only view the Reader page calls; the line boxes
/// stay available through <see cref="IOcrLineExtractor"/> for Slice 63.
/// </summary>
public sealed class LanguageImageTextExtractor : IImageTextExtractor, IOcrLineExtractor, IDisposable
{
    private readonly IBuiltInTextRecognizer _builtIn;
    private readonly IVoicePackService _packs;
    private readonly ISettingsStore _settings;
    private readonly IPixelImageLoader _loader;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private PaddleOcrEngine? _engine;
    private readonly ISystemLog _log;
    private readonly HashSet<(string Folder, string Version)> _failedToLoad = []; // packs that threw while loading: skipped until their folder or version changes

    public LanguageImageTextExtractor(IBuiltInTextRecognizer builtIn, IVoicePackService packs, ISettingsStore settings, IPixelImageLoader loader, ISystemLog log)
    {
        _log = log;
        _builtIn = builtIn;
        _packs = packs;
        _settings = settings;
        _loader = loader;
    }

    public async Task<string> ExtractTextAsync(string imagePath) =>
        ScanReadingText.From(await ReadAsync(imagePath, CurrentLanguage(), CancellationToken.None).ConfigureAwait(false));

    public Task<ImageText> ExtractLinesAsync(string imagePath, string language, CancellationToken cancellationToken) =>
        ReadAsync(imagePath, language, cancellationToken);

    // The page decides (Slice 84): the built-in recognizer reads first and a mostly-Latin result with
    // enough letters is the answer; any other page goes to the installed non-Latin packs.
    private async Task<ImageText> ReadAsync(string imagePath, string hint, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        (OcrReading builtIn, Exception? builtInError) = await TryBuiltInAsync(imagePath).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested(); // the built-in recognizer takes no token: honor it as soon as it returns
        ScriptShare share = ScriptDetector.Analyze(builtIn.Lines.Select(l => l.Text));
        IReadOnlyList<InstalledVoicePack> installed = _packs.GetInstalledOcrPacks();
        bool final = builtInError is null && OcrPackChoice.BuiltInIsFinal(share);
        IReadOnlyList<OcrModelPaths> candidates = final
            ? OcrPackChoice.HintPackOnly(installed, hint) // a Latin verdict still gets a second opinion from a non-Latin hint's pack
            : OcrPackChoice.Candidates(installed, hint, share, _engine?.Paths);
        if (final && candidates.Count == 0)
        {
            return new ImageText(builtIn.Lines, OcrSource.MlKit, builtIn.Width, builtIn.Height, builtIn.FullText);
        }

        if (await BestPackReadingAsync(imagePath, candidates, share, cancellationToken).ConfigureAwait(false) is { } reading)
        {
            return new ImageText(reading.Lines, OcrSource.PaddleOcr, reading.Width, reading.Height);
        }

        if (final)
        {
            return new ImageText(builtIn.Lines, OcrSource.MlKit, builtIn.Width, builtIn.Height, builtIn.FullText);
        }

        if (builtInError is not null)
        {
            ExceptionDispatchInfo.Capture(builtInError).Throw(); // nothing else could read the page
        }

        return new ImageText(builtIn.Lines, FallbackSource(hint, share), builtIn.Width, builtIn.Height, builtIn.FullText);
    }

    // A built-in failure (decode error, out of memory, ML Kit) leaves an empty reading and the error,
    // so an installed pack still gets its turn.
    private async Task<(OcrReading Reading, Exception? Error)> TryBuiltInAsync(string imagePath)
    {
        try
        {
            return (await _builtIn.ExtractLinesAsync(imagePath).ConfigureAwait(false), null);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
        {
            _log.Warning("The built-in recognizer failed, trying the OCR packs", null, ex);
            return (new OcrReading([], 0, 0), ex);
        }
    }

    // Each candidate pack loads at most once; OcrPackChoice names the winner and stops the scan at a clear win.
    private async Task<OcrReading?> BestPackReadingAsync(string imagePath, IReadOnlyList<OcrModelPaths> candidates, ScriptShare builtIn, CancellationToken cancellationToken)
    {
        var choice = new OcrPackChoice(builtIn);
        OcrReading? best = null;
        foreach (OcrModelPaths paths in candidates)
        {
            OcrReading? reading = await TryPaddleAsync(paths, imagePath, cancellationToken).ConfigureAwait(false);
            if (reading is not null && choice.Offer(paths, ScriptDetector.Analyze(reading.Lines.Select(l => l.Text))))
            {
                best = reading;
            }

            if (choice.IsDecided)
            {
                break;
            }
        }

        return best;
    }

    public void Dispose()
    {
        // Wait for an in-flight read so the engine is never disposed under it.
        _gate.Wait();
        try
        {
            _engine?.Dispose();
            _engine = null;
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private string CurrentLanguage() => _settings.Language ?? AppLanguages.Code(AppLanguage.English);

    // The page's script decides, not the app language: a script the built-in recognizer covers is a
    // settled built-in result; any other is waiting for its pack. A blank or failed reading has no
    // script, so the hint stands in for it.
    private OcrSource FallbackSource(string hint, ScriptShare share)
    {
        string language = share.Dominant is { } script ? ScriptDetector.LanguageOf(script) ?? hint : hint;

        return _builtIn.Covers(language) ? OcrSource.MlKit : OcrSource.MlKitPackPending;
    }

    // Null when the pack could not run or found no text: the caller falls back.
    private async Task<OcrReading?> TryPaddleAsync(OcrModelPaths paths, string imagePath, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_failedToLoad.Contains((paths.Folder, paths.Version)))
            {
                return null;
            }

            OcrReading reading = await Task.Run(() => ReadWithEngine(paths, imagePath, cancellationToken), cancellationToken).ConfigureAwait(false);
            return reading.Lines.Count > 0 ? reading : null; // a blank result is worth a second opinion from the built-in recognizer
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.Warning($"PaddleOCR pack '{paths.Folder}' failed, using the built-in recognizer", null, ex);
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
            _failedToLoad.Add((paths.Folder, paths.Version));
        }
    }

    private OcrReading ReadWithEngine(OcrModelPaths paths, string imagePath, CancellationToken cancellationToken)
    {
        if (_engine is null || !_engine.Paths.IsSamePack(paths))
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
        catch (Exception ex)
        {
            MarkFailedWhenStructural(paths, ex);
            throw;
        }
    }
}
