using System.Globalization;
using ReadTheStupidText.Application.Documents;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;

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
public partial class ReaderPage : ContentPage
{
    private static readonly FilePickerFileType SupportedFileTypes = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        { DevicePlatform.Android, ["text/plain", "application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"] },
        { DevicePlatform.iOS, ["public.plain-text", "com.adobe.pdf", "org.openxmlformats.wordprocessingml.document"] },
    });

    // A blank line between scanned pages, and between existing editor text and a new scan.
    private const string PageSeparator = "\n\n";

    private readonly ISpeechReader _reader;
    private readonly ISettingsStore _settings;
    private readonly IDocumentTextExtractor _documentExtractor;
    private readonly IImageTextExtractor _imageExtractor;
    private readonly IDocumentScanner? _scanner;
    private readonly IVoiceModelService _model;
    private readonly VoicePackInstaller _installer;
    private readonly IHeadStart _headStart;
    private PlaybackRate _speed;
    private string? _lastSpokenText;

    // True while a Paste/Scan/File action is in flight; further input taps are ignored.
    private bool _inputBusy;
    private bool _scannerPrepared;

    // "Reading page i of N" while OCR runs; shown in the header status line, which
    // RenderPackStatus restores to the pack status once it is cleared.
    private string? _scanProgressText;

    // Per-read token for the play-button spinner (see SpeakWithLoadingAsync).
    private int _readSequence;
    private int _pendingReadId;
    private string? _pendingReadText;
    private int _returnedReadId;

    // How long the spinner may outlast a SpeakAsync that returned before playback
    // started (system TTS returns as soon as the utterance is queued).
    private static readonly TimeSpan FirstAudioGrace = TimeSpan.FromSeconds(8);

    public ReaderPage(ISpeechReader reader, ISettingsStore settings, IDocumentTextExtractor documentExtractor, IImageTextExtractor imageExtractor, IVoiceModelService model, VoicePackInstaller installer, IPlatformServices platform, IHeadStart headStart, IDocumentScanner? scanner = null)
    {
        InitializeComponent();
        ApplyScanAvailability(platform.SupportsTextScan);

        _reader = reader;
        _settings = settings;
        _documentExtractor = documentExtractor;
        _imageExtractor = imageExtractor;
        _scanner = scanner;
        _headStart = headStart;
        _model = model;
        _installer = installer;
        _speed = _settings.Speed;
        UpdatePresetHighlight();
        UpdateVoiceLabel();
        InitHeadStart();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
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
        _reader.StateChanged -= OnReaderStateChanged;
        _reader.ProgressChanged -= OnReaderProgressChanged;
        _reader.TimingChanged -= OnReaderTimingChanged;
        _installer.Changed -= OnInstallerChanged;
        _model.ReadyChanged -= OnInstallerChanged;

        // Hide the spinner but keep the pending-read state: the read may still be
        // generating, and OnAppearing restores the spinner if so.
        SetPlayLoading(false);
    }

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
            if (!string.IsNullOrWhiteSpace(text))
            {
                SetEditorText(text);
            }
        }
        catch (Exception)
        {
            await DisplayAlertAsync("Couldn't paste", "The clipboard couldn't be read. Try copying the text again.", "OK");
        }
        finally
        {
            SetInputBusy(false);
        }
    }

    // Dims and disables Paste/Scan/File while one of them runs, so a second tap can't start a
    // second scan or overwrite the editor mid-OCR.
    private void SetInputBusy(bool busy)
    {
        _inputBusy = busy;
        ActionRow.IsEnabled = !busy;
        ActionRow.Opacity = busy ? 0.5 : 1;
    }

    private void SetScanProgress(string? text)
    {
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
            if (pages.Count > 0)
            {
                await ScanIntoEditorAsync(pages);
            }
        }
        catch (Exception)
        {
            await DisplayAlertAsync("Couldn't scan", "Something went wrong while scanning. Please try again.", "OK");
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
            await DisplayAlertAsync("Couldn't start the scanner", "Couldn't start the scanner — try again.", "OK");
            return [];
        }
        catch (DocumentScannerPreparingException)
        {
            await DisplayAlertAsync("Scanner getting ready", "Google Play services is still preparing the document scanner. Try again in a moment.", "OK");
            return [];
        }
        catch (DocumentScannerUnavailableException)
        {
            return await ScanUnavailableFallbackAsync();
        }
    }

    private async Task<IReadOnlyList<string>> ScanUnavailableFallbackAsync()
    {
        await DisplayAlertAsync("Scanner unavailable", "The document scanner needs Google Play services. Take a single photo instead.", "OK");
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
            await DisplayAlertAsync("Camera access needed", "Grant camera access in Settings to photograph text.", "OK");
            return [];
        }
        catch (FeatureNotSupportedException)
        {
        }

        await DisplayAlertAsync("No camera", "Camera access is needed to photograph text, and this device has none available.", "OK");
        return [];
    }

    // Pages are read one after another, in order. A page that fails or is empty never discards
    // the others: the pages that worked are appended below any text already there.
    private async Task ScanIntoEditorAsync(IReadOnlyList<string> pages)
    {
        BusyIndicator.IsRunning = true;
        BusyIndicator.IsVisible = true;
        try
        {
            IReadOnlyList<string?> texts = await ExtractPagesAsync(pages);
            await ApplyScanResultAsync(texts);
        }
        catch (OcrUnavailableException)
        {
            await DisplayAlertAsync("Scan unavailable", "Reading text from photos isn't available on this device yet.", "OK");
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
            await DisplayAlertAsync("Couldn't read that scan", "Try scanning the text again with better lighting or focus.", "OK");
            return;
        }

        string updated = ScannedText.AppendPages(TextEditor.Text, texts);
        if (updated == (TextEditor.Text ?? string.Empty))
        {
            await DisplayAlertAsync("No text found", "Try scanning again with better lighting or focus.", "OK");
            return;
        }

        SetEditorText(updated);
        if (failed.Count > 0)
        {
            string noun = failed.Count == 1 ? "page" : "pages";
            await DisplayAlertAsync("Some pages skipped", $"Couldn't read {noun} {ScannedText.FormatPageNumbers(failed)} — the rest was added.", "OK");
        }
    }

    // A null entry marks a page whose OCR threw; OcrUnavailableException is not per-page and
    // aborts the pass.
    private async Task<IReadOnlyList<string?>> ExtractPagesAsync(IReadOnlyList<string> pages)
    {
        var texts = new List<string?>();
        for (int i = 0; i < pages.Count; i++)
        {
            SetScanProgress(pages.Count > 1 ? $"Reading page {i + 1} of {pages.Count}" : "Reading page");
            texts.Add(await ExtractPageTextAsync(pages[i]));
        }

        return texts;
    }

    private async Task<string?> ExtractPageTextAsync(string page)
    {
        try
        {
            return await _imageExtractor.ExtractTextAsync(page);
        }
        catch (OcrUnavailableException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

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
                PickerTitle = "Choose a .txt, .pdf or .docx file",
                FileTypes = SupportedFileTypes,
            });
        }
        catch (PermissionException)
        {
            await DisplayAlertAsync("Permission needed", "Grant file access to read a document.", "OK");
            return;
        }

        if (file is null)
        {
            return; // user cancelled the picker
        }

        BusyIndicator.IsRunning = true;
        BusyIndicator.IsVisible = true;
        try
        {
            SetEditorText(await _documentExtractor.ExtractTextAsync(file.FullPath));
        }
        catch (DocumentTooLargeException ex)
        {
            await DisplayAlertAsync("File too large", $"That file has {ex.Actual} pages; the limit is {ex.Limit}.", "OK");
        }
        catch (Exception)
        {
            await DisplayAlertAsync("Couldn't read that file", "The file may be corrupted or in an unsupported format.", "OK");
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
            await _reader.SpeakAsync(text);
        }
        catch (Exception)
        {
            bool wasCurrent = id == _pendingReadId;
            ClearPendingReadIfCurrent(id);
            if (wasCurrent)
            {
                await DisplayAlertAsync("Couldn't read that text", "Something went wrong while reading.", "OK");
            }

            return;
        }

        await WaitForFirstAudioAsync(id);
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

    // The phone voice reads whenever it is chosen or the neural pack isn't ready yet,
    // so the label names what will actually speak (Decision 61).
    private void UpdateVoiceLabel()
    {
        if (_settings.VoiceId == SystemVoice.Id || !_model.IsReady)
        {
            VoiceNameLabel.Text = VoicePickerPage.PhoneVoiceName;
            return;
        }

        VoiceNameLabel.Text = (_model.Paths is { } paths ? NeuralVoiceMemory.Resolve(paths, _settings.VoiceId) : SupertonicVoiceTable.Default).DisplayName;
    }

    private void OnInstallerChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RenderPackStatus();
            UpdateVoiceLabel();
            RestartHeadStartIfKeyChanged();
        });

    private void RenderPackStatus()
    {
        string? text = _scanProgressText ?? PackStatusText.Compose(_installer.State, _installer.Progress);
        PackStatusLabel.Text = text;
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
        TextEditor.TextChanged += OnEditorTextChanged;
    }

    // Paste, file and scan put text in the editor in one go: prepare it immediately, and keep the
    // TextChanged that the assignment raises from also scheduling a debounced prepare.
    private void SetEditorText(string text)
    {
        _settingEditorText = true;
        try
        {
            TextEditor.Text = text;
        }
        finally
        {
            _settingEditorText = false;
        }

        StartHeadStart(TimeSpan.Zero);
    }

    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
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
        return string.Join('|', _settings.VoiceId, paths?.Kind, paths?.Language, paths?.RootDir, paths?.PackVersion);
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
                await Task.Run(() => _headStart.PrepareAsync(text), token);
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

    private void CancelHeadStart()
    {
        _headStartCts?.Cancel();
        _headStartCts?.Dispose();
        _headStartCts = null;
        _headStart.CancelPrepare();
    }
}
