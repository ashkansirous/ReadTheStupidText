using System.Globalization;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Screen 1 — "Type or paste" (Slice 30, Decision 43). Directly wires
/// <see cref="ISpeechReader"/> + <see cref="PlaybackRate"/>: unlike the Windows
/// app there is no auto-read/hotkey/clipboard orchestration to reuse (Decision
/// 38 — none of those triggers exist on Android), so this page is its own thin
/// use case rather than going through <c>ReadAloudService</c>.
/// </summary>
public partial class TypePage : ContentPage
{
    private static readonly Color SelectedPresetBackground = Color.FromArgb("#5B57E8");
    private static readonly Color SelectedPresetText = Colors.White;
    private static readonly Color UnselectedPresetBackground = Color.FromArgb("#0D000000");

    private readonly ISpeechReader _reader;
    private readonly ISettingsStore _settings;
    private PlaybackRate _speed;

    public TypePage(ISpeechReader reader, ISettingsStore settings)
    {
        InitializeComponent();

        _reader = reader;
        _settings = settings;
        _speed = _settings.Speed;
        UpdatePresetHighlight();
        UpdateSubtitle();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _reader.StateChanged += OnReaderStateChanged;
        _reader.ProgressChanged += OnReaderProgressChanged;
        _reader.TimingChanged += OnReaderTimingChanged;

        // Re-read on every appearance (not just construction): the voice picker
        // (Slice 31) may have changed the persisted voice while this tab was
        // hidden, and Shell keeps this page instance alive across tab switches.
        UpdateSubtitle();
    }

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
        UpdateSubtitle();
    }

    private async void OnVoicePickerTapped(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync(AppShell.VoicePickerRoute);

    private void OnReaderStateChanged(object? sender, PlaybackState state) =>
        MainThread.BeginInvokeOnMainThread(() =>
            PlayPauseGlyph.Text = state == PlaybackState.Playing ? "⏸" : "▶");

    private void OnReaderProgressChanged(object? sender, double progress) =>
        MainThread.BeginInvokeOnMainThread(() => ReadProgressBar.Progress = progress);

    private void OnReaderTimingChanged(object? sender, ReadTiming timing) =>
        MainThread.BeginInvokeOnMainThread(() => TimerLabel.Text = ReadTimingFormatter.Format(timing));

    private void UpdatePresetHighlight()
    {
        foreach (Button button in SpeedPresetRow.Children.OfType<Button>())
        {
            bool selected = button.ClassId is string classId
                && double.TryParse(classId, CultureInfo.InvariantCulture, out double value)
                && new PlaybackRate(value).Value == _speed.Value;

            button.BackgroundColor = selected ? SelectedPresetBackground : UnselectedPresetBackground;
            button.TextColor = selected ? SelectedPresetText : (Color)Resources["TextSecondaryLight"];
        }
    }

    private void UpdateSubtitle()
    {
        string voiceName = SupertonicVoiceTable.Voices
            .FirstOrDefault(v => v.Id == _settings.VoiceId, SupertonicVoiceTable.Default)
            .DisplayName;
        SubtitleLabel.Text = $"{voiceName} · {_speed.ToDisplayLabel()}";
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _reader.StateChanged -= OnReaderStateChanged;
        _reader.ProgressChanged -= OnReaderProgressChanged;
        _reader.TimingChanged -= OnReaderTimingChanged;
    }
}
