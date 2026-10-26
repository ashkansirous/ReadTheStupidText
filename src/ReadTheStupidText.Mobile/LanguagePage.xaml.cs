using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Mobile.Resources.Strings;
using ReadTheStupidText.Mobile.Shared;
using ReadTheStupidText.Mobile.Shared.Usage;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// First-run language choice (Slice 50, Decision 68). Picking a language stores it,
/// starts the matching voice pack download through <see cref="VoicePackInstaller"/>
/// and shows its progress; the user can enter the Reader straight away because the
/// phone voice reads until the pack lands (Decision 61).
/// </summary>
public partial class LanguagePage : ContentPage
{
    private static readonly Color SelectedRowTint = Color.FromArgb("#125B57E8");

    private readonly ISettingsStore _settings;
    private readonly VoicePackInstaller _installer;
    private readonly Dictionary<AppLanguage, Border> _rows = new();
    private AppLanguage? _selected;
    private bool _started;

    public LanguagePage(ISettingsStore settings, VoicePackInstaller installer)
    {
        InitializeComponent();
        FlowDirection = FlowDirection.LeftToRight;
        _settings = settings;
        _installer = installer;

        foreach ((AppLanguage language, string name) in LanguageChoices.All)
        {
            LanguageList.Children.Add(BuildRow(language, name));
        }

        ApplyTexts();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Preferences.Default.Set(RtstUsage.NoticeSeenKey, true); // the notice is on screen now, so the one-time alert is not for this install
        _installer.Changed -= OnInstallerChanged; // never double-subscribe
        _installer.Changed += OnInstallerChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _installer.Changed -= OnInstallerChanged;
    }

    private Border BuildRow(AppLanguage language, string name)
    {
        var border = new Border
        {
            Padding = new Thickness(16, 14),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.Rectangle(),
            Stroke = Colors.Transparent,
            BackgroundColor = Colors.Transparent,
            Content = BuildNameLabel(name),
        };
        border.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => Select(language)) });
        _rows[language] = border;
        return border;
    }

    // Each row is a language's own name, so it takes the direction of its own script.
    private static Label BuildNameLabel(string name)
    {
        var label = new Label { FontSize = 15 };
        TextFlow.ForContent(label, name);
        return label;
    }

    private void Select(AppLanguage language)
    {
        if (_started)
        {
            return;
        }

        _selected = language;
        UiCulture.Apply(UiCulture.Resolve(AppLanguages.Code(language), System.Globalization.CultureInfo.CurrentUICulture));
        ApplyTexts();
        ContinueButton.IsEnabled = true;
        foreach ((AppLanguage candidate, Border row) in _rows)
        {
            row.BackgroundColor = candidate == language ? SelectedRowTint : Colors.Transparent;
        }
    }

    private void OnContinueClicked(object? sender, EventArgs e)
    {
        if (!_started && _selected is { } language)
        {
            _started = true;
            _settings.Language = AppLanguages.Code(language);
            ShowProgress();
            _ = _installer.StartAsync();
            ApplyTexts();
            return;
        }

        if (_started)
        {
            _installer.Changed -= OnInstallerChanged; // before the root page is swapped out
            ((App)Microsoft.Maui.Controls.Application.Current!).ShowMain();
        }
    }

    // The first-run page speaks the device language until a row is picked, then the picked one
    // (strings, direction and Continue/Start label), so the choice shows before leaving the page.
    private void ApplyTexts()
    {
        Title = AppResources.Common_Language;
        TextFlow.ForUi(AppNameLabel, AppResources.App_Name);
        TextFlow.ForUi(IntroLabel, AppResources.Language_Intro);
        TextFlow.ForUi(CountsNoteLabel, AppResources.Language_CountsNote);
        RetryButton.Text = AppResources.Language_Retry;
        TextFlow.ForUi(RetryButton);
        TextFlow.ForUi(ContinueButton);
        ContinueButton.Text = _started ? AppResources.Language_StartReading : AppResources.Language_Continue;
        if (_started)
        {
            RenderStatus();
        }
    }

    private void OnRetryClicked(object? sender, EventArgs e)
    {
        _ = _installer.StartAsync();
    }

    private void ShowProgress()
    {
        ProgressSection.IsVisible = true;
        RenderStatus();
    }

    private void OnInstallerChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(RenderStatus);
    }

    private void RenderStatus()
    {
        DownloadProgressBar.Progress = _installer.Progress;
        TextFlow.ForUi(StatusLabel, LocalizedPackStatus.Compose(_installer.State, _installer.Progress) ?? string.Empty);
        DownloadProgressBar.IsVisible = _installer.State == PackInstallState.Downloading;
        RetryButton.IsVisible = _installer.CanRetry;
    }
}
