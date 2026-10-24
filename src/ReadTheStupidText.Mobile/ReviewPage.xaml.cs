using System.Globalization;
using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Mobile.Resources.Strings;
using AndroidPlatform = Microsoft.Maui.Controls.PlatformConfiguration.Android;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Wizard page 2, "Check the text" (Slice 101, Decision 105): almost all text box, editable. Reached from the
/// capture wizard's Done, from Text from a file, and from Edit and long-press Paste on the main page. When the
/// text is in another language than the app's and the phone can translate it, a ticked checkbox turns the big
/// button into "Translate and read": the text is replaced by its translation (a missing ML Kit model downloads
/// first) and the main page shows and reads it, so what is on screen is what is heard. Back changes nothing.
/// </summary>
public partial class ReviewPage : ContentPage
{
    private static readonly TimeSpan SweepDuration = TimeSpan.FromMilliseconds(1100);
    private const double TranslatingOpacity = 0.55;
    private const double DisabledOpacity = 0.45;
    private const double TranslatingTextAlpha = 0.45;
    private const string DisabledBorder = "#1C000000";
    private const string UntickedBorder = "#59000000";
    private const string DarkEditorText = "#E6E6EA";
    private const string LightEditorText = "#1F2027";
    private const string SweepAnimationName = "ReviewDownloadSweep";

    private readonly ReviewHandOff _handOff;
    private readonly ISettingsStore _settings;
    private readonly ITranslator? _translator;
    private readonly ILanguageDetector? _detector;

    private ReviewTextState _state = new(string.Empty);
    private string? _detected;
    private bool _optionShown;
    private bool _ticked = true;
    private bool _translating;
    private bool _leaving;
    private bool _settingText;
    private CancellationTokenSource? _detectCts;
    private CancellationTokenSource? _translateCts;

    public ReviewPage(ReviewHandOff handOff, ISettingsStore settings, ITranslator? translator = null, ILanguageDetector? detector = null)
    {
        InitializeComponent();
        FlowDirection = FlowDirection.LeftToRight;
        _handOff = handOff;
        _settings = settings;
        _translator = translator;
        _detector = detector;
        ApplyStaticTexts();
        RenderTick();
        RenderReadButton();
    }

    private string TargetCode() => AppLanguages.Code(AppLanguageSetting.Current(_settings));

    private void ApplyStaticTexts()
    {
        TextFlow.ForUi(TitleLabel, AppResources.Review_Title);
        TextFlow.ForUi(PasteLabel, AppResources.Reader_PasteButton);
        TextFlow.ForUi(ShowOriginalLink, AppResources.Review_ShowOriginal);
        TextFlow.ForUi(ProgressNote, AppResources.Review_FirstTimeOnly);
        SemanticProperties.SetDescription(BackButton, AppResources.Common_Back);
        SemanticProperties.SetDescription(PasteButton, AppResources.Reader_PasteButton);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Microsoft.Maui.Controls.Application.Current!.On<AndroidPlatform>().UseWindowSoftInputModeAdjust(WindowSoftInputModeAdjust.Resize);
        if (_handOff.TakeRequest() is { } request)
        {
            ApplyRequest(request);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Microsoft.Maui.Controls.Application.Current!.On<AndroidPlatform>().UseWindowSoftInputModeAdjust(WindowSoftInputModeAdjust.Pan);
    }

    // Cancels only when the page is really left (Back, hardware back, Read), not when the app goes to the background.
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        _detectCts?.Cancel();
        _translateCts?.Cancel();
    }

    private void ApplyRequest(ReviewRequest request)
    {
        SetState(new ReviewTextState(request.Text, request.Original), TimeSpan.Zero);
        ShowFailedBanner(request.FailedPages, request.ReadPages);
        if (string.IsNullOrEmpty(request.Text))
        {
            TextEditor.Focus();
        }
    }

    private void ShowFailedBanner(IReadOnlyList<int>? failed, IReadOnlyList<int>? read)
    {
        if (failed is not { Count: > 0 })
        {
            return;
        }

        string failedText = AppResources.Format(failed.Count == 1 ? AppResources.Review_FailedOne : AppResources.Review_FailedMany, ScannedText.FormatPageNumbers(failed));
        TextFlow.ForUi(FailedLabel, $"{failedText} {HereText(read)}".TrimEnd());
        FailedBanner.IsVisible = true;
    }

    private static string HereText(IReadOnlyList<int>? read) => read is not { Count: > 0 }
        ? string.Empty
        : AppResources.Format(read.Count == 1 ? AppResources.Review_HereOne : AppResources.Review_HereMany, ScannedText.FormatPageNumbers(read));

