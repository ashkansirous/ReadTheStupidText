using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;

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

    // Endonyms: each language is named in itself so a user can find theirs.
    private static readonly (AppLanguage Language, string NativeName)[] Choices =
    [
        (AppLanguage.English, "English"),
        (AppLanguage.Persian, "فارسی"),
        (AppLanguage.French, "Français"),
        (AppLanguage.Spanish, "Español"),
        (AppLanguage.Portuguese, "Português"),
        (AppLanguage.Chinese, "中文"),
        (AppLanguage.Arabic, "العربية"),
        (AppLanguage.Russian, "Русский"),
        (AppLanguage.Japanese, "日本語"),
    ];

    private readonly ISettingsStore _settings;
    private readonly VoicePackInstaller _installer;
    private readonly Dictionary<AppLanguage, Border> _rows = new();
    private AppLanguage? _selected;
    private bool _started;

    public LanguagePage(ISettingsStore settings, VoicePackInstaller installer)
    {
        InitializeComponent();
        _settings = settings;
        _installer = installer;

        foreach ((AppLanguage language, string name) in Choices)
        {
            LanguageList.Children.Add(BuildRow(language, name));
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
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
            Content = new Label { Text = name, FontSize = 15 },
        };
        border.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => Select(language)) });
        _rows[language] = border;
        return border;
    }

    private void Select(AppLanguage language)
    {
        if (_started)
        {
            return;
        }

        _selected = language;
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
            ContinueButton.Text = "Start reading";
            return;
        }

        if (_started)
        {
            _installer.Changed -= OnInstallerChanged; // before the root page is swapped out
            ((App)Microsoft.Maui.Controls.Application.Current!).ShowMain();
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
        StatusLabel.Text = PackStatusText.Compose(_installer.State, _installer.Progress) ?? string.Empty;
        DownloadProgressBar.IsVisible = _installer.State == PackInstallState.Downloading;
        RetryButton.IsVisible = _installer.CanRetry;
    }
}
