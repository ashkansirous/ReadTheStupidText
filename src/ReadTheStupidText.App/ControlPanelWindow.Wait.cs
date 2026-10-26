using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace ReadTheStupidText_App;

/// <summary>
/// The panel's side of the wait indicator (Slice 141, Batch 29 frame W2): the play button becomes the ring (a pack download's percent, Cancel on click) while a
/// read waits past 500 ms, and the header subtitle reads the stage. The timing and the stage come from <see cref="WaitFlyoutController"/>, which shows the tray flyout instead
/// while this panel is closed.
/// </summary>
public sealed partial class ControlPanelWindow
{
    private WaitFlyoutController? _wait;
    private string _playPauseName = string.Empty;
    private object? _playPauseTip;

    // Runs in the constructor. The controller is the app's only one; creating it here (the panel exists from launch) starts the tray flyout for reads made with the panel closed.
    private void InitWaitRow()
    {
        _playPauseName = AutomationProperties.GetName(PlayPauseButton);
        _playPauseTip = ToolTipService.GetToolTip(PlayPauseButton);
        _wait = WaitFlyoutController.Get(_readAloud, cancelRead: _readAloud.CancelRead);
        _wait.IsPanelOpen = () => AppWindow.IsVisible;
        _wait.Changed += (_, _) => ApplyWait();
        AppWindow.Changed += (_, args) =>
        {
            if (args.DidVisibilityChange)
            {
                OnPanelVisibilityChanged(); // minimize or close also moves the wait to the flyout
            }
        };
    }

    // The panel opened or closed: the wait moves between this row and the tray flyout.
    private void OnPanelVisibilityChanged()
    {
        ApplyWait();
        _wait?.PanelVisibilityChanged();
    }

    private void ApplyWait()
    {
        WaitDisplay? wait = _wait?.Current;
        SetGenerating(_readAloud.IsGeneratingAudio); // the play button's slot shows the ring (and Cancel) while the wait shows
        TextFlow.ForUi(StatusText, wait?.Line ?? DescribeStatus(_readAloud.State)); // the header subtitle reads the stage
        PaintWait(wait);
    }

    // The play button becomes the ring: a pack download's percent fills it, Cancel is its click, name and tooltip.
    private void PaintWait(WaitDisplay? wait)
    {
        PlayPauseRing.IsIndeterminate = wait?.Percent is null;
        PlayPauseRing.Value = wait?.Percent ?? 0;
        string? label = wait is { CanCancel: true } ? UiStrings.Get("Wait_Cancel") : null;
        AutomationProperties.SetName(PlayPauseButton, label ?? _playPauseName);
        ToolTipService.SetToolTip(PlayPauseButton, label ?? _playPauseTip);
    }

}
