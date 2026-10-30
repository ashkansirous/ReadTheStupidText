using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Mobile.Shared.Waiting;

namespace ReadTheStupidText.Mobile.Shared.ModelDownload;

/// <summary>The words of a translation model download wait, in the app's language (Slice 187); the two apps bring their own resources.</summary>
/// <param name="Language">The name of the language that is downloading.</param>
/// <param name="Downloading">The line while it downloads.</param>
/// <param name="DownloadingDetail">The second line while it downloads, or null.</param>
/// <param name="WaitingInternet">"Waiting for the internet".</param>
/// <param name="WaitingWifi">"Waiting for Wi-Fi", used on mobile data when the download needs Wi-Fi.</param>
/// <param name="WaitingPercent">"{0} is {1}% downloaded. It carries on by itself...": {0} the language, {1} the percent.</param>
/// <param name="WaitingNoPercent">"Downloading {0}. It carries on by itself...": for a platform that reports no percentage (Android).</param>
/// <param name="Retry">The Retry button.</param>
public sealed record TranslationDownloadTexts(
    string Language, string Downloading, string? DownloadingDetail, string WaitingInternet, string WaitingWifi, string WaitingPercent, string WaitingNoPercent, string Retry);

/// <summary>
/// Shows a <see cref="TranslationModelDownloadSession"/> on a page's <see cref="WaitController"/> (Slice 187): the downloading overlay while it runs, the
/// waiting-for-internet look (no-Wi-Fi icon, grey bar, Retry next to Cancel) while it waits, and back to downloading when it carries on. It reads the
/// session and sends Retry to it; Cancel stays the page's own (it cancels the session's token). The caller ends the wait after the session ends, and disposes this first.
/// Only one view owns a page's controller at a time: a view made with <c>owner: false</c> (the early download) shows and handles Retry only after <see cref="TakeOver"/>.
/// </summary>
public sealed class TranslationDownloadWait : IDisposable
{
    private readonly WaitController _wait;
    private readonly TranslationModelDownloadSession _session;
    private readonly TranslationDownloadTexts _texts;
    private readonly Func<bool> _cellularOnly;
    private volatile bool _owner;
    private volatile bool _disposed;

    public TranslationDownloadWait(WaitController wait, TranslationModelDownloadSession session, TranslationDownloadTexts texts, Func<bool>? cellularOnly = null, bool owner = true)
    {
        _wait = wait;
        _session = session;
        _texts = texts;
        _cellularOnly = cellularOnly ?? Network.PackResumeWatcher.CellularOnly;
        _session.Changed += OnChanged;
        if (owner)
        {
            TakeOwnership();
        }
    }

    /// <summary>Shows the session's current state now (the page calls it when it starts the wait).</summary>
    public void Show() => Apply(_session.Snapshot, requireActiveWait: false);

    /// <summary>Becomes the controller's one owner (a tap began while this download ran early) and shows the session's state at once.</summary>
    public void TakeOver()
    {
        TakeOwnership();
        Show();
    }

    private void TakeOwnership()
    {
        if (_owner || _disposed)
        {
            return;
        }

        _owner = true;
        _wait.RetryRequested += OnRetryRequested;
    }

    public void Dispose()
    {
        _disposed = true;
        _session.Changed -= OnChanged;
        if (_owner)
        {
            _owner = false;
            _wait.RetryRequested -= OnRetryRequested;
        }
    }

    private void OnRetryRequested()
    {
        if (!_session.Retry())
        {
            _wait.RearmRetry(); // the download had already moved on: the tap did nothing, so Retry stays usable
        }
    }

    private void OnChanged(TranslationDownloadSnapshot snapshot) =>
        MainThread.BeginInvokeOnMainThread(() => Apply(snapshot, requireActiveWait: true));

    // A queued snapshot can arrive after the page ended its wait: it must never start a new wait nothing would end.
    private void Apply(TranslationDownloadSnapshot snapshot, bool requireActiveWait)
    {
        if (_disposed || !_owner || (requireActiveWait && (!_wait.IsActive || _wait.Stage != WaitStage.DownloadingModel))) // another stage's wait is not ours to re-word
        {
            return;
        }

        if (snapshot.State == TranslationDownloadState.WaitingForInternet)
        {
            _wait.Begin(WaitStage.DownloadingModel, WaitingLine(), canCancel: true, WaitingDetail(snapshot), new WaitLook(true, snapshot.Progress, _texts.Retry));
        }
        else if (snapshot.State == TranslationDownloadState.Downloading)
        {
            WaitLook? look = snapshot.Progress is null ? null : new WaitLook(false, snapshot.Progress, null);
            _wait.Begin(WaitStage.DownloadingModel, _texts.Downloading, canCancel: true, _texts.DownloadingDetail, look);
        }
    }

    private string WaitingLine() => _cellularOnly() ? _texts.WaitingWifi : _texts.WaitingInternet;

    private string WaitingDetail(TranslationDownloadSnapshot snapshot) => snapshot.Progress is { } fraction
        ? string.Format(System.Globalization.CultureInfo.CurrentUICulture, _texts.WaitingPercent, _texts.Language, ReadWaitStages.Percent(fraction))
        : string.Format(System.Globalization.CultureInfo.CurrentUICulture, _texts.WaitingNoPercent, _texts.Language);
}
