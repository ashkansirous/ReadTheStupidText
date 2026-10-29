using ReadTheStupidText.Application;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile.Resources.Strings;

namespace ReadTheStupidText.Mobile;

/// <summary>One scanned page as read: its text (null when its OCR threw), the engine that read it and the scripts of what the built-in recognizer saw.</summary>
public sealed record ScanPageRead(string? Text, OcrSource Source, ScriptShare Share);

/// <summary>The screen side of <see cref="ScanPackFlow"/>: dialogs, the status line and the Cancel button.</summary>
public interface IScanPackUi
{
    bool IsRetired { get; }

    /// <summary>The index of the language the person picked, or -1 when they cancelled.</summary>
    Task<int> PickLanguageAsync(IReadOnlyList<string> names);

    void ShowProgress(string? text);

    /// <summary>Shows a Cancel button that runs <paramref name="cancel"/>; null hides it. <see cref="ShowProgress"/> may be called from any thread and must drop text once the button is hidden.</summary>
    void SetCancel(Action? cancel);

    /// <summary>A message with Retry and Cancel; true for Retry.</summary>
    Task<bool> AskRetryAsync(string title, string message);

    Task NotifyAsync(string title, string message);
}

/// <summary>
/// Slice 87: a scanned page in a script no installed reader covers asks which language it is in,
/// downloads that language's OCR pack with visible progress and Cancel, and reads the pending pages
/// again. The decisions live in the pure <see cref="PageLanguagePrompt"/> and <see cref="PagePackRetry"/>.
/// <see cref="ResolveAsync"/> returns the reads to use, or null when the scan must leave the editor
/// text alone (cancelled, offline, a failed download, or a page the new pack still could not read).
/// </summary>
public sealed class ScanPackFlow
{
    private readonly OcrPackInstaller _packs;
    private readonly IScanPackUi _ui;

    public ScanPackFlow(OcrPackInstaller packs, IScanPackUi ui)
    {
        _packs = packs;
        _ui = ui;
    }

    /// <param name="readAgain">Reads the given pages again with the picked language as the hint.</param>
    public async Task<IReadOnlyList<ScanPageRead>?> ResolveAsync(
        IReadOnlyList<string> pages,
        IReadOnlyList<ScanPageRead> reads,
        Func<IReadOnlyList<string>, string, Task<IReadOnlyList<ScanPageRead>>> readAgain)
    {
        List<int> pending = Enumerable.Range(0, reads.Count).Where(i => !PageLanguagePrompt.IsResolved(reads[i].Source)).ToList();
        if (pending.Count == 0 || _ui.IsRetired)
        {
            return reads;
        }

        using var cts = new CancellationTokenSource();
        _ui.SetCancel(cts.Cancel);
        try
        {
            return await ResolvePendingAsync(pages, reads, pending, readAgain, cts.Token);
        }
        finally
        {
            _ui.SetCancel(null);
        }
    }

    private async Task<IReadOnlyList<ScanPageRead>?> ResolvePendingAsync(
        IReadOnlyList<string> pages,
        IReadOnlyList<ScanPageRead> reads,
        IReadOnlyList<int> pending,
        Func<IReadOnlyList<string>, string, Task<IReadOnlyList<ScanPageRead>>> readAgain,
        CancellationToken cancellationToken)
    {
        PageLanguageOffer? offer = await OfferWithRetryAsync(reads, pending, cancellationToken);
        if (offer is null)
        {
            return null;
        }

        if (PageLanguagePrompt.Decide(offer) == PageLanguageStep.Proceed)
        {
            // Nothing left to download, yet a page is still unread: keep the editor text and say so.
            await _ui.NotifyAsync(AppResources.Reader_OcrDownloadFailedTitle, AppResources.Reader_OcrUnreadableMessage);
            return null;
        }

        string? language = await AskLanguageAsync(offer.Languages);
        if (language is null || !await DownloadWithRetryAsync(language, cancellationToken) || _ui.IsRetired)
        {
            return null;
        }

        return await ReadPendingAgainAsync(pages, reads, pending, language, readAgain);
    }

    // Null: the catalog could not be reached and the person gave up (or Retry ran out).
    private async Task<PageLanguageOffer?> OfferWithRetryAsync(IReadOnlyList<ScanPageRead> reads, IReadOnlyList<int> pending, CancellationToken cancellationToken)
    {
        TextScript? script = PageLanguagePrompt.PageScript(pending.Select(i => reads[i].Share));
        for (int attempt = 1; !_ui.IsRetired && !cancellationToken.IsCancellationRequested; attempt++)
        {
            PageLanguageOffer? offer = await FetchOfferAsync(reads, pending, script, cancellationToken);
            if (offer is null)
            {
                return null;
            }

            if (PageLanguagePrompt.Decide(offer) != PageLanguageStep.NeedsConnection)
            {
                return offer;
            }

            if (!await TryAgainAsync(offer.CatalogStatus, attempt, null))
            {
                return null;
            }
        }

        return null;
    }

