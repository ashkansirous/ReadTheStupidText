using Android.Runtime;
using Android.Gms.Extensions;
using ReadTheStupidText.Application.Translation;
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
public sealed class MlKitTranslator : AppTranslator, IDisposable
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

    public IReadOnlyList<string> SupportedLanguages { get; } = TranslateLanguage.AllLanguages!.Select(EngineLanguageCodes.FromEngine).ToArray();

    public async Task<bool> IsModelReadyAsync(string source, string target)
    {
        source = EngineLanguageCodes.ToEngine(source);
        target = EngineLanguageCodes.ToEngine(target);
        Java.Lang.Object? downloaded = await RemoteModelManager.Instance.GetDownloadedModels(Java.Lang.Class.FromType(typeof(TranslateRemoteModel)));
        HashSet<string> languages = DownloadedLanguages(downloaded);
        return RequiredModels(source, target).All(languages.Contains);
    }

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
