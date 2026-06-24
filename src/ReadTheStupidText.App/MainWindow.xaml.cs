using ReadTheStupidText.Application.Input;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Startup;
using ReadTheStupidText.Domain.Reading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using WinRT.Interop;

namespace ReadTheStupidText_App;

/// <summary>
/// Hosts the notification-area icon and its flyout, and owns the window handle
/// the global hotkey is registered against. The window itself is never shown.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const string PlayLabel = "Play";
    private const string PauseLabel = "Pause";
    private const string QuitLabel = "Quit";
    private const string AutoReadLabel = "Auto-read on selection";
    private const string StartupLabel = "Launch at startup";
    private const string VoiceLabel = "Voice";

    private static readonly Uri DarkTrayIconUri = new("ms-appx:///Assets/TrayIconDark.ico");
    private static readonly Uri LightTrayIconUri = new("ms-appx:///Assets/TrayIconLight.ico");

    private readonly ReadAloudService _readAloud;
    private readonly IHotkeyService _hotkey;
    private readonly IStartupService _startup;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly Windows.UI.ViewManagement.UISettings _uiSettings = new();

    // The tray menu runs in H.NotifyIcon's default PopupMenu mode, which only
    // invokes each item's Command (never the WinUI Click event), so every item
    // is wired through one of these.
    private readonly RelayCommand _togglePlayPauseCommand;
    private readonly RelayCommand _toggleAutoReadCommand;
    private readonly RelayCommand _toggleStartupCommand;
    private readonly RelayCommand _setSpeedCommand;
    private readonly RelayCommand _setVoiceCommand;
    private readonly RelayCommand _toggleControlPanelCommand;
    private readonly RelayCommand _quitCommand;

    private readonly ControlPanelWindow _controlPanel;

    private MenuFlyout? _flyout;
    private MenuFlyoutItem? _playPauseItem;
    private ToggleMenuFlyoutItem? _autoReadItem;
    private ToggleMenuFlyoutItem? _startupItem;
    private readonly List<ToggleMenuFlyoutItem> _speedItems = new();
    private readonly List<ToggleMenuFlyoutItem> _voiceItems = new();

    // The current rate surfaced as a menu item when it isn't one of the presets
    // (e.g. 1.05x picked from the panel slider), and where the speed group starts.
    private ToggleMenuFlyoutItem? _customSpeedItem;
    private int _speedStartIndex;

    public MainWindow(ReadAloudService readAloud, IHotkeyService hotkey, IStartupService startup)
    {
        _readAloud = readAloud;
        _hotkey = hotkey;
        _startup = startup;

        // The left-click control panel shares the same services as the tray menu,
        // so both surfaces read and write the same state.
        _controlPanel = new ControlPanelWindow(_readAloud, _startup);

        _togglePlayPauseCommand = new RelayCommand(_ => _ = _readAloud.PlayPauseOrReadAsync());
        _toggleAutoReadCommand = new RelayCommand(_ => ToggleAutoRead());
        _toggleStartupCommand = new RelayCommand(_ => _ = ToggleStartupAsync());
        _setSpeedCommand = new RelayCommand(p => ApplySpeed((PlaybackRate)p!));
        _setVoiceCommand = new RelayCommand(p => ApplyVoice((string)p!));
        _toggleControlPanelCommand = new RelayCommand(_ => _controlPanel.Toggle());
        _quitCommand = new RelayCommand(_ => Quit());

        InitializeComponent();

        _uiSettings.ColorValuesChanged += OnColorValuesChanged;

        _flyout = BuildTrayMenu();
        TrayIcon.ContextFlyout = _flyout;
        TrayIcon.LeftClickCommand = _toggleControlPanelCommand;
        TrayIcon.NoLeftClickDelay = true;
        TrayIcon.ForceCreate();

        // Surface a non-preset persisted speed (e.g. 1.05x) as its own menu item.
        UpdateSpeedChecks(_readAloud.Speed);

        // ForceCreate renders the light icon declared in XAML first; only then
        // adapt to the current taskbar theme (and keep it in sync afterwards).
        // Doing this after creation avoids generating a blank icon while the
        // image is still loading.
        UpdateTrayIconForTheme();

        // The startup-task state is read asynchronously; reflect it on the toggle
        // once known (well before the user can open the tray menu).
        _ = RefreshStartupStateAsync();

        _readAloud.StateChanged += OnStateChanged;

        // Either surface can change these; keep the tray menu's checkmarks in
        // sync when the change comes from the control panel.
        _readAloud.SpeedChanged += OnSpeedChanged;
        _readAloud.VoiceChanged += OnVoiceChanged;
        _readAloud.EnabledChanged += OnEnabledChanged;
        _readAloud.VoicesChanged += OnVoicesChanged;
        _controlPanel.StartupStateChanged += OnPanelStartupChanged;

        _hotkey.Register(WindowNative.GetWindowHandle(this));
    }

    // The neural voices download after launch; the menu is built before they
    // exist (no Voice submenu), so rebuild it once they're ready.
    private void OnVoicesChanged(object? sender, EventArgs e) =>
        _dispatcher.TryEnqueue(() =>
        {
            _flyout = BuildTrayMenu();
            TrayIcon.ContextFlyout = _flyout;
            UpdateSpeedChecks(_readAloud.Speed);
            _ = RefreshStartupStateAsync();
        });

    private void OnColorValuesChanged(Windows.UI.ViewManagement.UISettings sender, object args)
    {
        _dispatcher.TryEnqueue(UpdateTrayIconForTheme);
    }

    private void UpdateTrayIconForTheme()
    {
        var bg = _uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
        bool isDark = bg.R < 128;
        TrayIcon.IconSource = new BitmapImage(isDark ? DarkTrayIconUri : LightTrayIconUri);
    }

    private MenuFlyout BuildTrayMenu()
    {
        var flyout = new MenuFlyout();

        // The menu can be rebuilt (e.g. when neural voices arrive); start the
        // tracked-item lists fresh so they don't accumulate stale entries.
        _speedItems.Clear();
        _voiceItems.Clear();
        _customSpeedItem = null;

        _autoReadItem = new ToggleMenuFlyoutItem
        {
            Text = AutoReadLabel,
            IsChecked = _readAloud.IsEnabled,
            Command = _toggleAutoReadCommand,
        };
        flyout.Items.Add(_autoReadItem);

        // IsChecked is corrected by RefreshStartupStateAsync once the OS reports
        // the real state; starting unchecked avoids a misleading flash.
        _startupItem = new ToggleMenuFlyoutItem
        {
            Text = StartupLabel,
            IsChecked = false,
            Command = _toggleStartupCommand,
        };
        flyout.Items.Add(_startupItem);
        flyout.Items.Add(new MenuFlyoutSeparator());

        _playPauseItem = new MenuFlyoutItem { Text = PlayLabel, Command = _togglePlayPauseCommand };
        flyout.Items.Add(_playPauseItem);
        flyout.Items.Add(new MenuFlyoutSeparator());

        _speedStartIndex = flyout.Items.Count;
        foreach (PlaybackRate preset in SpeedPresets.All)
        {
            flyout.Items.Add(CreateSpeedItem(preset));
        }

        MenuFlyoutSubItem? voiceMenu = BuildVoiceSubmenu();
        if (voiceMenu is not null)
        {
            flyout.Items.Add(new MenuFlyoutSeparator());
            flyout.Items.Add(voiceMenu);
        }

        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(new MenuFlyoutItem { Text = QuitLabel, Command = _quitCommand });

        return flyout;
    }

    // The Voice submenu lists the installed Windows voices (an open set), with a
    // checkmark on the current one. Returns null when no voices are installed so
    // the menu isn't cluttered with an empty submenu.
    private MenuFlyoutSubItem? BuildVoiceSubmenu()
    {
        IReadOnlyList<VoiceInfo> voices = _readAloud.InstalledVoices;
        if (voices.Count == 0)
        {
            return null;
        }

        var submenu = new MenuFlyoutSubItem { Text = VoiceLabel };
        string? currentId = _readAloud.CurrentVoiceId;
        foreach (VoiceInfo voice in voices)
        {
            var item = new ToggleMenuFlyoutItem
            {
                Text = voice.DisplayName,
                Tag = voice.Id,
                IsChecked = voice.Id == currentId,
                Command = _setVoiceCommand,
                CommandParameter = voice.Id,
            };
            _voiceItems.Add(item);
            submenu.Items.Add(item);
        }

        return submenu;
    }

    // The menu offers preset rates only (it's a native menu — no slider); the
    // fine 0.05 control lives in the panel. ToggleMenuFlyoutItem is used rather
    // than RadioMenuFlyoutItem because the PopupMenu builder only renders a
    // checkmark for toggle items and doesn't enforce radio exclusivity, so
    // UpdateSpeedChecks manages which preset is checked.
    private ToggleMenuFlyoutItem CreateSpeedItem(PlaybackRate preset)
    {
        var item = new ToggleMenuFlyoutItem
        {
            Text = preset.ToDisplayLabel(),
            Tag = preset,
            IsChecked = preset == _readAloud.Speed,
            Command = _setSpeedCommand,
            CommandParameter = preset,
        };
        _speedItems.Add(item);
        return item;
    }

    // The checkmark is updated by OnEnabledChanged, so both this and the control
    // panel converge on the same state.
    private void ToggleAutoRead() => _readAloud.IsEnabled = !_readAloud.IsEnabled;

    private async Task RefreshStartupStateAsync()
    {
        bool enabled = await _startup.IsEnabledAsync();
        _dispatcher.TryEnqueue(() => SetStartupChecked(enabled));
    }

    private async Task ToggleStartupAsync()
    {
        if (_startupItem is null)
        {
            return;
        }

        // Reflect the actual resulting state: enabling can be refused by the user
        // (disabled in Task Manager) or by policy.
        bool enabled = await _startup.SetEnabledAsync(!_startupItem.IsChecked);
        _dispatcher.TryEnqueue(() => SetStartupChecked(enabled));
    }

    private void SetStartupChecked(bool enabled)
    {
        if (_startupItem is not null)
        {
            _startupItem.IsChecked = enabled;
        }
    }

    // Apply* just drive the service; the corresponding *Changed event updates the
    // menu checkmarks, so a change made in the control panel is reflected here too.
    private void ApplySpeed(PlaybackRate speed) => _readAloud.SetSpeed(speed);

    private void ApplyVoice(string voiceId) => _readAloud.SetVoice(voiceId);

    private void OnSpeedChanged(object? sender, PlaybackRate speed) =>
        _dispatcher.TryEnqueue(() => UpdateSpeedChecks(speed));

    // A preset is checked only when the current rate exactly matches it; a fine
    // 0.05 value chosen in the panel gets its own item at the top of the group.
    private void UpdateSpeedChecks(PlaybackRate speed)
    {
        bool isPreset = SpeedPresets.All.Contains(speed);
        SyncCustomSpeedItem(speed, isPreset);
        foreach (ToggleMenuFlyoutItem item in _speedItems)
        {
            item.IsChecked = item.Tag is PlaybackRate s && s == speed;
        }
    }

    // Shows the current rate as a checked item above the presets when it isn't a
    // preset, and removes that item once a preset is selected again.
    private void SyncCustomSpeedItem(PlaybackRate speed, bool isPreset)
    {
        if (_flyout is null)
        {
            return;
        }

        if (isPreset)
        {
            if (_customSpeedItem is not null)
            {
                _flyout.Items.Remove(_customSpeedItem);
                _customSpeedItem = null;
            }

            return;
        }

        if (_customSpeedItem is null)
        {
            _customSpeedItem = new ToggleMenuFlyoutItem { Command = _setSpeedCommand };
            _flyout.Items.Insert(_speedStartIndex, _customSpeedItem);
        }

        _customSpeedItem.Text = speed.ToDisplayLabel();
        _customSpeedItem.Tag = speed;
        _customSpeedItem.CommandParameter = speed;
        _customSpeedItem.IsChecked = true;
    }

    private void OnVoiceChanged(object? sender, string voiceId) =>
        _dispatcher.TryEnqueue(() => UpdateVoiceChecks(voiceId));

    private void UpdateVoiceChecks(string voiceId)
    {
        foreach (ToggleMenuFlyoutItem item in _voiceItems)
        {
            item.IsChecked = item.Tag is string id && id == voiceId;
        }
    }

    private void OnEnabledChanged(object? sender, bool enabled) =>
        _dispatcher.TryEnqueue(() =>
        {
            if (_autoReadItem is not null)
            {
                _autoReadItem.IsChecked = enabled;
            }
        });

    private void OnPanelStartupChanged(object? sender, bool enabled) =>
        _dispatcher.TryEnqueue(() => SetStartupChecked(enabled));

    private void Quit()
    {
        _hotkey.Dispose();
        TrayIcon.Dispose();
        _controlPanel.Close();
        Application.Current.Exit();
    }

    private void OnStateChanged(object? sender, PlaybackState state)
    {
        _dispatcher.TryEnqueue(() =>
        {
            if (_playPauseItem is not null)
            {
                _playPauseItem.Text = state == PlaybackState.Playing ? PauseLabel : PlayLabel;
            }
        });
    }
}
