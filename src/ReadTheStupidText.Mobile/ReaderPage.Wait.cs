using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Usage;
using ReadTheStupidText.Mobile.Resources.Strings;
using ReadTheStupidText.Mobile.Shared.Waiting;

namespace ReadTheStupidText.Mobile;

// The wait overlay of the main page (Slice 138, Decision 146): over the text box, 500 ms after the wait for the first sound
// begins, gone the moment audio starts. Cancel stops the read and leaves the text as it was.
public partial class ReaderPage
{
    private const double DimmedPlayerOpacity = 0.5;
    private const double LineSize = 18;
    private const double DetailSize = 13.5;
    private const double CancelHeight = 48;
    private const double CancelRadius = 14;
    private const double CancelSidePadding = 28;

    private WaitController _wait = null!;
    private WaitOverlay _waitOverlay = null!;
    private WaitThemeFollower _waitTheme = null!;

    /// <summary>The look of RTST's wait overlay (Batch 29 mock R8 and R9), also used by the review page.</summary>
    internal static WaitOverlay CreateWaitOverlay()
    {
        WaitOverlayStyle style = CreateWaitStyle();
        string cancelText = AppResources.Common_Cancel;
        return new WaitOverlay(style, cancelText);
    }

    /// <summary>The overlay's colours for the theme now showing; sizes are the mock's.</summary>
    internal static WaitOverlayStyle CreateWaitStyle()
    {
        bool dark = ThemeColors.IsDark();
        var style = new WaitOverlayStyle
        {
            Fill = ThemeColors.Get(dark ? ThemeColors.WaitFillDarkKey : ThemeColors.WaitFillLightKey),
            LineSize = LineSize,
            DetailSize = DetailSize,
            CancelHeight = CancelHeight,
            CancelSize = 16,
            CancelCornerRadius = CancelRadius,
            CancelSidePadding = CancelSidePadding,
        };
        if (dark)
        {
            style = style with
            {
                LineColor = ThemeColors.Get(ThemeColors.TextPrimaryDarkKey),
                DetailColor = ThemeColors.Get(ThemeColors.WaitDetailDarkKey),
                CancelFill = ThemeColors.Get(ThemeColors.WaitCancelFillDarkKey),
                CancelText = ThemeColors.Get(ThemeColors.WaitCancelTextDarkKey),
            };
        }

        return style;
    }

    private void InitWait()
    {
        _waitOverlay = CreateWaitOverlay();
        TextCardContent.Children.Add(_waitOverlay);
        _wait = new WaitController(_waitOverlay);
        _waitTheme = new WaitThemeFollower(_wait, _waitOverlay);
        _wait.CancelRequested += OnWaitCancelled;
        _wait.ShownChanged += OnWaitShownChanged;
    }

    // R9: the player is greyed and untouchable only while the preparing overlay is really on screen, so a read that starts
    // within 500 ms never flashes it, and a stray Idle event cannot wake it (RenderPlayerForState honours PlayerDimmedByWait).
    private void OnWaitShownChanged(bool shown)
    {
        RenderPlayerForState();
    }

    private bool PlayerDimmedByWait => _waitOverlay is { IsVisible: true } && _wait.Stage == WaitStage.PreparingVoice;

    // Slice 148: a read that had started waits for its next chunk. The same overlay says so ("Still getting the voice ready...") after 500 ms
    // and goes with the next sound; the preparing wait and the file import keep the overlay when they own it. The signal comes from any thread.
    private void OnReaderStallChanged(object? sender, bool stalled)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (stalled)
            {
                BeginStallWait();
            }
            else
            {
                EndStallWait();
            }
        });
    }

    private void BeginStallWait()
    {
        if (!_wait.IsActive)
        {
            _wait.Begin(WaitStage.Stalled, AppResources.Wait_Stalled, canCancel: true);
        }
    }

    private void EndStallWait()
    {
        if (_wait.IsActive && _wait.Stage == WaitStage.Stalled)
        {
            _wait.End();
        }
    }

    // Cancel: whatever is generating stops, the text stays, and nothing is counted as a failure.
    private void OnWaitCancelled()
    {
        _reader.Stop();
        EndStallWait();
        EndFirstAudioClock(UsageOutcome.Cancelled);
        ClearPendingRead();
    }

    // Cancel on the overlay is the way out while the voice prepares. Another wait (the file import) keeps the overlay: it
    // must not be re-worded into this one, ended by this one's end, or have its Cancel stop the read.
    private void BeginPreparingWait()
    {
        if (_wait.IsActive && _wait.Stage != WaitStage.PreparingVoice)
        {
            return;
        }

        _wait.Begin(WaitStage.PreparingVoice, AppResources.Wait_PreparingVoice, canCancel: true);
    }
}
