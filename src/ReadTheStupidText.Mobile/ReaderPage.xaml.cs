using System.Globalization;
using ReadTheStupidText.Application.Documents;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// The app's single home screen (design refresh, post-Slice 34 — see plan.md
/// Decision 43's second resync). Paste / Scan / File are <b>input actions</b>,
/// not separate modes: each fills the same <see cref="TextEditor"/>, and the one
/// voice row + transport card below applies to whatever text is in it. This
/// replaces the former three-tab Type/Camera/File navigation — Scan is now a
/// pushed page that hands its OCR result back via <see cref="PendingScanResult"/>
/// instead of reading it directly, and File no longer needs its own screen since
/// there is nothing left for it to show once the text lands in this box.
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
    private readonly PendingScanResult _pendingScan;
    private readonly IVoiceModelService _model;
    private readonly VoicePackInstaller _installer;
    private PlaybackRate _speed;
    private string? _lastSpokenText;

    // Per-read token for the play-button spinner (see SpeakWithLoadingAsync).
    private int _readSequence;
    private int _pendingReadId;
    private string? _pendingReadText;
    private int _returnedReadId;

    // How long the spinner may outlast a SpeakAsync that returned before playback
    // started (system TTS returns as soon as the utterance is queued).
    private static readonly TimeSpan FirstAudioGrace = TimeSpan.FromSeconds(8);

    public ReaderPage(ISpeechReader reader, ISettingsStore settings, IDocumentTextExtractor documentExtractor, PendingScanResult pendingScan, IVoiceModelService model, VoicePackInstaller installer, IPlatformServices platform)
    {
        InitializeComponent();
        ApplyScanAvailability(platform.SupportsTextScan);

        _reader = reader;
        _settings = settings;
        _documentExtractor = documentExtractor;
        _pendingScan = pendingScan;
        _model = model;
        _installer = installer;
        _speed = _settings.Speed;
        UpdatePresetHighlight();
        UpdateVoiceLabel();
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
        RenderPackStatus();
        RestorePendingReadSpinner();

        // Consumed and cleared in one step: see PendingScanResult's remarks for
        // why this replaces a Shell query-parameter hand-off.
        if (_pendingScan.TakeAndClear() is { } scannedText)
        {
            TextEditor.Text = scannedText;
        }
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
        string? text = await Clipboard.Default.GetTextAsync();
        if (!string.IsNullOrWhiteSpace(text))
        {
            TextEditor.Text = text;
        }
    }

    // A platform with no OCR engine hides Scan; File takes its place so no gap is left.
    // ScanPage still catches OcrUnavailableException as a safety net.
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

    private async void OnScanTapped(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync(AppShell.ScanRoute);

    private async void OnFileTapped(object? sender, TappedEventArgs e)
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
            TextEditor.Text = await _documentExtractor.ExtractTextAsync(file.FullPath);
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

        VoiceNameLabel.Text = SupertonicVoiceTable.Voices
            .FirstOrDefault(v => v.Id == _settings.VoiceId, SupertonicVoiceTable.Default)
            .DisplayName;
    }

    private void OnInstallerChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RenderPackStatus();
            UpdateVoiceLabel();
        });

    private void RenderPackStatus()
    {
        string? text = PackStatusText.Compose(_installer.State, _installer.Progress);
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
}
