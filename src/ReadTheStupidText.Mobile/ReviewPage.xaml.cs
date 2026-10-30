using System.Globalization;
using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Application.Usage;
using ReadTheStupidText.Mobile.Resources.Strings;
using ReadTheStupidText.Mobile.Shared.ModelDownload;
using ReadTheStupidText.Mobile.Shared.Network;
using ReadTheStupidText.Mobile.Shared.Usage;
using ReadTheStupidText.Mobile.Shared.Waiting;
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
    private const double TranslatingOpacity = 0.55;
    private const double DisabledOpacity = 0.45;
    private const double TranslatingTextAlpha = 0.45;
    private const string DisabledBorder = "#1C000000";
    private const string UntickedBorder = "#59000000";
    private const string LightEditorText = "#1F2027";

    private readonly ReviewHandOff _handOff;
    private readonly ISettingsStore _settings;
    private readonly ITranslator? _translator;
    private readonly ILanguageDetector? _detector;
    private readonly RtstUsage _usage;
    private readonly EarlyStartVoice _earlyVoice;
    private readonly IPackUpdateNetwork _network;

    // Early start (Slice 139): the head of the text is voiced, and translated when it is foreign, while the person decides.
    // Typing waits for one second of quiet in all: the detection's own wait, then this rest.
    private static readonly TimeSpan EarlyIdleAfterDetection = TimeSpan.FromMilliseconds(400);
    private readonly EarlyStartCoordinator _early = new();
    private TimeSpan _earlyDelay;

    // The pair the translate offer was last counted for on this text, so typing does not count it again (Slice 137).
    private string? _offeredPair;

    // The pair translate_chosen was last counted for on this text, so a retry after a failed translation is still one decision.
    private string? _chosenPair;

    private ReviewTextState _state = new(string.Empty);
    private string _failedBannerText = string.Empty;
    private bool _originalCut; // the original (Show original) was cut to the editor limit too
    private string? _detected;
    private bool _optionShown;
    private bool _ticked = true;
    private bool _translating;
    private bool _leaving;
    private bool _finishing; // waiting for the chosen side's early voice before handing the text over
    private bool _handedOver; // the text went to the main page: the chosen early work lives on there
    private bool _settleLeft; // the page was left (Back) while it waited, as opposed to Cancel on the overlay
    private CancellationTokenSource? _settleCts;
    private static readonly TimeSpan EarlySettleLimit = TimeSpan.FromSeconds(10);
    private bool _settingText;
    private CancellationTokenSource? _detectCts;
    private CancellationTokenSource? _translateCts;
    private readonly PackResumeWatcher _resume;
    private readonly WaitController _wait;
    private readonly WaitOverlay _overlay;
    private readonly WaitThemeFollower _overlayTheme;
    private Task? _inFlightCall; // the platform call a Cancel abandoned (or the one running now): a new translate waits for it
    private Task? _earlyCall; // the same for the early translation's calls, kept apart so neither flow overwrites the other's
    private readonly SingleModelCall _modelCall = new(); // one platform model download in flight per page, whichever flow asks
    private TranslationDownloadWait? _earlyDownloadView; // the early translation's download display, to show when the tap's wait begins
    private bool _earlyStale; // the page was left: its early work was stopped, and a return without a new request starts it over

    public ReviewPage(ReviewHandOff handOff, ISettingsStore settings, RtstUsage usage, EarlyStartVoice earlyVoice, IPackUpdateNetwork network, PackResumeWatcher resume, ITranslator? translator = null, ILanguageDetector? detector = null)
    {
        InitializeComponent();
        FlowDirection = FlowDirection.LeftToRight;
        _handOff = handOff;
        _settings = settings;
        _usage = usage;
        _earlyVoice = earlyVoice;
        _network = network;
        _resume = resume;
        _translator = translator;
        _detector = detector;
        _overlay = ReaderPage.CreateWaitOverlay();
        TextBoxContent.Children.Add(_overlay);
        _wait = new WaitController(_overlay);
        _overlayTheme = new WaitThemeFollower(_wait, _overlay);
        _wait.CancelRequested += OnWaitCancelRequested;
        ApplyStaticTexts();
        RenderTick();
        RenderReadButton();
    }

    // Cancel on the overlay: while waiting for the voice it abandons the read and stays on the page, else it stops the translation.
    private void OnWaitCancelRequested()
    {
        if (_finishing)
        {
            _settleCts?.Cancel();
            return;
        }

        _translateCts?.Cancel();
    }

    private string TargetCode() => AppLanguages.Code(AppLanguageSetting.Current(_settings));

    private void ApplyStaticTexts()
    {
        TextFlow.ForUi(TitleLabel, AppResources.Review_Title);
        TextFlow.ForUi(PasteLabel, AppResources.Reader_PasteButton);
        TextFlow.ForUi(ShowOriginalLink, AppResources.Review_ShowOriginal);
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
        else if (_earlyStale && !_leaving)
        {
            RestartEarlyWork(); // back on this page with the same text: what the tap chose or the leaving stopped starts over
        }

        _earlyStale = false;
        _handedOver = false;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Microsoft.Maui.Controls.Application.Current!.On<AndroidPlatform>().UseWindowSoftInputModeAdjust(WindowSoftInputModeAdjust.Pan);
        _overlayTheme.Detach();
    }

    // Cancels only when the page is really left (Back, hardware back, Read), not when the app goes to the background.
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        _detectCts?.Cancel();
        _translateCts?.Cancel();
        AbortSettle();
        if (_handedOver)
        {
            _early.CancelAll(); // the text was handed over: a side the tap chose keeps going for the read
        }
        else
        {
            _early.Discard(); // left without reading: nothing prepared is wanted, and it must not hold the translator or the voice
        }

        _earlyStale = true;
    }

    private void ApplyRequest(ReviewRequest request)
    {
        (string text, bool cut) = EditorTextLimit.Cap(request.Text);
        (string? original, _originalCut) = request.Original is null ? (null, false) : EditorTextLimit.Cap(request.Original); // Show original sets the editor too
        SetState(new ReviewTextState(text, original), TimeSpan.Zero);
        ShowFailedBanner(request.FailedPages, request.ReadPages);
        ShowCutBanner(cut);
        if (string.IsNullOrEmpty(request.Text))
        {
            TextEditor.Focus();
        }
    }

    // Slice 148: a text too long for the native editor is cut and the page says so, in the same note the failed pages use.
    private void ShowCutBanner(bool cut)
    {
        if (!cut)
        {
            return;
        }

        string cutText = AppResources.Format(AppResources.Review_TextCut, EditorTextLimit.MaxCharacters.ToString("N0", CultureInfo.CurrentCulture));
        TextFlow.ForUi(FailedLabel, string.IsNullOrEmpty(_failedBannerText) ? cutText : $"{_failedBannerText} {cutText}");
        FailedBanner.IsVisible = true;
    }

    private void ShowFailedBanner(IReadOnlyList<int>? failed, IReadOnlyList<int>? read)
    {
        if (failed is not { Count: > 0 })
        {
            return;
        }

        string failedText = AppResources.Format(failed.Count == 1 ? AppResources.Review_FailedOne : AppResources.Review_FailedMany, ScannedText.FormatPageNumbers(failed));
        _failedBannerText = $"{failedText} {HereText(read)}".TrimEnd();
        TextFlow.ForUi(FailedLabel, _failedBannerText);
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
        _failedBannerText = string.Empty; // the banner is rebuilt from this, never appended to what is on screen
        _detected = null;
        _offeredPair = null;
        _chosenPair = null;
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
        _early.Discard();
        _earlyVoice.Reset();
        _earlyDelay = delay > TimeSpan.Zero ? EarlyIdleAfterDetection : TimeSpan.Zero;
        RefreshTranslateOption();
        if (_detector is null || string.IsNullOrWhiteSpace(text))
        {
            _detected = null;
            StartEarlyWork();
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
        StartEarlyWork();
    });

    private void RefreshTranslateOption()
    {
        string target = TargetCode();
        bool canTranslate = _detected is not null && _translator is { IsAvailable: true } translator && translator.SupportsPair(_detected, target);
        _optionShown = ReviewDecisions.ShowTranslateOption(_detected, target, canTranslate);
        TranslateRow.IsVisible = _optionShown;
        if (_optionShown)
        {
            CountTranslateOffered(_detected!, target);
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
        if (_translating || _finishing)
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
        AbortSettle();
        await Shell.Current.GoToAsync(AppShell.BackRoute);
    }

    private async void OnPasteTapped(object? sender, TappedEventArgs e)
    {
        if (_translating || _finishing)
        {
            return;
        }

        try
        {
            string? text = await Clipboard.Default.GetTextAsync();
            if (!string.IsNullOrWhiteSpace(text))
            {
                (string capped, bool cut) = EditorTextLimit.Cap(text);
                _originalCut = false;
                SetState(new ReviewTextState(capped), TimeSpan.Zero);
                ShowCutBanner(cut);
            }
        }
        catch (Exception)
        {
            await DisplayAlertAsync(AppResources.Reader_PasteFailedTitle, AppResources.Reader_PasteFailedMessage, AppResources.Common_Ok, FlowDirection.LeftToRight);
        }
    }

    private void OnShowOriginalTapped(object? sender, TappedEventArgs e)
    {
        if (!_translating && !_finishing && _state.HasOriginal)
        {
            SetState(_state.ShowOriginal(), TimeSpan.Zero);
            ShowCutBanner(_originalCut); // the editor holds the capped original: keep telling the person
        }
    }

    // ---- Read and Translate and read --------------------------------------------------------

    private async void OnReadTapped(object? sender, TappedEventArgs e)
    {
        if (_translating || _leaving || _finishing || string.IsNullOrWhiteSpace(_state.Text))
        {
            return;
        }

        if (ReviewDecisions.TranslatesOnRead(_optionShown, _ticked))
        {
            await TranslateAndFinishAsync();
            return;
        }

        _early.ChooseRead();

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
        CountTranslateChosen(source, target);
        ReviewTextState before = _state;
        Task<EarlyTranslation?> earlyTask = _early.ChooseTranslate();
        SetTranslating(true);
        try
        {
            string translated = await TranslateAsync(source, target, earlyTask, token);
            if (!token.IsCancellationRequested)
            {
                _state = _state.Translated(translated);
                _wait.End(); // the page is about to leave: no "Translating..." with a live Cancel meanwhile
                await FinishAsync(translated, _state.Original, ReviewDecisions.ReadLanguage(true, source, target), before);
            }
        }
        catch (OperationCanceledException)
        {
            if (!_leaving)
            {
                RestartEarlyWork(); // Cancel on the wait overlay: the original text stays, so its early work starts over
            }

            return; // left the page, or Cancel on the wait overlay: the original text stays
        }
        catch (Exception)
        {
            _wait.End(); // gone before the alert, not behind it
            if (!_leaving)
            {
                RestartEarlyWork(); // the translation choice is withdrawn: nothing of it keeps running
            }

            await ReadOriginalAfterFailureAsync(source, target);
        }
        finally
        {
            SetTranslating(false);
        }
    }

    // The wait overlay names the stage: the download whenever the model is not on the phone yet. Cancel returns at once
    // (WaitAsync), even when the platform call does not look at the token; that call is remembered, so a new translate
    // waits for it to settle instead of starting a second download or translation of the same pair beside it.
    private async Task<string> TranslateAsync(string source, string target, Task<EarlyTranslation?> earlyTask, CancellationToken token)
    {
        string text = _state.Text;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        EarlyTranslation? early = await AwaitEarlyTranslationAsync(source, target, text, earlyTask, token);
        if (early is null)
        {
            return await TranslateTextAsync(text, source, target, early: false, token);
        }

        string rest = early.Split.Remainder;
        if (rest.Length == 0)
        {
            CountTiming(UsageStage.Translate, UsageOutcome.Done, clock.Elapsed, source, target, early: true); // the early work was all of it
            return early.TranslatedHead;
        }

        return early.Joined(await TranslateTextAsync(rest, source, target, early: true, token));
    }

    private async Task<string> TranslateTextAsync(string text, string source, string target, bool early, CancellationToken token)
    {
        ITranslator translator = _translator!;
        _wait.Begin(WaitStage.Translating, AppResources.Wait_Translating, canCancel: true);
        await SettleAbandonedCallAsync(token);
        bool downloading = !await Tracked(translator.IsModelReadyAsync(source, target), token);
        if (downloading)
        {
            _wait.Begin(WaitStage.DownloadingModel, AppResources.Format(AppResources.Wait_DownloadingModel, LanguageName(source)), canCancel: true, AppResources.Review_FirstTimeOnly);
            await FetchModelTimedAsync(translator, source, target, token);
        }

        if (!downloading || translator is IModelPreparer)
        {
            _wait.Begin(WaitStage.Translating, AppResources.Wait_Translating, canCancel: true);
        }

        string translated = await TimedAsync(UsageStage.Translate, source, target, early, () => Tracked(TranslationFlow.TranslateAsync(translator, text, source, target, token), token));
        return string.IsNullOrWhiteSpace(translated)
            ? throw new TranslationModelUnavailableException("The translation was empty.")
            : translated;
    }

    // Remembers the platform call and returns a task that gives up when Cancel is tapped, leaving the call itself running.
    private Task<T> Tracked<T>(Task<T> call, CancellationToken token)
    {
        _inFlightCall = call;
        return call.WaitAsync(token);
    }

    private Task<T> TrackedEarly<T>(Task<T> call, CancellationToken token)
    {
        _earlyCall = call;
        return call.WaitAsync(token);
    }

    private Task TrackedEarly(Task call, CancellationToken token)
    {
        _earlyCall = call;
        return call.WaitAsync(token);
    }

    private Task Tracked(Task call, CancellationToken token)
    {
        _inFlightCall = call;
        return call.WaitAsync(token);
    }

    // A call Cancel abandoned may still be downloading; its result is ignored, but a new one starts only after it ends.
    private async Task SettleAbandonedCallAsync(CancellationToken token)
    {
        await SettleAsync(_inFlightCall, token);
        await SettleAsync(_earlyCall, token);
    }

    private static async Task SettleAsync(Task? abandoned, CancellationToken token)
    {
        if (abandoned is null || abandoned.IsCompleted)
        {
            return;
        }

        try
        {
            await abandoned.WaitAsync(token);
        }
        catch (Exception) when (!token.IsCancellationRequested)
        {
            // the abandoned call failed or was cancelled: it has settled, which is all this wait needed
        }
    }

    // The translator could not be fetched or failed: the original is read, with one short line saying why.
    private async Task ReadOriginalAfterFailureAsync(string source, string target)
    {
        if (_leaving)
        {
            return;
        }

        await DisplayAlertAsync(AppResources.Review_Title, AppResources.Review_TranslateFailed, AppResources.Common_Ok, FlowDirection.LeftToRight);
        _early.ChooseRead(); // the original is read: its early work keeps going after the page is left
        await FinishAsync(_state.Text, null, ReviewDecisions.ReadLanguage(false, source, target));
    }

    // The chosen side's early work is awaited, never dropped: the reader's own start cancels a preparation still running,
    // so the read would begin cold. What ended is in the sentence cache.
    // False when the wait was abandoned (Cancel on the overlay, or the page was left): the text is not handed over.
    private async Task<bool> SettleChosenEarlyWorkAsync()
    {
        Task chosen = _early.SettleChosenAsync();
        if (chosen.IsCompleted)
        {
            return true;
        }

        _finishing = true;
        _settleLeft = false;
        TextEditor.IsReadOnly = true; // the text on screen is the text being handed over
        _settleCts = new CancellationTokenSource();
        _wait.Begin(WaitStage.PreparingVoice, AppResources.Wait_PreparingVoice, canCancel: true);
        try
        {
            await chosen.WaitAsync(EarlySettleLimit, _settleCts.Token);
            return true;
        }
        catch (TimeoutException)
        {
            return true; // slow engine: read now rather than keep the person waiting
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            _wait.End();
            _finishing = false;
            TextEditor.IsReadOnly = false;
            _settleCts.Dispose();
            _settleCts = null;
        }
    }

    // Back (or the system back) while waiting: the page is left, so the wait ends and nothing is handed over.
    private void AbortSettle()
    {
        if (!_finishing)
        {
            return;
        }

        _settleLeft = true;
        _settleCts?.Cancel();
    }

    private async Task<bool> FinishAsync(string text, string? original, string? language, ReviewTextState? undo = null)
    {
        if (!await SettleChosenEarlyWorkAsync() || _leaving || _settleLeft)
        {
            AbandonFinish(undo);
            return false;
        }

        _handedOver = true;
        _handOff.Finish(new ReviewResult(text, original, language));
        await LeaveAsync();
        return true;
    }

    // Cancel on the wait overlay stays on the page with the text it had; the side the tap chose is withdrawn, so both start over.
    private void AbandonFinish(ReviewTextState? undo)
    {
        if (undo is not null)
        {
            _state = undo;
        }

        if (!_leaving && !_settleLeft)
        {
            RestartEarlyWork();
        }
    }

    // ---- Early start (Slice 139) ------------------------------------------------------------

    // Voices the head of the text, and translates then voices the head of its translation when the checkbox is offered, so
    // the first sound and the first translation are already done when the person taps. The tap picks a side (Read or
    // Translate and read) and the other side stops. None of it is counted: only what the tap starts is.
    // The early work starts over at once (no idle rest) after a Translate and read that was cancelled or failed.
    private void RestartEarlyWork()
    {
        _early.Discard();
        _earlyDelay = TimeSpan.Zero;
        StartEarlyWork();
    }

    private void StartEarlyWork()
    {
        string text = _state.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            _early.Discard();
            return;
        }

        string target = TargetCode();
        string? source = _optionShown ? _detected : null;
        string language = VoiceRouting.ReadingLanguage(false, target, _settings.TranslateSourceLanguage, _detected, text);
        var jobs = new EarlyStartJobs(
            (head, token) => _earlyVoice.VoiceAsync(head, language, token),
            source is null || _translator is null ? null : (span, token) => TranslateHeadEarlyAsync(source, target, span, token),
            (voiced, token) => _earlyVoice.VoiceAsync(voiced, target, token));
        _early.Restart(text, source, target, jobs, _earlyDelay);
    }

    // A model that is not on the phone downloads early only on an unmetered network; on mobile data it waits for the tap,
    // whose wait overlay then names the download.
    private async Task<string?> TranslateHeadEarlyAsync(string source, string target, EarlySplit span, CancellationToken token)
    {
        ITranslator translator = _translator!;
        await SettleAbandonedCallAsync(token); // never beside a call that was abandoned for the same pair
        bool ready = await TrackedEarly(translator.IsModelReadyAsync(source, target), token);
        if (!ready && !_network.AllowsAutomaticUpdate)
        {
            return null;
        }

        if (!ready && translator is IModelPreparer preparer)
        {
            await DownloadModelEarlyAsync(preparer, source, target, token);
        }

        System.Diagnostics.Debug.WriteLine($"{EarlyStartVoice.LogTag} translate start ({source}->{target}, {span.Head.Length} chars)");
        string? translated = await TrackedEarly(TranslationFlow.TranslateAsync(translator, span.Head, source, target, token), token);
        System.Diagnostics.Debug.WriteLine($"{EarlyStartVoice.LogTag} translate done ({source}->{target}, ok={translated is not null}, cancelled={token.IsCancellationRequested})");
        return translated;
    }

    // The early translation of this very text and pair, joined in if it finished or is still running; null when there is none,
    // so the whole text is translated as before. A download still running behind it is named by the overlay.
    private async Task<EarlyTranslation?> AwaitEarlyTranslationAsync(string source, string target, string text, Task<EarlyTranslation?> earlyTask, CancellationToken token)
    {
        if (!earlyTask.IsCompleted)
        {
            await ShowStageWhileEarlyRunsAsync(source, target, token);
        }

        EarlyTranslation? early = await Tracked(earlyTask, token);
        return early is not null && early.Matches(text, source, target) ? early : null;
    }

    private async Task ShowStageWhileEarlyRunsAsync(string source, string target, CancellationToken token)
    {
        bool downloading;
        try
        {
            downloading = !await Tracked(_translator!.IsModelReadyAsync(source, target), token);
        }
        catch (Exception) when (!token.IsCancellationRequested)
        {
            return; // the early work is only an optimization: no stage named, the wait overlay stays as it is
        }

        if (downloading)
        {
            _wait.Begin(WaitStage.DownloadingModel, AppResources.Format(AppResources.Wait_DownloadingModel, LanguageName(source)), canCancel: true, AppResources.Review_FirstTimeOnly);
            _earlyDownloadView?.TakeOver(); // the early download may already be waiting for the internet: it now owns the wait and shows that look at once
        }
        else
        {
            _wait.Begin(WaitStage.Translating, AppResources.Wait_Translating, canCancel: true);
        }
    }

    // ---- Anonymous counts (Slice 137) -------------------------------------------------------

    // Once per loaded text and pair: the checkbox appearing, not every re-detection while the person types.
    private void CountTranslateOffered(string source, string target)
    {
        string pair = $"{source}>{target}";
        if (_offeredPair == pair)
        {
            return;
        }

        _offeredPair = pair;
        _usage.Record(context => UsageEvents.TranslateOffered(context, source, target));
    }

    private void CountTranslateChosen(string source, string target)
    {
        string pair = $"{source}>{target}";
        if (_chosenPair == pair)
        {
            return;
        }

        _chosenPair = pair;
        _usage.Record(context => UsageEvents.TranslateChosen(context, source, target));
    }

    // The wait for the ML Kit model on its own, when the translator can fetch it apart from translating; otherwise the download is part of the translate stage.
    // The download runs in a session (Slice 187): with no internet it waits and carries on by itself instead of failing.
    private async Task FetchModelTimedAsync(ITranslator translator, string source, string target, CancellationToken token)
    {
        if (translator is IModelPreparer preparer)
        {
            TranslationModelDownloadSession session = NewDownloadSession(preparer, source, target);
            using (session)
            {
                // the time spent waiting for the internet is not download time: it would skew the production stage durations
                await TimedAsync(UsageStage.ModelDownload, source, target, early: false, async () =>
                {
                    await RunDownloadSessionAsync(session, source, token);
                    return true;
                }, () => session.WaitedTotal);
            }
        }
    }

    private TranslationModelDownloadSession NewDownloadSession(IModelPreparer preparer, string source, string target)
    {
        string packKey = TranslationModelDownloadSession.PackKey(source, target);
        return ConnectivityTranslationNetwork.NewSession(
            packKey, (_, attemptToken) => _modelCall.RunAsync(packKey, () => preparer.EnsureModelAsync(source, target, attemptToken), attemptToken), _resume);
    }

    private async Task RunDownloadSessionAsync(TranslationModelDownloadSession session, string source, CancellationToken token)
    {
        using var view = new TranslationDownloadWait(_wait, session, DownloadTexts(source));
        view.Show();
        await session.RunAsync(token);
    }

    // The early translation's model download goes through a session too, so a tap meanwhile shows the waiting look (Slice 187).
    private async Task DownloadModelEarlyAsync(IModelPreparer preparer, string source, string target, CancellationToken token)
    {
        using TranslationModelDownloadSession session = NewDownloadSession(preparer, source, target);
        using var view = new TranslationDownloadWait(_wait, session, DownloadTexts(source), owner: false); // the tap's view takes the controller over
        _earlyDownloadView = view;
        try
        {
            await session.RunAsync(token);
        }
        finally
        {
            _earlyDownloadView = null;
        }
    }

    private static TranslationDownloadTexts DownloadTexts(string source) => new(
        LanguageName(source), AppResources.Format(AppResources.Wait_DownloadingModel, LanguageName(source)), AppResources.Review_FirstTimeOnly,
        AppResources.Wait_WaitingInternet, AppResources.Wait_WaitingWifi, AppResources.Wait_WaitingPercent, AppResources.Wait_WaitingNoPercent, AppResources.Wait_Retry);

    // Times one wait and counts it with how it ended, then lets the result or the error through unchanged.
    private async Task<T> TimedAsync<T>(string stage, string source, string target, bool early, Func<Task<T>> work, Func<TimeSpan>? excluded = null)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            T result = await work();
            CountTiming(stage, UsageOutcome.Done, clock.Elapsed - Excluded(excluded), source, target, early);
            return result;
        }
        catch (OperationCanceledException)
        {
            CountTiming(stage, UsageOutcome.Cancelled, clock.Elapsed - Excluded(excluded), source, target, early);
            throw;
        }
        catch (Exception)
        {
            CountTiming(stage, UsageOutcome.Failed, clock.Elapsed - Excluded(excluded), source, target, early);
            throw;
        }
    }

    private static TimeSpan Excluded(Func<TimeSpan>? excluded) => excluded?.Invoke() ?? TimeSpan.Zero;

    private void CountTiming(string stage, string outcome, TimeSpan elapsed, string source, string target, bool early) =>
        _usage.Record(context => UsageEvents.Timing(context, stage, outcome, elapsed, source, target, early));

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
            _wait.End();
            RenderReadButton();
        }
    }

    private static Color Brand() => (Color)Microsoft.Maui.Controls.Application.Current!.Resources["Primary"];

    private static Color EditorTextColor() =>
        ThemeColors.IsDark() ? ThemeColors.Get(ThemeColors.TextPrimaryDarkKey) : Color.FromArgb(LightEditorText);
}
