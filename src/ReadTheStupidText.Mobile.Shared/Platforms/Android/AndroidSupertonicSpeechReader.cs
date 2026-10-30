using System.Collections.Concurrent;
using System.Diagnostics;
using AndroidMedia = Android.Media;
using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using SherpaOnnx;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// Speaks text with the local Supertonic-3 neural engine on Android (Slice 31,
/// Decision 39) — a port of Windows' <c>SupertonicSpeechReader</c> onto
/// <see cref="AndroidMedia.MediaPlayer"/> instead of WinRT's <c>MediaPlayer</c>.
/// The synthesis/chunking/ordered-playback/voice-swap/skip logic is identical
/// (it's pure C# shared via <see cref="SpeechTextChunker"/> and
/// <see cref="ReadTimingTracker"/>, both moved to Application in this slice so
/// both platforms' readers can use them); only the playback primitive differs.
/// </summary>
/// <remarks>
/// Android's <see cref="AndroidMedia.MediaPlayer"/> has no stream-based "play
/// this buffer" API a native-loader-driven engine can target directly, so each
/// synthesized chunk is written to a temp WAV file and played via
/// <c>SetDataSource(path)</c> — mirroring the WinRT reader's in-memory
/// <c>IRandomAccessStream</c>, just file-backed. There is also no
/// position-changed event, so read-through progress/timing are driven by a
/// short poll loop while a chunk plays, rather than an event.
/// </remarks>
public sealed class AndroidSupertonicSpeechReader : ISpeechReader, IHeadStart, IModelScopedReader, IModelScopedSegmentReader, IDisposable
{
    // Same reasoning as the Windows reader: intra-op ONNX threads per synthesis,
    // scaled to the device but capped so a single Generate doesn't over-claim
    // cores that the concurrent chunks below also need.
    private static readonly int SynthesisThreads = Math.Clamp(Environment.ProcessorCount / 2, 2, 4);

    private static readonly int MaxSynthesisConcurrency =
        Math.Clamp(Environment.ProcessorCount / SynthesisThreads, 2, 4);

    private const string WarmUpText = "Ready.";

    // Slice 47: sentence audio is cached under CacheDirectory/sentences, keyed by sentence +
    // voice + engine + language (SentenceCacheKey). A chunk is played from a temp file joined
    // out of its cached sentences, so a cached file is never opened by the player or deleted
    // by playback; only the cache's own eviction removes one.
    private const string SentenceFolderName = "sentences";
    private const string ChunkFilePrefix = "read-chunk-";
    private const string ChunkFileExtension = ".wav";
    private const string ChunkFilePattern = ChunkFilePrefix + "*" + ChunkFileExtension;

    // The cache makes a two-sentence head cheap (prepared ahead), so Android allows it;
    // the shared chunker's default stays one short sentence for the reader without a cache.
    private static readonly SpeechTextChunker.HeadPolicy HeadPolicy = SpeechTextChunker.HeadPolicy.Cached;

    // A head start prepares the opening of the text only: this many chunks (the two-sentence
    // head plus a few after it), not the whole file, so a long document costs no battery.
    private const int PrepareMaxChunks = 4;

    // How many chunks may be generating at once during a read (see StartGenerations).
    private const int GenerationWindow = 8;

    // Only this much of the text is even chunked for a head start: planning the whole of a
    // large document just to keep its first few chunks would burn CPU for nothing.
    private const int PrepareMaxChars = 2000;

    // OfflineTtsGenerationConfig.Extra key that selects the Supertonic language.
    private const string SupertonicLanguageKey = "lang";

    private static readonly TimeSpan TimingRaiseInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SkipAmount = TimeSpan.FromSeconds(10);

    // How often the poll loop checks MediaPlayer's position while a chunk plays.
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

    private readonly IVoiceModelService _model;
    private readonly ISystemLog _log;
    private readonly SentenceAudioSource _sentences;
    private readonly ReadTimingTracker _timing = new();
    private readonly SentenceProgress _sentenceProgress = new();
    private readonly StallReporter _stall;

    // One engine-level pool of native synthesis slots shared by reads and head-start prepares,
    // so their combined concurrency never exceeds MaxSynthesisConcurrency.
    private readonly SemaphoreSlim _slots = new(MaxSynthesisConcurrency);

    private readonly object _gate = new();

    // The built engines (at most two, keyed by model folder and pack version), each with the model it was
    // built from (Slice 51: engine kind, language, voices). Each read captures one BuiltEngine and uses
    // its Model for everything, so a language or pack change mid-read can't mix models.
    private readonly EngineCache<BuiltEngine> _engines = new(CreateEngine);
    private string? _failedModelKey;
    private string? _voiceId;

    // The early start's prepared heads (Slice 139): this reader's own, so no other reader or test sees them.
    private readonly FirstChunkHints _hints = new();
    private readonly ConcurrentDictionary<VoiceEngineKind, string> _voiceByKind = new();
    private AndroidMedia.MediaPlayer? _player;
    private string? _currentChunkFile;
    private string? _previousChunkFile;

    private double _playbackRate = PlaybackRate.Default.Value;
    private PlaybackState _state = PlaybackState.Idle;

    // MediaPlayer.PlaybackParams is only settable once the player has reached
    // Prepared (or later) — calling it right after Reset() (still Idle, between
    // chunks) throws IllegalArgumentException. SetSpeed can fire from the UI at
    // any moment, including that gap, so it must check this rather than assume
    // _player's native state.
    // Guarded by _playerLock, like every call into _player: the poll, the UI and the playback loop
    // would otherwise act on the player between a ready check and the call it guards.
    private bool _playerReady;

    // Serialises all MediaPlayer access with the _playerReady check that guards it. Never held
    // across an await; the player's event handlers only take it briefly (MarkPlayerNotReady).
    private readonly object _playerLock = new();

    // Counts chunks started; a chunk's player handlers ignore events once a newer chunk has begun.
    private int _chunkSequence;

    private TaskCompletionSource<bool>? _chunkEnded;
    private CancellationTokenSource? _pollCts;

    private int _generation;
    private CancellationTokenSource? _synthCts;
    private CancellationTokenSource? _prepareCts;

    // The read between its start and its end (changed under _gate): longer than PlaybackState,
    // which is Idle while the first chunk is still being synthesized. It ends only when its last
    // owner leaves, so a Skip-continued playback keeps it alive after the original call returns.
    // A head-start prepare never runs while it is set.
    private ActiveRead? _activeRead;
    private string? _lastReadVoiceId; // Slice 177: the voice the latest read asked an engine for; its model folder is warmed too

    // Set when a read starts and cleared at its first audio, which logs the wait.
    private long _readStartTicks;
    private int _firstAudioPending;

    private int _chunkCount;
    private int _currentChunkIndex;
    private IReadOnlyList<SpeechChunkPlan> _chunks = [];

    private long _lastTimingRaiseTicks;

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler? Completed;
    public event EventHandler<double>? ProgressChanged;
    public event EventHandler<ReadTiming>? TimingChanged;

    // Raised once each chunk has started playing (after the player starts), with its real source range (Slice 60), as
    // the Windows reader does.
    public event EventHandler<ReadChunk>? ChunkChanged;

    public event EventHandler<SentenceChange>? SentenceChanged;

    // Raised while the read waits for the next chunk's audio (Slice 148); the UI shows its "still getting the voice ready" overlay.
    public event EventHandler<bool>? StallChanged
    {
        add => _stall.Changed += value;
        remove => _stall.Changed -= value;
    }

    public PlaybackState State => _state;

    public bool SupportsSegments => true;

    public bool ReadInProgress
    {
        get
        {
            lock (_gate)
            {
                return _activeRead != null;
            }
        }
    }

    public AndroidSupertonicSpeechReader(IVoiceModelService model, ISystemLog? log = null)
    {
        _stall = new StallReporter(this);
        _model = model;
        _log = log ?? new AndroidSystemLog();
        string cacheRoot = FileSystem.Current.CacheDirectory;
        _sentenceProgress.Changed += change => SentenceChanged?.Invoke(this, change);
        _sentences = new SentenceAudioSource(
            new SentenceAudioCache(new FileSentenceStore(Path.Combine(cacheRoot, SentenceFolderName))), _log);
        PurgeOrphanedChunkFiles(cacheRoot);
        _model.ReadyChanged += OnModelReadyChanged;
    }

