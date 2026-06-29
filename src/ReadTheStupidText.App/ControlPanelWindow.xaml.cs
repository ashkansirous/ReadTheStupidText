using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Application.Startup;
using ReadTheStupidText.Domain.Activity;
using ReadTheStupidText.Domain.Reading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;
using WinRT.Interop;

namespace ReadTheStupidText_App;

/// <summary>
/// The left-click control panel — the "Media Card" design (Decision 20): a
/// brand-gradient "now reading" header (waveform + status + transport) over a Fluent
/// settings list. A borderless, always-on-top window pinned above every other window:
/// it stays open until the user closes it (✕) or toggles the tray icon — it does not
/// dismiss on click-away (Decision 12 kept). The app keeps running in the tray; Quit
/// lives only in the right-click menu. Every control reads live state on open and
/// writes through the shared services, so it stays in sync with the tray menu.
/// </summary>
public sealed partial class ControlPanelWindow : Window
{
    // Segoe Fluent transport glyphs.
    private const string PlayGlyph = "";
    private const string PauseGlyph = "";

    private const string ReadyStatus = "Ready";
    private const string PausedStatus = "Paused";

    // Logical (effective-pixel) panel width and a generous fallback height used
    // until the content's real height is measured; both scaled to device pixels.
    private const int LogicalWidth = 376;
    private const int FallbackHeight = 520;
    private const int LogicalMargin = 12;

    private readonly ReadAloudService _readAloud;
    private readonly IStartupService _startup;
    private readonly ISettingsStore _settings;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly Brush _onIconBrush = new SolidColorBrush(Microsoft.UI.Colors.White);
    private Storyboard? _waveform;

    // Header-drag state: the screen cursor and window position captured when the drag
    // begins, so each move applies the raw cursor delta to the window (screen pixels,
    // not element-relative coords, which would jitter as the window moves under it).
    private bool _dragging;
    private PointInt32 _dragStartCursor;
    private PointInt32 _dragStartWindow;

    // Tolerance for matching the slider's value to a quick-preset chip.
    private const double PresetMatchTolerance = 0.001;

    // Suppresses control events while the panel is being populated from state,
    // so refreshing the UI does not echo back as a user change. Starts true so
    // the slider's initial coercion to its minimum (0.5) during XAML load isn't
    // mistaken for a user change and persisted over the real default (1x).
    private bool _refreshing = true;

    // The content's measured height (effective pixels), known after first layout.
    private double? _measuredHeight;

    /// <summary>Raised after the user changes the startup state from the panel,
    /// so the tray menu's matching toggle can be updated.</summary>
    public event EventHandler<bool>? StartupStateChanged;

    /// <summary>Raised when the activity-log button is clicked; the host owns the
    /// single activity-log window, so it handles opening/focusing it.</summary>
    public event EventHandler? ActivityLogRequested;

    public ControlPanelWindow(ReadAloudService readAloud, IStartupService startup, ISettingsStore settings)
    {
        _readAloud = readAloud;
        _startup = startup;
        _settings = settings;

        InitializeComponent();
        SystemBackdrop = new MicaBackdrop();
        _waveform = (Storyboard)RootGrid.Resources["WaveformStoryboard"];
        ConfigurePresenter();
        LoadVoices();

        RootGrid.Loaded += OnRootLoaded;

        // The icon toggles' colours are set as local brushes in code, so they don't
        // follow {ThemeResource} automatically — re-apply them when the theme flips.
        RootGrid.ActualThemeChanged += OnActualThemeChanged;

        _readAloud.StateChanged += OnPlaybackStateChanged;
        _readAloud.ProgressChanged += OnProgressChanged;

        // Neural voices arrive after the model loads; rebuild the picker then.
        _readAloud.VoicesChanged += OnVoicesChanged;
    }

    private void OnVoicesChanged(object? sender, EventArgs e) =>
        _dispatcher.TryEnqueue(() =>
        {
            LoadVoices();
            if (AppWindow.IsVisible)
            {
                PositionPanel();
            }
        });

    /// <summary>Opens the panel if hidden, hides it if shown (tray left-click).</summary>
    public void Toggle()
    {
        if (AppWindow.IsVisible)
        {
            Hide();
        }
        else
        {
            ShowPanel();
        }
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

    // Neural voices are the only selectable ones; until the model has loaded
    // the list is empty, shown as a "preparing" state rather than a hidden row.
    private void LoadVoices()
    {
        var voices = _readAloud.InstalledVoices;
        bool ready = voices.Count > 0;

        VoiceCombo.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
        VoiceStatus.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;

        if (ready)
        {
            _refreshing = true;
            VoiceCombo.ItemsSource = voices;
            SelectCurrentVoice();
            _refreshing = false;
        }
    }

    private void ShowPanel()
    {
        RefreshState();
        PositionPanel();
        AppWindow.Show();
        Activate();
    }

    private void Hide() => AppWindow.Hide();

    // Once the content has laid out we know its true height; remember it and
    // re-fit the window so nothing is clipped (the overflow fix).
    private void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        _measuredHeight = RootGrid.DesiredSize.Height;
        if (AppWindow.IsVisible)
        {
            PositionPanel();
        }
    }