    // Replaces what the box holds (a request, Paste, Show original) and looks at its language afresh.
    private void SetState(ReviewTextState state, TimeSpan detectDelay)
    {
        _state = state;
        _settingText = true;
        try
        {
            TextEditor.Text = state.Text;
        }
        finally
        {
            _settingText = false;
        }

        TextEditor.FlowDirection = TextFlow.ContentDirection(state.Text);
        ShowOriginalLink.IsVisible = state.HasOriginal;
        FailedBanner.IsVisible = false;
        _detected = null;
        ScheduleDetection(state.Text, detectDelay);
    }

    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_settingText)
        {
            return;
        }

        _state = _state.Edited(e.NewTextValue ?? string.Empty);
        TextEditor.FlowDirection = TextFlow.ContentDirection(_state.Text);
        _detected = null;
        ScheduleDetection(_state.Text, ReaderPage.DetectDebounce);
    }

    // ---- The text's language and the translate checkbox -------------------------------------

    // Detection runs whenever a detector exists: the voice follows the text's language even when the phone
    // cannot translate. The translator only gates the checkbox (RefreshTranslateOption). Always refreshes once.
    private void ScheduleDetection(string text, TimeSpan delay)
    {
        _detectCts?.Cancel();
        RefreshTranslateOption();
        if (_detector is null || string.IsNullOrWhiteSpace(text))
        {
            _detected = null;
            return;
        }

        _detectCts = new CancellationTokenSource();
        _ = DetectAfterDelayAsync(text, delay, _detectCts.Token);
    }

    private async Task DetectAfterDelayAsync(string text, TimeSpan delay, CancellationToken token)
    {
        try
        {
            await Task.Delay(delay, token);
            DetectedLanguage? found = await _detector!.DetectAsync(TranslationFlow.DetectionSample(text), token);
            await ApplyDetectionAsync(TranslationFlow.SourceFromDetection(found), token);
        }
        catch (OperationCanceledException)
        {
            return; // newer text: its own pass decides
        }
        catch (Exception)
        {
            await ApplyDetectionAsync(null, token); // a failed detection only hides the checkbox
        }
    }

    // Applied on the main thread, and only if no newer text or translation has superseded this pass.
    private Task ApplyDetectionAsync(string? detected, CancellationToken token) => MainThread.InvokeOnMainThreadAsync(() =>
    {
        if (token.IsCancellationRequested || _translating)
        {
            return;
        }

        _detected = detected;
        RefreshTranslateOption();
    });

    private void RefreshTranslateOption()
    {
        string target = TargetCode();
        bool canTranslate = _detected is not null && _translator is { IsAvailable: true } translator && translator.SupportsPair(_detected, target);
        _optionShown = ReviewDecisions.ShowTranslateOption(_detected, target, canTranslate);
        TranslateRow.IsVisible = _optionShown;
        if (_optionShown)
        {
            TextFlow.ForUi(TranslateTitle, AppResources.Format(AppResources.Review_TranslateTo, LanguageName(target)));
            TextFlow.ForUi(TranslateCaption, AppResources.Format(AppResources.Review_LooksLike, LanguageName(_detected!)));
            SemanticProperties.SetDescription(TranslateRow, TranslateTitle.Text);
        }

        RenderReadButton();
    }

    // Named in the UI language ("French" in an English app), as the mock shows.
    private static string LanguageName(string code)
    {
        try
        {
            string name = CultureInfo.GetCultureInfo(code).DisplayName;
            return string.IsNullOrWhiteSpace(name) ? code : name;
        }
        catch (CultureNotFoundException)
        {
            return code;
        }
    }

    private void OnTranslateRowTapped(object? sender, TappedEventArgs e)
    {
        if (_translating)
        {
            return;
        }

        _ticked = !_ticked;
        RenderTick();
        RenderReadButton();
    }

    private void RenderTick()
    {
        TickBox.BackgroundColor = _ticked ? Brand() : Colors.Transparent;
        TickBox.Stroke = new SolidColorBrush(_ticked ? Brand() : Color.FromArgb(UntickedBorder));
        TickMark.IsVisible = _ticked;
    }

    private void RenderReadButton()
    {
        bool translates = ReviewDecisions.TranslatesOnRead(_optionShown, _ticked);
        TextFlow.ForUi(ReadLabel, translates ? AppResources.Review_TranslateAndRead : AppResources.Review_Read);
        bool blocked = _translating || string.IsNullOrWhiteSpace(_state.Text);
        ReadButton.Opacity = _translating ? TranslatingOpacity : blocked ? DisabledOpacity : 1;
        ReadButton.InputTransparent = blocked;
    }

    // ---- Header, Paste and Show original ----------------------------------------------------

    private async void OnBackTapped(object? sender, TappedEventArgs e) => await LeaveAsync();

    private async Task LeaveAsync()
    {
        if (_leaving)
        {
            return;
        }

        _leaving = true;
        _translateCts?.Cancel();
        await Shell.Current.GoToAsync(AppShell.BackRoute);
    }

    private async void OnPasteTapped(object? sender, TappedEventArgs e)
    {
        if (_translating)
        {
            return;
        }

        try
        {
            string? text = await Clipboard.Default.GetTextAsync();
            if (!string.IsNullOrWhiteSpace(text))
            {
                SetState(new ReviewTextState(text), TimeSpan.Zero);
            }
        }
        catch (Exception)
        {
            await DisplayAlertAsync(AppResources.Reader_PasteFailedTitle, AppResources.Reader_PasteFailedMessage, AppResources.Common_Ok, FlowDirection.LeftToRight);
        }
    }

    private void OnShowOriginalTapped(object? sender, TappedEventArgs e)
    {
        if (!_translating && _state.HasOriginal)
        {
            SetState(_state.ShowOriginal(), TimeSpan.Zero);
        }
    }

    // ---- Read and Translate and read --------------------------------------------------------

    private async void OnReadTapped(object? sender, TappedEventArgs e)
    {
        if (_translating || _leaving || string.IsNullOrWhiteSpace(_state.Text))
        {
            return;
        }

        if (ReviewDecisions.TranslatesOnRead(_optionShown, _ticked))
        {
            await TranslateAndFinishAsync();
            return;
        }

        // A text that reads as foreign again (option shown) is no longer the translation its Original came from.
        await FinishAsync(_state.Text, _optionShown ? null : _state.Original, ReviewDecisions.ReadLanguage(false, _detected, TargetCode()));
    }

    private async Task TranslateAndFinishAsync()
    {
        string source = _detected!;
        string target = TargetCode();
        _detectCts?.Cancel();
        _translateCts = new CancellationTokenSource();
        CancellationToken token = _translateCts.Token;
        SetTranslating(true);
        try
        {
            string translated = await TranslateAsync(source, target, token);
            if (!token.IsCancellationRequested)
            {
                _state = _state.Translated(translated);
                await FinishAsync(translated, _state.Original, ReviewDecisions.ReadLanguage(true, source, target));
            }
        }
        catch (OperationCanceledException)
        {
            return; // left the page
        }
        catch (Exception)
        {
            await ReadOriginalAfterFailureAsync(source, target);
        }
        finally
        {
            SetTranslating(false);
        }
    }

    // Shows the download card only when the model is not on the phone yet.
    private async Task<string> TranslateAsync(string source, string target, CancellationToken token)
    {
        ITranslator translator = _translator!;
        string text = _state.Text;
        if (!await translator.IsModelReadyAsync(source, target))
        {
            ShowDownloadCard(source);
        }

        string translated = await TranslationFlow.TranslateAsync(translator, text, source, target, token);
        return string.IsNullOrWhiteSpace(translated)
            ? throw new TranslationModelUnavailableException("The translation was empty.")
            : translated;
    }

    // The translator could not be fetched or failed: the original is read, with one short line saying why.
    private async Task ReadOriginalAfterFailureAsync(string source, string target)
    {
        if (_leaving)
        {
            return;
        }

        await DisplayAlertAsync(AppResources.Review_Title, AppResources.Review_TranslateFailed, AppResources.Common_Ok, FlowDirection.LeftToRight);
        await FinishAsync(_state.Text, null, ReviewDecisions.ReadLanguage(false, source, target));
    }

    private async Task FinishAsync(string text, string? original, string? language)
    {
        _handOff.Finish(new ReviewResult(text, original, language));
        await LeaveAsync();
    }

    // ---- The translating look (R7) ----------------------------------------------------------

    private void SetTranslating(bool translating)
    {
        _translating = translating;
        TextEditor.IsReadOnly = translating;
        TextEditor.TextColor = translating
            ? EditorTextColor().WithAlpha((float)TranslatingTextAlpha)
            : EditorTextColor();
        TextBox.StrokeThickness = translating ? 1 : 2;
        TextBox.Stroke = translating ? new SolidColorBrush(Color.FromArgb(DisabledBorder)) : new SolidColorBrush(Brand());
        TranslateRow.Opacity = translating ? TranslatingOpacity : 1;
        TranslateCaption.IsVisible = !translating;
        ReadButton.Opacity = translating ? TranslatingOpacity : 1;
        ReadButton.InputTransparent = translating;
        if (!translating)
        {
            HideDownloadCard();
            RenderReadButton();
        }
    }

    private static Color Brand() => (Color)Microsoft.Maui.Controls.Application.Current!.Resources["Primary"];

    private static Color EditorTextColor() =>
        Microsoft.Maui.Controls.Application.Current!.RequestedTheme == AppTheme.Dark ? Color.FromArgb(DarkEditorText) : Color.FromArgb(LightEditorText);

    private void ShowDownloadCard(string source)
    {
        TextFlow.ForUi(ProgressTitle, AppResources.Format(AppResources.Review_GettingTranslator, LanguageName(source)));
        ProgressCard.IsVisible = true;
        ReadButton.IsVisible = false;
        StartSweep();
    }

    private void HideDownloadCard()
    {
        this.AbortAnimation(SweepAnimationName);
        ProgressCard.IsVisible = false;
        ReadButton.IsVisible = true;
        ProgressBar.Progress = 0;
    }

    // ML Kit reports no download percentage, so one repeating animation sweeps the bar while the model comes down.
    private void StartSweep()
    {
        var sweep = new Animation(value => ProgressBar.Progress = value, 0, 1);
        sweep.Commit(this, SweepAnimationName, length: (uint)SweepDuration.TotalMilliseconds, easing: Easing.Linear, repeat: () => true);
    }
}