    // A killed app leaves its playing chunk's temp file behind; none is in use at startup.
    private static void PurgeOrphanedChunkFiles(string cacheRoot)
    {
        foreach (string orphan in Directory.EnumerateFiles(cacheRoot, ChunkFilePattern))
        {
            TryDelete(orphan);
        }
    }

    // On a cold start the model is only resolved after a background scan, so a warm-up asked
    // for before that found nothing to warm: warm each time the resolved model reports ready (a warm engine is left alone, see WarmUp).
    private void OnModelReadyChanged(object? sender, EventArgs e)
    {
        if (_model.IsReady)
        {
            _ = WarmUpAsync();
        }
    }

    public Task SpeakAsync(string text, int? activityId = null) =>
        StartReadAsync(PlanText(text, null));

    // Slice 60: each segment is chunked on its own and each chunk is synthesized in its
    // segment's voice, so a character keeps one voice for the whole read.
    public Task SpeakSegmentsAsync(IReadOnlyList<VoicedSegment> segments, int? activityId = null) =>
        StartReadAsync(SpeechChunkPlanner.ForSegments(segments, HeadPolicy));

    // Slice 90: a read in the text's own language names its pack; the app-language model is untouched.
    public Task SpeakAsync(string text, VoiceModelPaths model, int? activityId = null) =>
        StartReadAsync(PlanText(text, model), model);

    // Slice 173: voiced segments in the pack the caller names (the child's story language), the app-language model untouched.
    public Task SpeakSegmentsAsync(IReadOnlyList<VoicedSegment> segments, VoiceModelPaths model, int? activityId = null) =>
        StartReadAsync(SpeechChunkPlanner.ForSegments(segments, HeadPolicy), model);

    private async Task StartReadAsync(IReadOnlyList<SpeechChunkPlan> chunks, VoiceModelPaths? model = null)
    {
        // A cold model build takes seconds: never on the caller's (UI) thread.
        BuiltEngine? owned = await Task.Run(() => AcquireEngine(model)) ?? throw new NeuralEngineUnavailableException();
        BuiltEngine engine = owned;

        ActiveRead? read = null;
        try
        {
            read = BeginRead(engine);
            (int generation, CancellationToken token) = BeginGeneration();
            _readStartTicks = Stopwatch.GetTimestamp();
            Volatile.Write(ref _firstAudioPending, 1);

            _chunks = chunks;
            _sentenceProgress.Reset();
            _timing.Start(chunks.Count);
            RaiseTiming(TimeSpan.Zero);

            owned = null; // the playback loop owns the reference from here and gives it back when it moves on
            await SpeakChunksAsync(engine, chunks, 0, Volatile.Read(ref _voiceId), generation, token);
        }
        finally
        {
            if (read is not null)
            {
                LeaveRead(read);
            }

            owned?.Release();
        }
    }

    // Makes this the active read and cancels any prepare, atomically with a prepare's own
    // check, so a prepare either finished its start before the read or never starts.
    private ActiveRead BeginRead(BuiltEngine engine)
    {
        lock (_gate)
        {
            var read = new ActiveRead(engine, new ReadOwnership());
            _activeRead = read;
            CancelPrepareLocked();
            return read;
        }
    }

    // Paired with BeginRead or a successful TryJoin. The read ends with its last owner, and only
    // if a newer read hasn't replaced it (or Stop cleared it) meanwhile.
    private void LeaveRead(ActiveRead read)
    {
        if (!read.Owners.Leave())
        {
            return;
        }

        lock (_gate)
        {
            if (ReferenceEquals(_activeRead, read))
            {
                _activeRead = null;
            }
        }
    }

    public Task SkipForward() => SkipByAsync(SkipAmount);

    public Task SkipBackward() => SkipByAsync(-SkipAmount);

    private async Task SkipByAsync(TimeSpan delta)
    {
        ActiveRead? read;
        lock (_gate)
        {
            read = _activeRead;
        }

        if (read is null || _state == PlaybackState.Idle || !read.Owners.TryJoin())
        {
            return;
        }

        BuiltEngine engine = read.Engine;
        if (!engine.TryAcquire())
        {
            LeaveRead(read);
            return;
        }

        try
        {
            await SkipOnEngineAsync(engine, delta); // takes over the reference taken here
        }
        finally
        {
            LeaveRead(read);
        }
    }

    // Owns the engine reference it is given: the playback loop gives it back, and so does a skip with
    // nowhere to go.
    private async Task SkipOnEngineAsync(BuiltEngine engine, TimeSpan delta)
    {
        TimeSpan elapsed = _timing.CurrentTiming(CurrentChunkPosition()).Elapsed;
        if (_timing.ComputeSkipTarget(elapsed, delta) is not { } target)
        {
            engine.Release();
            return;
        }

        (int generation, CancellationToken newToken) = BeginGeneration();
        _chunkEnded?.TrySetResult(false);
        StopPolling();
        _timing.SeekTo(target);
        RaiseTiming(TimeSpan.Zero);

        await SpeakChunksAsync(engine, _chunks, target.ChunkIndex, Volatile.Read(ref _voiceId), generation, newToken);
    }

