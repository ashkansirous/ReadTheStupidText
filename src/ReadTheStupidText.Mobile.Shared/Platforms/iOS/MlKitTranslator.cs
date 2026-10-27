using Foundation;
using MLKitTranslate;
using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Application.Usage;
using AppTranslator = ReadTheStupidText.Application.Translation.ITranslator;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// Translates on the device with ML Kit Translation on iOS (Slice 160), the twin of the Android class: the same engine, the
/// same languages and the same rules. ML Kit downloads one model per language (about 30 MB, English is the pivot for most
/// pairs) on first use through its own model manager; the text itself is never sent anywhere. The last language pair's
/// translator is kept so a long text's chunks reuse it.
/// </summary>
public sealed class MlKitTranslator : AppTranslator, IModelPreparer, ITranslationPacks, IDisposable
{
    private const string EnglishCode = "en";
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(250);

    // The gate covers opening a translator and each translate call, never a model download, so a
    // cancelled caller (or one waiting on a long download) never blocks the next one.
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Opening? _opening;

    private sealed record Opening((string Source, string Target) Pair, MLKTranslator Client, Task Download);

    /// <inheritdoc />
    public bool AutoDownload { get; set; } = true;

    public IReadOnlyList<string> SupportedLanguages { get; } = EngineLanguages().Select(EngineLanguageCodes.FromEngine).ToArray();

    // The languages of ML Kit's MLKTranslateAllLanguages, which the binding does not expose as a list.
    private static IEnumerable<string> EngineLanguages() => new NSString?[]
    {
        Constants.MLKTranslateLanguageAfrikaans, Constants.MLKTranslateLanguageAlbanian, Constants.MLKTranslateLanguageArabic,
        Constants.MLKTranslateLanguageBelarusian, Constants.MLKTranslateLanguageBengali, Constants.MLKTranslateLanguageBulgarian,
        Constants.MLKTranslateLanguageCatalan, Constants.MLKTranslateLanguageChinese, Constants.MLKTranslateLanguageCroatian,
        Constants.MLKTranslateLanguageCzech, Constants.MLKTranslateLanguageDanish, Constants.MLKTranslateLanguageDutch,
        Constants.MLKTranslateLanguageEnglish, Constants.MLKTranslateLanguageEsperanto, Constants.MLKTranslateLanguageEstonian,
        Constants.MLKTranslateLanguageFinnish, Constants.MLKTranslateLanguageFrench, Constants.MLKTranslateLanguageGalician,
        Constants.MLKTranslateLanguageGeorgian, Constants.MLKTranslateLanguageGerman, Constants.MLKTranslateLanguageGreek,
        Constants.MLKTranslateLanguageGujarati, Constants.MLKTranslateLanguageHaitianCreole, Constants.MLKTranslateLanguageHebrew,
        Constants.MLKTranslateLanguageHindi, Constants.MLKTranslateLanguageHungarian, Constants.MLKTranslateLanguageIcelandic,
        Constants.MLKTranslateLanguageIndonesian, Constants.MLKTranslateLanguageIrish, Constants.MLKTranslateLanguageItalian,
        Constants.MLKTranslateLanguageJapanese, Constants.MLKTranslateLanguageKannada, Constants.MLKTranslateLanguageKorean,
        Constants.MLKTranslateLanguageLatvian, Constants.MLKTranslateLanguageLithuanian, Constants.MLKTranslateLanguageMacedonian,
        Constants.MLKTranslateLanguageMalay, Constants.MLKTranslateLanguageMaltese, Constants.MLKTranslateLanguageMarathi,
        Constants.MLKTranslateLanguageNorwegian, Constants.MLKTranslateLanguagePersian, Constants.MLKTranslateLanguagePolish,
        Constants.MLKTranslateLanguagePortuguese, Constants.MLKTranslateLanguageRomanian, Constants.MLKTranslateLanguageRussian,
        Constants.MLKTranslateLanguageSlovak, Constants.MLKTranslateLanguageSlovenian, Constants.MLKTranslateLanguageSpanish,
        Constants.MLKTranslateLanguageSwahili, Constants.MLKTranslateLanguageSwedish, Constants.MLKTranslateLanguageTagalog,
        Constants.MLKTranslateLanguageTamil, Constants.MLKTranslateLanguageTelugu, Constants.MLKTranslateLanguageThai,
        Constants.MLKTranslateLanguageTurkish, Constants.MLKTranslateLanguageUkrainian, Constants.MLKTranslateLanguageUrdu,
        Constants.MLKTranslateLanguageVietnamese, Constants.MLKTranslateLanguageWelsh,
    }.Select(language => language?.ToString()).OfType<string>();

