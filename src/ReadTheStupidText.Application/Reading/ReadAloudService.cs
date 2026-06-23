using ReadTheStupidText.Application.Input;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Coordinates the read-aloud use case. Two trigger paths feed the reader: the
/// global hotkey (always available — copies the focused selection and reads it,
/// the fallback for non-UIA apps), and the UI Automation selection monitor
/// (auto-read, gated by <see cref="IsEnabled"/>). Also forwards the flyout's
/// play/pause and speed choices. Speed and enabled state are persisted.
/// </summary>
public sealed class ReadAloudService : IDisposable
{
    private readonly ISpeechReader _reader;
    private readonly IClipboardReader _clipboard;
    private readonly IHotkeyService _hotkey;
    private readonly ISelectionCopier _selectionCopier;
    private readonly ISelectionMonitor _selectionMonitor;
    private readonly IVoiceCatalog _voices;
    private readonly ISettingsStore _settings;

    public ReadAloudService(
        ISpeechReader reader,
        IClipboardReader clipboard,
        IHotkeyService hotkey,
        ISelectionCopier selectionCopier,
        ISelectionMonitor selectionMonitor,
        IVoiceCatalog voices,
        ISettingsStore settings)
    {
        _reader = reader;
        _clipboard = clipboard;
        _hotkey = hotkey;
        _selectionCopier = selectionCopier;
        _selectionMonitor = selectionMonitor;
        _voices = voices;
        _settings = settings;

        _reader.SetSpeed(_settings.Speed);
        ApplyPersistedVoice();
        _hotkey.Pressed += OnHotkeyPressed;
        _selectionMonitor.SelectionChanged += OnSelectionChanged;

        if (_settings.IsEnabled)
        {
            _selectionMonitor.Start();
        }
    }

    public PlaybackState State => _reader.State;

    public ReadingSpeed Speed => _settings.Speed;

    /// <summary>The narrator voices installed on the machine.</summary>
    public IReadOnlyList<VoiceInfo> InstalledVoices => _voices.InstalledVoices;

    /// <summary>
    /// The id of the voice currently in effect: the persisted choice if it is
    /// still installed, otherwise the system default.
    /// </summary>
    public string? CurrentVoiceId
    {
        get
        {
            string? saved = _settings.VoiceId;
            if (saved is not null && _voices.InstalledVoices.Any(v => v.Id == saved))
            {
                return saved;
            }

            return _voices.DefaultVoice?.Id;
        }
    }

    /// <summary>
    /// Whether auto-read on selection is on. Persisted, and starts/stops the
    /// selection monitor. The hotkey works regardless of this flag.
    /// </summary>
    public bool IsEnabled
    {
        get => _settings.IsEnabled;
        set
        {
            _settings.IsEnabled = value;
            ApplyAutoRead(value);
        }
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
        await SpeakAsync(text);
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

    /// <summary>Selects and persists the narrator voice; applies to the next read.</summary>
    public void SetVoice(string voiceId)
    {
        _settings.VoiceId = voiceId;
        _reader.SetVoice(voiceId);
    }

    private void ApplyPersistedVoice()
    {
        if (CurrentVoiceId is { } voiceId)
        {
            _reader.SetVoice(voiceId);
        }
    }

    private void ApplyAutoRead(bool enabled)
    {
        if (enabled)
        {
            _selectionMonitor.Start();
        }
        else
        {
            _selectionMonitor.Stop();
        }
    }

    private async Task SpeakAsync(string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            await _reader.SpeakAsync(text);
        }
    }

    // The hotkey is an explicit, manual action — it reads the selection whether
    // or not auto-read is enabled, so it remains the fallback for non-UIA apps.
    private async void OnHotkeyPressed(object? sender, EventArgs e) => await ReadSelectionAsync();

    private async void OnSelectionChanged(object? sender, string text) => await SpeakAsync(text);

    public void Dispose()
    {
        _hotkey.Pressed -= OnHotkeyPressed;
        _selectionMonitor.SelectionChanged -= OnSelectionChanged;
        _selectionMonitor.Stop();
    }
}
