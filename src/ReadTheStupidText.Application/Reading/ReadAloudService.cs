using System.Diagnostics;
using ReadTheStupidText.Application.Activity;
using ReadTheStupidText.Application.Documents;
using ReadTheStupidText.Application.Input;
using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Sanitizing;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Domain.Activity;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Coordinates the read-aloud use case. Four trigger paths feed the reader: the
/// global hotkey (always available — copies the focused selection and reads it,
/// the fallback for non-UIA apps), the UI Automation selection monitor (auto-read
/// on selection), the clipboard monitor (auto-read on copy — the path for the
/// console and other apps with no UIA text selection), and an uploaded file
/// (<see cref="ReadFileAsync"/> — the Upload button / tray "Read file…" item). The
/// two auto-read paths are gated independently by <see cref="AutoReadOnSelection"/>
/// and <see cref="AutoReadOnCopy"/>; <see cref="_lastTriggeredText"/> de-dupes the
/// same text arriving from more than one path. Also forwards the flyout's
/// play/pause and speed choices. Speed and enabled state are persisted.
/// </summary>
public sealed partial class ReadAloudService : IDisposable
{
    // Auto-read waits for the selection to settle before reading, so a drag-select
    // (many UIA events, one per character grown) or a multi-message clipboard update
    // collapses into one read. The settle is adaptive: a lone event (a click /
    // double-click select) fires after a short baseline so it feels instant...
    private const int SettleBaselineMs = 150;

    // ...but once events are actively arriving (a live drag), each one extends the
    // wait to this longer window, so a brief mid-drag pause can't fire a premature
    // first read followed by a second. A late extra read is harmless anyway — the
    // swapped _selectionCts supersede cancels the older one.
    private const int SettleDraggingMs = 500;

    // Two auto-read events closer together than this mark an active burst (a drag in
    // progress), switching the settle from the baseline to the dragging window.
    private const int BurstGapMs = 250;

    // The head-start prepare begins only once the selection has been stable this long: shorter than
    // SettleBaselineMs, so a lone click-select still has its opening sentences in the cache before
    // the read starts, and shorter than BurstGapMs, so every event of a drag-select restarts the
    // wait and only the final text is synthesized.
    private const int HeadStartStableMs = 80;

    // The lone-select settle in effect; tests lengthen it to make "prepare runs before the read" deterministic.
    internal int LoneSelectSettleMs
    {
        get => _loneSelectSettleMs;
        set => _loneSelectSettleMs = Math.Max(0, value);
    }

    private int _loneSelectSettleMs = SettleBaselineMs;

    private readonly ISpeechReader _reader;
    private readonly IClipboardReader _clipboard;
    private readonly IHotkeyService _hotkey;
    private readonly ISelectionCopier _selectionCopier;
    private readonly ISelectionMonitor _selectionMonitor;
    private readonly IClipboardMonitor _clipboardMonitor;
    private readonly IForegroundWindow _foreground;
    private readonly IVoiceCatalog _voices;
    private readonly IVoiceModelService _voiceModel;
    private readonly IDocumentTextExtractor _documents;
    private readonly IActivityLog _log;
    private readonly ISettingsStore _settings;
    private readonly ITextSanitizer _sanitizer;
    private readonly ISystemLog _systemLog;
    private readonly IHeadStart? _headStart;
    private readonly ReadTranslator? _translation;

    // Cancels the translation of the read being prepared when it is superseded (Slice 57).
    private CancellationTokenSource? _translationCts;

    // Cancels a pending debounced read when a newer selection supersedes it.
    private CancellationTokenSource? _selectionCts;

    // Cancels a head-start prepare still waiting for the selection to stabilize.
    private CancellationTokenSource? _prepareCts;

    // Monotonic timestamp of the last auto-read event, to tell a lone select from an
    // in-progress drag (events arriving within BurstGapMs of each other).
    private long _lastAutoReadTick;

    // Monotonic baselines for the local timing diagnostics (Decision 26): when the
    // active entry was created (time-to-first-audio) and when its synthesis began
    // (synthesis duration). Both are read on the first Playing transition.
    private long _activeReadStartTicks;
    private long _synthStartTicks;

    // The entry currently pending or being read, tracked so a new selection,
    // deselect, or completion can transition it (ignored/interrupted/read).
    private ActivityEntry? _activeEntry;