    public Task<bool> IsModelReadyAsync(string source, string target) =>
        Task.Run(() =>
        {
            HashSet<string> languages = DownloadedLanguages();
            return RequiredModels(EngineLanguageCodes.ToEngine(source), EngineLanguageCodes.ToEngine(target)).All(languages.Contains);
        });

    /// <inheritdoc />
    public Task<bool> IsPackReadyAsync(string language) => IsEnginePackReadyAsync(EngineLanguageCodes.ToEngine(language));

    private static Task<bool> IsEnginePackReadyAsync(string engineCode) => Task.Run(() => DownloadedLanguages().Contains(engineCode));

    /// <inheritdoc />
    /// <remarks>ML Kit reports the model download's progress as an <c>NSProgress</c>, polled here. Cancel asks ML Kit to stop and the caller returns at once; when
    /// the download lands anyway and the pack was not on the device before, it is deleted again, so a cancelled pack never installs behind the caller's back.
    /// A pack already on the device returns at once, and every wait ends by construction (see <see cref="PackDownload"/>).</remarks>
    public async Task DownloadPackAsync(string language, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        string code = EngineLanguageCodes.ToEngine(language);
        ReclaimCancelledDownload(code); // an earlier cancelled download of this pack is wanted again: it is kept, not deleted, and nothing waits for it
        if (await IsEnginePackReadyAsync(code))
        {
            progress?.Report(1);
            return;
        }

        MLKTranslateRemoteModel model = MLKTranslateRemoteModel.TranslateRemoteModelWithLanguage(code);
        var download = new PackDownload(model, progress);
        try
        {
            await download.Completion.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            download.Cancel();
            var abandoned = new CancelledDownload();
            _cancelledDownloads[code] = abandoned;
            _ = DeleteWhenDoneAsync(download.Completion, model, code, abandoned);
            throw;
        }
        catch (Exception ex)
        {
            throw new TranslationModelUnavailableException("The translation pack could not be downloaded.", ex);
        }

        if (!await IsEnginePackReadyAsync(code))
        {
            throw new TranslationModelUnavailableException("The translation pack is not on this device after its download.");
        }

        progress?.Report(1);
    }

    /// <summary>
    /// One pack download. <see cref="Completion"/> always finishes: on ML Kit's succeed or fail notification, when the NSProgress finishes or is
    /// cancelled (a pack that needed no download posts no notification), or at the timeout. The single <see cref="Cleanup"/> then removes both
    /// observers and stops the polling timer, exactly once, whichever of those came first.
    /// </summary>
    private sealed class PackDownload
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(30);

        private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenSource _stop = new();
        private readonly NSObject _succeeded;
        private readonly NSObject _failed;
        private readonly NSProgress _nsProgress;
        private int _cleanedUp;

        public PackDownload(MLKTranslateRemoteModel model, IProgress<double>? progress)
        {
            // Both observers exist before the download starts, so no notification can arrive while a field is still unset.
            _succeeded = NSNotificationCenter.DefaultCenter.AddObserver(Constants.MLKModelDownloadDidSucceedNotification, note =>
            {
                if (IsForModel(note, model))
                {
                    Finish(null);
                }
            });
            _failed = NSNotificationCenter.DefaultCenter.AddObserver(Constants.MLKModelDownloadDidFailNotification, note =>
            {
                if (IsForModel(note, model))
                {
                    Finish(new InvalidOperationException("ML Kit reported that the model download failed."));
                }
            });
            try
            {
                _nsProgress = MLKModelManager.ModelManager().DownloadModel(model, new MLKModelDownloadConditions(true, false));
            }
            catch
            {
                Cleanup();
                throw;
            }

            _ = PollAsync(progress);
        }

