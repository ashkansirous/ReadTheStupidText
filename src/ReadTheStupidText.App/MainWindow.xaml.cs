using ReadTheStupidText.Application.Input;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    private readonly ReadAloudService _readAloud;
    private readonly IHotkeyService _hotkey;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();

    private MenuFlyoutItem? _playPauseItem;

    public MainWindow(ReadAloudService readAloud, IHotkeyService hotkey)
    {
        _readAloud = readAloud;
        _hotkey = hotkey;

        InitializeComponent();

        TrayIcon.ContextFlyout = BuildTrayMenu();
        TrayIcon.ForceCreate();

        _readAloud.StateChanged += OnStateChanged;
        _hotkey.Register(WindowNative.GetWindowHandle(this));
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
