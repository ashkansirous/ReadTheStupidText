using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace ReadTheStupidText_App;

/// <summary>
/// The first-run language window (Slice 56, Decision 68): fourteen languages named in themselves, the same set and
/// persistence as the mobile apps (<see cref="ISettingsStore.Language"/>). Choosing one starts
/// <see cref="VoicePackSetup"/>; the window shows its progress and closes once the natural voice is ready.
/// </summary>
public sealed partial class LanguageWindow : Window
{
    private readonly ISettingsStore _settings;
    private readonly VoicePackSetup _setup;
    private readonly ISystemLog _log;
    private readonly Action _restart;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private bool _populating;
    private bool _closed;
    private bool _languageChanged;
    private readonly string? _initialLanguage;

    public LanguageWindow(ISettingsStore settings, VoicePackSetup setup, ISystemLog log, Action restart)
    {
        _settings = settings;
        _setup = setup;
        _initialLanguage = settings.Language;
        _log = log;
        _restart = restart;
        InitializeComponent();
        RootGrid.FlowDirection = FlowDirection.LeftToRight; // layout never mirrors (Slice 88)
        Title = $"{ProductInfo.Name} — {UiStrings.Get("Window_Language")}";
        AppWindow.Resize(new SizeInt32(460, 640));
        AppWindow.SetIcon("Assets/AppIcon.ico");

        TextFlow.ForUi(IntroText, PackSetupText.Intro);
        PopulateChoices();
        ShowSetupState();
        _setup.Changed += OnSetupChanged;
        Closed += OnWindowClosed;
    }

    private void PopulateChoices()
    {
        _populating = true;
        LanguageList.ItemsSource = LanguageChoices.All;
        bool known = AppLanguages.TryParse(_settings.Language, out AppLanguage current);
        LanguageList.SelectedItem = known ? LanguageChoices.All.First(c => c.Language == current) : null;
        _populating = false;
    }

    private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_populating || LanguageList.SelectedItem is not LanguageChoice choice)
        {
            return;
        }

        _settings.Language = AppLanguages.Code(choice.Language);
        UiStrings.ApplyLanguage(_settings.Language);
        // Restart only when the language differs from the one the window opened with (picking it back is a no-op).
        _languageChanged = !string.Equals(_settings.Language, _initialLanguage, StringComparison.OrdinalIgnoreCase);
        TextFlow.ForUi(IntroText, PackSetupText.Intro);
        StartSetup();
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _closed = true;
        _setup.Changed -= OnSetupChanged;
        if (_languageChanged)
        {
            _restart(); // the other windows were built in the old language; the settings are already stored
        }
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
            _dispatcher.TryEnqueue(() => TextFlow.ForUi(StatusText, PackSetupText.For(PackSetupState.Failed, 0)));
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
        TextFlow.ForUi(StatusText, PackSetupText.For(state, _setup.Progress));
        DownloadBar.Value = _setup.Progress;
        DownloadBar.Visibility = state == PackSetupState.Downloading ? Visibility.Visible : Visibility.Collapsed;
        RetryButton.Visibility = _setup.CanRetry ? Visibility.Visible : Visibility.Collapsed;
        LanguageList.IsEnabled = state is not (PackSetupState.Resolving or PackSetupState.Downloading);
    }
}