        public Task Completion => _done.Task;

        public void Cancel() => _nsProgress.Cancel();

        private async Task PollAsync(IProgress<double>? progress)
        {
            try
            {
                _stop.CancelAfter(Timeout);
                using var timer = new PeriodicTimer(ProgressInterval);
                while (await timer.WaitForNextTickAsync(_stop.Token))
                {
                    PollOnce(progress);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
            {
                // Cleanup stopped the timer (already finished), or the timeout did (below).
                Finish(new TimeoutException("The translation pack download timed out."));
            }
        }

        private void PollOnce(IProgress<double>? progress)
        {
            if (_nsProgress.Finished)
            {
                Finish(null);
            }
            else if (_nsProgress.Cancelled)
            {
                Finish(new OperationCanceledException());
            }
            else
            {
                progress?.Report(Math.Clamp(_nsProgress.FractionCompleted, 0, 1));
            }
        }

        // The first finish wins; later ones (a notification after the poll saw the end) change nothing.
        private void Finish(Exception? error)
        {
            if (error is null)
            {
                _done.TrySetResult();
            }
            else if (error is OperationCanceledException)
            {
                _done.TrySetCanceled();
            }
            else
            {
                _done.TrySetException(error);
            }

            Cleanup();
        }

        private void Cleanup()
        {
            if (Interlocked.Exchange(ref _cleanedUp, 1) == 1)
            {
                return;
            }

            _stop.Cancel();
            _stop.Dispose();
            _succeeded?.Dispose();
            _failed?.Dispose();
        }

        private static bool IsForModel(NSNotification note, MLKTranslateRemoteModel model)
        {
            NSObject? remote = note.UserInfo?[Constants.MLKModelDownloadUserInfoKeyRemoteModel];
            return remote is MLKTranslateRemoteModel other && (ReferenceEquals(other, model) || other.IsEqual(model));
        }
    }

    private sealed class CancelledDownload
    {
        public volatile bool Reclaimed;
    }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, CancelledDownload> _cancelledDownloads = new();

    private void ReclaimCancelledDownload(string code)
    {
        if (_cancelledDownloads.TryRemove(code, out CancelledDownload? abandoned))
        {
            abandoned.Reclaimed = true;
        }
    }

    // The pack is deleted only when nobody wants it any more: not when a later request for it began, when the RTST path (AutoDownload) may rely on it,
    // or when the open translator pair needs it. Never throws: it runs unobserved once the caller has gone.
    private async Task DeleteWhenDoneAsync(Task download, MLKTranslateRemoteModel model, string code, CancelledDownload abandoned)
    {
        try
        {
            await download.ContinueWith(_ => { }, TaskScheduler.Default); // settled one way or another; a failed or cancelled download is not an error here
            if (abandoned.Reclaimed || AutoDownload || IsNeededByOpenPair(code) || !await IsEnginePackReadyAsync(code))
            {
                return;
            }

            MLKModelManager.ModelManager().DeleteDownloadedModel(model, _ => { });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"A cancelled translation pack could not be removed: {ex.Message}");
        }
        finally
        {
            _cancelledDownloads.TryRemove(new KeyValuePair<string, CancelledDownload>(code, abandoned));
        }
    }

    private bool IsNeededByOpenPair(string code) =>
        _opening is { } open && RequiredModels(open.Pair.Source, open.Pair.Target).Contains(code);

    // ML Kit pivots through English, so a pair with neither side English needs the English model too.
    private static IEnumerable<string> RequiredModels(string source, string target)
    {
        yield return source;
        yield return target;
        if (source != EnglishCode && target != EnglishCode)
        {
            yield return EnglishCode;
        }
    }

