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
    private const string SpeedGroup = "ReadTheStupidTextSpeed";

    private static readonly Uri DarkTrayIconUri = new("ms-appx:///Assets/TrayIconDark.ico");
    private static readonly Uri LightTrayIconUri = new("ms-appx:///Assets/TrayIconLight.ico");

    private readonly ReadAloudService _readAloud;
    private readonly IHotkeyService _hotkey;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly Windows.UI.ViewManagement.UISettings _uiSettings = new();

    private MenuFlyoutItem? _playPauseItem;

    public MainWindow(ReadAloudService readAloud, IHotkeyService hotkey)
    {
        _readAloud = readAloud;
        _hotkey = hotkey;

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

        var autoRead = new ToggleMenuFlyoutItem
        {
            Text = AutoReadLabel,
            IsChecked = _readAloud.IsEnabled,
        };
        autoRead.Click += OnAutoReadToggle;
        flyout.Items.Add(autoRead);
        flyout.Items.Add(new MenuFlyoutSeparator());

        _playPauseItem = new MenuFlyoutItem { Text = PlayLabel };
        _playPauseItem.Click += OnPlayPauseClick;
        flyout.Items.Add(_playPauseItem);
        flyout.Items.Add(new MenuFlyoutSeparator());

        foreach (ReadingSpeed speed in Enum.GetValues<ReadingSpeed>())
        {
            flyout.Items.Add(CreateSpeedItem(speed));
        }

        flyout.Items.Add(new MenuFlyoutSeparator());
        var quit = new MenuFlyoutItem { Text = QuitLabel };
        quit.Click += OnQuitClick;
        flyout.Items.Add(quit);

        return flyout;
    }

    private RadioMenuFlyoutItem CreateSpeedItem(ReadingSpeed speed)
    {
        var item = new RadioMenuFlyoutItem
        {
            Text = speed.ToDisplayLabel(),
            GroupName = SpeedGroup,
            Tag = speed,
            IsChecked = speed == _readAloud.Speed,
        };
        item.Click += OnSpeedClick;
        return item;
    }

    private void OnAutoReadToggle(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleMenuFlyoutItem item)
        {
            _readAloud.IsEnabled = item.IsChecked;
        }
    }

    private void OnPlayPauseClick(object sender, RoutedEventArgs e) => _readAloud.TogglePlayPause();

    private void OnSpeedClick(object sender, RoutedEventArgs e)
    {
        if (sender is RadioMenuFlyoutItem { Tag: ReadingSpeed speed })
        {
            _readAloud.SetSpeed(speed);
        }
    }

    private void OnQuitClick(object sender, RoutedEventArgs e)
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