    // Same ordered-consume design as the Windows reader — see that file for the
    // full rationale. Speaker is a parameter (not the mutable field) so a
    // mid-read voice change can't half-apply to already-queued chunks. Owns the engine
    // reference it is given and gives it back as soon as a voice change moves the read to
    // another model, so a long read that toggles voices keeps at most two models resident.
    private async Task SpeakChunksAsync(
        BuiltEngine engine, IReadOnlyList<SpeechChunkPlan> chunks, int startIndex, string? voiceId, int generation,
        CancellationToken token)
    {
        try
        {
            while (true)
            {
                _chunkCount = chunks.Count;
                _currentChunkIndex = startIndex;
                ProgressChanged?.Invoke(this, chunks.Count == 0 ? 0 : (double)startIndex / chunks.Count);

                List<Task<string>> generations = StartGenerations(engine, chunks, startIndex, voiceId, token);
                (bool played, VoiceSwap? swap) = await PlayRunAsync(generations, engine, chunks, startIndex, voiceId, generation, token);
                if (swap is null)
                {
                    if (!played)
                    {
                        return;
                    }

                    break;
                }

                (engine, startIndex, voiceId, generation, token) = TakeOver(engine, swap);
            }
        }
        finally
        {
            engine.Release();
        }

        if (IsCurrent(generation))
        {
            FinishNaturalEnd();
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }

    private static (BuiltEngine Engine, int StartIndex, string? Voice, int Generation, CancellationToken Token) TakeOver(
        BuiltEngine current, VoiceSwap swap)
    {
        if (!ReferenceEquals(current, swap.Engine))
        {
            current.Release();
        }

        return (swap.Engine, swap.NextIndex, swap.Voice, swap.Generation, swap.Token);
    }

    // A voice change the read goes on in: the engine for the new voice (the run's own when the model is the
    // same), the chunk to resume at and the generation that replaces the old one.
    private sealed record VoiceSwap(BuiltEngine Engine, int NextIndex, string? Voice, int Generation, CancellationToken Token);

    // Plays the generated chunks in order. Played is false when the read stopped or was superseded; the swap
    // is set when the voice changed and the read goes on in it (the loop above restarts it); otherwise every
    // chunk played.
    private async Task<(bool Played, VoiceSwap? Swap)> PlayRunAsync(
        List<Task<string>> generations, BuiltEngine engine, IReadOnlyList<SpeechChunkPlan> chunks, int startIndex,
        string? voiceId, int generation, CancellationToken token)
    {
        for (int offset = 0; offset < generations.Count; offset++)
        {
            int index = startIndex + offset;
            if (!await PlayGeneratedChunkAsync(generations[offset], chunks, index, generation, token))
            {
                return (false, null);
            }

            if (await RestartForVoiceSwapAsync(engine, chunks, index, voiceId, generation) is { } swap)
            {
                return (false, swap);
            }
        }

        return (true, null);
    }

    // A read that ends by itself leaves the same state Stop() does: the poll loop is stopped
    // and the timer and progress are back at zero, so a stale player never feeds the next
    // tick (the timer used to run on past the total, e.g. 00:09/00:04, and never reset).
    private void FinishNaturalEnd()
    {
        StopPolling();
        _chunkCount = 0;
        ProgressChanged?.Invoke(this, 0);
        _sentenceProgress.Clear();
        _timing.Reset();
        RaiseTiming(TimeSpan.Zero);
        UpdateState(PlaybackState.Idle);
    }

    // Kicks off synthesis of the remaining chunks; the first runs straight away and the rest wait for it, so
    // the chunk the listener waits on is never starved. At most GenerationWindow chunks are in flight: chunk
    // k also waits for chunk k - GenerationWindow to finish, so a long text of cached clips cannot hold
    // thousands of chunks in memory at once (a 300k-character read was killed by lmkd, Slice 148).
    private List<Task<string>> StartGenerations(
        BuiltEngine engine, IReadOnlyList<SpeechChunkPlan> chunks, int startIndex, string? voiceId,
        CancellationToken token)
    {
        SemaphoreSlim slots = _slots;
        var generations = new List<Task<string>>();
        Task<string>? firstChunk = null;
        for (int index = startIndex; index < chunks.Count; index++)
        {
            int behind = index - startIndex - GenerationWindow;
            Task<string> chunkTask = GenerateOnVoiceEngineAsync(
                behind >= 0 ? generations[behind] : firstChunk, engine, chunks[index], index, voiceId, slots, token);
            firstChunk ??= chunkTask;
            generations.Add(chunkTask);
        }

        return generations;
    }

    // A chunk whose voice lives in another model folder of the pack (a cast of pack voices, Slice 89)
    // is built on that folder's engine, loaded off the caller's thread and under no gate: a model load
    // takes seconds and must not hold up the first chunk. The chunk holds a reference on it while it
    // synthesizes. If that model cannot load, the chunk falls back to the run's engine.
    private async Task<string> GenerateOnVoiceEngineAsync(
        Task<string>? gate, BuiltEngine engine, SpeechChunkPlan chunk, int index, string? voiceId,
        SemaphoreSlim slots, CancellationToken token)
    {
        if (gate is not null)
        {
            try
            {
                await gate;
            }
            catch
            {
                // Swallowed: the first chunk's real outcome surfaces on its own await.
            }
        }

        BuiltEngine? extra = await LoadExtraEngineAsync(engine, chunk, voiceId);
        try
        {
            BuiltEngine chunkEngine = extra ?? engine;
            VoiceInfo voice = ResolveVoice(chunkEngine, chunk.VoiceId, voiceId);
            return await GenerateChunkAsync(chunkEngine, chunk.Text, index, voice, chunk.Transform, slots, token);
        }
        finally
        {
            extra?.Release();
        }
    }

    // The engine of another model folder the chunk's voice needs, with a reference for the caller; null
    // when the run's own serves it, or (with a warning) when that model cannot load.
    private async Task<BuiltEngine?> LoadExtraEngineAsync(BuiltEngine engine, SpeechChunkPlan chunk, string? voiceId)
    {
        VoiceInfo wanted = PickVoice(engine.Model, chunk.VoiceId, voiceId);
        if (wanted.ModelDir == engine.Model.ModelDir)
        {
            return null;
        }

        BuiltEngine? built = await Task.Run(() => TryAcquireForVoice(engine.Model, wanted.Id));
        if (built is null)
        {
            _log.Warning($"voice {wanted.Id} could not be loaded; its lines use the read's voice");
        }

        return built;
    }

    // Waits for one chunk's audio and plays it. False when the read was cancelled,
    // stopped or superseded, so the caller stops consuming chunks.
    private async Task<bool> PlayGeneratedChunkAsync(
        Task<string> generated, IReadOnlyList<SpeechChunkPlan> chunks, int index, int generation,
        CancellationToken token)
    {
        string wavPath;
        try
        {
            wavPath = await _stall.WatchAsync(generated);
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        if (!IsCurrent(generation))
        {
            return false;
        }

        _currentChunkIndex = index;
        SpeechChunkPlan chunk = chunks[index];
        var readChunk = new ReadChunk(index, chunks.Count, chunk.Text, chunk.SourceStart, chunk.SourceEnd);
        bool endedNaturally = await PlayChunkAsync(wavPath, readChunk, token);
        if (!endedNaturally || !IsCurrent(generation))
        {
            return false;
        }

        _timing.AdvancePastChunk(index);
        return true;
    }

    // A chunk with its own voice (a segment read) never follows the narrator picker; a plain
    // read restarts at the next chunk when the selected voice changed mid-read. Voices are
    // compared by id within the read's own model, so a language or pack change never
    // restarts a read. Returns the swap the playback loop goes on with, or null when the read
    // carries on as it was.
    private async Task<VoiceSwap?> RestartForVoiceSwapAsync(
        BuiltEngine engine, IReadOnlyList<SpeechChunkPlan> chunks, int index, string? voiceId, int generation)
    {
        string? selected = Volatile.Read(ref _voiceId);
        VoiceInfo target = PickVoice(engine.Model, selected);
        bool sameVoice = target.Id == PickVoice(engine.Model, voiceId).Id;
        if (sameVoice || index + 1 >= chunks.Count || chunks[index + 1].VoiceId is not null)
        {
            return null;
        }

        // A voice with its own model folder needs its own engine (Slice 89), loaded off the playback loop
        // (this runs on the main thread) and under no gate. The engine this read came in on is given back
        // by the loop once the new one is in place (generations still running hold their own reference).
        // If the new model cannot load, the read goes on in the current voice.
        BuiltEngine next = engine;
        if (target.ModelDir != engine.Model.ModelDir)
        {
            next = await Task.Run(() => TryAcquireForVoice(engine.Model, selected)) ?? engine;
        }

        if (!IsCurrent(generation))
        {
            ReleaseUnlessSame(next, engine); // stopped or superseded while the model loaded
            return null;
        }

        (int nextGeneration, CancellationToken nextToken) = BeginGeneration();
        ReplaceActiveEngine(engine, next);
        return new VoiceSwap(next, index + 1, selected, nextGeneration, nextToken);
    }

    private static void ReleaseUnlessSame(BuiltEngine next, BuiltEngine engine)
    {
        if (!ReferenceEquals(next, engine))
        {
            next.Release();
        }
    }

    // Built from the read's own model (Slice 90 reads name their pack), not the app-language one.
    private BuiltEngine? TryAcquireForVoice(VoiceModelPaths model, string? voiceId)
    {
        try
        {
            return AcquireEngine(model, voiceId);
        }
        catch (NeuralEngineUnavailableException)
        {
            return null;
        }
    }

    // Skip must continue on the engine the read now speaks with.
    private void ReplaceActiveEngine(BuiltEngine from, BuiltEngine? to)
    {
        lock (_gate)
        {
            if (to is not null && _activeRead is { } read && ReferenceEquals(read.Engine, from))
            {
                read.Engine = to;
            }
        }
    }

    // The first of the given ids the read's model offers (a chunk's own voice, then the
    // selected one), then the voice last picked for this engine family, then the model's
    // default: a Piper pick for another language never displaces the Supertonic choice.
    // A voice whose model folder differs from the engine's cannot speak on it (Slice 89), so it gives
    // way to the engine's own first voice; a mid-read voice change gets its own engine instead.
    private VoiceInfo ResolveVoice(BuiltEngine engine, params string?[] ids)
    {
        VoiceInfo picked = PickVoice(engine.Model, ids);
        if (picked.ModelDir == engine.Model.ModelDir)
        {
            return picked;
        }

        return engine.Model.AvailableVoices.FirstOrDefault(v => v.ModelDir == engine.Model.ModelDir) ?? picked;
    }

    private VoiceInfo PickVoice(VoiceModelPaths model, params string?[] ids)
    {
        _voiceByKind.TryGetValue(model.Kind, out string? remembered);
        string?[] candidates = [.. ids, Volatile.Read(ref _voiceId), remembered];
        return VoiceChoices.Resolve(model, candidates);
    }

    public Task WarmUpAsync() => Task.Run(WarmUp);

    // Slice 177: warms the engine the resolved model's reads would use. The warm state lives on the engine itself (RefCountedTts): a warm-up
    // synthesises one short sentence only if nothing has synthesised on that engine yet, under the lock a read takes to acquire it, so a
    // read and a warm-up never use the engine at once and a read that got there first has already warmed it. An evicted engine comes back
    // cold and the next trigger warms it. Duplicate triggers are harmless.
    private void WarmUp()
    {
        if (_model.Paths is { } paths)
        {
            WarmEnginesBestEffort(paths);
        }
    }

    // Slice 173: the child's picked language is warmed by the same rules (WarmEngine / Acquire forWarmUp), so the first read in its pack does not build the model.
    public Task WarmUpAsync(VoiceModelPaths model) => Task.Run(() => WarmEnginesBestEffort(model));

    private void WarmEnginesBestEffort(VoiceModelPaths paths)
    {
        try
        {
            WarmEngines(paths);
        }
        catch
        {
            // Best-effort: AcquireEngine() in the read remains the safety net.
        }
    }

    // The default voice's folder and, when the latest read used another folder of the pack (a cast voice, Slice 89), that one too.
    private void WarmEngines(VoiceModelPaths paths)
    {
        WarmEngine(paths, null);
        if (Volatile.Read(ref _lastReadVoiceId) is { } lastVoice)
        {
            WarmEngine(paths, lastVoice);
        }
    }

    private void WarmEngine(VoiceModelPaths paths, string? voiceId)
    {
        BuiltEngine? engine = AcquireEngine(paths, voiceId, forWarmUp: true);
        if (engine is null)
        {
            return;
        }

        try
        {
            engine.Tts.WarmOnce(() => GenerateAudio(engine, WarmUpText, ResolveVoice(engine).SpeakerId).Dispose());
        }
        finally
        {
            engine.Release();
        }
    }

    // A chunk is joined from its sentences' audio, each fetched from the cache or synthesized
    // (Slice 47). The sentences of one chunk are requested together; `slots` bounds how many
    // actually synthesize at once.
    private async Task<string> GenerateChunkAsync(
        BuiltEngine engine, string chunk, int index, VoiceInfo voice, IVoicedAudioTransform? transform, SemaphoreSlim slots,
        CancellationToken token)
    {
        long started = Stopwatch.GetTimestamp();
        IReadOnlyList<string> units = SentenceUnits.Split(chunk);
        if (index == 0)
        {
            LogFirstUnit("read", units, voice);
        }

        (byte[] wav, SentenceTimeline? timeline) = await JoinSentencesAsync(engine, units, voice, transform, slots, token);
        RecordChunkDuration(index, WavFile.TryGetDuration(wav) ?? TimeSpan.Zero);
        _log.Debug($"chunk {index} ({chunk.Length} chars, {units.Count} sentence(s)) ready in " +
            $"{Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms");
        string path = await WriteChunkFileAsync(wav);
        _sentenceProgress.Register(path, timeline);
        return path;
    }

    // Fetches every sentence's clip and joins them. A cached clip that will not join (corrupt,
    // or a different sample rate) is a miss, not a failed read: the chunk's cached clips are
    // dropped and synthesized afresh. A second failure is real and propagates.
    private async Task<(byte[] Wav, SentenceTimeline? Timeline)> JoinSentencesAsync(
        BuiltEngine engine, IReadOnlyList<string> units, VoiceInfo voice, IVoicedAudioTransform? transform, SemaphoreSlim slots,
        CancellationToken token)
    {
        byte[][] clips = await GetClipsAsync(engine, units, voice, transform, slots, token);
        try
        {
            return (JoinClips(clips, transform), TimelineOf(clips, transform));
        }
        catch (InvalidDataException ex)
        {
            _log.Warning("cached sentence audio would not join; resynthesizing the chunk", error: ex);
            units.ToList().ForEach(u => _sentences.Invalidate(KeyFor(engine, u, voice, transform)));
            clips = await GetClipsAsync(engine, units, voice, transform, slots, token);
            return (JoinClips(clips, transform), TimelineOf(clips, transform));
        }
    }

    private async Task<byte[][]> GetClipsAsync(
        BuiltEngine engine, IReadOnlyList<string> units, VoiceInfo voice, IVoicedAudioTransform? transform, SemaphoreSlim slots,
        CancellationToken token)
    {
        byte[][] clips = await Task.WhenAll(units.Select(u => GetSentenceAsync(engine, u, voice, transform, slots, token)));
        token.ThrowIfCancellationRequested();
        return clips;
    }

    private static byte[] JoinClips(byte[][] clips, IVoicedAudioTransform? transform) =>
        clips.Length == 1 ? ValidClip(clips[0]) : WavFile.Concat(clips, VoicedAudio.GapOf(transform));

    private static SentenceTimeline? TimelineOf(byte[][] clips, IVoicedAudioTransform? transform) =>
        SentenceTimeline.FromClips(clips, VoicedAudio.GapOf(transform));

    private static byte[] ValidClip(byte[] clip) =>
        WavFile.IsComplete(clip, clip.LongLength) ? clip : throw new InvalidDataException("Not a WAV written by this app.");

    // The key carries the voice, engine, language and model, so audio is never reused across
    // them; the engine and voice come from the read's own captured model.
    private Task<byte[]> GetSentenceAsync(
        BuiltEngine engine, string sentence, VoiceInfo voice, IVoicedAudioTransform? transform, SemaphoreSlim slots,
        CancellationToken token)
    {
        SentenceCacheKey key = KeyFor(engine, sentence, voice, transform);
        return _sentences.GetClipAsync(
            key, t => SynthesizeSentenceAsync(engine, sentence, voice, transform, slots, t), token);
    }

    private static SentenceCacheKey KeyFor(BuiltEngine engine, string sentence, VoiceInfo voice, IVoicedAudioTransform? transform) =>
        new(sentence, engine.Model.Kind, engine.Model.Language, voice.Id, engine.Model.RootDir, engine.Model.PackVersion, SiblingVoice.CacheTag(voice, transform));

    // The native Generate call cannot be interrupted, and its result may be wanted by another
    // caller that joined this synthesis, so a cancelled caller only stops waiting for a slot;
    // a call already running finishes and its audio goes to whoever is still waiting.
    private async Task<SentenceClip> SynthesizeSentenceAsync(
        BuiltEngine engine, string sentence, VoiceInfo voice, IVoicedAudioTransform? transform, SemaphoreSlim slots,
        CancellationToken token)
    {
        RescueResult rescue;
        await slots.WaitAsync(token);
        try
        {
            float pace = VoicedAudio.PaceOf(transform);
            token.ThrowIfCancellationRequested(); // superseded while queued: never start the native call
            rescue = await SilentOutputGuard.RescueUnitAsync(
                sentence, voice.Id, pace, SiblingVoice.IsDropProne(voice), phrase => TakeAsync(engine, phrase, voice.SpeakerId, pace),
                message => _log.Warning(message), token, phrase => SiblingTakeAsync(engine, phrase, voice, pace, slots, token));
        }
        finally
        {
            slots.Release();
        }

        // The product's voice transform is plain DSP: it runs after the slot is released so the next sentence can start synthesising.
        // A take the guard gave up on is played this once but never cached, so the next read tries again.
        SpokenTake take = rescue.Take;
        return new SentenceClip(WavFile.Encode(VoicedAudio.Apply(transform, take.Samples, take.SampleRate), take.SampleRate), !rescue.GaveUp);
    }

    // The pack's other voice speaks the unit when this one stays silent (Slice 149; shared logic in SiblingRescue).
    private Task<SpokenTake?> SiblingTakeAsync(BuiltEngine engine, string text, VoiceInfo voice, float pace, SemaphoreSlim slots, CancellationToken token) =>
        SiblingRescue.TakeAsync(
            engine, engine.Model, voice, text,
            new SiblingEngineOps<BuiltEngine>(
                paths => _engines.Acquire(paths), e => e.Release(), (e, speakerId) => TakeAsync(e, text, speakerId, pace), slots,
                (m, e) => _log.Warning(m, error: e)),
            token);

    // One take of the voice, copied out so the native audio is released at once (Slice 149).
    private static async Task<SpokenTake> TakeAsync(BuiltEngine engine, string text, int speakerId, float pace)
    {
        OfflineTtsGeneratedAudio audio = await GenerateGuardedAsync(engine, text, speakerId, pace);
        try
        {
            return new SpokenTake(audio.Samples, audio.SampleRate);
        }
        finally
        {
            audio.Dispose();
        }
    }

    // The native Generate call keeps running after its caller is cancelled, so it holds its
    // own reference on the engine, released by the task itself when the call returns (success,
    // fault or abandonment alike, so the count stays balanced): a retired engine is disposed
    // only once it returns.
    private static Task<OfflineTtsGeneratedAudio> GenerateGuardedAsync(BuiltEngine engine, string chunk, int speakerId, float pace)
    {
        if (!engine.TryAcquire())
        {
            throw new ObjectDisposedException(nameof(AndroidSupertonicSpeechReader));
        }

        return Task.Run(() =>
        {
            try
            {
                return GenerateTracked(engine, chunk, speakerId, pace);
            }
            finally
            {
                engine.Release();
            }
        });
    }

    private void RecordChunkDuration(int index, TimeSpan duration)
    {
        bool totalWasUnknown = _timing.CurrentTiming(TimeSpan.Zero).Total is null;
        _timing.RecordChunkDuration(index, duration);
        if (totalWasUnknown && _timing.CurrentTiming(TimeSpan.Zero).Total is not null)
        {
            RaiseTiming(CurrentChunkPosition());
        }
    }

    // Plays one chunk's WAV file and awaits its natural end (true) or a
    // stop/supersede (false). The previous chunk's temp file is deleted once
    // this one has taken over the player (it's no longer open by then).
    private async Task<bool> PlayChunkAsync(string wavPath, ReadChunk readChunk, CancellationToken token)
    {
        var ended = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var prepared = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _chunkEnded = ended;
        int sequence = Interlocked.Increment(ref _chunkSequence);

        AndroidMedia.MediaPlayer player = EnsurePlayer();
        using var subscription = new PlayerSubscription(
            player,
            (s, e) => HandlePrepared(sequence, prepared),
            (s, e) => HandleChunkCompleted(sequence, ended),
            (s, e) => HandlePlayerError(e, sequence, prepared, ended));
        if (!BeginPrepare(player, wavPath, token))
        {
            DropChunkFile(wavPath);
            return false;
        }

        return await PrepareAndPlayAsync(player, readChunk, sequence, prepared, ended, token);
    }

    // Resets the player and starts preparing the chunk in one step under the player lock, so a Stop
    // or Pause cannot land between them. False when the read was stopped or superseded meanwhile.
    private bool BeginPrepare(AndroidMedia.MediaPlayer player, string wavPath, CancellationToken token)
    {
        lock (_playerLock)
        {
            if (token.IsCancellationRequested)
            {
                return false;
            }

            ResetPlayerForChunk(player, wavPath);
            player.SetDataSource(wavPath);
            player.PrepareAsync();
            return true;
        }
    }

    // Called under _playerLock.
    private void ResetPlayerForChunk(AndroidMedia.MediaPlayer player, string wavPath)
    {
        MarkPlayerNotReady();
        StopPolling();
        player.Reset();
        _sentenceProgress.Select(wavPath);
        DeletePreviousChunkFile();
        _previousChunkFile = _currentChunkFile;
        _currentChunkFile = wavPath;
    }

    // The flag goes down before the player is touched.
    private void MarkPlayerNotReady()
    {
        lock (_playerLock)
        {
            _playerReady = false;
        }
    }

    private bool IsCurrentChunk(int sequence) => Volatile.Read(ref _chunkSequence) == sequence;

    private void HandlePrepared(int sequence, TaskCompletionSource<bool> prepared)
    {
        if (IsCurrentChunk(sequence))
        {
            prepared.TrySetResult(true);
        }
    }

    private async Task<bool> PrepareAndPlayAsync(AndroidMedia.MediaPlayer player, ReadChunk readChunk, int sequence,
        TaskCompletionSource<bool> prepared, TaskCompletionSource<bool> ended, CancellationToken token)
    {
        if (!await WaitPreparedAsync(prepared, ended, token))
        {
            // Stop/skip/supersede ends the chunk (false); a prepare error already completed it (true).
            return !token.IsCancellationRequested && ended.Task is { IsCompletedSuccessfully: true, Result: true };
        }

        StartPreparedChunk(player, readChunk, sequence, token);

        using (token.Register(() => ended.TrySetResult(false)))
        {
            return await ended.Task;
        }
    }

    // A Reset() during PREPARING raises no Prepared, so a cancel or a chunk end must release this wait.
    private static async Task<bool> WaitPreparedAsync(
        TaskCompletionSource<bool> prepared, TaskCompletionSource<bool> ended, CancellationToken token)
    {
        using (token.Register(() => prepared.TrySetResult(false)))
        {
            await Task.WhenAny(prepared.Task, ended.Task);
            return prepared.Task.IsCompletedSuccessfully && prepared.Task.Result;
        }
    }

    // A pause pressed while the chunk was preparing is honoured: the chunk stays prepared, not playing.
    // The state check, the Start and the state change are one step under the player lock, so a
    // Pause or Resume lands either before it (and is honoured) or after it (and acts on a started chunk).
    private void StartPreparedChunk(
        AndroidMedia.MediaPlayer player, ReadChunk readChunk, int sequence, CancellationToken token)
    {
        PlaybackState state;
        bool changed;
        string? chunkFile;
        lock (_playerLock)
        {
            if (token.IsCancellationRequested || !IsCurrentChunk(sequence))
            {
                return;
            }

            chunkFile = _currentChunkFile;
            _playerReady = true;
            ApplyPlaybackRate(player);
            state = _state == PlaybackState.Paused ? PlaybackState.Paused : PlaybackState.Playing;
            if (state == PlaybackState.Playing)
            {
                player.Start();
            }

            changed = SwapState(state);
            StartPolling(player, token);
        }

        LogFirstAudio();
        _sentenceProgress.Begin(readChunk.Index, chunkFile, () => ChunkChanged?.Invoke(this, readChunk));
        RaiseStateChanged(changed, state);
    }

    private void RaiseSentence(TimeSpan position) => _sentenceProgress.Update(position);

    // The chunk played to its end: nothing left to poll or query until the next chunk is prepared.
    private void HandleChunkCompleted(int sequence, TaskCompletionSource<bool> ended)
    {
        if (!IsCurrentChunk(sequence))
        {
            return;
        }

        MarkPlayerNotReady();
        StopPolling();
        ended.TrySetResult(true);
    }

    // A MediaPlayer error (e.g. -38 on a state misuse) is logged and marked handled, so Android
    // raises no Completion that would pass for a natural end. The failed prepare is released and
    // the playing chunk ends once, on this chunk's own completion sources.
    // A mid-play error still ends the chunk and the read moves on (logged as a warning). A superseded
    // chunk's error is marked handled and otherwise ignored.
    private void HandlePlayerError(AndroidMedia.MediaPlayer.ErrorEventArgs e, int sequence,
        TaskCompletionSource<bool> prepared, TaskCompletionSource<bool> ended)
    {
        e.Handled = true;
        if (!IsCurrentChunk(sequence))
        {
            return;
        }

        MarkPlayerNotReady();
        StopPolling();
        _log.Warning($"media player error what={e.What} extra={e.Extra}; the chunk is skipped");
        prepared.TrySetResult(false);
        ended.TrySetResult(true);
    }

    private void LogFirstAudio()
    {
        if (Interlocked.Exchange(ref _firstAudioPending, 0) == 1)
        {
            _log.Debug($"first audio after {Stopwatch.GetElapsedTime(_readStartTicks).TotalMilliseconds:F0} ms");
        }
    }

    // Only a prepared player may be paused or started; between chunks the state alone records the
    // request and StartPreparedChunk honours it.
    public void Pause()
    {
        bool changed;
        lock (_playerLock)
        {
            if (_playerReady && _player is { } player)
            {
                player.Pause();
            }

            changed = SwapState(PlaybackState.Paused);
        }

        RaiseStateChanged(changed, PlaybackState.Paused);
    }

    // Only a paused reader resumes. A chunk still preparing is started by StartPreparedChunk, which
    // sees the Playing state set here.
    public void Resume()
    {
        bool changed;
        lock (_playerLock)
        {
            if (_state != PlaybackState.Paused || _player is not { } player)
            {
                return;
            }

            if (_playerReady)
            {
                player.Start();
            }

            changed = SwapState(PlaybackState.Playing);
        }

        RaiseStateChanged(changed, PlaybackState.Playing);
    }

    public void Stop()
    {
        lock (_gate)
        {
            _synthCts?.Cancel();
            _generation++;
            _activeRead = null;
        }

        _chunkEnded?.TrySetResult(false);
        StopPolling();
        lock (_playerLock)
        {
            _playerReady = false;
            _player?.Reset();
        }

        DeletePreviousChunkFile();
        DeleteCurrentChunkFile();
        _chunkCount = 0;
        ProgressChanged?.Invoke(this, 0);
        _sentenceProgress.Clear();
        _timing.Reset();
        RaiseTiming(TimeSpan.Zero);
        UpdateState(PlaybackState.Idle);
    }

    public void SetSpeed(PlaybackRate speed)
    {
        _playbackRate = speed.Value;
        lock (_playerLock)
        {
            if (_playerReady && _player is { } player)
            {
                ApplyPlaybackRate(player);
            }
        }
    }

    public void SetVoice(string voiceId)
    {
        if (VoiceChoices.KindOf(voiceId) is { } kind)
        {
            _voiceByKind[kind] = voiceId;
        }

        Volatile.Write(ref _voiceId, voiceId);
    }

    // Supertonic chooses its language per generation (Extra "lang"); the VITS-family
    // models are single-language and take only the speaker id. Both come from the model
    // the read captured, never from whatever is current now.
    // The pace is 1 unless a voice transform changes it (Slice 113).
    private static OfflineTtsGeneratedAudio GenerateAudio(BuiltEngine engine, string text, int speakerId, float pace = 1.0f)
    {
        VoiceModelPaths model = engine.Model;
        if (model.Kind != VoiceEngineKind.Supertonic)
        {
            return engine.Tts.Tts.Generate(text, pace, speakerId);
        }

        var config = new OfflineTtsGenerationConfig { Sid = speakerId, Speed = pace };
        config.Extra[SupertonicLanguageKey] = model.Language;
        return engine.Tts.Tts.GenerateWithConfig(text, config, KeepGenerating);
    }

    // Slice 177: a read's synthesis waits for a warm-up in progress and marks the engine warm only once it has completed.
    private static OfflineTtsGeneratedAudio GenerateTracked(BuiltEngine engine, string text, int speakerId, float pace)
    {
        engine.Tts.BeginSynthesis();
        bool completed = false;
        try
        {
            OfflineTtsGeneratedAudio audio = GenerateAudio(engine, text, speakerId, pace);
            completed = true;
            return audio;
        }
        finally
        {
            engine.Tts.EndSynthesis(completed);
        }
    }

    // Returning 1 tells sherpa-onnx to keep generating (0 would stop early).
    private static int KeepGenerating(IntPtr samples, int count, float progress, IntPtr arg) => 1;

    // Returns the engine for the language's current model with a reference taken for the
    // caller, who must Release() it. A different pack folder, version or engine kind is another
    // engine (a pack update is a new folder, so it is picked up here); the same folder under
    // another language reuses the model (a Kokoro pack excepted: each language has an engine of its own). An engine nobody asks for any more is disposed once the
    // last read or generation holding it lets go.
    // A warm-up (Slice 177) is refused (null) rather than evict an engine that is in use.
    private BuiltEngine? AcquireEngine(VoiceModelPaths? model = null, string? voiceId = null, bool forWarmUp = false)
    {
        if ((model ?? _model.Paths) is not { } current)
        {
            return null;
        }

        // Aimed at the voice that will speak: voices of one pack can each have their own model folder.
        VoiceModelPaths paths = current.ForVoice(PickVoice(current, voiceId));
        if (!forWarmUp && voiceId is not null)
        {
            Volatile.Write(ref _lastReadVoiceId, voiceId);
        }

        if (ModelKey(paths) == Volatile.Read(ref _failedModelKey))
        {
            // This exact model already failed to load: don't retry the multi-hundred-MB build on every
            // chunk or read. A different pack folder, engine or version clears the block.
            throw new NeuralEngineUnavailableException();
        }

        try
        {
            return AcquireEngineCore(paths, forWarmUp);
        }
        catch (Exception ex) when (ex is not NeuralEngineUnavailableException)
        {
            Volatile.Write(ref _failedModelKey, ModelKey(paths));
            _log.Warning("the neural engine could not be built", error: ex);
            RefreshModelInBackground();
            throw new NeuralEngineUnavailableException(ex);
        }
    }

    private static string ModelKey(VoiceModelPaths paths) => $"{paths.RootDir}|{paths.Kind}|{paths.PackVersion}|{paths.ModelDir}|{KokoroLanguageOf(paths)}";

    // A Kokoro engine is configured for one language (Slice 118), so a failure to build one is that language's only; any other engine kind has no language in its key.
    private static string KokoroLanguageOf(VoiceModelPaths paths) => paths.Kind == VoiceEngineKind.Kokoro ? paths.Language : string.Empty;

    // A pack that is "ready" but cannot be loaded: the model re-checks the installed packs.
    private void RefreshModelInBackground() =>
        _ = _model.InitializeAsync().ContinueWith(
            t => _log.Warning("voice model refresh failed", error: t.Exception),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

    private BuiltEngine? AcquireEngineCore(VoiceModelPaths paths, bool forWarmUp) => _engines.Acquire(paths, forWarmUp);

    // The engine for a model folder: the live one of the same folder and pack version re-labelled for the
    // new language (it shares the loaded model), else a freshly built one.
    private static BuiltEngine CreateEngine(BuiltEngine? previous, VoiceModelPaths paths) =>
        previous is not null && previous.Tts.TryAcquire()
            ? new BuiltEngine(previous.Tts, paths)
            : new BuiltEngine(new RefCountedTts(BuildTts(paths)), paths);

    private static OfflineTts BuildTts(VoiceModelPaths paths)
    {
        var config = new OfflineTtsConfig();
        switch (paths.Kind)
        {
            case VoiceEngineKind.Supertonic:
                ConfigureSupertonic(ref config, paths.RootDir);
                break;
            case VoiceEngineKind.Vits:
                ConfigureVits(ref config, paths);
                break;
            case VoiceEngineKind.Melo:
                ConfigureMelo(ref config, paths.RootDir);
                break;
            case VoiceEngineKind.Kokoro:
                KokoroEngineConfig.Configure(ref config, paths.RootDir, paths.Language);
                break;
            default:
                throw new NotSupportedException($"No engine for voice model kind {paths.Kind}.");
        }

        config.Model.NumThreads = SynthesisThreads;
        config.Model.Provider = "cpu";
        RequireModelFiles(paths, config);
        RequirePiperTokens(paths, config);
        return new OfflineTts(config);
    }

    // OfflineTtsConfig and its nested model configs are structs, so a Configure* helper must
    // take the config by ref: a by-value copy silently drops every path. The native
    // constructor reports a rejected config only by returning a null handle, which the C#
    // wrapper never checks (the first Generate then logs "tts is nullptr"), so a missing
    // model file is turned into an exception here, with the path in it.
    private static void RequireModelFiles(VoiceModelPaths paths, OfflineTtsConfig config)
    {
        string[] required = paths.Kind switch
        {
            VoiceEngineKind.Supertonic =>
            [
                config.Model.Supertonic.DurationPredictor, config.Model.Supertonic.TextEncoder,
                config.Model.Supertonic.VectorEstimator, config.Model.Supertonic.Vocoder,
                config.Model.Supertonic.TtsJson, config.Model.Supertonic.UnicodeIndexer,
                config.Model.Supertonic.VoiceStyle,
            ],
            VoiceEngineKind.Kokoro => KokoroEngineConfig.RequiredPaths(config),
            _ => [config.Model.Vits.Model, config.Model.Vits.Tokens],
        };

        foreach (string file in required)
        {
            if (string.IsNullOrEmpty(file) || !(File.Exists(file) || Directory.Exists(file)))
            {
                string message = $"Voice model file missing for {paths.Kind}: '{file}'.";
                System.Diagnostics.Debug.WriteLine(message);
                throw new FileNotFoundException(message, file);
            }
        }
    }

    // Slice 201: sherpa-onnx's Piper reader aborts the whole process (no managed exception) on a tokens.txt token of
    // more than one code point, so a Piper pack is checked here, after the install check, before the native load.
    private static void RequirePiperTokens(VoiceModelPaths paths, OfflineTtsConfig config)
    {
        if (paths.Kind != VoiceEngineKind.Vits)
        {
            return;
        }

        string? problem = PiperTokens.ProblemInFile(config.Model.Vits.Tokens);
        if (problem is not null)
        {
            throw new InvalidDataException($"Piper tokens file '{config.Model.Vits.Tokens}' would crash the speech engine: {problem}.");
        }
    }

    private static void ConfigureSupertonic(ref OfflineTtsConfig config, string dir)
    {
        config.Model.Supertonic.DurationPredictor = Path.Combine(dir, SupertonicFiles.DurationPredictor);
        config.Model.Supertonic.TextEncoder = Path.Combine(dir, SupertonicFiles.TextEncoder);
        config.Model.Supertonic.VectorEstimator = Path.Combine(dir, SupertonicFiles.VectorEstimator);
        config.Model.Supertonic.Vocoder = Path.Combine(dir, SupertonicFiles.Vocoder);
        config.Model.Supertonic.TtsJson = Path.Combine(dir, SupertonicFiles.TtsJson);
        config.Model.Supertonic.UnicodeIndexer = Path.Combine(dir, SupertonicFiles.UnicodeIndexer);
        config.Model.Supertonic.VoiceStyle = Path.Combine(dir, SupertonicFiles.VoiceStyle);
    }

    // Piper: the espeak-ng data directory phonemizes the text (it lives only in the pack).
    // The voice's own model + tokens may sit in a folder of the pack (Slice 89); the data directory
    // is shared and stays at the pack root.
    private static void ConfigureVits(ref OfflineTtsConfig config, VoiceModelPaths paths)
    {
        config.Model.Vits.Model = Path.Combine(paths.ModelFolder, VitsFiles.Model);
        config.Model.Vits.Tokens = Path.Combine(paths.ModelFolder, VitsFiles.Tokens);
        config.Model.Vits.DataDir = Path.Combine(paths.RootDir, VitsFiles.EspeakDataDir);
    }

    // MeloTTS zh_en: lexicon + jieba dictionary, with the phone/date/number rule FSTs the
    // upstream example uses so digits and dates are spoken as words.
    private static void ConfigureMelo(ref OfflineTtsConfig config, string dir)
    {
        config.Model.Vits.Model = Path.Combine(dir, MeloFiles.Model);
        config.Model.Vits.Lexicon = Path.Combine(dir, MeloFiles.Lexicon);
        config.Model.Vits.Tokens = Path.Combine(dir, MeloFiles.Tokens);
        config.Model.Vits.DictDir = Path.Combine(dir, MeloFiles.DictDir);
        config.RuleFsts = string.Join(
            ',',
            MeloFiles.RuleFsts.Select(file => Path.Combine(dir, file)));
    }

    // Head start (Slice 47): synthesizes the sentences of the text's first chunks into the
    // cache in the voice that would read them now, without playing. A newer prepare, the start
    // of a read, CancelPrepare and Dispose all cancel it; audio already cached stays.
    public Task PrepareAsync(string text) => PrepareAsync(text, null);

    // Slice 90: the head start for the text's own language prepares with that language's pack.
    public Task PrepareAsync(string text, VoiceModelPaths? model)
    {
        if (BlankText.IsBlank(text))
        {
            return HeadStartRun.Skipped;
        }

        // The token is taken here, synchronously and in call order, so a later call always
        // supersedes an earlier one and CancelPrepare reaches a prepare still queued on the pool.
        // The task completes normally only when the clips are stored (HeadStartRun).
        return BeginPrepare() is { } token
            ? Task.Run(() => HeadStartRun.CompleteAsync(token, t => PrepareOffCallerAsync(text, model, t)))
            : HeadStartRun.Skipped;
    }

    // Runs on the thread pool, so acquiring the engine (which may build the model), planning the
    // chunks and indexing the cache never block the UI thread that asked.
    private async Task<bool> PrepareOffCallerAsync(string text, VoiceModelPaths? model, CancellationToken token)
    {
        BuiltEngine? engine = null;
        try
        {
            if (token.IsCancellationRequested || (engine = AcquireEngine(model)) is null)
            {
                return false;
            }

            token.ThrowIfCancellationRequested();
            await PrepareChunksAsync(engine, text, model, token);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.Warning("head start failed", error: ex);
            return false;
        }
        finally
        {
            engine?.Release();
        }
    }

    // Debug builds only (the call is compiled out of Release, so user text is never logged there): lets an emulator run match
    // the early voice's first cache key with the read's. The first 30 text elements, never cut inside a surrogate pair.
    [Conditional("DEBUG")]
    private void LogFirstUnit(string path, IReadOnlyList<string> units, VoiceInfo voice)
    {
        string text = units.Count > 0 ? SentenceCacheKey.Normalize(units[0]) : string.Empty;
        _log.Debug($"first unit ({path}, voice {voice.Id}): {FirstTextElements(text, 30)}");
    }

    private static string FirstTextElements(string text, int count)
    {
        var elements = System.Globalization.StringInfo.GetTextElementEnumerator(text);
        int end = 0;
        for (int i = 0; i < count && elements.MoveNext(); i++)
        {
            end = elements.ElementIndex + elements.GetTextElement().Length;
        }

        return text[..end];
    }

    private async Task PrepareChunksAsync(BuiltEngine engine, string text, VoiceModelPaths? model, CancellationToken token)
    {
        string? voiceId = Volatile.Read(ref _voiceId);
        VoiceInfo voice = ResolveVoice(engine, voiceId);
        string prefix = text.Length > PrepareMaxChars ? text[..PrepareMaxChars] : text;
        List<string> units = SpeechChunkPlanner.ForText(prefix, HeadPolicy)
            .Take(PrepareMaxChunks)
            .SelectMany(chunk => SentenceUnits.Split(chunk.Text))
            .ToList();

        LogFirstUnit("early", units, voice);
        await Task.WhenAll(units.Select(s => GetSentenceAsync(engine, s, voice, null, _slots, token)));
        _hints.Remember(text, FirstChunkHints.ScopeOf(voiceId, model), HeadPolicy); // only now that the units are stored
    }

    // The head the early start prepared for this voice and pack, if the text starts with it; consumed by this read.
    private IReadOnlyList<SpeechChunkPlan> PlanText(string text, VoiceModelPaths? model) =>
        SpeechChunkPlanner.ForText(
            text, HeadPolicy, firstChunkHint: _hints.Take(text, FirstChunkHints.ScopeOf(Volatile.Read(ref _voiceId), model)));

    public void CancelPrepare()
    {
        lock (_gate)
        {
            CancelPrepareLocked();
        }
    }

    // Called under _gate.
    private void CancelPrepareLocked()
    {
        _prepareCts?.Cancel();
        _prepareCts?.Dispose();
        _prepareCts = null;
    }

    // A newer prepare supersedes the one in flight; null when a read is in progress, which
    // must never share the engine with a prepare. Checked under the same lock that
    // BeginRead takes.
    private CancellationToken? BeginPrepare()
    {
        lock (_gate)
        {
            if (_activeRead != null)
            {
                return null;
            }

            CancelPrepareLocked();
            _prepareCts = new CancellationTokenSource();
            return _prepareCts.Token;
        }
    }

    private (int generation, CancellationToken token) BeginGeneration()
    {
        lock (_gate)
        {
            _synthCts?.Cancel();
            _synthCts?.Dispose();
            _synthCts = new CancellationTokenSource();
            return (++_generation, _synthCts.Token);
        }
    }

    private bool IsCurrent(int generation) => Volatile.Read(ref _generation) == generation;

    private AndroidMedia.MediaPlayer EnsurePlayer()
    {
        lock (_playerLock)
        {
            return _player ??= new AndroidMedia.MediaPlayer();
        }
    }

    // Called under _playerLock.
    private void ApplyPlaybackRate(AndroidMedia.MediaPlayer player)
    {
        bool wasPlaying = player.IsPlaying;
        var parameters = new AndroidMedia.PlaybackParams();
        parameters.SetSpeed((float)_playbackRate);
        player.PlaybackParams = parameters;
        if (wasPlaying && !player.IsPlaying)
        {
            player.Start();
        }
    }

    // Android has no PositionChanged event, so read-through progress/timing are
    // driven by a short poll loop for as long as the current chunk plays —
    // functionally the same signal the WinRT reader gets from an event, just
    // sampled instead of pushed.
    private void StartPolling(AndroidMedia.MediaPlayer player, CancellationToken stopToken)
    {
        StopPolling();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(stopToken);
        Volatile.Write(ref _pollCts, cts);
        _ = PollLoopAsync(player, cts.Token);
    }

    // Callable from the Android looper thread too: take the source atomically so it is cancelled
    // and disposed exactly once.
    private void StopPolling()
    {
        CancellationTokenSource? cts = Interlocked.Exchange(ref _pollCts, null);
        cts?.Cancel();
        cts?.Dispose();
    }

    private async Task PollLoopAsync(AndroidMedia.MediaPlayer player, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(PollInterval, token);
                ReportProgress(player);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on stop/skip/supersede.
        }
    }

    private void ReportProgress(AndroidMedia.MediaPlayer player)
    {
        int chunkCount = Volatile.Read(ref _chunkCount);
        int chunkIndex = Volatile.Read(ref _currentChunkIndex);
        if (chunkCount == 0 || !TryReadPlayer(player, out int duration, out int position))
        {
            return;
        }

        double withinChunk = duration > 0 ? (double)position / duration : 0;
        double fraction = (chunkIndex + Math.Clamp(withinChunk, 0, 1)) / chunkCount;
        ProgressChanged?.Invoke(this, Math.Clamp(fraction, 0, 1));
        RaiseSentence(TimeSpan.FromMilliseconds(position));

        if (Stopwatch.GetElapsedTime(_lastTimingRaiseTicks) >= TimingRaiseInterval)
        {
            RaiseTiming(CurrentChunkPosition());
        }
    }

    // The player is queried only while prepared, under the lock that serialises it against Reset.
    private bool TryReadPlayer(AndroidMedia.MediaPlayer player, out int duration, out int position)
    {
        lock (_playerLock)
        {
            duration = _playerReady ? player.Duration : 0;
            position = _playerReady ? player.CurrentPosition : 0;
            return _playerReady;
        }
    }

    // Zero unless a chunk is actually playing (player prepared and the reader not Idle), so a
    // finished or reset player can never leak its position into the next read's timer.
    private TimeSpan CurrentChunkPosition()
    {
        lock (_playerLock)
        {
            return _playerReady && _state != PlaybackState.Idle && _player is { } player
                ? TimeSpan.FromMilliseconds(player.CurrentPosition)
                : TimeSpan.Zero;
        }
    }

    private void RaiseTiming(TimeSpan positionInCurrentChunk)
    {
        _lastTimingRaiseTicks = Stopwatch.GetTimestamp();
        TimingChanged?.Invoke(this, _timing.CurrentTiming(positionInCurrentChunk));
    }

    private void UpdateState(PlaybackState next) => RaiseStateChanged(SwapState(next), next);

    // True when the state changed. Callers holding _playerLock raise the event after releasing it.
    private bool SwapState(PlaybackState next)
    {
        lock (_playerLock)
        {
            if (next == _state)
            {
                return false;
            }

            _state = next;
            return true;
        }
    }

    private void RaiseStateChanged(bool changed, PlaybackState state)
    {
        if (changed)
        {
            StateChanged?.Invoke(this, state);
        }
    }

    private static async Task<string> WriteChunkFileAsync(byte[] wav)
    {
        string path = Path.Combine(FileSystem.Current.CacheDirectory, $"{ChunkFilePrefix}{Guid.NewGuid():N}{ChunkFileExtension}");
        await File.WriteAllBytesAsync(path, wav);
        return path;
    }

    private void DeletePreviousChunkFile()
    {
        if (_previousChunkFile is { } path)
        {
            DropChunkFile(path);
            _previousChunkFile = null;
        }
    }

    private void DeleteCurrentChunkFile()
    {
        if (_currentChunkFile is { } path)
        {
            DropChunkFile(path);
            _currentChunkFile = null;
        }
    }

    // Deletes a generated chunk file and forgets its sentence timeline.
    private void DropChunkFile(string path)
    {
        _sentenceProgress.Discard(path);
        TryDelete(path);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Best-effort cleanup; the cache directory is periodically reclaimed
            // by the OS regardless.
        }
    }

    public void Dispose()
    {
        _model.ReadyChanged -= OnModelReadyChanged;
        StopPolling();
        CancelPrepare();
        _synthCts?.Cancel();
        _synthCts?.Dispose();
        lock (_playerLock)
        {
            _playerReady = false;
            _player?.Release();
            _player = null;
        }

        DeletePreviousChunkFile();
        DeleteCurrentChunkFile();
        _engines.Dispose();
    }

    /// <summary>Subscribes one chunk's handlers to the shared player and unsubscribes them on dispose.</summary>
    private sealed class PlayerSubscription : IDisposable
    {
        private readonly AndroidMedia.MediaPlayer _player;
        private readonly EventHandler _onPrepared;
        private readonly EventHandler _onCompleted;
        private readonly EventHandler<AndroidMedia.MediaPlayer.ErrorEventArgs> _onError;

        public PlayerSubscription(AndroidMedia.MediaPlayer player, EventHandler onPrepared, EventHandler onCompleted,
            EventHandler<AndroidMedia.MediaPlayer.ErrorEventArgs> onError)
        {
            _player = player;
            _onPrepared = onPrepared;
            _onCompleted = onCompleted;
            _onError = onError;
            _player.Prepared += _onPrepared;
            _player.Completion += _onCompleted;
            _player.Error += _onError;
        }

        public void Dispose()
        {
            _player.Prepared -= _onPrepared;
            _player.Completion -= _onCompleted;
            _player.Error -= _onError;
        }
    }

    /// <summary>An <see cref="OfflineTts"/> disposed when its last holder releases it. The
    /// reader holds one reference while the engine is current; each read and each in-flight
    /// generation holds another.</summary>
    private sealed class RefCountedTts(OfflineTts tts)
    {
        private int _references = 1;
        private readonly object _warmLock = new();
        private bool _synthesized;
        private int _active;

        public OfflineTts Tts { get; } = tts;

        public bool IsAlive => Volatile.Read(ref _references) > 0;

        /// <summary>Slice 177: a read is about to synthesise here; waits for a warm-up in progress, and keeps a new warm-up off the engine until <see cref="EndSynthesis"/>.</summary>
        public void BeginSynthesis()
        {
            lock (_warmLock)
            {
                _active++;
            }
        }

        /// <summary>A synthesis begun with <see cref="BeginSynthesis"/> ended; only one that completed makes the engine warm.</summary>
        public void EndSynthesis(bool completed)
        {
            lock (_warmLock)
            {
                _active--;
                _synthesized |= completed;
            }
        }

        /// <summary>Runs <paramref name="synthesize"/> only if nothing has synthesised on this engine yet and no read is synthesising on it; the engine counts as warm once it completes.</summary>
        public void WarmOnce(Action synthesize)
        {
            lock (_warmLock)
            {
                if (_synthesized || _active > 0)
                {
                    return;
                }

                synthesize();
                _synthesized = true;
            }
        }

        /// <summary>References held right now, the cache's own included.</summary>
        public int References => Volatile.Read(ref _references);

        public bool TryAcquire()
        {
            int current;
            do
            {
                current = Volatile.Read(ref _references);
                if (current <= 0)
                {
                    return false;
                }
            }
            while (Interlocked.CompareExchange(ref _references, current + 1, current) != current);

            return true;
        }

        public void Release()
        {
            if (Interlocked.Decrement(ref _references) == 0)
            {
                Tts.Dispose();
            }
        }
    }

    /// <summary>The read in progress: its engine and who still owns it (the original read and
    /// any Skip that continued its playback).</summary>
    private sealed class ActiveRead(BuiltEngine engine, ReadOwnership owners)
    {
        private BuiltEngine _engine = engine;

        public ReadOwnership Owners { get; } = owners;

        /// <summary>The engine the read speaks with; replaced when a mid-read voice change needs another model.</summary>
        public BuiltEngine Engine
        {
            get => Volatile.Read(ref _engine);
            set => Volatile.Write(ref _engine, value);
        }
    }

    /// <summary>An engine and the model it was built from, never changed after creation.
    /// A language change within one pack shares the <see cref="RefCountedTts"/>.</summary>
    private sealed record BuiltEngine(RefCountedTts Tts, VoiceModelPaths Model) : IEngineHandle
    {
        public bool TryAcquire() => Tts.TryAcquire();

        public void Release() => Tts.Release();

        // The cache holds one reference; any more is a read or generation.
        public bool InUse => Tts.References > 1;
    }
}