    // Sizes the panel to its content (scaled to the monitor's DPI — AppWindow works
    // in physical device pixels) and places it: at the user's last-dragged position
    // if they've moved it (clamped to the work area so it can't end up offscreen),
    // otherwise pinned in the bottom-right corner just inside the work area.
    private void PositionPanel()
    {
        double scale = GetDpiForWindow(WindowNative.GetWindowHandle(this)) / 96.0;
        int width = (int)(LogicalWidth * scale);
        int height = (int)((_measuredHeight ?? FallbackHeight) * scale);
        int margin = (int)(LogicalMargin * scale);

        AppWindow.Resize(new SizeInt32(width, height));

        if (_settings.PanelPosition is { } saved)
        {
            RectInt32 savedWork = DisplayArea
                .GetFromPoint(new PointInt32(saved.X, saved.Y), DisplayAreaFallback.Nearest).WorkArea;
            AppWindow.Move(ClampToWorkArea(saved.X, saved.Y, width, height, savedWork));
            return;
        }

        RectInt32 work = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        AppWindow.Move(new PointInt32(
            work.X + work.Width - width - margin,
            work.Y + work.Height - height - margin));
    }

    // Keeps a point within the work area so a window of the given size stays fully
    // on-screen (e.g. after the panel grew, or a monitor was removed/rearranged).
    private static PointInt32 ClampToWorkArea(int x, int y, int width, int height, RectInt32 work)
    {
        int maxX = Math.Max(work.X, work.X + work.Width - width);
        int maxY = Math.Max(work.Y, work.Y + work.Height - height);
        return new PointInt32(Math.Clamp(x, work.X, maxX), Math.Clamp(y, work.Y, maxY));
    }