    public async Task<string> TranslateAsync(string text, string source, string target, CancellationToken cancellationToken)
    {
        source = EngineLanguageCodes.ToEngine(source);
        target = EngineLanguageCodes.ToEngine(target);
        if (!AutoDownload && !await IsModelReadyAsync(source, target))
        {
            throw new TranslationModelUnavailableException("The translation pack is not on this device.");
        }

        while (true)
        {
            Opening opening = await StartOrJoinOpeningAsync(source, target, cancellationToken);
            await AwaitDownloadAsync(opening, cancellationToken);
            string? translated = await TranslateIfCurrentAsync(opening, text, cancellationToken);
            if (translated is not null)
            {
                return translated;
            }
        }
    }

    /// <summary>Fetches the models for the pair on their own, so the wait for a download is timed apart from the translation (Slice 137).</summary>
    public async Task EnsureModelAsync(string source, string target, CancellationToken cancellationToken)
    {
        Opening opening = await StartOrJoinOpeningAsync(EngineLanguageCodes.ToEngine(source), EngineLanguageCodes.ToEngine(target), cancellationToken);
        await AwaitDownloadAsync(opening, cancellationToken);
    }

    public void Dispose()
    {
        DisposeWhenDownloaded(_opening);
        _gate.Dispose();
    }

    // A client whose model download is still pending is disposed when that download ends, so its callback is never lost or run on a disposed object.
    private static void DisposeWhenDownloaded(Opening? opening)
    {
        if (opening is null)
        {
            return;
        }

        if (opening.Download.IsCompleted)
        {
            opening.Client.Dispose();
            return;
        }

        _ = opening.Download.ContinueWith(_ => opening.Client.Dispose(), TaskScheduler.Default);
    }

    // Starts the model download (not awaited here) for a new pair, or joins the one in flight.
    private async Task<Opening> StartOrJoinOpeningAsync(string source, string target, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_opening is { } current && current.Pair == (source, target) && !current.Download.IsFaulted && !current.Download.IsCanceled)
            {
                return current;
            }

            DisposeWhenDownloaded(_opening);
            MLKTranslator client = MLKTranslator.TranslatorWithOptions(new MLKTranslatorOptions(source, target));
            _opening = new Opening((source, target), client, DownloadModelAsync(client));
            return _opening;
        }
        finally
        {
            _gate.Release();
        }
    }

    // A cancelled caller stops waiting at once; the download carries on for the next caller.
    private static async Task AwaitDownloadAsync(Opening opening, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await opening.Download.WaitAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }

    // Null when another pair replaced this opening while the caller waited: the caller starts over.
    private async Task<string?> TranslateIfCurrentAsync(Opening opening, string text, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_opening != opening)
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();
            return await TranslateTextAsync(opening.Client, text);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static Task<string> TranslateTextAsync(MLKTranslator client, string text)
    {
        var done = new TaskCompletionSource<string>();
        client.TranslateText(text, (result, error) =>
        {
            if (error is not null)
            {
                done.TrySetException(new NSErrorException(error));
                return;
            }

            done.TrySetResult(result ?? string.Empty);
        });
        return done.Task;
    }

    private static async Task DownloadModelAsync(MLKTranslator translator)
    {
        var done = new TaskCompletionSource();
        var conditions = new MLKModelDownloadConditions(true, false);
        translator.DownloadModelIfNeededWithConditions(conditions, error =>
        {
            if (error is not null)
            {
                done.TrySetException(new NSErrorException(error));
                return;
            }

            done.TrySetResult();
        });
        try
        {
            await done.Task;
        }
        catch (Exception ex)
        {
            throw new TranslationModelUnavailableException("The translation model could not be downloaded.", ex);
        }
    }

    private static HashSet<string> DownloadedLanguages() =>
        MLKModelManager_Translate.DownloadedTranslateModels(MLKModelManager.ModelManager())
            .ToArray<MLKTranslateRemoteModel>()
            .Select(model => model.Language)
            .OfType<string>()
            .ToHashSet();
}
