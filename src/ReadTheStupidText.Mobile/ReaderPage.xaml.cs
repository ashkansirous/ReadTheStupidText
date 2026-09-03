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
    });

    private readonly ISpeechReader _reader;
    private readonly ISettingsStore _settings;
    private readonly IDocumentTextExtractor _documentExtractor;
    private readonly PendingScanResult _pendingScan;
    private PlaybackRate _speed;
    private string? _lastSpokenText;

    public ReaderPage(ISpeechReader reader, ISettingsStore settings, IDocumentTextExtractor documentExtractor, PendingScanResult pendingScan)
    {
        InitializeComponent();

        _reader = reader;
        _settings = settings;
        _documentExtractor = documentExtractor;
        _pendingScan = pendingScan;
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

        // Re-read on every appearance: the voice picker may have changed the
        // persisted voice while this page was off-stack.
        UpdateVoiceLabel();

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
    }

    private async void OnPasteTapped(object? sender, TappedEventArgs e)
    {
        string? text = await Clipboard.Default.GetTextAsync();
        if (!string.IsNullOrWhiteSpace(text))
        {
            TextEditor.Text = text;
        }
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

        _reader.Stop();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _lastSpokenText = text;
        await _reader.SpeakAsync(text);
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

    private void OnReaderStateChanged(object? sender, PlaybackState state) =>
        MainThread.BeginInvokeOnMainThread(() =>
            PlayPauseGlyph.Text = state == PlaybackState.Playing ? "⏸" : "▶");

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

    private void UpdateVoiceLabel()
    {
        VoiceNameLabel.Text = SupertonicVoiceTable.Voices
            .FirstOrDefault(v => v.Id == _settings.VoiceId, SupertonicVoiceTable.Default)
            .DisplayName;
    }
}
