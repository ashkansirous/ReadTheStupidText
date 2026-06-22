using ReadTheStupidText.Application.Input;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Coordinates the read-aloud use case: when the global hotkey fires, copy the
/// focused app's selection and read it aloud; and forward the flyout's play/pause
/// and speed choices to the speech reader. Speed and enabled state are persisted.
/// </summary>
public sealed class ReadAloudService : IDisposable
{
    private readonly ISpeechReader _reader;
    private readonly IClipboardReader _clipboard;
    private readonly IHotkeyService _hotkey;
    private readonly ISelectionCopier _selectionCopier;
    private readonly ISettingsStore _settings;

    public ReadAloudService(
        ISpeechReader reader,
        IClipboardReader clipboard,
        IHotkeyService hotkey,
        ISelectionCopier selectionCopier,
        ISettingsStore settings)
    {
        _reader = reader;
        _clipboard = clipboard;
        _hotkey = hotkey;
        _selectionCopier = selectionCopier;
        _settings = settings;

        _reader.SetSpeed(_settings.Speed);
        _hotkey.Pressed += OnHotkeyPressed;
    }

    public PlaybackState State => _reader.State;

    public ReadingSpeed Speed => _settings.Speed;

    public bool IsEnabled
    {
        get => _settings.IsEnabled;
        set => _settings.IsEnabled = value;
    }

    public event EventHandler<PlaybackState>? StateChanged
    {
        add => _reader.StateChanged += value;
        remove => _reader.StateChanged -= value;
    }

    /// <summary>Copies the current selection, then reads it aloud.</summary>
    public async Task ReadSelectionAsync()
    {
        await _selectionCopier.CopyAsync();
        await ReadClipboardAsync();
    }

    /// <summary>Reads whatever text is already on the clipboard aloud.</summary>
    public async Task ReadClipboardAsync()
    {
        var text = await _clipboard.GetTextAsync();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        await _reader.SpeakAsync(text);
    }

    /// <summary>Pauses if playing, resumes if paused; no-op when idle.</summary>
    public void TogglePlayPause()
    {
        if (_reader.State == PlaybackState.Playing)
        {
            _reader.Pause();
        }
        else if (_reader.State == PlaybackState.Paused)
        {
            _reader.Resume();
        }
    }

    public void SetSpeed(ReadingSpeed speed)
    {
        _settings.Speed = speed;
        _reader.SetSpeed(speed);
    }

    private async void OnHotkeyPressed(object? sender, EventArgs e)
    {
        if (!_settings.IsEnabled)
        {
            return;
        }

        await ReadSelectionAsync();
    }

    public void Dispose()
    {
        _hotkey.Pressed -= OnHotkeyPressed;
    }
}
