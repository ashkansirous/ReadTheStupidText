using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Windows.Graphics;
using WinRT.Interop;

namespace ReadTheStupidText_App;

/// <summary>
/// The wait flyout by the tray clock (Slice 141, Decision 146): a borderless always-on-top card that shows the stage a hotkey or
/// selection read is waiting in, a bar for a pack download's progress, and Cancel. It only paints a <see cref="WaitDisplay"/>;
/// <see cref="WaitFlyoutController"/> decides when. Shown without taking focus, so it never steals the text selection.
/// </summary>
public sealed partial class WaitFlyoutWindow : Window
{
    private const int LogicalWidth = 320;
    private const int LogicalMargin = 16;
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    /// <summary>The user pressed Cancel.</summary>
    public event EventHandler? CancelRequested;

    private bool _canShow = true;
    private (bool Detail, bool Bar, bool Cancel)? _layout;

    public WaitFlyoutWindow()
    {
        InitializeComponent();
        Title = ProductInfo.Name;
        RootGrid.FlowDirection = FlowDirection.LeftToRight; // layout never mirrors (Slice 88)
        SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
        CancelButton.Content = UiStrings.Get("Wait_Cancel");
        ConfigurePresenter();
        MakeNonActivating();
        AppWindow.Closing += OnClosing;
    }

    // Clicking Cancel or the card must not move focus off the app with the selected text, and the window stays out of Alt+Tab.
    // If the style cannot be set the card would take focus (clearing the selection and cancelling the read it shows), so it is never shown.
    private void MakeNonActivating()
    {
        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        int style = GetWindowLong(hwnd, GwlExStyle);
        Marshal.SetLastPInvokeError(0);
        int previous = SetWindowLong(hwnd, GwlExStyle, style | WsExNoActivate | WsExToolWindow);
        _canShow = previous != 0 || Marshal.GetLastPInvokeError() == 0;
    }

    // Alt+F4 on the card only hides it: a closed window would throw on the next ShowFor.
    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        args.Cancel = true;
        HideFlyout();
    }

    private void ConfigurePresenter()
    {
        var presenter = OverlappedPresenter.Create();
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;
    }

    /// <summary>Paints <paramref name="display"/> and shows the flyout (without activating it) in the corner by the tray.</summary>
    public void ShowFor(WaitDisplay display)
    {
        if (!_canShow)
        {
            return;
        }

        TextFlow.ForUi(TitleText, display.Line);
        TextFlow.ForUi(SubtitleText, display.Detail ?? ProductInfo.Name);
        Bar.Visibility = display.Percent is null ? Visibility.Collapsed : Visibility.Visible;
        Bar.Value = display.Percent ?? 0;
        CancelButton.Visibility = display.CanCancel ? Visibility.Visible : Visibility.Collapsed;
        var layout = (display.Detail is not null, display.Percent is not null, display.CanCancel);
        if (!AppWindow.IsVisible || _layout != layout)
        {
            _layout = layout; // a progress tick changes text and bar only: no re-measure, move or re-show
            Place();
            AppWindow.Show(false);
        }
    }

    /// <summary>Hides the flyout.</summary>
    public void HideFlyout()
    {
        _layout = null;
        AppWindow.Hide();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => CancelRequested?.Invoke(this, EventArgs.Empty);

    // Sized to the content and pinned bottom-right of the work area, where the control panel opens (the shared placer).
    private void Place()
    {
        RootGrid.Measure(new Size(LogicalWidth, double.PositiveInfinity));
        WindowPlacer.Place(AppWindow, LogicalWidth, RootGrid.DesiredSize.Height, LogicalWidth, LogicalMargin, null);
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
}
