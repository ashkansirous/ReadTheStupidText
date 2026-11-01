using System.Globalization;
using ReadTheStupidText.Application.Documents;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Application.Usage;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Mobile.Resources.Strings;
using ReadTheStupidText.Mobile.Shared;
using ReadTheStupidText.Mobile.Shared.Usage;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// The app's single home screen, reduced to reading (Slice 99, Decision 103): the read-only text
/// with the spoken sentence highlighted, two buttons that add text (pictures, a file) and the
/// player. Language, voice and speed live on <see cref="SettingsPage"/>. <see cref="TextEditor"/> is only
/// the page's hidden text holder: Text from pictures opens <see cref="CapturePage"/> (Slice 100), and
/// pictures, a file, Edit and long-press Paste all end on <see cref="ReviewPage"/> (Slice 101), whose Read
/// hands the text back to be shown and read.
/// </summary>
public partial class ReaderPage : ContentPage
{
    private static readonly FilePickerFileType SupportedFileTypes = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        { DevicePlatform.Android, ["text/plain", "application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"] },
        { DevicePlatform.iOS, ["public.plain-text", "com.adobe.pdf", "org.openxmlformats.wordprocessingml.document"] },
    });

    private readonly ISpeechReader _reader;
    private readonly ISettingsStore _settings;
    private readonly IDocumentTextExtractor _documentExtractor;
    private readonly IDocumentScanner? _scanner;
    private readonly IVoiceModelService _model;
    private readonly VoicePackInstaller _installer;
    private readonly IPlatformServices _platform;
    private readonly IHeadStart _headStart;
    private readonly EditorDraft _draft;
    private readonly ReviewHandOff _review;
    private readonly RtstUsage _usage;
    private readonly EarlyStartVoice _earlyVoice;
    private string? _lastSpokenText;
    private int _ownPreparations; // this page's own head-start preparations running now (the early start must not cancel them)

    // The text a translation on the screen came from, so Edit can offer "Show original" (Slice 101).
    private string? _original;

    // True while a Paste/Scan/File action is in flight; further input taps are ignored.
    private bool _inputBusy;
    private bool _openingCapture;
    private bool _openingReview;
    private bool _scannerPrepared;

    // Set when a language change replaces this page: its in-flight scan or file read must not
    // touch the editor or show alerts on a page nobody sees any more.
    private bool _retired;

    // Per-read token for the play-button spinner (see SpeakWithLoadingAsync).
    private int _readSequence;
    private int _pendingReadId;
    private string? _pendingReadText;
    private int _returnedReadId;

    // How long the spinner may outlast a SpeakAsync that returned before playback
    // started (system TTS returns as soon as the utterance is queued).
    private static readonly TimeSpan FirstAudioGrace = TimeSpan.FromSeconds(8);

    public ReaderPage(ISpeechReader reader, ISettingsStore settings, IDocumentTextExtractor documentExtractor, IVoiceModelService model, VoicePackInstaller installer, IPlatformServices platform, EditorDraft draft, ReviewHandOff review, IHeadStart headStart, RtstUsage usage, EarlyStartVoice earlyVoice, IDocumentScanner? scanner = null, ILanguageDetector? detector = null)
    {
        InitializeComponent();
        FlowDirection = FlowDirection.LeftToRight;
        ApplyScanAvailability(platform.SupportsTextScan);
        // Before the first text is set: that fires TextChanged, which renders the player and reads the wait parts.
        InitWait();

        _reader = reader;
        _settings = settings;
        _documentExtractor = documentExtractor;
        _scanner = scanner;
        _headStart = headStart;
        _model = model;
        _installer = installer;
        _platform = platform;
        _draft = draft;
        _review = review;
        _usage = usage;
        _earlyVoice = earlyVoice;
        TextEditor.Text = draft.Take();
        _original = draft.TakeOriginal();
        TextEditor.FlowDirection = TextFlow.ContentDirection(TextEditor.Text);
        draft.RegisterRebuildHook(PrepareForRebuild);
        ApplyStaticTexts();
        InitUpdateCard();
        InitTranslation(detector);
        InitHeadStart();
        earlyVoice.Handler = VoiceEarlyAsync;
        RenderReading();
    }

    private void ApplyStaticTexts()
    {
        TextFlow.ForUi(EmptyHintLabel, AppResources.Reader_EmptyHint);
        TextFlow.ForUi(ScanLabel, AppResources.Reader_TextFromPictures);
        TextFlow.ForUi(FileLabel, AppResources.Reader_TextFromFile);
        TextFlow.ForUi(EditLabel, AppResources.Reader_EditLink);
        ApplyAccessibilityNames();
    }

    // The icon-only controls are gesture views, so TalkBack and VoiceOver read nothing unless they are named.
    private void ApplyAccessibilityNames()
    {
        SemanticProperties.SetDescription(SettingsButton, AppResources.Settings_Title);
        SemanticProperties.SetDescription(EditLink, AppResources.Reader_EditLink);
        SemanticProperties.SetDescription(ScanButton, AppResources.Reader_TextFromPictures);
        SemanticProperties.SetDescription(FileButton, AppResources.Reader_TextFromFile);
        SemanticProperties.SetDescription(SkipBackButton, AppResources.Reader_SkipBack);
        SemanticProperties.SetDescription(SkipForwardButton, AppResources.Reader_SkipForward);
        SemanticProperties.SetDescription(PlayButton, AppResources.Reader_Play);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _earlyVoice.Handler = VoiceEarlyAsync; // idempotent; keeps early voicing alive across a Review round trip
        TextEditor.FlowDirection = TextFlow.ContentDirection(TextEditor.Text); // an empty editor follows the UI language as it is now
        RenderReading();
        _reader.StateChanged += OnReaderStateChanged;
        _reader.ProgressChanged += OnReaderProgressChanged;
        _reader.TimingChanged += OnReaderTimingChanged;
        _reader.ChunkChanged += OnReaderChunkChanged;
        _reader.SentenceChanged += OnSentenceChanged;
        _reader.StallChanged += OnReaderStallChanged;
        _installer.Changed += OnInstallerChanged;
        _model.ReadyChanged += OnInstallerChanged;
        AttachUpdateCard();

        // The read may have paused or ended while Settings was on top.
        ApplyPlaybackState(_reader.State);

        // Settings may have changed the voice or language while this page was off-stack.
        RestartHeadStartIfKeyChanged();
        RenderPackStatus();
        RestorePendingReadSpinner();
        PrepareScannerOnce();
        TakeReviewResult();
        _ = ShowCountsNoticeOnceAsync();
    }

    // The review page's Read leaves the checked (or translated) text here; it is shown and read at once (Slice 101).
    private void TakeReviewResult()
    {
        if (_review.TakeResult() is not { } result)
        {
            return;
        }

        ReplaceEditorText(result.Text);
        _original = result.Original;
        _detectedSource = result.Language;
        if (result.Language is null && _detector is not null)
        {
            ScheduleDetection(result.Text); // the review page could not tell: look again, the voice follows the text
        }

        OnReadingLanguageMayHaveChanged();
        _ = BeginReadAsync(result.Text);
    }

    // Asks the platform to have the scanner ready (Play-services module download) before the
    // first Scan tap, so that tap usually finds it installed.
    private void PrepareScannerOnce()
    {
        if (_scanner is null || _scannerPrepared)
        {
            return;
        }

        _scannerPrepared = true;
        _ = _scanner.PrepareAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        DetachHandlers();

        // Hide the spinner but keep the pending-read state: the read may still be
        // generating, and OnAppearing restores the spinner if so.
        SetPlayLoading(false);
    }

    /// <summary>
    /// Unsubscribes from the singleton reader, installer and model. Also called before the shell is
    /// swapped out for a language change, where OnDisappearing is not guaranteed to run.
    /// </summary>
    public void DetachHandlers()
    {
        _waitTheme.Detach();
        DetachUpdateCard();
        _reader.StateChanged -= OnReaderStateChanged;
        _reader.ProgressChanged -= OnReaderProgressChanged;
        _reader.TimingChanged -= OnReaderTimingChanged;
        _reader.ChunkChanged -= OnReaderChunkChanged;
        _reader.SentenceChanged -= OnSentenceChanged;
        _reader.StallChanged -= OnReaderStallChanged;
        EndStallWait(); // nobody is left to hear the end of the stall: a late chunk would leave the overlay up over a playing read
        _installer.Changed -= OnInstallerChanged;
        _model.ReadyChanged -= OnInstallerChanged;
    }

    /// <summary>
    /// Called by a language change before the shell is rebuilt: stops the read, keeps the editor text for
    /// the new page, detaches from the singletons and retires this page's async work.
    /// </summary>
    public void PrepareForRebuild()
    {
        _retired = true;
        CancelDetection();
        _reader.Stop();
        EndFirstAudioClock(UsageOutcome.Cancelled);
        _draft.Store(TextEditor.Text, _original);
        _earlyVoice.Clear(VoiceEarlyAsync); // only a retired page gives up early voicing; Review sitting on top must still reach it
        DetachHandlers();
    }

    private Task AlertAsync(string title, string message) =>
        _retired ? Task.CompletedTask : DisplayAlertAsync(title, message, AppResources.Common_Ok, FlowDirection.LeftToRight);

    // Dims and disables the two add-text buttons while one of them runs, so a second tap can't start a
    // second scan or overwrite the editor mid-OCR. The editor is read-only meanwhile so typed edits are not lost.
    private void SetInputBusy(bool busy)
    {
        _inputBusy = busy;
        ActionRow.IsEnabled = !busy;
        TextEditor.IsReadOnly = busy;
        ActionRow.Opacity = busy ? 0.5 : 1;
        EditLink.IsEnabled = !busy;
    }

    // A platform with no OCR engine hides "Text from pictures"; "Text from a file" takes its place so no gap is left.
    // ScanIntoEditorAsync still catches OcrUnavailableException as a safety net.
    private void ApplyScanAvailability(bool supportsTextScan)
    {
        if (supportsTextScan)
        {
            return;
        }

        ScanButton.IsVisible = false;
        Grid.SetColumn(FileButton, 0);
        Grid.SetColumnSpan(FileButton, 2);
    }

    // Text from pictures opens the capture wizard (Slice 100, Decision 104); its Done opens the review page.
    private async void OnScanTapped(object? sender, TappedEventArgs e)
    {
        if (_inputBusy || _openingCapture)
        {
            return;
        }

        _openingCapture = true;
        try
        {
            await Shell.Current.GoToAsync(AppShell.CaptureRoute);
        }
        finally
        {
            _openingCapture = false;
        }
    }

    private async void OnFileTapped(object? sender, TappedEventArgs e)
    {
        if (_inputBusy || _wait.IsActive)
        {
            return; // a read still preparing owns the wait overlay; importing now would end it
        }

        SetInputBusy(true);
        try
        {
            await PickAndReadFileAsync();
        }
        finally
        {
            SetInputBusy(false);
        }
    }

    private async Task PickAndReadFileAsync()
    {
        FileResult? file;
        try
        {
            file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = AppResources.Reader_FilePickerTitle,
                FileTypes = SupportedFileTypes,
            });
        }
        catch (PermissionException)
        {
            await AlertAsync(AppResources.Reader_FilePermissionTitle, AppResources.Reader_FilePermissionMessage);
            return;
        }

        if (file is null || _retired)
        {
            return; // user cancelled the picker, or the page was replaced meanwhile
        }

        string? extracted = null;
        try
        {
            extracted = await _wait.RunAsync(WaitStage.GettingFile, AppResources.Wait_ReadingFile, canCancel: false,
                _ => _documentExtractor.ExtractTextAsync(file.FullPath));
        }
        catch (DocumentTooLargeException ex)
        {
            await AlertAsync(AppResources.Reader_FileTooLargeTitle, AppResources.Format(AppResources.Reader_FileTooLargeMessage, ex.Actual, ex.Limit));
        }
        catch (Exception)
        {
            await AlertAsync(AppResources.Reader_FileUnreadableTitle, AppResources.Reader_FileUnreadableMessage);
        }

        if (extracted is not null)
        {
            await OpenReviewAsync(new ReviewRequest(extracted));
        }
    }

    // Resuming only makes sense if the text box still holds what's paused —
    // otherwise a stale paused read would keep playing over edited text with no
    // way for the user to make it stop (this was the "text sticks" bug: editing
    // the box while paused never restarted the reader, since Resume() doesn't
    // look at the text box at all).
    private async void OnPlayPauseTapped(object? sender, TappedEventArgs e)
    {
        string text = TextEditor.Text ?? string.Empty;

        if (_reader.State == PlaybackState.Playing)
        {
            _reader.Pause();
            return;
        }

        if (_reader.State == PlaybackState.Paused && text == _lastSpokenText)
        {
            _reader.Resume();
            return;
        }

        // A tap while a read of this same text is still generating does nothing;
        // different text starts a new read (superseding the pending one).
        if (_pendingReadId != 0 && text == _pendingReadText)
        {
            return;
        }

        await BeginReadAsync(text);
    }

    // Starts a fresh read of the text on screen, superseding whatever is playing or pending.
    private async Task BeginReadAsync(string text)
    {
        CancelHeadStart();
        _reader.Stop();
        EndFirstAudioClock(UsageOutcome.Cancelled); // replaced by a new read; no-op when the first sound already played
        ClearPendingRead();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _lastSpokenText = text;
        await SpeakWithLoadingAsync(text);
    }

    // Each fresh read gets an incrementing id; the spinner is "pending for id N".
    // It is cleared when the reader reports Playing (ApplyPlaybackState), or when
    // SpeakAsync returns/throws while N is still current and nothing is playing
    // (a Stop mid-generation, a cancelled read, a reader that returned early). A
    // superseded read's completion never touches the current read's spinner.
    private async Task SpeakWithLoadingAsync(string text)
    {
        int id = ++_readSequence;
        _pendingReadId = id;
        _pendingReadText = text;
        SetPlayLoading(true);
        StartFirstAudioClock(text);
        try
        {
            // The text on screen is what is read: a translation was made on the review page (Slice 101).
            VoicePlan plan = await PlanVoiceAsync(ReadingLanguageFor(text), atPlay: true);
            if (!IsCurrentRead(id) || !await AllowReadAsync(id, plan))
            {
                return;
            }

            CountReadStarted(text, plan);
            await SpeakPlannedAsync(text, plan);
        }
        catch (Exception ex)
        {
            bool wasCurrent = id == _pendingReadId;
            if (wasCurrent)
            {
                // a superseded read's error must not end the newer read's clock
                EndFirstAudioClock(ex is OperationCanceledException ? UsageOutcome.Cancelled : UsageOutcome.Failed);
            }

            ClearPendingReadIfCurrent(id);
            if (wasCurrent)
            {
                await AlertAsync(AppResources.Reader_ReadFailedTitle, AppResources.Reader_ReadFailedMessage);
            }

            return;
        }

        await WaitForFirstAudioAsync(id);
    }

    private bool IsCurrentRead(int id) => id == _pendingReadId && !_retired;

    // Who reads one read and in which language, decided without touching the stored voice choice or
    // the app-language model (Slice 90).
    // Scope is null for the app language: that read takes the ordinary neural path, head start included.
    private sealed record VoicePlan(string Language, ReadVoiceScope? Scope, VoiceRoute Route, VoiceModelPaths? Pack, bool PhoneReads);

    // The last head-start/label plan: same inputs, same answer, so typing in one language plans once.
    private sealed record PlanKey(string Language, string VoiceId, string? PackRoot, string? PackVersion, bool ScopedReads, bool Downloading, string AppLanguage);

    private (PlanKey Key, VoicePlan Plan)? _planCache;

    // The app-language baseline first (previews and later reads speak it), then the read: a one-off
    // language travels in the scope with this read only and is never kept on the reader.
    private async Task SpeakPlannedAsync(string text, VoicePlan plan)
    {
        (_reader as IReadingLanguage)?.SetReadingLanguage(TargetCode());
        if (plan.Scope is { } scope && _reader is IReadScoped scoped)
        {
            await scoped.SpeakScopedAsync(text, scope);
            return;
        }

        await _reader.SpeakAsync(text);
    }

    /// <summary>
    /// The language a read is spoken in (Slice 90, replacing Slice 82's app-language rule): the
    /// translation target when translated, else the text's own language: the picker's choice, the translate
    /// card's detection of this text, else a guess from its script. No detection runs here.
    /// </summary>
    private string ReadingLanguageFor(string text)
    {
        var key = new LanguageKey(text, TargetCode(), _settings.TranslateSourceLanguage, _detectedSource);
        if (_languageMemo is { } memo && memo.Key == key)
        {
            return memo.Language;
        }

        string language = VoiceRouting.ReadingLanguage(false, key.Target, key.Choice, key.Detected, text);
        _languageMemo = (key, language);
        return language;
    }

    private sealed record LanguageKey(string Text, string Target, string? Choice, string? Detected);

    private (LanguageKey Key, string Language)? _languageMemo;

    private bool IsAppLanguage(string language) =>
        string.Equals(VoiceAvailability.Primary(language), VoiceAvailability.Primary(TargetCode()), StringComparison.OrdinalIgnoreCase);

    private async Task<VoiceModelPaths?> ModelForLanguageAsync(string language) =>
        _model is IReadingModelResolver resolver
            ? await resolver.ModelForAsync(language)
            : IsAppLanguage(language) ? _model.Paths : null;

    // A "Phone voice" choice the phone cannot speak in this language gives way to the natural voice for this
    // read only; the stored choice is migrated separately and only for the app language (the picker's rule).
    private async Task<VoicePlan> PlanVoiceAsync(string language, bool atPlay)
    {
        bool appLanguage = IsAppLanguage(language);
        bool scopedReads = (_reader as IReadScoped)?.SupportsModelScope ?? false;
        VoiceModelPaths? pack = appLanguage ? _model.Paths : await ModelForLanguageAsync(language);
        bool downloading = VoiceRouting.DownloadingFor(
            _installer.State is PackInstallState.Resolving or PackInstallState.Downloading, TargetCode(), language);
        var key = new PlanKey(language, _settings.VoiceId, pack?.RootDir, pack?.PackVersion, scopedReads, downloading, TargetCode());
        if (!atPlay && _planCache is { } cached && cached.Key == key)
        {
            return cached.Plan;
        }

        VoicePlan plan = await BuildPlanAsync(language, appLanguage, pack, scopedReads, downloading, atPlay);
        _planCache = (key, plan);
        return plan;
    }

    private async Task<VoicePlan> BuildPlanAsync(string language, bool appLanguage, VoiceModelPaths? pack, bool scopedReads, bool downloading, bool atPlay)
    {
        VoiceAvailability voices = await PlatformVoiceAvailability.BuildAsync(_platform);
        if (atPlay && appLanguage)
        {
            LeavePhoneVoiceIfItCannotReadTheApp(voices);
        }

        // Only a voice of this language ever reads it: on a platform that cannot name a pack per read, another
        // language's pack is not usable and the phone voice (or the block) decides.
        bool? phoneCan = PhoneCanRead(voices, language);
        bool packUsable = VoiceRouting.NaturalPackUsable(appLanguage, pack is not null, scopedReads);
        bool phoneChosen = _settings.VoiceId == SystemVoice.Id && (appLanguage || phoneCan != false);
        VoiceRoute route = VoiceRouting.Resolve(phoneChosen, packUsable, phoneCan, downloading);
        bool phoneReads = VoiceRouting.PhoneReads(phoneChosen, packUsable);
        VoiceModelPaths? usedPack = packUsable ? pack : null;
        ReadVoiceScope? scope = VoiceRouting.UsesScope(appLanguage) ? new ReadVoiceScope(usedPack, phoneReads, language) : null;
        return new VoicePlan(language, scope, route, usedPack, phoneReads);
    }

    // When the phone voice would speak and is known to have none for the language, say so rather than
    // read only the Latin fragments: the natural voice is either still downloading or not available.
    private async Task<bool> AllowReadAsync(int id, VoicePlan plan)
    {
        if (plan.Route == VoiceRoute.Read)
        {
            return true;
        }

        _firstAudioClock = null; // a blocked read never had a first sound to wait for
        ClearPendingReadIfCurrent(id);
        string message = plan.Route == VoiceRoute.Downloading ? AppResources.Reader_VoiceDownloadingMessage : AppResources.Reader_NoVoiceMessage;
        await AlertAsync(AppResources.Reader_VoiceUnavailableTitle, AppResources.Format(message, ReadingLanguageName(plan.Language)));
        return false;
    }

    private string ReadingLanguageName(string language) =>
        IsAppLanguage(language) ? LanguageChoices.NativeName(CurrentLanguage()) : SourceName(language);

    // Null (unknown) unless the phone was listed and the language is one it was asked about.
    private static bool? PhoneCanRead(VoiceAvailability voices, string language) =>
        voices.PhoneKnown ? voices.CanPhoneRead(language) : null;

    // The picker hides the Phone voice row when the phone can't read the app language; a stored
    // choice of it moves to the natural voice so the reader follows what the picker shows. A text in
    // another language never rewrites the stored choice.
    private void LeavePhoneVoiceIfItCannotReadTheApp(VoiceAvailability voices)
    {
        bool leave = VoiceRouting.ShouldLeavePhoneVoice(_settings.VoiceId == SystemVoice.Id, _model.IsReady, PhoneCanRead(voices, TargetCode()));
        if (leave && _model.Paths is { } paths)
        {
            PhoneVoiceMigration.MoveToNatural(_settings, _reader, paths);
        }
    }

    // SpeakAsync returned while id is still current and nothing is playing yet. Keep
    // the spinner until Playing (ApplyPlaybackState), an Idle arriving after the
    // return (also ApplyPlaybackState), or the grace expires. A superseded id never
    // clears; _pendingReadId stays set so the same-text tap guard holds meanwhile.
    private async Task WaitForFirstAudioAsync(int id)
    {
        if (id != _pendingReadId || _reader.State == PlaybackState.Playing)
        {
            ClearPendingReadIfCurrent(id);
            return;
        }

        _returnedReadId = id;
        await Task.Delay(FirstAudioGrace);
        ClearPendingReadIfCurrent(id);
    }

    private void ClearPendingReadIfCurrent(int id)
    {
        if (id == _pendingReadId && _reader.State != PlaybackState.Playing)
        {
            ClearPendingRead();
        }
    }

    private void ClearPendingRead()
    {
        _pendingReadId = 0;
        _returnedReadId = 0;
        _pendingReadText = null;
        SetPlayLoading(false);
    }

    private void RestorePendingReadSpinner()
    {
        if (_pendingReadId == 0)
        {
            return;
        }

        if (_reader.State == PlaybackState.Playing)
        {
            ClearPendingRead();
            return;
        }

        SetPlayLoading(true);
    }

    // The wait overlay (Slice 138) stands for the play button's old spinner: it shows after 500 ms and ends with the pending read.
    private void SetPlayLoading(bool loading)
    {
        if (loading)
        {
            BeginPreparingWait();
            return;
        }

        if (_wait.Stage == WaitStage.PreparingVoice)
        {
            _wait.End();
        }
    }

    private async void OnSkipBackTapped(object? sender, TappedEventArgs e) => await _reader.SkipBackward();

    private async void OnSkipForwardTapped(object? sender, TappedEventArgs e) => await _reader.SkipForward();

    private void OnReaderStateChanged(object? sender, PlaybackState state)
    {
        MainThread.BeginInvokeOnMainThread(() => ApplyPlaybackState(state));
    }

    private void ApplyPlaybackState(PlaybackState state)
    {
        bool playing = state == PlaybackState.Playing;
        PlayShape.IsVisible = !playing;
        PauseShape.IsVisible = playing;
        SemanticProperties.SetDescription(PlayButton, playing ? AppResources.Reader_Pause : AppResources.Reader_Play);
        if (state == PlaybackState.Idle)
        {
            ClearHighlight();
            EndStallWait();
        }

        // The dimmed, untouchable player is for an idle page only: a read still playing keeps its controls.
        RenderPlayerForState();

        if (PendingReadSettle.IsDone(state, _pendingReadId, _returnedReadId))
        {
            // Idle straight after the return means a short text played before the Playing event was handled; a late Playing still counts as done.
            EndFirstAudioClock(UsageOutcome.Done);
            ClearPendingRead();
        }
    }

    private void OnReaderProgressChanged(object? sender, double progress) =>
        MainThread.BeginInvokeOnMainThread(() => ReadProgressBar.Progress = progress);

    // What the next Play would read in: the text is read as it is, in its own language.
    private string CurrentReadingLanguage() =>
        ReadingLanguageFor(TextEditor.Text ?? string.Empty);

    // The detection or the text changed: the head start follows the new language.
    private void OnReadingLanguageMayHaveChanged() => RestartHeadStartIfKeyChanged();

    private void OnReaderTimingChanged(object? sender, ReadTiming timing) =>
        MainThread.BeginInvokeOnMainThread(() => ShowTiming(timing));

    private void ShowTiming(ReadTiming timing)
    {
        ElapsedLabel.Text = ReadTimingFormatter.FormatClock(timing.Elapsed);
        TotalLabel.Text = ReadTimingFormatter.FormatClock(timing.Total);
    }

    private static readonly TimeSpan ReadingLanguageDebounce = TimeSpan.FromMilliseconds(400);

    private CancellationTokenSource? _languageRefreshCts;

    // Every text change: the head-start key follows the new text's language once typing pauses.
    private void ScheduleReadingLanguageRefresh()
    {
        _languageRefreshCts?.Cancel();
        _languageRefreshCts = new CancellationTokenSource();
        _ = RefreshReadingLanguageAfterDelayAsync(_languageRefreshCts.Token);
    }

    private async Task RefreshReadingLanguageAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(ReadingLanguageDebounce, token);
            if (!_retired)
            {
                OnReadingLanguageMayHaveChanged();
            }
        }
        catch (OperationCanceledException)
        {
            // A newer change decides.
        }
    }

    // The stored language, or what the UI resolved to when none is stored yet.
    private AppLanguage CurrentLanguage() => AppLanguageSetting.Current(_settings);

    private async void OnSettingsTapped(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync(AppShell.SettingsRoute);

    private void OnInstallerChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _planCache = null;
            RenderPackStatus();
            RestartHeadStartIfKeyChanged();
        });

    private void RenderPackStatus()
    {
        string? text = LocalizedPackStatus.Compose(_installer.State, _installer.Progress);
        TextFlow.ForUi(PackStatusLabel, text);
        PackStatusLabel.IsVisible = text is not null;
    }

    private void OnPackStatusTapped(object? sender, TappedEventArgs e)
    {
        if (_installer.CanRetry)
        {
            _ = _installer.StartAsync(PackStartReason.User);
        }
    }

    // ---- The reading area (Slice 99): read-only text, the spoken-sentence highlight, Edit, the empty state. ----

    private const float HighlightAlpha = 0.16f;
    private (int Start, int End)? _highlight;
    private readonly SentenceHighlightGate _sentenceGate = new();
    private IReadOnlyList<ChunkSentenceSpan>? _sentenceSpans;
    private void OnReaderChunkChanged(object? sender, ReadChunk chunk) =>
        MainThread.BeginInvokeOnMainThread(() => ShowHighlight(chunk));

    // Only a read of the text now on screen highlights it.
    private void ShowHighlight(ReadChunk chunk)
    {
        string text = TextEditor.Text ?? string.Empty;
        if (_lastSpokenText != text || !chunk.MatchesSource(text))
        {
            _sentenceSpans = null;
            _sentenceGate.Reset(-1);
            return;
        }

        int start = Math.Clamp(chunk.SourceStart, 0, text.Length);
        _highlight = (start, Math.Clamp(chunk.SourceEnd, start, text.Length));
        _sentenceGate.Reset(chunk.Index);
        _sentenceSpans = SentenceSpans.Compute(chunk.Text, text, start);
        RenderReading();
        ScrollToHighlight(text.Length);
    }

    // Slice 190: a later sentence of the playing chunk starts. The chunk highlight narrows to one sentence only once the reader announces the chunk's first sentence; a reader that tracks none (the phone's own voice) never does, so its chunk highlight stays.
    private void OnSentenceChanged(object? sender, SentenceChange change) =>
        MainThread.BeginInvokeOnMainThread(() => ShowSentence(change));

    private void ShowSentence(SentenceChange change)
    {
        string text = TextEditor.Text ?? string.Empty;
        if (_lastSpokenText != text || !_sentenceGate.Accept(change)
            || _sentenceSpans is not { } spans || change.SentenceIndex >= spans.Count)
        {
            return;
        }

        ChunkSentenceSpan span = spans[change.SentenceIndex];
        _highlight = (span.Start, span.End);
        RenderReading();
        ScrollToHighlight(text.Length);
#if DEBUG
        ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"SENTENCE {change.ChunkIndex} {change.SentenceIndex}");
