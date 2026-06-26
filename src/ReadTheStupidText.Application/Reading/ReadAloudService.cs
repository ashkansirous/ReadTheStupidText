using ReadTheStupidText.Application.Activity;
using ReadTheStupidText.Application.Input;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Activity;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Coordinates the read-aloud use case. Three trigger paths feed the reader: the
/// global hotkey (always available — copies the focused selection and reads it,
/// the fallback for non-UIA apps), the UI Automation selection monitor (auto-read
/// on selection), and the clipboard monitor (auto-read on copy — the path for the
/// console and other apps with no UIA text selection). The two auto-read paths are
/// gated independently by <see cref="AutoReadOnSelection"/> and
/// <see cref="AutoReadOnCopy"/>; <see cref="_lastTriggeredText"/> de-dupes the
/// same text arriving from more than one path. Also forwards the flyout's
/// play/pause and speed choices. Speed and the toggles are persisted.
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
    private readonly IClipboardMonitor _clipboardMonitor;
    private readonly IForegroundWindow _foreground;
    private readonly IVoiceCatalog _voices;
    private readonly IVoiceModelService _voiceModel;
    private readonly IActivityLog _log;
    private readonly ISettingsStore _settings;

    // Cancels a pending debounced read when a newer selection supersedes it.
    private CancellationTokenSource? _selectionCts;

    // The entry currently pending or being read, tracked so a new selection,
    // deselect, or completion can transition it (ignored/interrupted/read).
    private ActivityEntry? _activeEntry;

    // The text of the most recent read we started, on any path. Used to drop the
    // clipboard echo of our own hotkey copy (and a copy-on-select duplicate of a
    // UIA selection) so the same text isn't read twice from two sources.
    private string? _lastTriggeredText;

    // True only while a hotkey/manual read is synthesizing its own Ctrl+C, so the
    // clipboard update that copy produces isn't mistaken for a user copy.
    private bool _copyingForRead;

    public ReadAloudService(
        ISpeechReader reader,
        IClipboardReader clipboard,
        IHotkeyService hotkey,
        ISelectionCopier selectionCopier,
        ISelectionMonitor selectionMonitor,
        IClipboardMonitor clipboardMonitor,
        IForegroundWindow foreground,
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
        _clipboardMonitor = clipboardMonitor;
        _foreground = foreground;
        _voices = voices;
        _voiceModel = voiceModel;
        _log = log;
        _settings = settings;

        _reader.SetSpeed(_settings.Speed);
        ApplyPersistedVoice();
        _hotkey.Pressed += OnHotkeyPressed;
        _selectionMonitor.SelectionChanged += OnSelectionChanged;
        _selectionMonitor.SelectionCleared += OnSelectionCleared;
        _clipboardMonitor.ContentChanged += OnClipboardContentChanged;
        _reader.StateChanged += OnReaderStateChanged;
        _reader.Completed += OnReaderCompleted;

        // The neural voice model ships in the package; initialize locates it and
        // marks itself ready, then we apply the voice and refresh the UI.
        _voiceModel.ReadyChanged += OnVoiceModelReady;
        _ = _voiceModel.InitializeAsync();

        if (_settings.AutoReadOnSelection)
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
    /// Whether auto-read on text selection is on. Persisted, and starts/stops the
    /// UIA selection monitor. The hotkey works regardless of this flag.
    /// </summary>
    public bool AutoReadOnSelection
    {
        get => _settings.AutoReadOnSelection;
        set
        {
            _settings.AutoReadOnSelection = value;
            ApplySelectionMonitor(value);
            AutoReadOnSelectionChanged?.Invoke(this, value);
        }
    }

    /// <summary>
    /// Whether auto-read on clipboard copy is on. Persisted; gates the clipboard
    /// monitor path (the console / non-UIA fallback). The hotkey is unaffected.
    /// </summary>
    public bool AutoReadOnCopy
    {
        get => _settings.AutoReadOnCopy;
        set
        {
            _settings.AutoReadOnCopy = value;
            AutoReadOnCopyChanged?.Invoke(this, value);
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

    /// <summary>Raised after auto-read on selection is toggled on or off.</summary>
    public event EventHandler<bool>? AutoReadOnSelectionChanged;

    /// <summary>Raised after auto-read on copy is toggled on or off.</summary>
    public event EventHandler<bool>? AutoReadOnCopyChanged;

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
                return ReadCopiedSelectionAsync(ActivityTrigger.Manual);
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

    private void ApplySelectionMonitor(bool enabled)
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
        await ReadCopiedSelectionAsync(ActivityTrigger.Hotkey);

    // Copies the current selection to the clipboard and reads it (hotkey / manual).
    // The synthesized Ctrl+C itself changes the clipboard, so the resulting update
    // is suppressed (see _copyingForRead) to avoid a duplicate clipboard auto-read.
    private async Task ReadCopiedSelectionAsync(ActivityTrigger trigger)
    {
        string? text;
        _copyingForRead = true;
        try
        {
            await _selectionCopier.CopyAsync();
            text = await _clipboard.GetTextAsync();
        }
        finally
        {
            _copyingForRead = false;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        ActivityEntry entry = StartEntry(trigger, text);
        await ReadEntryAsync(entry, text);
    }

    // Auto-read from a UI Automation selection change.
    private void OnSelectionChanged(object? sender, string text) =>
        BeginAutoRead(ActivityTrigger.AutoRead, text);

    // Auto-read from a clipboard copy — the path for the console and other apps
    // that expose no UIA selection. Skipped while we're copying for a hotkey/manual
    // read (our own Ctrl+C echo) and when the text repeats one we just triggered
    // (e.g. a copy-on-select duplicate of a UIA selection).
    private async void OnClipboardContentChanged(object? sender, EventArgs e)
    {
        if (!_settings.AutoReadOnCopy || _copyingForRead)
        {
            return;
        }

        string? text = await _clipboard.GetTextAsync();
        if (string.IsNullOrWhiteSpace(text) || text == _lastTriggeredText)
        {
            return;
        }

        BeginAutoRead(ActivityTrigger.Clipboard, text);
    }

    // Shared auto-read entry point: debounced so a drag-select (many events) or a
    // multi-message clipboard update collapses into a single read.
    private void BeginAutoRead(ActivityTrigger trigger, string text)
    {
        var cts = new CancellationTokenSource();
        CancellationTokenSource? previous = Interlocked.Exchange(ref _selectionCts, cts);
        previous?.Cancel();
        previous?.Dispose();

        ActivityEntry entry = StartEntry(trigger, text);
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
        Supersede(ActivityReason.Deselected);
    }

    // Finalizes the prior active entry (pending → ignored, reading → interrupted
    // and the reader paused), then opens a new pending entry — tagged with the
    // foreground window it came from — and makes it active.
    private ActivityEntry StartEntry(ActivityTrigger trigger, string text)
    {
        Supersede(ActivityReason.NewSelection);
        _lastTriggeredText = text;
        ActivityEntry entry = _log.Add(trigger, _foreground.Capture(), text);
        _activeEntry = entry;
        return entry;
    }

    private void Supersede(ActivityReason reason)
    {
        ActivityEntry? active = _activeEntry;
        _activeEntry = null;
        if (active is null)
        {
            return;
        }

        if (active.State == ActivityState.Pending)
        {
            _log.SetState(active, ActivityState.Ignored, reason);
        }
        else if (active.State is ActivityState.GeneratingAudio or ActivityState.Reading)
        {
            // Stop (not Pause) so the superseded synthesis/playback is cancelled —
            // otherwise a slow long read could still play after this point.
            _reader.Stop();
            _log.SetState(active, ActivityState.Interrupted, reason);
        }
    }

    private async Task ReadEntryAsync(ActivityEntry entry, string text)
    {
        try
        {
            // Synthesis runs first (no audio yet) → GeneratingAudio; the reader's
            // first Playing transition flips it to Reading (OnReaderStateChanged),
            // natural completion → Read (OnReaderCompleted), errors → catch below.
            _log.SetState(entry, ActivityState.GeneratingAudio);
            await _reader.SpeakAsync(text);
        }
        catch
        {
            _log.SetState(entry, ActivityState.Failed, ActivityReason.Error);
            ClearIfActive(entry);
        }
    }

    // The reader starting to play flips the active entry from GeneratingAudio (the
    // synthesis wait) to Reading. Later Playing transitions (per chunk) are no-ops.
    private void OnReaderStateChanged(object? sender, PlaybackState state)
    {
        if (state == PlaybackState.Playing && _activeEntry is { State: ActivityState.GeneratingAudio } entry)
        {
            _log.SetState(entry, ActivityState.Reading);
        }
    }

    // Natural end of playback marks the active read as Read. Stop/supersede does
    // not raise Completed, so an interrupted read is never mis-marked as Read.
    private void OnReaderCompleted(object? sender, EventArgs e)
    {
        if (_activeEntry is { State: ActivityState.GeneratingAudio or ActivityState.Reading } entry)
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
        _clipboardMonitor.ContentChanged -= OnClipboardContentChanged;
        _reader.StateChanged -= OnReaderStateChanged;
        _reader.Completed -= OnReaderCompleted;
        _selectionMonitor.Stop();
        _selectionCts?.Cancel();
        _selectionCts?.Dispose();
    }
}
