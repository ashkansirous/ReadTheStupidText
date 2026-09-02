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
/// pushed page that hands its OCR result back via the <see cref="ScannedText"/>
/// query property instead of reading it directly, and File no longer needs its
/// own screen since there is nothing left for it to show once the text lands in
/// this box.
/// </summary>
[QueryProperty(nameof(ScannedText), ScannedTextKey)]
public partial class ReaderPage : ContentPage
{
    public const string ScannedTextKey = "scannedText";

    private static readonly FilePickerFileType SupportedFileTypes = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        { DevicePlatform.Android, ["text/plain", "application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"] },
    });

    private readonly ISpeechReader _reader;
    private readonly ISettingsStore _settings;
    private readonly IDocumentTextExtractor _documentExtractor;
    private PlaybackRate _speed;

    public ReaderPage(ISpeechReader reader, ISettingsStore settings, IDocumentTextExtractor documentExtractor)
    {
        InitializeComponent();

        _reader = reader;
        _settings = settings;
        _documentExtractor = documentExtractor;
        _speed = _settings.Speed;
        UpdatePresetHighlight();
        UpdateVoiceLabel();
    }

    /// <summary>
    /// Receives the OCR result handed back by <see cref="ScanPage"/> when it pops
    /// itself via <c>GoToAsync("..", ...)</c> — see the type's remarks.
    /// </summary>
    public string? ScannedText
    {
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                TextEditor.Text = value;
            }
        }
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

    private async void OnPlayPauseTapped(object? sender, TappedEventArgs e)
    {
        switch (_reader.State)
        {
            case PlaybackState.Playing:
                _reader.Pause();
                break;
            case PlaybackState.Paused:
                _reader.Resume();
                break;
            default:
                string text = TextEditor.Text ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    await _reader.SpeakAsync(text);
                }

                break;
        }
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
