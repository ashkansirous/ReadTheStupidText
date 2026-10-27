using Android.Runtime;
using Android.Gms.Extensions;
using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Application.Usage;
using Xamarin.Google.MLKit.Common.Model;
using Xamarin.Google.MLKit.NL.Translate;
using MlKitClient = Xamarin.Google.MLKit.NL.Translate.ITranslator;
using AppTranslator = ReadTheStupidText.Application.Translation.ITranslator;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// Translates on the device with ML Kit Translation (Slice 54). ML Kit downloads one model per
/// language (about 30 MB, English is the pivot for most pairs) on first use, through its own
/// on-device model manager; the text itself is never sent anywhere. The last language pair's
/// translator is kept open so a long text's chunks reuse it.
/// </summary>
public sealed class MlKitTranslator : AppTranslator, IModelPreparer, ITranslationPacks, IDisposable
{
    // The model is only fetched when the user turns translation on and plays, so the download is
    // on demand; requiring Wi-Fi would make that tap fail silently on mobile data.
    private const bool RequireWifi = false;
    private const string EnglishCode = "en";

    // The gate covers the open client and each translate call, never a model download, so a
    // cancelled caller (or one waiting on a long download) never blocks the next one.
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Opening? _opening;

    private sealed record Opening((string Source, string Target) Pair, MlKitClient Client, Task Download);

    /// <inheritdoc />
    public bool AutoDownload { get; set; } = true;

    public IReadOnlyList<string> SupportedLanguages { get; } = TranslateLanguage.AllLanguages!.Select(EngineLanguageCodes.FromEngine).ToArray();

    public async Task<bool> IsModelReadyAsync(string source, string target)
    {
        source = EngineLanguageCodes.ToEngine(source);
        target = EngineLanguageCodes.ToEngine(target);
        Java.Lang.Object? downloaded = await RemoteModelManager.Instance.GetDownloadedModels(Java.Lang.Class.FromType(typeof(TranslateRemoteModel)));
        HashSet<string> languages = DownloadedLanguages(downloaded);
        return RequiredModels(source, target).All(languages.Contains);
    }

    /// <inheritdoc />
    public async Task<bool> IsPackReadyAsync(string language)
    {
        Java.Lang.Object? downloaded = await RemoteModelManager.Instance.GetDownloadedModels(Java.Lang.Class.FromType(typeof(TranslateRemoteModel)));
        return DownloadedLanguages(downloaded).Contains(EngineLanguageCodes.ToEngine(language));
    }

    /// <inheritdoc />
    /// <remarks>ML Kit reports no byte progress for a model download, so <paramref name="progress"/> only gets the final 1. ML Kit cannot abort a download either:
    /// on Cancel the caller returns at once and, when the pack was not on the device before, it is deleted again as soon as the download lands, so a cancelled
    /// pack never installs behind the caller's back.</remarks>
    public async Task DownloadPackAsync(string language, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        string code = EngineLanguageCodes.ToEngine(language);
        ReclaimCancelledDownload(code); // an earlier cancelled download of this pack is wanted again: it is kept, not deleted, and nothing waits for it
        bool wasReady = await IsPackReadyAsync(language);
        TranslateRemoteModel model = new TranslateRemoteModel.Builder(code).Build();
        Task download = DownloadPackModelAsync(model);
        try
        {
            await download.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!wasReady)
            {
                var abandoned = new CancelledDownload();
                _cancelledDownloads[code] = abandoned;
                _ = DeleteWhenDoneAsync(download, model, code, abandoned);
            }

            throw;
        }
        catch (Exception ex)
        {
            throw new TranslationModelUnavailableException("The translation pack could not be downloaded.", ex);
        }

        progress?.Report(1);
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
    private async Task DeleteWhenDoneAsync(Task download, TranslateRemoteModel model, string code, CancelledDownload abandoned)
    {
        try
        {
            await download;
            if (abandoned.Reclaimed || AutoDownload || IsNeededByOpenPair(code))
            {
                return;
            }

            _cancelledDownloads.TryRemove(new KeyValuePair<string, CancelledDownload>(code, abandoned));
            await RemoteModelManager.Instance.DeleteDownloadedModel(model);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"A cancelled translation pack could not be removed: {ex.Message}");
        }
    }

    private bool IsNeededByOpenPair(string code) =>
        _opening is { } open && RequiredModels(open.Pair.Source, open.Pair.Target).Contains(code);

    private static async Task DownloadPackModelAsync(TranslateRemoteModel model) =>
        await RemoteModelManager.Instance.Download(model, new DownloadConditions.Builder().Build());

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
        _opening?.Client.Close();
        _gate.Dispose();
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

            _opening?.Client.Close();
            MlKitClient client = Translation.GetClient(new TranslatorOptions.Builder()
                .SetSourceLanguage(source)
                .SetTargetLanguage(target)
                .Build());
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
            Java.Lang.Object? result = await opening.Client.Translate(text);
            return result?.ToString() ?? string.Empty;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task DownloadModelAsync(MlKitClient translator)
    {
        DownloadConditions conditions = RequireWifi
            ? new DownloadConditions.Builder().RequireWifi().Build()
            : new DownloadConditions.Builder().Build();
        try
        {
            await translator.DownloadModelIfNeeded(conditions);
        }
        catch (Exception ex)
        {
            throw new TranslationModelUnavailableException("The translation model could not be downloaded.", ex);
        }
    }

    private static HashSet<string> DownloadedLanguages(Java.Lang.Object? models) =>
        models?.JavaCast<Java.Util.ISet>()?.ToEnumerable<TranslateRemoteModel>().Select(m => m.Language).OfType<string>().ToHashSet() ?? [];
}
