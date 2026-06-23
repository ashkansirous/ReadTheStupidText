using ReadTheStupidText.Application.Input;
using ReadTheStupidText.Application.Reading;
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
    private const string VoiceLabel = "Voice";

    private static readonly Uri DarkTrayIconUri = new("ms-appx:///Assets/TrayIconDark.ico");
    private static readonly Uri LightTrayIconUri = new("ms-appx:///Assets/TrayIconLight.ico");

    private readonly ReadAloudService _readAloud;
    private readonly IHotkeyService _hotkey;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly Windows.UI.ViewManagement.UISettings _uiSettings = new();

    // The tray menu runs in H.NotifyIcon's default PopupMenu mode, which only
    // invokes each item's Command (never the WinUI Click event), so every item
    // is wired through one of these.
    private readonly RelayCommand _togglePlayPauseCommand;
    private readonly RelayCommand _toggleAutoReadCommand;
    private readonly RelayCommand _setSpeedCommand;
    private readonly RelayCommand _setVoiceCommand;
    private readonly RelayCommand _quitCommand;

    private MenuFlyoutItem? _playPauseItem;
    private ToggleMenuFlyoutItem? _autoReadItem;
    private readonly List<ToggleMenuFlyoutItem> _speedItems = new();
    private readonly List<ToggleMenuFlyoutItem> _voiceItems = new();

    public MainWindow(ReadAloudService readAloud, IHotkeyService hotkey)
    {
        _readAloud = readAloud;
        _hotkey = hotkey;

        _togglePlayPauseCommand = new RelayCommand(_ => _readAloud.TogglePlayPause());
        _toggleAutoReadCommand = new RelayCommand(_ => ToggleAutoRead());
        _setSpeedCommand = new RelayCommand(p => ApplySpeed((ReadingSpeed)p!));
        _setVoiceCommand = new RelayCommand(p => ApplyVoice((string)p!));
        _quitCommand = new RelayCommand(_ => Quit());

        InitializeComponent();

        _uiSettings.ColorValuesChanged += OnColorValuesChanged;

        TrayIcon.ContextFlyout = BuildTrayMenu();
        TrayIcon.ForceCreate();

        // ForceCreate renders the light icon declared in XAML first; only then
        // adapt to the current taskbar theme (and keep it in sync afterwards).
        // Doing this after creation avoids generating a blank icon while the
        // image is still loading.
        UpdateTrayIconForTheme();

        _readAloud.StateChanged += OnStateChanged;
        _hotkey.Register(WindowNative.GetWindowHandle(this));
    }

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

        _autoReadItem = new ToggleMenuFlyoutItem
        {
            Text = AutoReadLabel,
            IsChecked = _readAloud.IsEnabled,
            Command = _toggleAutoReadCommand,
        };
        flyout.Items.Add(_autoReadItem);
        flyout.Items.Add(new MenuFlyoutSeparator());

        _playPauseItem = new MenuFlyoutItem { Text = PlayLabel, Command = _togglePlayPauseCommand };
        flyout.Items.Add(_playPauseItem);
        flyout.Items.Add(new MenuFlyoutSeparator());

        foreach (ReadingSpeed speed in Enum.GetValues<ReadingSpeed>())
        {
            flyout.Items.Add(CreateSpeedItem(speed));
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

    // Speeds use ToggleMenuFlyoutItem rather than RadioMenuFlyoutItem: the
    // PopupMenu builder only renders a checkmark for toggle items, and it does
    // not enforce radio-group exclusivity, so ApplySpeed manages that.
    private ToggleMenuFlyoutItem CreateSpeedItem(ReadingSpeed speed)
    {
        var item = new ToggleMenuFlyoutItem
        {
            Text = speed.ToDisplayLabel(),
            Tag = speed,
            IsChecked = speed == _readAloud.Speed,
            Command = _setSpeedCommand,
            CommandParameter = speed,
        };
        _speedItems.Add(item);
        return item;
    }

    private void ToggleAutoRead()
    {
        _readAloud.IsEnabled = !_readAloud.IsEnabled;
        if (_autoReadItem is not null)
        {
            _autoReadItem.IsChecked = _readAloud.IsEnabled;
        }
    }

    private void ApplySpeed(ReadingSpeed speed)
    {
        _readAloud.SetSpeed(speed);
        foreach (ToggleMenuFlyoutItem item in _speedItems)
        {
            item.IsChecked = item.Tag is ReadingSpeed s && s == speed;
        }
    }

    private void ApplyVoice(string voiceId)
    {
        _readAloud.SetVoice(voiceId);
        foreach (ToggleMenuFlyoutItem item in _voiceItems)
        {
            item.IsChecked = item.Tag is string id && id == voiceId;
        }
    }

    private void Quit()
    {
        _hotkey.Dispose();
        TrayIcon.Dispose();
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
