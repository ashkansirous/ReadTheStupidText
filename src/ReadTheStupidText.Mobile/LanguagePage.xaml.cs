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

    private static readonly TimeSpan CatalogMaxAge = TimeSpan.FromMinutes(10);

    private const double RetryHitSize = 44;

    private readonly ISettingsStore _settings;
    private readonly VoicePackInstaller _installer;
    private readonly IVoicePackService _packs;
    private readonly Dictionary<AppLanguage, Border> _rows = new();
    private readonly Dictionary<AppLanguage, RowParts> _parts = new();
    private readonly Dictionary<AppLanguage, long> _sizes = new();
    private CancellationTokenSource? _sizesCancel;
    private bool _sizesComplete; // the catalog answered once; the table is not fetched again
    private AppLanguage? _selected;
    private bool _started;

    // The labels of one language row (Slice 373, Missing States mock RT1): its second line, the end label (percent or Retry) and the red strip under it.
    private sealed record RowParts(Label Detail, Label End, Border Strip, Label StripText);

    public LanguagePage(ISettingsStore settings, VoicePackInstaller installer, IVoicePackService packs)
    {
        InitializeComponent();
        FlowDirection = FlowDirection.LeftToRight;
        _settings = settings;
        _installer = installer;
        _packs = packs;

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
        _ = LoadSizesAsync();
    }

    // The catalog gives each row's size; a failed fetch leaves the rows without a second line (the page works without it).
    // Each appearance starts a fetch with its own token unless the table is complete; leaving the page cancels it, and a cancelled or failed fetch leaves the table incomplete so the next appearance asks again.
    private async Task LoadSizesAsync()
    {
        if (_sizesComplete)
        {
            return;
        }

        _sizesCancel?.Cancel(); // a fetch still running from an earlier appearance is superseded
        using var cancel = new CancellationTokenSource();
        _sizesCancel = cancel;
        try
        {
            PackCatalogResult catalog = await _packs.GetCatalogCachedAsync(CatalogMaxAge, cancel.Token);
            if (!cancel.IsCancellationRequested)
            {
                ApplySizes(catalog);
            }
        }
        catch (Exception)
        {
            // no-fallback: the size line is cosmetic; the rows work without it and the installer reports a catalog failure on its own run
        }
        finally
        {
            if (ReferenceEquals(_sizesCancel, cancel))
            {
                _sizesCancel = null;
            }
        }
    }

    private void ApplySizes(PackCatalogResult catalog)
    {
        if (catalog.Status != PackDownloadResult.Ok)
        {
            return;
        }

        var sizes = new Dictionary<AppLanguage, long>();
        foreach ((AppLanguage language, _) in LanguageChoices.All)
        {
            if (VoicePackSelector.Select(catalog.Packs, AppLanguages.Code(language)) is { } pack)
            {
                sizes[language] = pack.SizeBytes;
            }
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            foreach ((AppLanguage language, long bytes) in sizes)
            {
                _sizes[language] = bytes; // only the main thread touches _sizes and _sizesComplete
            }

            _sizesComplete = true; // set with the table, so a page that reappears before this runs still fetches

            RenderRows();
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _sizesCancel?.Cancel();
        _installer.Changed -= OnInstallerChanged;
    }

    // One language: the row (name, second line, end label) with its red strip underneath (mock RT1: row min height 64, padding 0 16, gap 12).
    private View BuildRow(AppLanguage language, string name)
    {
        string code = AppLanguages.Code(language);
        Label detail = BuildDetailLabel(code);
        Label end = BuildEndLabel(code);
        var texts = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Children = { BuildNameLabel(name), detail } };
        var content = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 12 };
        content.Add(texts, 0);
        content.Add(end, 1);
        var border = new Border
        {
            Padding = new Thickness(16, 0),
            MinimumHeightRequest = 64,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.Rectangle(),
            Stroke = Colors.Transparent,
            BackgroundColor = Colors.Transparent,
            Content = content, // no AutomationId on the container: on Android it can hide the child ids (LanguageRowEnd_, LanguageRowError_) the flows tap and read
        };
        border.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => Select(language)) });
        (Border strip, Label stripText) = BuildStrip(code);
        _rows[language] = border;
        _parts[language] = new RowParts(detail, end, strip, stripText);
        return new VerticalStackLayout { Spacing = 0, Children = { border, strip } };
    }

    private static Label BuildDetailLabel(string code)
    {
        var label = new Label { FontSize = 13, IsVisible = false, AutomationId = $"LanguageRowDetail_{code}" };
        label.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#99000000"), Color.FromArgb("#B3FFFFFF"));
        return label;
    }

    // The end label is the percent while its pack downloads, and the Retry (or Download) action when it can start again.
    private Label BuildEndLabel(string code)
    {
        var label = new Label
        {
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.End,
            MinimumHeightRequest = RetryHitSize,
            MinimumWidthRequest = RetryHitSize,
            IsVisible = false,
            AutomationId = $"LanguageRowEnd_{code}",
        };
        label.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(OnRetryTapped) });
        return label;
    }

    private static (Border Strip, Label Text) BuildStrip(string code)
    {
        var text = new Label { FontSize = 13.5, LineHeight = 1.4, AutomationId = $"LanguageRowError_{code}" };
        text.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#9B2C2C"), Color.FromArgb("#F2B8B5"));
        var strip = new Border
        {
            Margin = new Thickness(12, 0, 12, 12),
            Padding = new Thickness(12, 9),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Stroke = Colors.Transparent,
            IsVisible = false,
            Content = text,
        };
        strip.SetAppThemeColor(Border.BackgroundColorProperty, Color.FromArgb("#FDECEC"), Color.FromArgb("#3A1F1F"));
        return (strip, text);
    }

    // Each row is a language's own name, so it takes the direction of its own script.
    private static Label BuildNameLabel(string name)
    {
        var label = new Label { FontSize = 16, FontAttributes = FontAttributes.Bold };
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

    private async void OnContinueClicked(object? sender, EventArgs e)
    {
        if (!_started && _selected is { } language)
        {
            _started = true;
            _settings.Language = AppLanguages.Code(language);
            ApplyTexts();
            await PackNotificationAsk.AskOnceAsync(AskKeepDownloadingAsync); // Slice 183: asked once, before the first big download
            _ = _installer.StartAsync(PackStartReason.User);
            ApplyTexts();
            return;
        }

        if (_started)
        {
            _installer.Changed -= OnInstallerChanged; // before the root page is swapped out
            ((App)Microsoft.Maui.Controls.Application.Current!).ShowMain();
        }
    }

    private Task<bool> AskKeepDownloadingAsync() =>
        DisplayAlertAsync(AppResources.Notify_Title, AppResources.Notify_Body, AppResources.Notify_Continue, AppResources.Notify_NotNow);

    // The first-run page speaks the device language until a row is picked, then the picked one
    // (strings, direction and Continue/Start label), so the choice shows before leaving the page.
    private void ApplyTexts()
    {
        Title = AppResources.Common_Language;
        TextFlow.ForUi(AppNameLabel, AppResources.App_Name);
        TextFlow.ForUi(IntroLabel, AppResources.Language_Intro);
        TextFlow.ForUi(CountsNoteLabel, AppResources.Language_CountsNote);
        TextFlow.ForUi(ContinueButton);
        ContinueButton.Text = _started ? AppResources.Language_StartReading : AppResources.Language_Continue;
        RenderRows();
    }

    private void OnRetryTapped()
    {
        if (_started)
        {
            _ = _installer.StartAsync(PackStartReason.User);
        }
    }

    private void OnInstallerChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(RenderRows);
    }

    // Slice 373: every row shows its own size, and the chosen row (once started) its download state, Retry and red strip. A status the installer holds for another language never shows.
    private void RenderRows()
    {
        foreach ((AppLanguage language, RowParts parts) in _parts)
        {
            bool live = _started && _selected == language;
            string? size = LocalizedPackStatus.SizeLine(_sizes.TryGetValue(language, out long bytes) ? bytes : null);
            LocalizedPackStatus.RowView view = live
                ? LocalizedPackStatus.ForRow(_installer.State, _installer.Progress, _installer.FailureKind, _installer.CanRetry, size)
                : new LocalizedPackStatus.RowView(size, null, null, false);
            ShowText(parts.Detail, view.Detail);
            ShowText(parts.End, view.EndText);
            ColorEnd(parts.End, view.EndIsRetry, view.EndText);
            ShowText(parts.StripText, view.ErrorText);
            parts.Strip.IsVisible = view.ErrorText is not null;
        }
    }

    // Mock RT1: Retry / Download in the accent colour and tappable; the percent grey and not a button (a tap on it must not start the download again).
    private static void ColorEnd(Label end, bool isRetry, string? endText)
    {
        end.InputTransparent = !isRetry;
        if (isRetry)
        {
            end.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#4A46D6"), Color.FromArgb("#B4B1FF"));
        }
        else
        {
            end.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#99000000"), Color.FromArgb("#B3FFFFFF"));
        }

        SemanticProperties.SetDescription(end, isRetry ? endText : null); // the text this render shows, not the label's, which TextFlow may still be updating
    }

    private static void ShowText(Label label, string? text)
    {
        label.IsVisible = !string.IsNullOrEmpty(text);
        TextFlow.ForUi(label, text ?? string.Empty);
    }
}