    // Header pointer-drag (Slice 24, Decision 31): drag the borderless panel by its
    // gradient header. Child controls (buttons, slider, pill) handle their own pointer
    // input and mark it handled, so a drag only starts on the header's empty areas.
    private void OnHeaderPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(HeaderBorder).Properties.IsLeftButtonPressed)
        {
            return;
        }

        GetCursorPos(out POINT cursor);
        _dragStartCursor = new PointInt32(cursor.X, cursor.Y);
        _dragStartWindow = AppWindow.Position;
        _dragging = HeaderBorder.CapturePointer(e.Pointer);
        e.Handled = _dragging;
    }

    private void OnHeaderPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        GetCursorPos(out POINT cursor);
        AppWindow.Move(new PointInt32(
            _dragStartWindow.X + (cursor.X - _dragStartCursor.X),
            _dragStartWindow.Y + (cursor.Y - _dragStartCursor.Y)));
        e.Handled = true;
    }

    private void OnHeaderPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_dragging)
        {
            HeaderBorder.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }
    }

    // Ending the drag — on button release the capture is lost, which fires here.
    // Persists the final position so the panel reopens where the user left it.
    private void OnHeaderPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        PointInt32 position = AppWindow.Position;
        _settings.PanelPosition = new PanelPosition(position.X, position.Y);
    }

    private void RefreshState()
    {
        _refreshing = true;
        UpdateSpeed(_readAloud.Speed);
        SelectCurrentVoice();
        SetSelectionToggle(_readAloud.AutoReadOnSelection);
        SetCopyToggle(_readAloud.AutoReadOnCopy);
        _refreshing = false;

        ApplyPlaybackState(_readAloud.State);
        _ = RefreshStartupAsync();
    }

    private void UpdateSpeed(PlaybackRate rate)
    {
        SpeedSlider.Value = rate.Value;
        SpeedPill.Text = rate.ToDisplayLabel();
        UpdatePresetHighlight(rate);
    }

    // The three compact icon toggles carry a hover tooltip naming the control and
    // its current state (the design's "· on" / "· off" tooltips).
    private void SetSelectionToggle(bool on)
    {
        AutoReadSelectionToggle.IsChecked = on;
        ApplyToggleVisual(AutoReadSelectionToggle, SelectionIcon, on);
        ToolTipService.SetToolTip(AutoReadSelectionToggle,
            $"Auto-read selection · {OnOff(on)} — Reads aloud as you select");
    }

    private void SetCopyToggle(bool on)
    {
        AutoReadCopyToggle.IsChecked = on;
        ApplyToggleVisual(AutoReadCopyToggle, CopyIcon, on);
        ToolTipService.SetToolTip(AutoReadCopyToggle,
            $"Auto-read on copy · {OnOff(on)} — Reads aloud when you copy");
    }

    private void SetStartupToggleTooltip(bool on) =>
        ToolTipService.SetToolTip(StartupToggle,
            $"Launch at startup · {OnOff(on)} — Start with Windows, in the tray");

    private static string OnOff(bool on) => on ? "on" : "off";

    // Off = card fill + hairline border + muted (grey) icon; on = accent fill +
    // white icon. Set explicitly (rather than via visual states) so toggling off
    // reliably returns the icon to grey. Brushes are resolved from the active
    // theme dictionary so they stay correct in light and dark. The icons are now
    // stroked line glyphs, so the colour drives Stroke rather than Foreground.
    private void ApplyToggleVisual(ToggleButton toggle, Shape icon, bool on)
    {
        toggle.Background = ThemeBrush(on ? "PanelAccent" : "PanelCard");
        toggle.BorderBrush = ThemeBrush(on ? "PanelAccent" : "PanelStroke");
        icon.Stroke = on ? _onIconBrush : ThemeBrush("PanelText2");
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        ApplyToggleVisual(AutoReadSelectionToggle, SelectionIcon, AutoReadSelectionToggle.IsChecked == true);
        ApplyToggleVisual(AutoReadCopyToggle, CopyIcon, AutoReadCopyToggle.IsChecked == true);
        ApplyToggleVisual(StartupToggle, StartupIcon, StartupToggle.IsChecked == true);
    }

    // Pulls a panel brush from the active light/dark theme dictionary by key.
    private Brush ThemeBrush(string key)
    {
        string theme = RootGrid.ActualTheme == ElementTheme.Dark ? "Dark" : "Light";
        var dictionary = (ResourceDictionary)RootGrid.Resources.ThemeDictionaries[theme];
        return (Brush)dictionary[key];
    }

    private void SelectCurrentVoice()
    {
        string? currentId = _readAloud.CurrentVoiceId;
        VoiceCombo.SelectedItem = _readAloud.InstalledVoices.FirstOrDefault(v => v.Id == currentId);
    }

    private async Task RefreshStartupAsync()
    {
        bool enabled = await _startup.IsEnabledAsync();
        _dispatcher.TryEnqueue(() => SetStartupSwitch(enabled));
    }

    private void SetStartupSwitch(bool enabled)
    {
        _refreshing = true;
        StartupToggle.IsChecked = enabled;
        ApplyToggleVisual(StartupToggle, StartupIcon, enabled);
        SetStartupToggleTooltip(enabled);
        _refreshing = false;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Hide();

    private void OnPlayPauseClick(object sender, RoutedEventArgs e) => _ = _readAloud.PlayPauseOrReadAsync();

    private void OnActivityLogClick(object sender, RoutedEventArgs e) =>
        ActivityLogRequested?.Invoke(this, EventArgs.Empty);

    // Reveal/hide the fine speed slider; the chevron flips and the panel re-fits
    // its height since the content grew or shrank.
    private void OnSpeedPillToggled(object sender, RoutedEventArgs e)
    {
        bool expanded = SpeedPillToggle.IsChecked == true;
        SpeedRevealPanel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        SpeedChevronRotate.Angle = expanded ? 180 : 0;
        RefitToContent();
    }

    // Re-measures the content (its height changed) and re-fits the window so the
    // bottom-right pin stays flush against the work area with nothing clipped.
    private void RefitToContent()
    {
        RootGrid.UpdateLayout();
        _measuredHeight = RootGrid.DesiredSize.Height;
        if (AppWindow.IsVisible)
        {
            PositionPanel();
        }
    }

    private void OnSpeedSliderChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_refreshing)
        {
            return;
        }

        var rate = new PlaybackRate(e.NewValue);
        SpeedPill.Text = rate.ToDisplayLabel();
        UpdatePresetHighlight(rate);
        _readAloud.SetSpeed(rate);
    }

    // A quick-preset chip sets the slider, which flows through OnSpeedSliderChanged
    // to update the pill, the highlight, and the service. Clicking the active chip
    // is a no-op (the value doesn't change).
    private void OnSpeedPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } &&
            double.TryParse(tag, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            SpeedSlider.Value = new PlaybackRate(value).Value;
        }
    }

    // Highlights the chip whose rate matches the current value; the others revert to
    // the translucent off look. The active chip is solid white with blue, bold text.
    private void UpdatePresetHighlight(PlaybackRate rate)
    {
        foreach (var child in SpeedPresetRow.Children)
        {
            if (child is Button { Tag: string tag } button &&
                double.TryParse(tag, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                ApplyPresetVisual(button, Math.Abs(value - rate.Value) < PresetMatchTolerance);
            }
        }
    }

    private void ApplyPresetVisual(Button button, bool active)
    {
        button.Background = active ? _onIconBrush : (Brush)RootGrid.Resources["HeaderPresetFill"];
        button.Foreground = active ? (Brush)RootGrid.Resources["HeaderPresetActiveText"] : (Brush)RootGrid.Resources["HeaderText"];
        button.FontWeight = active ? FontWeights.Bold : FontWeights.SemiBold;
    }

    private void OnVoiceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing || VoiceCombo.SelectedItem is not VoiceInfo voice)
        {
            return;
        }

        _readAloud.SetVoice(voice.Id);
    }

    private void OnAutoReadSelectionToggled(object sender, RoutedEventArgs e)
    {
        bool on = AutoReadSelectionToggle.IsChecked == true;
        ApplyToggleVisual(AutoReadSelectionToggle, SelectionIcon, on);
        ToolTipService.SetToolTip(AutoReadSelectionToggle,
            $"Auto-read selection · {OnOff(on)} — Reads aloud as you select");
        if (!_refreshing)
        {
            _readAloud.AutoReadOnSelection = on;
        }
    }

    private void OnAutoReadCopyToggled(object sender, RoutedEventArgs e)
    {
        bool on = AutoReadCopyToggle.IsChecked == true;
        ApplyToggleVisual(AutoReadCopyToggle, CopyIcon, on);
        ToolTipService.SetToolTip(AutoReadCopyToggle,
            $"Auto-read on copy · {OnOff(on)} — Reads aloud when you copy");
        if (!_refreshing)
        {
            _readAloud.AutoReadOnCopy = on;
        }
    }

    private void OnStartupToggled(object sender, RoutedEventArgs e)
    {
        bool on = StartupToggle.IsChecked == true;
        ApplyToggleVisual(StartupToggle, StartupIcon, on);
        SetStartupToggleTooltip(on);
        if (!_refreshing)
        {
            _ = ApplyStartupAsync(on);
        }
    }

    // Reflects the actual resulting state (enabling can be refused by the user
    // or policy) and notifies the tray menu so its toggle agrees.
    private async Task ApplyStartupAsync(bool requested)
    {
        bool enabled = await _startup.SetEnabledAsync(requested);
        _dispatcher.TryEnqueue(() =>
        {
            SetStartupSwitch(enabled);
            StartupStateChanged?.Invoke(this, enabled);
        });
    }

    private void OnPlaybackStateChanged(object? sender, PlaybackState state) =>
        _dispatcher.TryEnqueue(() => ApplyPlaybackState(state));

    // Drives every state-dependent header element together: the play/pause glyph,
    // the dynamic status line, and the waveform animation. Progress is cleared when
    // idle so the bar reads empty on Ready.
    private void ApplyPlaybackState(PlaybackState state)
    {
        PlayPauseIcon.Glyph = state == PlaybackState.Playing ? PauseGlyph : PlayGlyph;
        StatusText.Text = DescribeStatus(state);
        SetWaveformPlaying(state == PlaybackState.Playing);
        if (state == PlaybackState.Idle)
        {
            SetProgress(0);
        }
    }

    // The status line is composed here (presentation), from the structured read
    // source the service exposes: clipboard reads name no window; selection reads
    // name the foreground app when known.
    private string DescribeStatus(PlaybackState state)
    {
        if (state == PlaybackState.Paused)
        {
            return PausedStatus;
        }

        if (state != PlaybackState.Playing)
        {
            return ReadyStatus;
        }

        if (_readAloud.CurrentReadTrigger == ActivityTrigger.Clipboard)
        {
            return "Reading clipboard…";
        }

        string? app = _readAloud.CurrentReadWindow?.App;
        return string.IsNullOrWhiteSpace(app)
            ? "Reading selection…"
            : $"Reading selection from {app}…";
    }

    private void SetWaveformPlaying(bool playing)
    {
        if (_waveform is null)
        {
            return;
        }

        if (playing)
        {
            _waveform.Begin();
        }
        else
        {
            _waveform.Stop();
        }
    }

    private void OnProgressChanged(object? sender, double progress) =>
        _dispatcher.TryEnqueue(() => SetProgress(progress));

    // The progress bar is two star-weighted columns (fill / rest); the thumb sits
    // at their boundary. Display-only — seeking is out of scope (Decision 21).
    private void SetProgress(double progress)
    {
        double fill = Math.Clamp(progress, 0, 1);
        ProgressFillColumn.Width = new GridLength(fill, GridUnitType.Star);
        ProgressRestColumn.Width = new GridLength(1 - fill, GridUnitType.Star);
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    // The current cursor position in physical screen pixels — the same space as
    // AppWindow.Position, so drag deltas apply directly without DPI conversion.
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT point);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }
}