    // Null: the person cancelled while the catalog was being fetched.
    private async Task<PageLanguageOffer?> FetchOfferAsync(IReadOnlyList<ScanPageRead> reads, IReadOnlyList<int> pending, TextScript? script, CancellationToken cancellationToken)
    {
        try
        {
            return await _packs.OfferedLanguagesAsync(pending.Select(i => reads[i].Source), script, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private async Task<string?> AskLanguageAsync(IReadOnlyList<string> offered)
    {
        int index = await _ui.PickLanguageAsync(offered.Select(NativeLanguageName).ToArray());

        return index < 0 ? null : offered[index];
    }

    private static string NativeLanguageName(string code) =>
        AppLanguages.TryParse(code, out AppLanguage language) ? LanguageChoices.NativeName(language) : code;

    // True when the pack is installed. A failure offers Retry a few times; Cancel gives up.
    private async Task<bool> DownloadWithRetryAsync(string language, CancellationToken cancellationToken)
    {
        string name = NativeLanguageName(language);
        for (int attempt = 1; !cancellationToken.IsCancellationRequested && !_ui.IsRetired; attempt++)
        {
            PackDownloadResult result;
            try
            {
                result = await DownloadOnceAsync(language, name, cancellationToken);
            }
            catch (PackCancelledByNotificationException)
            {
                return false; // the person cancelled on the notification: no error, no retry
            }

            if (result == PackDownloadResult.Ok)
            {
                return true;
            }

            if (cancellationToken.IsCancellationRequested || !await TryAgainAsync(result, attempt, name))
            {
                break;
            }
        }

        return false;
    }

    private async Task<PackDownloadResult> DownloadOnceAsync(string language, string name, CancellationToken cancellationToken)
    {
        var progress = new InlineProgress(fraction => _ui.ShowProgress(AppResources.Format(AppResources.Reader_OcrDownloading, name, (int)(fraction * 100))));
        _ui.ShowProgress(AppResources.Format(AppResources.Reader_OcrDownloading, name, 0));
        try
        {
            return await _packs.EnsureForLanguageAsync(language, progress, cancellationToken);
        }
        catch (PackCancelledByNotificationException)
        {
            throw; // Cancel on the download notification: the caller gives up without an error
        }
        catch (OperationCanceledException)
        {
            return PackDownloadResult.Failed;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OCR pack for '{language}' failed: {ex}");
            return PackDownloadResult.Failed;
        }
    }

    // Tells the person why it failed; true when they chose Retry (only offered while the policy allows it).
    // A null name means the catalog itself could not be reached.
    private async Task<bool> TryAgainAsync(PackDownloadResult result, int attempt, string? name)
    {
        if (_ui.IsRetired)
        {
            return false;
        }

        string title = AppResources.Reader_OcrDownloadFailedTitle;
        string message = FailureMessage(PagePackRetry.Classify(result), name);
        if (PagePackRetry.OffersRetry(result, attempt))
        {
            return await _ui.AskRetryAsync(title, message);
        }

        await _ui.NotifyAsync(title, message);
        return false;
    }

    private static string FailureMessage(PackFailureKind kind, string? name)
    {
        if (name is null)
        {
            return AppResources.Reader_OcrOfflineMessage;
        }

        return kind == PackFailureKind.Offline
            ? AppResources.Format(AppResources.Reader_OcrDownloadFailedMessage, name)
            : AppResources.Format(AppResources.Reader_OcrFailedMessage, name);
    }

    // Only the pending pages are read again; a page still pending afterwards means the pack did not help.
    private async Task<IReadOnlyList<ScanPageRead>?> ReadPendingAgainAsync(
        IReadOnlyList<string> pages,
        IReadOnlyList<ScanPageRead> reads,
        IReadOnlyList<int> pending,
        string language,
        Func<IReadOnlyList<string>, string, Task<IReadOnlyList<ScanPageRead>>> readAgain)
    {
        IReadOnlyList<ScanPageRead> again = await readAgain(pending.Select(i => pages[i]).ToList(), language);
        if (_ui.IsRetired)
        {
            return null;
        }

        if (again.Count != pending.Count || again.Any(r => !PageLanguagePrompt.IsResolved(r.Source)))
        {
            await _ui.NotifyAsync(AppResources.Reader_OcrDownloadFailedTitle, AppResources.Format(AppResources.Reader_OcrFailedMessage, NativeLanguageName(language)));
            return null;
        }

        List<ScanPageRead> merged = reads.ToList();
        for (int i = 0; i < pending.Count; i++)
        {
            merged[pending[i]] = again[i];
        }

        return merged;
    }
}
