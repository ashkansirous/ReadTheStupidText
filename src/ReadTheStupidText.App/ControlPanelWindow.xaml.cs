using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Startup;
using ReadTheStupidText.Domain.Activity;
using ReadTheStupidText.Domain.Reading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
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
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private Storyboard? _waveform;

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

    public ControlPanelWindow(ReadAloudService readAloud, IStartupService startup)
    {
        _readAloud = readAloud;
        _startup = startup;

        InitializeComponent();
        SystemBackdrop = new MicaBackdrop();
        _waveform = (Storyboard)RootGrid.Resources["WaveformStoryboard"];
        ConfigurePresenter();
        LoadVoices();

        RootGrid.Loaded += OnRootLoaded;
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

    // Places the panel in the bottom-right corner just inside the work area,
    // sized to its content and scaled to the monitor's DPI (AppWindow works in
    // physical device pixels).
    private void PositionPanel()
    {
        double scale = GetDpiForWindow(WindowNative.GetWindowHandle(this)) / 96.0;
        int width = (int)(LogicalWidth * scale);
        int height = (int)((_measuredHeight ?? FallbackHeight) * scale);
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
        UpdateSpeed(_readAloud.Speed);
        SelectCurrentVoice();
        AutoReadSelectionSwitch.IsOn = _readAloud.AutoReadOnSelection;
        AutoReadCopySwitch.IsOn = _readAloud.AutoReadOnCopy;
        _refreshing = false;

        ApplyPlaybackState(_readAloud.State);
        _ = RefreshStartupAsync();
    }

    private void UpdateSpeed(PlaybackRate rate)
    {
        SpeedSlider.Value = rate.Value;
        SpeedLabel.Text = rate.ToDisplayLabel();
        SpeedPill.Text = rate.ToDisplayLabel();
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

    private void OnPlayPauseClick(object sender, RoutedEventArgs e) => _ = _readAloud.PlayPauseOrReadAsync();

    private void OnSpeedSliderChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_refreshing)
        {
            return;
        }

        var rate = new PlaybackRate(e.NewValue);
        SpeedLabel.Text = rate.ToDisplayLabel();
        SpeedPill.Text = rate.ToDisplayLabel();
        _readAloud.SetSpeed(rate);
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
        if (!_refreshing)
        {
            _readAloud.AutoReadOnSelection = AutoReadSelectionSwitch.IsOn;
        }
    }

    private void OnAutoReadCopyToggled(object sender, RoutedEventArgs e)
    {
        if (!_refreshing)
        {
            _readAloud.AutoReadOnCopy = AutoReadCopySwitch.IsOn;
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
}
