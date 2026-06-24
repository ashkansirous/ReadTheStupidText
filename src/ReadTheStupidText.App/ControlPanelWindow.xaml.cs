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
/// The left-click control panel. A borderless, always-on-top window pinned above
/// every other window: it stays open until the user closes it (✕) or toggles the
/// tray icon — it does not dismiss on click-away. The app keeps running in the
/// tray; Quit lives only in the right-click menu. Every control reads live state
/// on open and writes through the shared services, so it stays in sync with the
/// tray menu.
/// </summary>
public sealed partial class ControlPanelWindow : Window
{
    private const string PlayLabel = "Play";
    private const string PauseLabel = "Pause";

    // Logical (effective-pixel) panel width and a generous fallback height used
    // until the content's real height is measured; both scaled to device pixels.
    private const int LogicalWidth = 300;
    private const int FallbackHeight = 460;
    private const int LogicalMargin = 12;

    private readonly ReadAloudService _readAloud;
    private readonly IStartupService _startup;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();

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
        ConfigurePresenter();
        LoadVoices();

        RootGrid.Loaded += OnRootLoaded;
        _readAloud.StateChanged += OnPlaybackStateChanged;

        // Neural voices arrive after the model downloads; rebuild the picker then.
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

    // Neural voices are the only selectable ones; until the model has downloaded
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
        SetPlayPauseLabel(_readAloud.State);
        UpdateSpeed(_readAloud.Speed);
        SelectCurrentVoice();
        AutoReadSwitch.IsOn = _readAloud.IsEnabled;
        _refreshing = false;

        _ = RefreshStartupAsync();
    }

    private void UpdateSpeed(PlaybackRate rate)
    {
        SpeedSlider.Value = rate.Value;
        SpeedLabel.Text = rate.ToDisplayLabel();
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
