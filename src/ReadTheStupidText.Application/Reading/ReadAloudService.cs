using ReadTheStupidText.Application.Activity;
using ReadTheStupidText.Application.Input;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Activity;
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
    // A text selection fires many UIA change events while the user is dragging
    // (one per character grown). Wait for the selection to settle this long
    // before reading, so a drag collapses into a single read instead of a burst.
    private const int SelectionDebounceMs = 500;

    private readonly ISpeechReader _reader;
    private readonly IClipboardReader _clipboard;
    private readonly IHotkeyService _hotkey;
    private readonly ISelectionCopier _selectionCopier;
    private readonly ISelectionMonitor _selectionMonitor;
    private readonly IVoiceCatalog _voices;
    private readonly IVoiceModelService _voiceModel;
    private readonly IActivityLog _log;
    private readonly ISettingsStore _settings;

    // Cancels a pending debounced read when a newer selection supersedes it.
    private CancellationTokenSource? _selectionCts;

    // The entry currently pending or being read, tracked so a new selection,
    // deselect, or completion can transition it (ignored/interrupted/read).
    private ActivityEntry? _activeEntry;

    public ReadAloudService(
        ISpeechReader reader,
        IClipboardReader clipboard,
        IHotkeyService hotkey,
        ISelectionCopier selectionCopier,
        ISelectionMonitor selectionMonitor,
        IVoiceCatalog voices,
        IVoiceModelService voiceModel,
        IActivityLog log,
        ISettingsStore settings)
    {
        _reader = reader;
        _clipboard = clipboard;
        _hotkey = hotkey;
        _selectionCopier = selectionCopier;
        _selectionMonitor = selectionMonitor;
        _voices = voices;
        _voiceModel = voiceModel;
        _log = log;
        _settings = settings;

        _reader.SetSpeed(_settings.Speed);
        ApplyPersistedVoice();
        _hotkey.Pressed += OnHotkeyPressed;
        _selectionMonitor.SelectionChanged += OnSelectionChanged;
        _selectionMonitor.SelectionCleared += OnSelectionCleared;
        _reader.StateChanged += OnReaderStateChanged;

        // The neural voice model ships in the package; initialize locates it and
        // marks itself ready, then we apply the voice and refresh the UI.
        _voiceModel.ReadyChanged += OnVoiceModelReady;
        _ = _voiceModel.InitializeAsync();

        if (_settings.IsEnabled)
        {
            _selectionMonitor.Start();
        }
    }

    /// <summary>Whether the neural voices are loaded and selectable.</summary>
    public bool VoicesReady => _voiceModel.IsReady;

    public PlaybackState State => _reader.State;

    public PlaybackRate Speed => _settings.Speed;

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
            EnabledChanged?.Invoke(this, value);
        }
    }

    public event EventHandler<PlaybackState>? StateChanged
    {
        add => _reader.StateChanged += value;
        remove => _reader.StateChanged -= value;
    }

    /// <summary>Raised after the speed changes, so every control surface (tray
    /// menu, control panel) reflects the new value without polling.</summary>
    public event EventHandler<PlaybackRate>? SpeedChanged;

    /// <summary>Raised after the narrator voice changes.</summary>
    public event EventHandler<string>? VoiceChanged;

    /// <summary>Raised after auto-read is toggled on or off.</summary>
    public event EventHandler<bool>? EnabledChanged;

    /// <summary>Raised when the set of selectable voices becomes available (the
    /// neural model loaded), so control surfaces can rebuild their pickers.</summary>
    public event EventHandler? VoicesChanged;

    /// <summary>
    /// The "Play" action shared by the tray menu and control panel: pauses if
    /// playing, resumes if paused, and when idle starts a fresh read of the
    /// current selection (falling back to the clipboard when nothing is selected).
    /// </summary>
    public Task PlayPauseOrReadAsync()
    {
        switch (State)
        {
            case PlaybackState.Playing:
                _reader.Pause();
                return Task.CompletedTask;
            case PlaybackState.Paused:
                _reader.Resume();
                return Task.CompletedTask;
            default:
                return ReadCopiedSelectionAsync(ActivitySource.Manual);
        }
    }

    public void SetSpeed(PlaybackRate speed)
    {
        _settings.Speed = speed;
        _reader.SetSpeed(speed);
        SpeedChanged?.Invoke(this, speed);
    }

    /// <summary>Selects and persists the narrator voice; applies to the next read.</summary>
    public void SetVoice(string voiceId)
    {
        _settings.VoiceId = voiceId;
        _reader.SetVoice(voiceId);
        VoiceChanged?.Invoke(this, voiceId);
    }

    private void ApplyPersistedVoice()
    {
        if (CurrentVoiceId is { } voiceId)
        {
            _reader.SetVoice(voiceId);
        }
    }

    // Once the model is ready the catalog has voices, so the persisted (or
    // default) choice can finally be applied and the UI refreshed.
    private void OnVoiceModelReady(object? sender, EventArgs e)
    {
        ApplyPersistedVoice();
        VoicesChanged?.Invoke(this, EventArgs.Empty);
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

    // The hotkey copies the focused selection then reads it — the fallback for
    // non-UIA apps, working whether or not auto-read is enabled.
    private async void OnHotkeyPressed(object? sender, EventArgs e) =>
        await ReadCopiedSelectionAsync(ActivitySource.Hotkey);

    // Copies the current selection to the clipboard and reads it (hotkey / manual).
    private async Task ReadCopiedSelectionAsync(ActivitySource source)
    {
        await _selectionCopier.CopyAsync();
        string? text = await _clipboard.GetTextAsync();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        ActivityEntry entry = StartEntry(source, text);
        await ReadEntryAsync(entry, text);
    }

    // Auto-read: debounced so a drag-select (many events) collapses to one read.
    private void OnSelectionChanged(object? sender, string text)
    {
        var cts = new CancellationTokenSource();
        CancellationTokenSource? previous = Interlocked.Exchange(ref _selectionCts, cts);
        previous?.Cancel();
        previous?.Dispose();

        ActivityEntry entry = StartEntry(ActivitySource.AutoRead, text);
        _ = ReadAfterDebounceAsync(entry, text, cts.Token);
    }

    private async Task ReadAfterDebounceAsync(ActivityEntry entry, string text, CancellationToken token)
    {
        try
        {
            await Task.Delay(SelectionDebounceMs, token);
        }
        catch (OperationCanceledException)
        {
            return; // superseded during the wait; already marked ignored/interrupted
        }

        await ReadEntryAsync(entry, text);
    }

    // A deselect stops a read in progress (and drops a still-pending one).
    private void OnSelectionCleared(object? sender, EventArgs e)
    {
        _selectionCts?.Cancel();
        Supersede();
    }

    // Finalizes the prior active entry (pending → ignored, reading → interrupted
    // and the reader paused), then opens a new pending entry and makes it active.
    private ActivityEntry StartEntry(ActivitySource source, string text)
    {
        Supersede();
        ActivityEntry entry = _log.Add(source, text);
        _activeEntry = entry;
        return entry;
    }

    private void Supersede()
    {
        ActivityEntry? active = _activeEntry;
        _activeEntry = null;
        if (active is null)
        {
            return;
        }

        if (active.State == ActivityState.Pending)
        {
            _log.SetState(active, ActivityState.Ignored);
        }
        else if (active.State == ActivityState.Reading)
        {
            _reader.Pause();
            _log.SetState(active, ActivityState.Interrupted);
        }
    }

    private async Task ReadEntryAsync(ActivityEntry entry, string text)
    {
        try
        {
            _log.SetState(entry, ActivityState.Reading);
            await _reader.SpeakAsync(text);
            // Completion → Read is signalled when the reader returns to Idle
            // (OnReaderStateChanged); a synthesis/playback error falls below.
        }
        catch
        {
            _log.SetState(entry, ActivityState.Failed);
            ClearIfActive(entry);
        }
    }

    // Natural end of playback marks the active read as Read.
    private void OnReaderStateChanged(object? sender, PlaybackState state)
    {
        if (state == PlaybackState.Idle && _activeEntry is { State: ActivityState.Reading } entry)
        {
            _log.SetState(entry, ActivityState.Read);
            _activeEntry = null;
        }
    }

    private void ClearIfActive(ActivityEntry entry)
    {
        if (ReferenceEquals(_activeEntry, entry))
        {
            _activeEntry = null;
        }
    }

    public void Dispose()
    {
        _hotkey.Pressed -= OnHotkeyPressed;
        _selectionMonitor.SelectionChanged -= OnSelectionChanged;
        _selectionMonitor.SelectionCleared -= OnSelectionCleared;
        _reader.StateChanged -= OnReaderStateChanged;
        _selectionMonitor.Stop();
        _selectionCts?.Cancel();
        _selectionCts?.Dispose();
    }
}
