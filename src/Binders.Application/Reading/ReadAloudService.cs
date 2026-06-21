using Binders.Application.Input;
using Binders.Domain.Reading;

namespace Binders.Application.Reading;

/// <summary>
/// Coordinates the read-aloud use case: when the global hotkey fires, read the
/// clipboard text aloud; and forward the flyout's play/pause and speed choices
/// to the speech reader. This is the single place the slice's behaviour lives.
/// </summary>
public sealed class ReadAloudService : IDisposable
{
    private readonly ISpeechReader _reader;
    private readonly IClipboardReader _clipboard;
    private readonly IHotkeyService _hotkey;

    private ReadingSpeed _speed = ReadingSpeedExtensions.Default;

    public ReadAloudService(
        ISpeechReader reader,
        IClipboardReader clipboard,
        IHotkeyService hotkey)
    {
        _reader = reader;
        _clipboard = clipboard;
        _hotkey = hotkey;
        _hotkey.Pressed += OnHotkeyPressed;
    }

    public PlaybackState State => _reader.State;

    public ReadingSpeed Speed => _speed;

    public event EventHandler<PlaybackState>? StateChanged
    {
        add => _reader.StateChanged += value;
        remove => _reader.StateChanged -= value;
    }

    /// <summary>Reads the current clipboard text aloud at the chosen speed.</summary>
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
        _speed = speed;
        _reader.SetSpeed(speed);
    }

    private async void OnHotkeyPressed(object? sender, EventArgs e)
    {
        await ReadClipboardAsync();
    }

    public void Dispose()
    {
        _hotkey.Pressed -= OnHotkeyPressed;
    }
}