    // Last value published through GeneratingChanged, so it only fires on a change.
    // The lock keeps the compare-and-set atomic across threads (UI thread, reader
    // callbacks, synthesis threads). Event order across threads is NOT guaranteed:
    // subscribers must re-read IsGeneratingAudio instead of trusting the argument.
    private readonly object _generatingLock = new();
    private bool _publishedGenerating;

    /// <summary>How long a reader that returned from SpeakAsync may take to report
    /// its first Playing before the read is failed. Readers like the WinRT fallback
    /// return before playback actually starts.</summary>
    public static readonly TimeSpan DefaultFirstAudioGrace = TimeSpan.FromSeconds(8);

    /// <summary>The first-audio grace window in effect; tests shorten it.</summary>
    public TimeSpan FirstAudioGrace { get; set; } = DefaultFirstAudioGrace;

    // Cancelled when the active entry plays, ends or is superseded, ending its grace wait.
    private CancellationTokenSource? _graceCts;

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
        IDocumentTextExtractor documents,
        IActivityLog log,
        ISettingsStore settings,
        ITextSanitizer sanitizer,
        ISystemLog systemLog,
        IHeadStart? headStart = null,
        ReadTranslator? translation = null)
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
        _documents = documents;
        _log = log;
        _settings = settings;
        _sanitizer = sanitizer;
        _systemLog = systemLog;
        _headStart = headStart;
        _translation = translation;

        _reader.SetSpeed(_settings.Speed);
        ApplyPersistedVoice();
        _hotkey.Pressed += OnHotkeyPressed;
        _selectionMonitor.SelectionChanged += OnSelectionChanged;
        _selectionMonitor.SelectionCleared += OnSelectionCleared;
        _clipboardMonitor.ContentChanged += OnClipboardContentChanged;
        _reader.StateChanged += OnReaderStateChanged;
        _reader.Completed += OnReaderCompleted;

        // The neural voice model is a downloaded pack (Slice 56); initialize locates an
        // installed one and marks itself ready, then we apply the voice and refresh the UI.
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
    /// Whether selecting text in a UIA app auto-reads it. Persisted, and
    /// starts/stops the selection monitor. The hotkey works regardless of this flag.
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
    /// Whether copying text auto-reads it (the console / non-UIA path). Persisted;
    /// gates the clipboard handler. The hotkey works regardless of this flag.
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

    /// <summary>Read-through progress (0..1) of the current utterance, for the
    /// control panel's progress bar. Best-effort for chunked synthesis.</summary>
    public event EventHandler<double>? ProgressChanged
    {
        add => _reader.ProgressChanged += value;
        remove => _reader.ProgressChanged -= value;
    }

    /// <summary>Read-through elapsed/total timing of the current utterance, for the
    /// control panel's timer (Decision 33).</summary>
    public event EventHandler<ReadTiming>? TimingChanged
    {
        add => _reader.TimingChanged += value;
        remove => _reader.TimingChanged -= value;
    }

    /// <summary>Raised with the full text of a new read, right before synthesis
    /// starts — the reading text box's source of the text it displays (Slice 37).
    /// <see cref="ChunkChanged"/>'s source ranges are relative to this same text.</summary>
    public event EventHandler<ReadText>? ReadTextChanged;

    /// <summary>Raised before <see cref="ReadTextChanged"/> when a read attempted translation
    /// (translated, or fell back to the original with a reason), so the UI can say which (Slice 57).</summary>
    public event EventHandler<ReadTranslationResult>? TranslationFinished;

    /// <summary>Whether this service can translate reads at all (an engine is wired in and available).</summary>
    public bool CanTranslate => _translation?.IsAvailable == true;

    /// <summary>Whether reads are translated into the chosen language; persisted (Slice 57).</summary>
    public bool TranslateEnabled
    {
        get => CanTranslate && _settings.TranslateEnabled;
        set
        {
            if (!CanTranslate || value == TranslateEnabled)
            {
                return;
            }

            _settings.TranslateEnabled = value;
            if (value)
            {
                _translation?.StartTargetPackDownload();
            }

            TranslateEnabledChanged?.Invoke(this, value);
        }
    }

    /// <summary>Raised after the translate toggle changes.</summary>
    public event EventHandler<bool>? TranslateEnabledChanged;

    /// <summary>The chunk currently playing, for the reading text box's whole-chunk
    /// highlight (Decision 46).</summary>
    public event EventHandler<ReadChunk>? ChunkChanged
    {
        add => _reader.ChunkChanged += value;
        remove => _reader.ChunkChanged -= value;
    }

    /// <summary>The foreground window the active read came from, or null when no
    /// read is in progress. The UI composes the status line from this.</summary>
    public WindowSource? CurrentReadWindow => _activeEntry?.Window;

    /// <summary>How the active read was triggered, or null when idle.</summary>
    public ActivityTrigger? CurrentReadTrigger => _activeEntry?.Trigger;

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

    /// <summary>True from the moment a read starts synthesizing until its first
    /// audio plays (or it is superseded, fails or completes): the "loading" window
    /// control surfaces show a spinner for. Single source of truth (Slice 46).</summary>
    public bool IsGeneratingAudio => _activeEntry is { State: ActivityState.GeneratingAudio };

    /// <summary>Raised when <see cref="IsGeneratingAudio"/> changes, with the value
    /// at the time of the change. May fire on a non-UI thread and events from
    /// different threads can arrive out of order, so handlers should re-read
    /// <see cref="IsGeneratingAudio"/> rather than trust the argument.</summary>
    public event EventHandler<bool>? GeneratingChanged;

    /// <summary>
    /// The "Play" action shared by the tray menu and control panel: pauses if
    /// playing, resumes if paused, and when idle starts a fresh read of the
    /// current selection (falling back to the clipboard when nothing is selected).
    /// </summary>
    public Task PlayPauseOrReadAsync()
    {
        // While the first audio is being synthesized Play is a no-op: a second tap
        // must not supersede the read that is about to start.
        if (IsGeneratingAudio)
        {
            return Task.CompletedTask;
        }

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

    /// <summary>
    /// Extracts the given file's text and reads it, superseding any in-progress
    /// read (Decision 34). The file name (not a foreground window) is what shows
    /// in the activity log's Source column for these entries.
    /// </summary>
    public async Task ReadFileAsync(string filePath)
    {
        string text;
        try
        {
            text = await _documents.ExtractTextAsync(filePath);
        }
        catch (DocumentTooLargeException ex)
        {
            _systemLog.Warning(
                $"'{Path.GetFileName(filePath)}' has {ex.Actual} pages, exceeding the {ex.Limit}-page limit — skipping",
                null, ex);
            return;
        }
        catch (Exception ex)
        {
            _systemLog.Error($"failed to extract text from '{Path.GetFileName(filePath)}'", null, ex);
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string clean = _sanitizer.Sanitize(text);
        var source = new WindowSource(string.Empty, Path.GetFileName(filePath));
        ActivityEntry entry = StartEntry(ActivityTrigger.FileUpload, source, clean);
        await ReadEntryAsync(entry, clean);
    }

    /// <summary>Jumps the current read ~10s forward (Decision 32); a no-op while idle.</summary>
    public Task SkipForwardAsync() => SkipAsync(_reader.SkipForward);

    /// <summary>Jumps the current read ~10s backward (Decision 32); a no-op while idle.</summary>
    public Task SkipBackwardAsync() => SkipAsync(_reader.SkipBackward);

    // Drops a skip request that arrives while a previous one hasn't settled yet
    // (Slice 35). A skip cancels the reader's in-flight chunk synthesis (Decision 32),
    // but sherpa-onnx's native Generate call can't actually be interrupted once
    // started, so back-to-back skips don't replace one another — they pile up as
    // overlapping "zombie" synthesis work competing for the same thread pool, which
    // starves the read that's actually current and made it look permanently stuck
    // (reproduced: repeated Skip presses on a long file upload left chunks 8-29
    // resynthesizing in overlapping bursts for two minutes without the read ever
    // completing). Serializing skips here — the shared entry point for the panel's
    // skip buttons — keeps that cascade from starting.
    private int _skipInFlight;

    private async Task SkipAsync(Func<Task> skip)
    {
        if (Interlocked.CompareExchange(ref _skipInFlight, 1, 0) != 0)
        {
            return;
        }

        try
        {
            await skip();
        }
        finally
        {
            Volatile.Write(ref _skipInFlight, 0);
        }
    }

    public void SetSpeed(PlaybackRate speed)
    {
        _settings.Speed = speed;
        _reader.SetSpeed(speed);
        _systemLog.Info($"speed set to {speed.ToDisplayLabel()}");
        SpeedChanged?.Invoke(this, speed);
    }

    /// <summary>Selects and persists the narrator voice; applies to the next read.</summary>
    public void SetVoice(string voiceId)
    {
        _settings.VoiceId = voiceId;
        _reader.SetVoice(voiceId);
        _systemLog.Info($"voice set to {voiceId}");
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

        // The model is located; eagerly build and warm the neural engine off the UI
        // thread (after the voice is applied) so the first real read is near-instant.
        _ = _reader.WarmUpAsync();
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

        string clean = _sanitizer.Sanitize(text);
        ActivityEntry entry = StartEntry(trigger, clean);
        await ReadEntryAsync(entry, clean);
    }

    // Auto-read from a UI Automation selection change. The text is sanitized here
    // so what gets read, de-duped and logged is always the redacted form.
    private void OnSelectionChanged(object? sender, string text) =>
        BeginAutoRead(ActivityTrigger.AutoRead, _sanitizer.Sanitize(text));

    // Auto-read from a clipboard copy — the path for the console and other apps
    // that expose no UIA selection. Skipped while we're copying for a hotkey/manual
    // read (our own Ctrl+C echo) and when the text repeats one we just triggered
    // (e.g. a copy-on-select duplicate of a UIA selection).
    private async void OnClipboardContentChanged(object? sender, EventArgs e)
    {
        if (!AutoReadOnCopy || _copyingForRead)
        {
            return;
        }

        string? text = await _clipboard.GetTextAsync();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        // Sanitize before the dedup check: _lastTriggeredText holds the sanitized
        // text of the prior read, so a copy-on-select echo of a UIA selection (or
        // our own hotkey copy) is recognised and dropped.
        string clean = _sanitizer.Sanitize(text);
        if (clean == _lastTriggeredText)
        {
            _systemLog.Debug("clipboard echo of the current read ignored");
            return;
        }

        BeginAutoRead(ActivityTrigger.Clipboard, clean);
    }

    // Shared auto-read entry point: debounced so a drag-select (many events) or a
    // multi-message clipboard update collapses into a single read. A lone event
    // settles after the short baseline; one arriving mid-burst (a drag) extends the
    // wait so the read fires only once the selection stops growing.
    private void BeginAutoRead(ActivityTrigger trigger, string text)
    {
        int settleMs = NextSettleDelayMs();

        var cts = new CancellationTokenSource();
        CancellationTokenSource? previous = Interlocked.Exchange(ref _selectionCts, cts);
        previous?.Cancel();
        previous?.Dispose();

        ActivityEntry entry = StartEntry(trigger, text);
        PrepareHeadStart(text);
        _ = ReadAfterDebounceAsync(entry, text, settleMs, cts.Token);
    }

    // Slice 56: the opening sentences are synthesized into the cache while the settle delay runs, so the
    // read that follows starts almost at once. The prepare waits HeadStartStableMs after the latest
    // event, so a drag-select (events a few ms apart) restarts it each time and only the text the drag
    // ends on is synthesized; text superseded inside that wait is never handed to the engine. Best
    // effort: a failed prepare never affects the read.
    private void PrepareHeadStart(string text)
    {
        if (_headStart is null)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        CancelPendingPrepare(Interlocked.Exchange(ref _prepareCts, cts));
        _ = PrepareWhenStableAsync(_headStart, text, cts.Token);
    }

    private async Task PrepareWhenStableAsync(IHeadStart headStart, string text, CancellationToken token)
    {
        try
        {
            await Task.Delay(HeadStartStableMs, token);
            await headStart.PrepareAsync(text);
        }
        catch (OperationCanceledException)
        {
            // Superseded while waiting: nothing was started.
        }
        catch (Exception ex)
        {
            _systemLog.Warning("head start failed", null, ex);
        }
    }

    // The translation token is still in use by its owner (TextToSpeakAsync), which disposes it; cancelling
    // a source the owner has just disposed is harmless.
    private static void CancelWithoutDispose(CancellationTokenSource? pending)
    {
        try
        {
            pending?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // the owner finished first
        }
    }

    private static void CancelPendingPrepare(CancellationTokenSource? pending)
    {
        pending?.Cancel();
        pending?.Dispose();
    }

    // The baseline for a lone select, or the longer dragging window when this event
    // follows the previous one closely enough to be part of an active drag.
    private int NextSettleDelayMs()
    {
        long now = Environment.TickCount64;
        bool dragging = now - _lastAutoReadTick < BurstGapMs;
        _lastAutoReadTick = now;
        return dragging ? SettleDraggingMs : LoneSelectSettleMs;
    }

    private async Task ReadAfterDebounceAsync(ActivityEntry entry, string text, int settleMs, CancellationToken token)
    {
        try
        {
            await Task.Delay(settleMs, token);
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
        CancelPendingPrepare(Interlocked.Exchange(ref _prepareCts, null));
        _headStart?.CancelPrepare();
        Supersede(ActivityReason.Deselected);
    }

    // Finalizes the prior active entry (pending → ignored, reading → interrupted
    // and the reader paused), then opens a new pending entry — tagged with the
    // foreground window it came from — and makes it active.
    private ActivityEntry StartEntry(ActivityTrigger trigger, string text) =>
        StartEntry(trigger, _foreground.Capture(), text);

    // As above, but for a source other than the foreground window (e.g. an
    // uploaded file's name).
    private ActivityEntry StartEntry(ActivityTrigger trigger, WindowSource? window, string text)
    {
        Supersede(ActivityReason.NewSelection);
        _lastTriggeredText = text;
        ActivityEntry entry = _log.Add(trigger, window, text);
        _activeEntry = entry;
        _activeReadStartTicks = Stopwatch.GetTimestamp();
        _systemLog.Info($"intercepted via {trigger}, {text.Length} chars", entry.Id);
        return entry;
    }

    private void Supersede(ActivityReason reason)
    {
        ActivityEntry? active = _activeEntry;
        _activeEntry = null;
        CancelWithoutDispose(Interlocked.Exchange(ref _translationCts, null));
        CancelGrace();
        PublishGenerating();
        if (active is null)
        {
            return;
        }

        if (active.State == ActivityState.Pending)
        {
            _log.SetState(active, ActivityState.Ignored, reason);
            _systemLog.Debug($"ignored before reading ({reason})", active.Id);
        }
        else if (active.State is ActivityState.GeneratingAudio or ActivityState.Reading)
        {
            // Stop (not Pause) so the superseded synthesis/playback is cancelled —
            // otherwise a slow long read could still play after this point.
            _reader.Stop();
            _log.SetState(active, ActivityState.Interrupted, reason);
            _systemLog.Info($"interrupted ({reason})", active.Id);
        }
    }

    private async Task ReadEntryAsync(ActivityEntry entry, string text)
    {
        try
        {
            // Synthesis runs first (no audio yet) → GeneratingAudio; the reader's
            // first Playing transition flips it to Reading (OnReaderStateChanged),
            // natural completion → Read (OnReaderCompleted), errors → catch below.
            _synthStartTicks = Stopwatch.GetTimestamp();
            _log.SetState(entry, ActivityState.GeneratingAudio);
            PublishGenerating();
            _systemLog.Debug("generating audio", entry.Id);
            if (await TryStreamReadAsync(entry, text))
            {
                return;
            }

            ReadText spoken = await TextToSpeakAsync(entry, text);
            if (!ReferenceEquals(_activeEntry, entry))
            {
                PublishGenerating(); // the superseding read (or a stop) may have left the flag stale
                return; // superseded while translating: Supersede already finalized the entry
            }

            DropHeadStartIfTranslated(spoken);
            ReadTextChanged?.Invoke(this, spoken);
            await _reader.SpeakAsync(spoken.Text, entry.Id);
            await FailIfNeverPlayedAsync(entry);
        }
        catch (Exception ex)
        {
            _log.SetState(entry, ActivityState.Failed, ActivityReason.Error);
            _systemLog.Error("read failed during synthesis/playback", entry.Id, ex);
            ClearIfActive(entry);
        }
    }

    // Slice 57: every read path funnels through ReadEntryAsync, so translation happens here, once, before
    // the reader sees the text. The reading text box and the chunk source ranges then refer to the text
    // actually spoken. A timeout, a missing pack or a failure reads the original (see ReadTranslator).
    private async Task<ReadText> TextToSpeakAsync(ActivityEntry entry, string text)
    {
        if (_translation is null || !TranslateEnabled)
        {
            return new ReadText(text, false);
        }

        var cts = new CancellationTokenSource();
        CancelWithoutDispose(Interlocked.Exchange(ref _translationCts, cts));
        try
        {
            ReadTranslationResult result = await _translation.PrepareAsync(text, entry.Id, cts.Token);
            if (!cts.IsCancellationRequested && result.IsNotable)
            {
                TranslationFinished?.Invoke(this, result);
            }

            return new ReadText(result.Text, result.Outcome == TranslationOutcome.Translated);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return new ReadText(text, false); // superseded: the caller sees the entry is no longer active
        }
        finally
        {
            Interlocked.CompareExchange(ref _translationCts, null, cts);
            cts.Dispose(); // the owner always disposes: a supersede only cancels, so the token is never used disposed
        }
    }

    // The head start was prepared for the original text; once the read is translated it is not what
    // is spoken, so the engine stops spending effort on it.
    private void DropHeadStartIfTranslated(ReadText spoken)
    {
        if (!spoken.Translated)
        {
            return;
        }

        CancelPendingPrepare(Interlocked.Exchange(ref _prepareCts, null));
        _headStart?.CancelPrepare();
    }

    // SpeakAsync returned but the reader may not have started playing yet (the WinRT
    // fallback only sets the source; Play and the Playing transition come later on
    // another thread). Wait up to FirstAudioGrace for it, so a slow-but-working
    // reader isn't failed, while one that never plays (engine missing, synthesis
    // produced nothing) can't leave the entry, log and spinner stuck forever.
    private async Task FailIfNeverPlayedAsync(ActivityEntry entry)
    {
        if (!IsAwaitingFirstAudio(entry))
        {
            return;
        }

        var cts = new CancellationTokenSource();
        Interlocked.Exchange(ref _graceCts, cts)?.Cancel();
        try
        {
            await Task.Delay(FirstAudioGrace, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (IsAwaitingFirstAudio(entry))
        {
            _log.SetState(entry, ActivityState.Failed, ActivityReason.Error);
            _systemLog.Warning("reader never played audio within the grace window", entry.Id);
            ClearIfActive(entry);
        }
    }

    private bool IsAwaitingFirstAudio(ActivityEntry entry) =>
        ReferenceEquals(_activeEntry, entry) && entry.State == ActivityState.GeneratingAudio;

    private void CancelGrace() => Interlocked.Exchange(ref _graceCts, null)?.Cancel();

    // The reader starting to play flips the active entry from GeneratingAudio (the
    // synthesis wait) to Reading, and records the local timing diagnostics (the
    // first Playing transition is "first audio"). Later Playing transitions (per
    // chunk) are no-ops.
    private void OnReaderStateChanged(object? sender, PlaybackState state)
    {
        if (state == PlaybackState.Playing && _activeEntry is { State: ActivityState.GeneratingAudio } entry)
        {
            TimeSpan firstAudio = Stopwatch.GetElapsedTime(_activeReadStartTicks);
            TimeSpan synth = Stopwatch.GetElapsedTime(_synthStartTicks);
            _log.RecordTiming(entry, firstAudio, synth);
            _log.SetState(entry, ActivityState.Reading);
            CancelGrace();
            PublishGenerating();
            _systemLog.Info(
                $"reading; first audio {firstAudio.TotalMilliseconds:F0} ms, synth {synth.TotalMilliseconds:F0} ms",
                entry.Id);
        }
    }

    // Natural end of playback marks the active read as Read. Stop/supersede does
    // not raise Completed, so an interrupted read is never mis-marked as Read.
    private void OnReaderCompleted(object? sender, EventArgs e)
    {
        if (_activeEntry is { State: ActivityState.GeneratingAudio or ActivityState.Reading } entry)
        {
            _log.SetState(entry, ActivityState.Read);
            _systemLog.Info("read complete", entry.Id);
            _activeEntry = null;
            CancelGrace();
            PublishGenerating();
        }
    }

    private void ClearIfActive(ActivityEntry entry)
    {
        if (ReferenceEquals(_activeEntry, entry))
        {
            _activeEntry = null;
        }

        PublishGenerating();
    }

    // Records the flag under a lock and raises the event on a change only. Handlers
    // must re-read IsGeneratingAudio (see GeneratingChanged).
    private void PublishGenerating()
    {
        bool generating;
        lock (_generatingLock)
        {
            generating = IsGeneratingAudio;
            if (generating == _publishedGenerating)
            {
                return;
            }

            _publishedGenerating = generating;
        }

        GeneratingChanged?.Invoke(this, generating);
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
        CancelWithoutDispose(Interlocked.Exchange(ref _translationCts, null));
        CancelPendingPrepare(Interlocked.Exchange(ref _prepareCts, null));
    }
}
