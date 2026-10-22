using System.Globalization;
using ReadTheStupidText.Application.Documents;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Mobile.Resources.Strings;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// The app's single home screen (design refresh, post-Slice 34 — see plan.md
/// Decision 43's second resync). Paste / Scan / File are <b>input actions</b>,
/// not separate modes: each fills the same <see cref="TextEditor"/>, and the one
/// voice row + transport card below applies to whatever text is in it. This
/// replaces the former three-tab Type/Camera/File navigation — Scan opens the system
/// document scanner (Slice 45) and OCRs its pages straight into this box, and File no
/// longer needs its own screen since there is nothing left for it to show once the
/// text lands in this box.
/// </summary>
public partial class ReaderPage : ContentPage, IScanPackUi
{
    private static readonly FilePickerFileType SupportedFileTypes = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        { DevicePlatform.Android, ["text/plain", "application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"] },
        { DevicePlatform.iOS, ["public.plain-text", "com.adobe.pdf", "org.openxmlformats.wordprocessingml.document"] },
    });

    private readonly ISpeechReader _reader;
    private readonly ISettingsStore _settings;
    private readonly IDocumentTextExtractor _documentExtractor;
    private readonly IImageTextExtractor _imageExtractor;
    private readonly IOcrLineExtractor? _lineExtractor; // keeps which engine read a page (Slice 87)
    private readonly OcrPackInstaller? _ocrPacks;
    private readonly IDocumentScanner? _scanner;
    private readonly IVoiceModelService _model;
    private readonly VoicePackInstaller _installer;
    private readonly IPlatformServices _platform;
    private readonly IHeadStart _headStart;
    private readonly EditorDraft _draft;
    private PlaybackRate _speed;
    private string? _lastSpokenText;

    // True while a Paste/Scan/File action is in flight; further input taps are ignored.
    private bool _inputBusy;
    private bool _scannerPrepared;

    // Set when a language change replaces this page: its in-flight scan or file read must not
    // touch the editor or show alerts on a page nobody sees any more.
    private bool _retired;

    // "Reading page i of N" while OCR runs; shown in the header status line, which
    // RenderPackStatus restores to the pack status once it is cleared.
    private string? _scanProgressText;

    // Set while a page-language OCR pack downloads; only the Cancel button runs it (Slice 87).
    private Action? _cancelOcrDownload;

    // Per-read token for the play-button spinner (see SpeakWithLoadingAsync).
    private int _readSequence;
    private int _pendingReadId;
    private string? _pendingReadText;
    private int _returnedReadId;

    // How long the spinner may outlast a SpeakAsync that returned before playback
    // started (system TTS returns as soon as the utterance is queued).
    private static readonly TimeSpan FirstAudioGrace = TimeSpan.FromSeconds(8);

    public ReaderPage(ISpeechReader reader, ISettingsStore settings, IDocumentTextExtractor documentExtractor, IImageTextExtractor imageExtractor, IVoiceModelService model, VoicePackInstaller installer, IPlatformServices platform, EditorDraft draft, IHeadStart headStart, IDocumentScanner? scanner = null, ILanguageDetector? detector = null, ITranslator? translator = null, IOcrLineExtractor? lineExtractor = null, OcrPackInstaller? ocrPacks = null)
    {
        InitializeComponent();
        FlowDirection = FlowDirection.LeftToRight;
        ApplyScanAvailability(platform.SupportsTextScan);

        _reader = reader;
        _settings = settings;
        _documentExtractor = documentExtractor;
        _imageExtractor = imageExtractor;
        _lineExtractor = lineExtractor;
        _ocrPacks = ocrPacks;
        _scanner = scanner;
        _headStart = headStart;
        _model = model;
        _installer = installer;
        _platform = platform;
        _draft = draft;
        TextEditor.Text = draft.Take();
        TextEditor.FlowDirection = TextFlow.ContentDirection(TextEditor.Text);
        _speed = _settings.Speed;
        UpdatePresetHighlight();
        UpdateVoiceLabel();
        TextFlow.ForContent(LanguageNameLabel, LanguageChoices.NativeName(CurrentLanguage()));
        ApplySourcePickerDirection();
        InitTranslation(detector, translator);
        InitHeadStart();
    }

    // The Source picker sits at the right of its row in every language: its direction is the UI
    // language's, so End/Start is chosen to keep that side.
    private void ApplySourcePickerDirection()
    {
        bool rightToLeft = TextFlow.UiDirection() == FlowDirection.RightToLeft;
        SourcePicker.FlowDirection = TextFlow.UiDirection();
        SourcePicker.HorizontalTextAlignment = rightToLeft ? TextAlignment.Start : TextAlignment.End;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        TextEditor.FlowDirection = TextFlow.ContentDirection(TextEditor.Text); // an empty editor follows the UI language as it is now
        _reader.StateChanged += OnReaderStateChanged;
        _reader.ProgressChanged += OnReaderProgressChanged;
        _reader.TimingChanged += OnReaderTimingChanged;
        _installer.Changed += OnInstallerChanged;
        _model.ReadyChanged += OnInstallerChanged;

        // Re-read on every appearance: the voice picker may have changed the
        // persisted voice while this page was off-stack.
        UpdateVoiceLabel();
        RestartHeadStartIfKeyChanged();
        RenderPackStatus();
        RestorePendingReadSpinner();
        PrepareScannerOnce();
        LoadVoiceAvailability();
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
        _reader.StateChanged -= OnReaderStateChanged;
        _reader.ProgressChanged -= OnReaderProgressChanged;
        _reader.TimingChanged -= OnReaderTimingChanged;
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
        CancelTranslation();
        _reader.Stop();
        _draft.Store(TextEditor.Text);
        DetachHandlers();
    }

    private Task AlertAsync(string title, string message) =>
        _retired ? Task.CompletedTask : DisplayAlertAsync(title, message, AppResources.Common_Ok, FlowDirection.LeftToRight);

    private async void OnPasteTapped(object? sender, TappedEventArgs e)
    {
        if (_inputBusy)
        {
            return;
        }

        SetInputBusy(true);
        try
        {
            string? text = await Clipboard.Default.GetTextAsync();
            if (!_retired && !string.IsNullOrWhiteSpace(text))
            {
                ReplaceEditorText(text);
            }
        }
        catch (Exception)
        {
            await AlertAsync(AppResources.Reader_PasteFailedTitle, AppResources.Reader_PasteFailedMessage);
        }
        finally
        {
            SetInputBusy(false);
        }
    }

    // Dims and disables Paste/Scan/File while one of them runs, so a second tap can't start a
    // second scan or overwrite the editor mid-OCR. The editor is read-only meanwhile so typed edits are not lost.
    private void SetInputBusy(bool busy)
    {
        _inputBusy = busy;
        ActionRow.IsEnabled = !busy;
        TextEditor.IsReadOnly = busy;
        ActionRow.Opacity = busy ? 0.5 : 1;
    }

    private void SetScanProgress(string? text)
    {
        if (_retired)
        {
            return;
        }

        _scanProgressText = text;
        RenderPackStatus();
    }

    // A platform with no OCR engine hides Scan; File takes its place so no gap is left.
    // ScanIntoEditorAsync still catches OcrUnavailableException as a safety net.
    private void ApplyScanAvailability(bool supportsTextScan)
    {
        if (supportsTextScan)
        {
            return;
        }

        ScanButton.IsVisible = false;
        Grid.SetColumn(FileButton, 1);
        Grid.SetColumnSpan(FileButton, 2);
    }

    // Scan runs the system document scanner and puts the text straight into the editor, with
    // no result screen and no Shell hand-off (the replay bug PendingScanResult once worked
    // around). Without Play services it falls back to one photo.
    private async void OnScanTapped(object? sender, TappedEventArgs e)
    {
        if (_inputBusy)
        {
            return;
        }

        SetInputBusy(true);
        try
        {
            IReadOnlyList<string> pages = await ScanPagesAsync();
            if (pages.Count > 0 && _retired)
            {
                DeleteImages(pages);
            }
            else if (pages.Count > 0)
            {
                await ScanIntoEditorAsync(pages);
            }
        }
        catch (Exception)
        {
            await AlertAsync(AppResources.Reader_ScanFailedTitle, AppResources.Reader_ScanFailedMessage);
        }
        finally
        {
            SetInputBusy(false);
        }
    }

    private async Task<IReadOnlyList<string>> ScanPagesAsync()
    {
        if (_scanner is null)
        {
            return await ScanUnavailableFallbackAsync();
        }

        try
        {
            return await _scanner.ScanAsync();
        }
        catch (DocumentScannerStartFailedException)
        {
            await AlertAsync(AppResources.Reader_ScannerStartFailedTitle, AppResources.Reader_ScannerStartFailedMessage);
            return [];
        }
        catch (DocumentScannerPreparingException)
        {
            await AlertAsync(AppResources.Reader_ScannerPreparingTitle, AppResources.Reader_ScannerPreparingMessage);
            return [];
        }
        catch (DocumentScannerUnavailableException)
        {
            return await ScanUnavailableFallbackAsync();
        }
    }

    private async Task<IReadOnlyList<string>> ScanUnavailableFallbackAsync()
    {
        await AlertAsync(AppResources.Reader_ScannerUnavailableTitle, AppResources.Reader_ScannerUnavailableMessage);
        return await CapturePhotoAsync();
    }

    private async Task<IReadOnlyList<string>> CapturePhotoAsync()
    {
        try
        {
            if (MediaPicker.Default.IsCaptureSupported)
            {
                FileResult? photo = await MediaPicker.Default.CapturePhotoAsync();
                return photo is null ? [] : [photo.FullPath];
            }
        }
        catch (PermissionException)
        {
            await AlertAsync(AppResources.Reader_CameraAccessTitle, AppResources.Reader_CameraAccessMessage);
            return [];
        }
        catch (FeatureNotSupportedException)
        {
        }

        await AlertAsync(AppResources.Reader_NoCameraTitle, AppResources.Reader_NoCameraMessage);
        return [];
    }

    // Pages are read one after another, in order. A page that fails or is empty never discards
    // the others: the pages that worked replace the editor text.
    private async Task ScanIntoEditorAsync(IReadOnlyList<string> pages)
    {
        BusyIndicator.IsRunning = true;
        BusyIndicator.IsVisible = true;
        try
        {
            IReadOnlyList<ScanPageRead> first = await ExtractPagesAsync(pages, AppLanguages.Code(CurrentLanguage()));
            IReadOnlyList<ScanPageRead>? reads = _ocrPacks is null ? first : await new ScanPackFlow(_ocrPacks, this).ResolveAsync(pages, first, ExtractPagesAsync);
            if (reads is not null && !_retired)
            {
                await ApplyScanResultAsync(reads.Select(r => r.Text).ToList());
            }
        }
        catch (OcrUnavailableException)
        {
            await AlertAsync(AppResources.Reader_ScanUnavailableTitle, AppResources.Reader_ScanUnavailableMessage);
        }
        finally
        {
            SetScanProgress(null);
            // There is no retry UI, so the page images go whether or not OCR succeeded.
            DeleteImages(pages);
            BusyIndicator.IsRunning = false;
            BusyIndicator.IsVisible = false;
        }
    }

    private async Task ApplyScanResultAsync(IReadOnlyList<string?> texts)
    {
        IReadOnlyList<int> failed = ScannedText.FailedPageNumbers(texts);
        if (failed.Count == texts.Count)
        {
            await AlertAsync(AppResources.Reader_ScanUnreadableTitle, AppResources.Reader_ScanUnreadableMessage);
            return;
        }

        string scanned = ScannedText.JoinPages(texts);
        if (scanned.Length == 0)
        {
            await AlertAsync(AppResources.Reader_NoTextFoundTitle, AppResources.Reader_NoTextFoundMessage);
            return;
        }

        ReplaceEditorText(scanned);
        if (failed.Count > 0)
        {
            string template = failed.Count == 1 ? AppResources.Reader_PagesSkippedOneMessage : AppResources.Reader_PagesSkippedManyMessage;
            await AlertAsync(AppResources.Reader_PagesSkippedTitle, AppResources.Format(template, ScannedText.FormatPageNumbers(failed)));
        }
    }

    // A null text marks a page whose OCR threw; OcrUnavailableException is not per-page and
    // aborts the pass.
    private async Task<IReadOnlyList<ScanPageRead>> ExtractPagesAsync(IReadOnlyList<string> pages, string language)
    {
        var reads = new List<ScanPageRead>();
        for (int i = 0; i < pages.Count && !_retired; i++)
        {
            SetScanProgress(pages.Count > 1 ? AppResources.Format(AppResources.Reader_ScanProgressPage, i + 1, pages.Count) : AppResources.Reader_ScanProgressSingle);
            reads.Add(await ExtractPageAsync(pages[i], language));
        }

        return reads;
    }

    private async Task<ScanPageRead> ExtractPageAsync(string page, string language)
    {
        try
        {
            if (_lineExtractor is null)
            {
                return new ScanPageRead(await _imageExtractor.ExtractTextAsync(page), OcrSource.MlKit, ScriptShare.Empty);
            }

            ImageText image = await _lineExtractor.ExtractLinesAsync(page, language, CancellationToken.None);
            return new ScanPageRead(ScanReadingText.From(image), image.Source, ScriptDetector.Analyze(image.Lines.Select(l => l.Text)));
        }
        catch (OcrUnavailableException)
        {
            throw;
        }
        catch (Exception)
        {
            return new ScanPageRead(null, OcrSource.MlKit, ScriptShare.Empty);
        }
    }

    // Slice 87: the page-language pack flow (ScanPackFlow) talks to the screen through IScanPackUi.
    bool IScanPackUi.IsRetired => _retired;

    async Task<int> IScanPackUi.PickLanguageAsync(IReadOnlyList<string> names)
    {
        string? picked = await DisplayActionSheetAsync(AppResources.Reader_PageLanguageTitle, AppResources.Common_Cancel, null, FlowDirection.LeftToRight, names.ToArray());

        return picked is null ? -1 : names.ToList().IndexOf(picked);
    }

    // Ticks arrive from the download thread; one that lands after the flow ended (Cancel hidden) is dropped.
    void IScanPackUi.ShowProgress(string? text) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_cancelOcrDownload is not null)
            {
                SetScanProgress(text);
            }
        });

    void IScanPackUi.SetCancel(Action? cancel)
    {
        _cancelOcrDownload = cancel;
        CancelOcrButton.IsVisible = cancel is not null;
    }

    Task<bool> IScanPackUi.AskRetryAsync(string title, string message) =>
        _retired ? Task.FromResult(false) : DisplayAlertAsync(title, message, AppResources.Language_Retry, AppResources.Common_Cancel, FlowDirection.LeftToRight);

    Task IScanPackUi.NotifyAsync(string title, string message) => AlertAsync(title, message);

    private void OnCancelOcrDownloadClicked(object? sender, EventArgs e) => _cancelOcrDownload?.Invoke();

    private static void DeleteImages(IReadOnlyList<string> pages)
    {
        foreach (string page in pages)
        {
            ScannedImageFiles.TryDelete(page);
        }
    }

    private async void OnFileTapped(object? sender, TappedEventArgs e)
    {
        if (_inputBusy)
        {
            return;
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

        BusyIndicator.IsRunning = true;
        BusyIndicator.IsVisible = true;
        try
        {
            string extracted = await _documentExtractor.ExtractTextAsync(file.FullPath);
            if (!_retired)
            {
                ReplaceEditorText(extracted);
            }
        }
        catch (DocumentTooLargeException ex)
        {
            await AlertAsync(AppResources.Reader_FileTooLargeTitle, AppResources.Format(AppResources.Reader_FileTooLargeMessage, ex.Actual, ex.Limit));
        }
        catch (Exception)
        {
            await AlertAsync(AppResources.Reader_FileUnreadableTitle, AppResources.Reader_FileUnreadableMessage);
        }
        finally
        {
            BusyIndicator.IsRunning = false;
            BusyIndicator.IsVisible = false;
        }
    }

    private async void OnVoiceRowTapped(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync(AppShell.VoicePickerRoute);

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

        CancelHeadStart();
        _reader.Stop();
        CancelTranslationRead();
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
        try
        {
            // The spinner covers the translation too; the reader gets the translation, while
            // _lastSpokenText and _pendingReadText stay the source text so Resume still matches.
            SpokenText? spoken = await TextToSpeakAsync(text, id);
            if (spoken is null || !IsCurrentRead(id))
            {
                return;
            }

            VoicePlan plan = await PlanVoiceAsync(ReadingLanguageFor(text, spoken.Translated), atPlay: true);
            if (!IsCurrentRead(id) || !await AllowReadAsync(id, plan))
            {
                return;
            }

            await SpeakPlannedAsync(spoken.Text, plan);
        }
        catch (Exception)
        {
            bool wasCurrent = id == _pendingReadId;
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
    private string ReadingLanguageFor(string text, bool translated)
    {
        var key = new LanguageKey(text, translated, TargetCode(), _settings.TranslateSourceLanguage, _detectedSource);
        if (_languageMemo is { } memo && memo.Key == key)
        {
            return memo.Language;
        }

        string language = VoiceRouting.ReadingLanguage(translated, key.Target, key.Choice, key.Detected, text);
        _languageMemo = (key, language);
        return language;
    }

    private sealed record LanguageKey(string Text, bool Translated, string Target, string? Choice, string? Detected);

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
            UpdateVoiceLabel();
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

    private void SetPlayLoading(bool loading)
    {
        PlayLoadingIndicator.IsRunning = loading;
        PlayLoadingIndicator.IsVisible = loading;
        PlayPauseGlyph.IsVisible = !loading;
    }

    private async void OnSkipBackTapped(object? sender, TappedEventArgs e) => await _reader.SkipBackward();

    private async void OnSkipForwardTapped(object? sender, TappedEventArgs e) => await _reader.SkipForward();

    private void OnSpeedPresetClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { ClassId: string classId } || !double.TryParse(classId, CultureInfo.InvariantCulture, out double value))
        {
            return;
        }

        _speed = new PlaybackRate(value);
        _settings.Speed = _speed;
        _reader.SetSpeed(_speed);
        UpdatePresetHighlight();
    }

    private void OnReaderStateChanged(object? sender, PlaybackState state)
    {
        MainThread.BeginInvokeOnMainThread(() => ApplyPlaybackState(state));
    }

    private void ApplyPlaybackState(PlaybackState state)
    {
        PlayPauseGlyph.Text = state == PlaybackState.Playing ? "⏸" : "▶";
        bool idleAfterReturn = state == PlaybackState.Idle
            && _pendingReadId != 0 && _pendingReadId == _returnedReadId;
        if (state == PlaybackState.Playing || idleAfterReturn)
        {
            ClearPendingRead();
        }
    }

    private void OnReaderProgressChanged(object? sender, double progress) =>
        MainThread.BeginInvokeOnMainThread(() => ReadProgressBar.Progress = progress);

    private void OnReaderTimingChanged(object? sender, ReadTiming timing) =>
        MainThread.BeginInvokeOnMainThread(() => TimerLabel.Text = ReadTimingFormatter.Format(timing));

    private void UpdatePresetHighlight()
    {
        bool isDark = Microsoft.Maui.Controls.Application.Current!.RequestedTheme == AppTheme.Dark;
        var unselectedBackground = (Color)Microsoft.Maui.Controls.Application.Current!.Resources[isDark ? "ChipBackgroundDark" : "ChipBackgroundLight"];
        var unselectedText = (Color)Microsoft.Maui.Controls.Application.Current!.Resources[isDark ? "TextSecondaryDark" : "TextSecondaryLight"];

        foreach (Button button in SpeedPresetRow.Children.OfType<Button>())
        {
            bool selected = button.ClassId is string classId
                && double.TryParse(classId, CultureInfo.InvariantCulture, out double value)
                && new PlaybackRate(value).Value == _speed.Value;

            button.BackgroundColor = selected ? Color.FromArgb("#5B57E8") : unselectedBackground;
            button.TextColor = selected ? Colors.White : unselectedText;
        }
    }

    // The label names the voice that will read next (Decision 61, Slice 90): the app language's voice at
    // once, then, once the reading language is known, the voice for that language. It only reads; the stored
    // choice is never changed here.
    private void UpdateVoiceLabel()
    {
        VoiceNameLabel.Text = VoiceNameFor(_model.Paths, _settings.VoiceId == SystemVoice.Id || !_model.IsReady);
        _ = RefreshVoiceLabelAsync();
    }

    private string VoiceNameFor(VoiceModelPaths? paths, bool phoneReads) =>
        phoneReads || paths is null ? VoicePickerPage.PhoneVoiceName : NeuralVoiceMemory.Resolve(paths, _settings.VoiceId).DisplayName;

    private int _labelVersion;

    private async Task RefreshVoiceLabelAsync()
    {
        int version = ++_labelVersion;
        try
        {
            VoicePlan plan = await PlanVoiceAsync(CurrentReadingLanguage(), atPlay: false);
            if (version == _labelVersion && !_retired)
            {
                VoiceNameLabel.Text = VoiceNameFor(plan.Pack, plan.PhoneReads);
            }
        }
        catch (Exception)
        {
            // The label keeps the app language's voice.
        }
    }

    // What the next Play would read in: the text is read untranslated unless translation is on.
    private string CurrentReadingLanguage() =>
        ReadingLanguageFor(TextEditor.Text ?? string.Empty, TranslationActive());

    // The picker, the detection or the text changed: the head start and the label follow the new language.
    private void OnReadingLanguageMayHaveChanged()
    {
        RestartHeadStartIfKeyChanged();
        _ = RefreshVoiceLabelAsync();
    }

    private static readonly TimeSpan ReadingLanguageDebounce = TimeSpan.FromMilliseconds(400);

    private CancellationTokenSource? _languageRefreshCts;

    // Every text change: the label and head-start key follow the new text's language once typing pauses.
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

    private async void OnLanguageRowTapped(object? sender, TappedEventArgs e) =>
        await LanguageChooser.ChooseAsync(this, _settings);

    private void OnInstallerChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _planCache = null;
            RenderPackStatus();
            UpdateVoiceLabel();
            RestartHeadStartIfKeyChanged();
        });

    private void RenderPackStatus()
    {
        string? text = _scanProgressText ?? LocalizedPackStatus.Compose(_installer.State, _installer.Progress);
        TextFlow.ForUi(PackStatusLabel, text);
        PackStatusLabel.IsVisible = text is not null;
    }

    private void OnPackStatusTapped(object? sender, TappedEventArgs e)
    {
        if (_installer.CanRetry)
        {
            _ = _installer.StartAsync();
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

            if (!string.IsNullOrWhiteSpace(text) && !_headStart.ReadInProgress && !TranslationActive())
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
    private async Task PrepareInReadingLanguageAsync(string text, CancellationToken token)
    {
        VoicePlan plan = await PlanVoiceAsync(ReadingLanguageFor(text, false), atPlay: false);
        token.ThrowIfCancellationRequested();
        if (plan.Scope is null)
        {
            await Task.Run(() => _headStart.PrepareAsync(text), token);
        }
        else if (_headStart is IReadScoped scoped)
        {
            await Task.Run(() => scoped.PrepareAsync(text, plan.Scope), token);
        }
    }

    // With translate on, the read speaks the translated text, so preparing the source would cache
    // the wrong sentences: skip the head start until the toggle goes off.
    private bool TranslationActive() => _translator is not null && _settings.TranslateEnabled;

    private void CancelHeadStart()
    {
        _headStartCts?.Cancel();
        _headStartCts?.Dispose();
        _headStartCts = null;
        _headStart.CancelPrepare();
    }
}
