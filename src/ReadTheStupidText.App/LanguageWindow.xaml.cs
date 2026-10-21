using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace ReadTheStupidText_App;

/// <summary>
/// The first-run language window (Slice 56, Decision 68): nine languages named in themselves, the same set and
/// persistence as the mobile apps (<see cref="ISettingsStore.Language"/>). Choosing one starts
/// <see cref="VoicePackSetup"/>; the window shows its progress and closes once the natural voice is ready.
/// </summary>
public sealed partial class LanguageWindow : Window
{
    private static readonly LanguageChoice[] Choices =
    [
        new(AppLanguage.English, "English"),
        new(AppLanguage.Persian, "فارسی"),
        new(AppLanguage.French, "Français"),
        new(AppLanguage.Spanish, "Español"),
        new(AppLanguage.Portuguese, "Português"),
        new(AppLanguage.Chinese, "中文"),
        new(AppLanguage.Arabic, "العربية"),
        new(AppLanguage.Russian, "Русский"),
        new(AppLanguage.Japanese, "日本語"),
    ];

    private readonly ISettingsStore _settings;
    private readonly VoicePackSetup _setup;
    private readonly ISystemLog _log;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private bool _populating;
    private bool _closed;

    public LanguageWindow(ISettingsStore settings, VoicePackSetup setup, ISystemLog log)
    {
        _settings = settings;
        _setup = setup;
        _log = log;
        InitializeComponent();
        Title = "Read The Stupid Text - Language";
        AppWindow.Resize(new SizeInt32(460, 640));
        AppWindow.SetIcon("Assets/AppIcon.ico");

        IntroText.Text = PackSetupText.Intro;
        PopulateChoices();
        ShowSetupState();
        _setup.Changed += OnSetupChanged;
        Closed += OnWindowClosed;
    }

    private void PopulateChoices()
    {
        _populating = true;
        LanguageList.ItemsSource = Choices;
        bool known = AppLanguages.TryParse(_settings.Language, out AppLanguage current);
        LanguageList.SelectedItem = known ? Choices.First(c => c.Language == current) : null;
        _populating = false;
    }

    private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_populating || LanguageList.SelectedItem is not LanguageChoice choice)
        {
            return;
        }

        _settings.Language = AppLanguages.Code(choice.Language);
        StartSetup();
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _closed = true;
        _setup.Changed -= OnSetupChanged;
    }

    private void OnRetryClick(object sender, RoutedEventArgs e) => StartSetup();

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private async void StartSetup()
    {
        try
        {
            await _setup.StartAsync();
            CloseWhenReady();
        }
        catch (Exception ex)
        {
            _log.Error("voice pack setup failed", null, ex);
            _dispatcher.TryEnqueue(() => StatusText.Text = PackSetupText.For(PackSetupState.Failed, 0));
        }
    }

    private void OnSetupChanged(object? sender, EventArgs e) => _dispatcher.TryEnqueue(ShowSetupStateIfOpen);

    private void ShowSetupStateIfOpen()
    {
        if (!_closed)
        {
            ShowSetupState();
        }
    }

    // Closes only after a run this window started (StartAsync returns once its language is served)
    // ends with the natural voice ready. A background run or a re-published state never closes it.
    private void CloseWhenReady()
    {
        if (!_closed && _setup.State == PackSetupState.Ready)
        {
            Close();
        }
    }

    // Mirrors the setup state: progress while downloading and Retry when it failed. The list is locked
    // while a download is in flight, because a second choice could not start another run until the
    // first one finishes.
    private void ShowSetupState()
    {
        PackSetupState state = _setup.State;
        StatusText.Text = PackSetupText.For(state, _setup.Progress);
        DownloadBar.Value = _setup.Progress;
        DownloadBar.Visibility = state == PackSetupState.Downloading ? Visibility.Visible : Visibility.Collapsed;
        RetryButton.Visibility = _setup.CanRetry ? Visibility.Visible : Visibility.Collapsed;
        LanguageList.IsEnabled = state is not (PackSetupState.Resolving or PackSetupState.Downloading);
    }

    public sealed record LanguageChoice(AppLanguage Language, string NativeName);
}
