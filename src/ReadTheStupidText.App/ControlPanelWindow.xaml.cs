using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Startup;
using ReadTheStupidText.Domain.Reading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;

namespace ReadTheStupidText_App;

/// <summary>
/// The left-click control panel. A borderless, always-on-top window positioned
/// above the taskbar that mirrors every read-aloud control in one place. It
/// light-dismisses on <see cref="Window.Activated"/> losing focus and only ever
/// hides — the app keeps running in the tray. Every control reads live state on
/// open and writes through the shared services, so it stays in sync with the
/// right-click tray menu.
/// </summary>
public sealed partial class ControlPanelWindow : Window
{
    private const string PlayLabel = "Play";
    private const string PauseLabel = "Pause";

    // Logical (effective-pixel) panel size; scaled to device pixels per monitor.
    private const int LogicalWidth = 300;
    private const int LogicalHeight = 360;
    private const int LogicalMargin = 12;

    // Window swallows the tray click that dismissed it; ignore a reopen that
    // arrives within this window of the hide.
    private const long ReopenGuardMs = 350;

    private readonly ReadAloudService _readAloud;
    private readonly IStartupService _startup;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly ReadingSpeed[] _speeds = Enum.GetValues<ReadingSpeed>();

    // Suppresses control events while the panel is being populated from state,
    // so refreshing the UI does not echo back as a user change.
    private bool _refreshing;
    private long _lastHiddenTick;

    /// <summary>Raised after the user changes the startup state from the panel,
    /// so the tray menu's matching toggle can be updated.</summary>
    public event EventHandler<bool>? StartupStateChanged;

    public ControlPanelWindow(ReadAloudService readAloud, IStartupService startup)
    {
        _readAloud = readAloud;
        _startup = startup;

        InitializeComponent();
        SystemBackdrop = new MicaBackdrop();
        ConfigurePresenter();
        LoadVoices();

        Activated += OnActivated;
        _readAloud.StateChanged += OnPlaybackStateChanged;
    }

    /// <summary>Opens the panel if hidden, hides it if shown (tray left-click).</summary>
    public void Toggle()
    {
        if (AppWindow.IsVisible)
        {
            Hide();
            return;
        }

        if (Environment.TickCount64 - _lastHiddenTick < ReopenGuardMs)
        {
            return;
        }

        ShowPanel();
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

    private void LoadVoices()
    {
        var voices = _readAloud.InstalledVoices;
        if (voices.Count == 0)
        {
            VoiceRow.Visibility = Visibility.Collapsed;
            return;
        }

        VoiceCombo.ItemsSource = voices;
    }

    private void ShowPanel()
    {
        RefreshState();
        PositionAboveTray();
        AppWindow.Show();
        Activate();
    }

    private void Hide()
    {
        _lastHiddenTick = Environment.TickCount64;
        AppWindow.Hide();
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            Hide();
        }
    }

    // Places the panel in the bottom-right corner just inside the work area,
    // scaled to the monitor's DPI (AppWindow works in physical device pixels).
    private void PositionAboveTray()
    {
        double scale = GetDpiForWindow(WindowNative.GetWindowHandle(this)) / 96.0;
        int width = (int)(LogicalWidth * scale);
        int height = (int)(LogicalHeight * scale);
        int margin = (int)(LogicalMargin * scale);

        DisplayArea area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        RectInt32 work = area.WorkArea;
        int x = work.X + work.Width - width - margin;
        int y = work.Y + work.Height - height - margin;

        AppWindow.Resize(new SizeInt32(width, height));
        AppWindow.Move(new PointInt32(x, y));
    }

    private void RefreshState()
    {
        _refreshing = true;
        SetPlayPauseLabel(_readAloud.State);
        UpdateSpeed(_readAloud.Speed);
        SelectCurrentVoice();
        AutoReadSwitch.IsOn = _readAloud.IsEnabled;
        _refreshing = false;

        _ = RefreshStartupAsync();
    }

    private void UpdateSpeed(ReadingSpeed speed)
    {
        int index = Array.IndexOf(_speeds, speed);
        SpeedSlider.Value = index < 0 ? 0 : index;
        SpeedLabel.Text = speed.ToDisplayLabel();
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
        StartupSwitch.IsOn = enabled;
        _refreshing = false;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Hide();

    private void OnPlayPauseClick(object sender, RoutedEventArgs e) => _readAloud.TogglePlayPause();

    private void OnSpeedSliderChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_refreshing)
        {
            return;
        }

        ReadingSpeed speed = _speeds[(int)e.NewValue];
        SpeedLabel.Text = speed.ToDisplayLabel();
        _readAloud.SetSpeed(speed);
    }

    private void OnVoiceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing || VoiceCombo.SelectedItem is not VoiceInfo voice)
        {
            return;
        }

        _readAloud.SetVoice(voice.Id);
    }

    private void OnAutoReadToggled(object sender, RoutedEventArgs e)
    {
        if (!_refreshing)
        {
            _readAloud.IsEnabled = AutoReadSwitch.IsOn;
        }
    }

    private void OnStartupToggled(object sender, RoutedEventArgs e)
    {
        if (!_refreshing)
        {
            _ = ApplyStartupAsync(StartupSwitch.IsOn);
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
        _dispatcher.TryEnqueue(() => SetPlayPauseLabel(state));

    private void SetPlayPauseLabel(PlaybackState state) =>
        PlayPauseButton.Content = state == PlaybackState.Playing ? PauseLabel : PlayLabel;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