#endif
    }

    private void ClearHighlight()
    {
        _sentenceSpans = null;
        _sentenceGate.Reset(-1);
        if (_highlight is not null)
        {
            _highlight = null;
            RenderReading();
        }
    }

    // The label is one block, so the highlight's place is estimated from its share of the text.
    private void ScrollToHighlight(int textLength)
    {
        if (_highlight is not { } range || textLength == 0)
        {
            return;
        }

        double target = ReadingLabel.Height * range.Start / textLength - ReadingScroll.Height / 3;
        _ = ReadingScroll.ScrollToAsync(0, Math.Max(0, target), true);
    }

    private static Color BrandColor() => (Color)Microsoft.Maui.Controls.Application.Current!.Resources["Primary"];

    // Read text dimmed, the spoken sentence tinted, the unread text in full colour.
    private void RenderReading()
    {
        string text = TextEditor.Text ?? string.Empty;
        bool isDark = Microsoft.Maui.Controls.Application.Current!.RequestedTheme == AppTheme.Dark;
        Color unread = isDark ? Color.FromArgb("#E6E6EA") : Color.FromArgb("#1F2027");
        Color read = isDark ? Color.FromArgb("#73FFFFFF") : Color.FromArgb("#73000000");
        var formatted = new FormattedString();
        if (_highlight is { } range && range.End > range.Start)
        {
            formatted.Spans.Add(new Span { Text = text[..range.Start], TextColor = read });
            formatted.Spans.Add(new Span { Text = text[range.Start..range.End], TextColor = unread, BackgroundColor = BrandColor().WithAlpha(HighlightAlpha) });
            formatted.Spans.Add(new Span { Text = text[range.End..], TextColor = unread });
        }
        else
        {
            formatted.Spans.Add(new Span { Text = text, TextColor = unread });
        }

        ReadingLabel.FormattedText = formatted;
        ReadingLabel.FlowDirection = TextFlow.ContentDirection(text);
        RenderEmptyState(string.IsNullOrWhiteSpace(text));
    }

    // With no text: a dashed card with one friendly line, outlined buttons, and a dimmed player whose Play does nothing.
    private void RenderEmptyState(bool empty)
    {
        EmptyHint.IsVisible = empty;
        TextCard.StrokeDashArray = empty ? [4, 3] : null;
        RenderPlayerForState();
        Color outline = empty ? (Color)Microsoft.Maui.Controls.Application.Current!.Resources["Primary"] : Color.FromArgb("#1C000000");
        double width = empty ? 2 : 1;
        ScanButton.Stroke = new SolidColorBrush(outline);
        FileButton.Stroke = new SolidColorBrush(outline);
        ScanButton.StrokeThickness = width;
        FileButton.StrokeThickness = width;
    }

    // The one definition of "empty player": no text, and nothing playing. Both the text and the state
    // are read here, so every caller agrees.
    private void RenderPlayerForState() =>
        RenderEmptyPlayer(EmptyPlayer.Is(TextEditor.Text, _reader.State));

    // The empty player is dimmed: a grey Play circle, grey skip buttons, and nothing to tap.
    private void RenderEmptyPlayer(bool empty)
    {
        bool dimmed = empty || PlayerDimmedByWait;
        PlayerCard.Opacity = dimmed ? DimmedPlayerOpacity : 1;
        PlayerCard.InputTransparent = dimmed;
        _playButtonBackground ??= PlayButton.Background;
        PlayButton.Background = empty ? new SolidColorBrush(Color.FromArgb("#C9CBD6")) : _playButtonBackground;
        Color skipFill = empty ? Color.FromArgb("#0F000000") : Color.FromArgb("#1A5B57E8");
        SkipBackButton.BackgroundColor = skipFill;
        SkipForwardButton.BackgroundColor = skipFill;
        Brush? skipStroke = empty ? new SolidColorBrush(Color.FromArgb("#66000000")) : null;
        SetSkipStroke(SkipBackIcon, skipStroke);
        SetSkipStroke(SkipForwardIcon, skipStroke);
    }

    // A null brush gives the icon its themed style stroke back.
    private static void SetSkipStroke(Microsoft.Maui.Controls.Shapes.Path icon, Brush? stroke)
    {
        if (stroke is null)
        {
            icon.ClearValue(Microsoft.Maui.Controls.Shapes.Shape.StrokeProperty);
            return;
        }

        icon.Stroke = stroke;
    }

    private Brush? _playButtonBackground;

    // Long-press on the text offers Paste, which opens the review page with the clipboard text (Slice 101).
    private async void OnTextLongPressed(object? sender, EventArgs e)
    {
        if (_inputBusy || _retired)
        {
            return;
        }

        string? choice = await DisplayActionSheetAsync(null, AppResources.Common_Cancel, null, FlowDirection.LeftToRight, AppResources.Reader_PasteButton);
        if (choice == AppResources.Reader_PasteButton && !_retired)
        {
            await PasteFromClipboardAsync();
        }
    }

    private async Task PasteFromClipboardAsync()
    {
        SetInputBusy(true);
        string? text = null;
        try
        {
            text = await Clipboard.Default.GetTextAsync();
        }
        catch (Exception)
        {
            await AlertAsync(AppResources.Reader_PasteFailedTitle, AppResources.Reader_PasteFailedMessage);
        }
        finally
        {
            SetInputBusy(false);
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            await OpenReviewAsync(new ReviewRequest(text));
        }
    }

    // Edit opens the review page on the text (and, after Translate and read, its original).
    private async void OnEditTapped(object? sender, TappedEventArgs e)
    {
        if (_inputBusy || _retired)
        {
            return;
        }

        await OpenReviewAsync(new ReviewRequest(TextEditor.Text ?? string.Empty, _original));
    }

    private async Task OpenReviewAsync(ReviewRequest request)
    {
        if (_retired || _openingReview)
        {
            return;
        }

        _openingReview = true;
        try
        {
            if (_reader.State == PlaybackState.Playing)
            {
                // The text may change under the read: pause it. Play resumes only if the text is unchanged.
                _reader.Pause();
            }

            _review.Open(request);
            await Shell.Current.GoToAsync(AppShell.ReviewRoute);
        }
        finally
        {
            _openingReview = false;
        }
    }

    // ---- Head start (Slice 47): synthesize the opening sentences before Play is tapped. ----

    // Typing waits this long after the last keystroke; a paste, file or scan prepares at once.
    private static readonly TimeSpan HeadStartTypingDelay = TimeSpan.FromSeconds(1);

    private CancellationTokenSource? _headStartCts;
    private string? _headStartKey;
    private bool _settingEditorText;

    private void InitHeadStart()
    {
        _headStartKey = CurrentHeadStartKey();
        TextEditor.TextChanged += OnEditorTextChangedForHeadStart;
    }

    private void OnEditorTextChangedForHeadStart(object? sender, TextChangedEventArgs e)
    {
        if (!_settingEditorText)
        {
            StartHeadStart(HeadStartTypingDelay);
        }
    }

    // Everything the sentence cache key holds besides the text: a change to any of them makes
    // every cached sentence a miss, so the head start prepares again.
    private string CurrentHeadStartKey()
    {
        VoiceModelPaths? paths = _model.Paths;
        return string.Join('|', _settings.VoiceId, paths?.Kind, paths?.Language, paths?.RootDir, paths?.PackVersion, CurrentReadingLanguage());
    }

    private void RestartHeadStartIfKeyChanged()
    {
        if (CurrentHeadStartKey() != _headStartKey)
        {
            StartHeadStart(TimeSpan.Zero);
        }
    }

    private void StartHeadStart(TimeSpan delay)
    {
        CancelHeadStart();
        _headStartKey = CurrentHeadStartKey();
        var cts = new CancellationTokenSource();
        _headStartCts = cts;
        _ = PrepareHeadStartAsync(TextEditor.Text ?? string.Empty, delay, cts.Token);
    }

    private async Task PrepareHeadStartAsync(string text, TimeSpan delay, CancellationToken token)
    {
        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, token);
            }

            if (!string.IsNullOrWhiteSpace(text) && !_headStart.ReadInProgress)
            {
                await PrepareInReadingLanguageAsync(text, token);
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer edit, or Play was tapped.
        }
        catch (Exception)
        {
            // A head start is an optimization: failing to prepare never reaches the user.
        }
    }

    // Slice 90: prepared with the pack and voice the read will use, so the cached sentences hit at Play;
    // when the phone voice would read there is nothing to prepare.
    private Task PrepareInReadingLanguageAsync(string text, CancellationToken token) =>
        PrepareInLanguageAsync(text, ReadingLanguageFor(text), token, own: true);

    // Whether a preparation was started (false when the phone voice reads, so there is nothing to cache).
    // An own preparation counts in _ownPreparations only once it holds the reader's slot (after the plan), so an early
    // cancel never spares an abandoned early synthesis because an own one is still planning.
    private async Task<bool> PrepareInLanguageAsync(string text, string language, CancellationToken token, bool own = false)
    {
        VoicePlan plan = await PlanVoiceAsync(language, atPlay: false);
        token.ThrowIfCancellationRequested();
        if (plan.PhoneReads)
        {
            return false;
        }

        if (own)
        {
            _ownPreparations++;
        }

        try
        {
            return await PrepareWithPlanAsync(text, plan, token);
        }
        finally
        {
            if (own)
            {
                _ownPreparations--;
            }
        }
    }

    private async Task<bool> PrepareWithPlanAsync(string text, VoicePlan plan, CancellationToken token)
    {
        if (plan.Scope is null)
        {
            await Task.Run(() => _headStart.PrepareAsync(text), token);
            return true;
        }

        if (_headStart is not IReadScoped scoped)
        {
            return false;
        }

        await Task.Run(() => scoped.PrepareAsync(text, plan.Scope), token);
        return true;
    }

    private void CancelHeadStart()
    {
        _headStartCts?.Cancel();
        _headStartCts?.Dispose();
        _headStartCts = null;
        _headStart.CancelPrepare();
    }
}
