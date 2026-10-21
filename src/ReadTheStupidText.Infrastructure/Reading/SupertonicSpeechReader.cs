using System.Collections.Concurrent;
using System.Diagnostics;
using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// Speaks text with the local neural engine (sherpa-onnx) for the voice pack that serves the user's
/// language: Supertonic-3, Piper or MeloTTS (Slice 56 / Slice 51). Synthesis produces PCM per sentence,
/// stored in a persistent sentence cache (Slice 47); each chunk's sentences are joined into a WAV that is
/// written to a temp file per chunk (Decision 49) and played through a <see cref="MediaPlayer"/>, so speed
/// stays live and pitch-corrected via <see cref="MediaPlaybackSession.PlaybackRate"/> (synthesis runs at
/// 1x). The engine is built lazily from <see cref="IVoiceModelService.Paths"/>, and rebuilt when the pack
/// or engine family changes.
///
/// Long text is split into chunks (<see cref="SpeechTextChunker"/>) that synthesize concurrently (up to
/// <see cref="MaxSynthesisConcurrency"/>) but play strictly in order. As an <see cref="IHeadStart"/> it
/// can also prepare the opening sentences of a text before Play, so the read starts almost at once.
/// </summary>
public sealed class SupertonicSpeechReader : ISpeechReader, IHeadStart, IDisposable
{
    // ONNX intra-op threads per synthesis (sherpa-onnx config.Model.NumThreads), scaled to the machine
    // but capped: a single Generate sees diminishing returns past ~4 threads, and cores must be left for
    // the concurrent chunks. Latency-first - the time-to-first-audio the user feels is one chunk's synthesis.
    private static readonly int SynthesisThreads = Math.Clamp(Environment.ProcessorCount / 2, 2, 4);

    // Sentences synthesize this many at a time (the pool is shared with head-start prepares), sized so
    // SynthesisThreads * concurrency roughly fits the cores.
    private static readonly int MaxSynthesisConcurrency =
        Math.Clamp(Environment.ProcessorCount / SynthesisThreads, 2, 4);

    // A throwaway utterance synthesized once to warm the ONNX graph; its audio is discarded.
    private const string WarmUpText = "Ready.";

    // A two-sentence head is only cheap when its audio is already cached (Slice 56): a read uses it
    // only then (decided at read start), otherwise the one-sentence default keeps first audio short.
    // A head-start prepare always plans with it, so what it caches is exactly what such a read wants.
    private static readonly SpeechTextChunker.HeadPolicy CachedHead = SpeechTextChunker.HeadPolicy.Cached;

    // A head start prepares the opening of the text only: this many chunks, and only this much text is
    // even chunked, so a large document costs nothing.
    private const int PrepareMaxChunks = 4;
    private const int PrepareMaxChars = 2000;

    // Timing (Decision 33) is raised at most this often while reading, except when a total is learned.
    private static readonly TimeSpan TimingRaiseInterval = TimeSpan.FromSeconds(1);

    // Skip amount for SkipForward/SkipBackward (Decision 32); lands on the nearest chunk boundary.
    private static readonly TimeSpan SkipAmount = TimeSpan.FromSeconds(10);

    private readonly IVoiceModelService _model;
    private readonly ISystemLog _systemLog;
    private readonly AudioChunkPaths _audioPaths;
    private readonly NeuralSentenceSource _sentences;
    private readonly ReadTimingTracker _timing = new();
    private readonly MediaPlayer _player = new() { AutoPlay = false };

    // Serializes the generation counter / synthesis-cancellation swap and the read/prepare bookkeeping.
    private readonly object _gate = new();

    // Serializes engine builds so a warm-up and a first read racing each other build one model.
    private readonly object _ttsGate = new();

    // The model (folder, engine kind, pack version) whose engine build last failed; reads skip straight
    // to the Windows voice until the installed model changes.
    private string? _failedModelKey;

    // The engine plus the model it was built from, swapped as one object under _ttsGate. Each read
    // captures one BuiltEngine and uses its Model for everything.
    private BuiltEngine? _engine;

    // The read now playing, with the engine it plays on and how many call stacks (the original
    // SpeakAsync, then each Skip that continued it) still own it. Replaced by the next read and cleared
    // by Stop, both under _gate; a superseded read's late cleanup compares the reference and does nothing.
    private ActiveRead? _activeRead;
    private bool _disposed;
    private string? _voiceId;
    private readonly ConcurrentDictionary<VoiceEngineKind, string> _voiceByKind = new();
    private MediaSource? _currentSource;
    private double _playbackRate = PlaybackRate.Default.Value;
    private PlaybackState _state = PlaybackState.Idle;

    // Completes when the current chunk finishes playing (true) or playback is stopped/superseded (false).
    private TaskCompletionSource<bool>? _chunkEnded;

    // Incremented for every utterance and every Stop, so a superseded synthesis never reaches the player.
    private int _generation;
    private CancellationTokenSource? _synthCts;
    private CancellationTokenSource? _prepareCts;

    // Read-through progress weights each chunk equally: (completed chunks + position in the current) / total.
    private int _chunkCount;
    private int _currentChunkIndex;

    // The current read's chunks (text + source range), kept so Skip can restart at a different chunk.
    private IReadOnlyList<SpeechChunkPlan> _chunks = [];

    // Latency diagnostics (Decision 30): the activity-log id of the in-flight read and when it began.
    private int? _readActivityId;
    private long _readStartTicks;
    private bool _firstAudioPending;

    private long _lastTimingRaiseTicks;

    public SupertonicSpeechReader(IVoiceModelService model, ISystemLog systemLog, AudioChunkPaths audioPaths)
    {
        _model = model;
        _systemLog = systemLog;
        _audioPaths = audioPaths;
        _sentences = new NeuralSentenceSource(audioPaths.SentenceFolder, systemLog, MaxSynthesisConcurrency);
        _player.MediaOpened += OnMediaOpened;
        _player.MediaEnded += OnMediaEnded;
        _player.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
        _player.PlaybackSession.PositionChanged += OnPositionChanged;
    }

    public event EventHandler<PlaybackState>? StateChanged;

    public event EventHandler? Completed;

    public event EventHandler<double>? ProgressChanged;

    public event EventHandler<ReadTiming>? TimingChanged;

    public event EventHandler<ReadChunk>? ChunkChanged;

    public PlaybackState State => _state;

    public bool ReadInProgress
    {
        get
        {
            lock (_gate)
            {
                return _activeRead is not null;
            }
        }
    }

    public async Task SpeakAsync(string text, int? activityId = null)
    {
        BuiltEngine engine = AcquireEngine() ?? throw new NeuralEngineUnavailableException();
        ActiveRead read = BeginRead(engine);
        try
        {
            (int generation, CancellationToken token) = BeginGeneration();
            string? voiceId = Volatile.Read(ref _voiceId);
            SpeechTextChunker.HeadPolicy head = await ChooseHeadPolicyAsync(engine, text, voiceId);
            if (!IsCurrent(generation))
            {
                return; // stopped or superseded while the cache was consulted
            }

            _readActivityId = activityId;
            _readStartTicks = Stopwatch.GetTimestamp();
            _firstAudioPending = true;

            IReadOnlyList<SpeechChunkPlan> chunks = PlanChunks(text, head, activityId);
            _chunks = chunks;
            _timing.Start(chunks.Count);
            RaiseTiming(TimeSpan.Zero);

            await SpeakChunksAsync(engine, chunks, 0, voiceId, generation, token);
        }
        finally
        {
            LeaveRead(read);
            engine.Release();
        }
    }

    // The two-sentence head only when its audio is already in the cache (a prepare, or an earlier read,
    // put it there); a cold head would make the listener wait for two syntheses. Off the caller's thread,
    // because the first look at the cache indexes its folder.
    private Task<SpeechTextChunker.HeadPolicy> ChooseHeadPolicyAsync(BuiltEngine engine, string text, string? voiceId) =>
        Task.Run(() =>
        {
            IReadOnlyList<SpeechChunkPlan> plan = SpeechChunkPlanner.ForText(text, CachedHead);
            bool cached = plan.Count > 0
                && _sentences.AreCached(engine, SentenceUnits.Split(plan[0].Text), ResolveVoice(engine, voiceId));
            return cached ? CachedHead : SpeechTextChunker.HeadPolicy.Default;
        });

    private IReadOnlyList<SpeechChunkPlan> PlanChunks(string text, SpeechTextChunker.HeadPolicy head, int? activityId)
    {
        long splitStart = Stopwatch.GetTimestamp();
        IReadOnlyList<SpeechChunkPlan> chunks = SpeechChunkPlanner.ForText(text, head);
        _systemLog.Debug(
            $"split {text.Length} chars into {chunks.Count} chunk(s) in " +
            $"{Stopwatch.GetElapsedTime(splitStart).TotalMilliseconds:F1} ms " +
            $"(threads {SynthesisThreads}, concurrency {MaxSynthesisConcurrency})",
            activityId);
        return chunks;
    }

    // Makes this the current read and cancels any prepare, atomically with a prepare's own check, so a
    // prepare either finished its start before the read or never starts. Replacing the previous read
    // here is what lets a prepare for the next text run while an old read's native synthesis winds down.
    private ActiveRead BeginRead(BuiltEngine engine)
    {
        var read = new ActiveRead(engine, new ReadOwnership());
        lock (_gate)
        {
            _activeRead = read;
            CancelPrepareLocked();
        }

        return read;
    }

    // One owner (the original SpeakAsync, or a Skip that continued the read) is done. The last one out
    // ends the read, unless Stop or a newer read already replaced it.
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
        ActiveRead? read = Volatile.Read(ref _activeRead);

        // _chunkCount isn't cleared on natural completion (only Stop() does that), so it alone
        // can't tell a finished read from one in progress - State can.
        if (read is null || _state == PlaybackState.Idle || !read.Owners.TryJoin())
        {
            return;
        }

        try
        {
            if (read.Engine.TryAcquire())
            {
                try
                {
                    await SkipOnEngineAsync(read.Engine, delta);
                }
                finally
                {
                    read.Engine.Release();
                }
            }
        }
        finally
        {
            LeaveRead(read);
        }
    }

    // Tears down the playing chunk (generation counter, as for a mid-read voice change) and restarts the
    // ordered playback loop at the target chunk found by the tracker (Decision 32).
    private async Task SkipOnEngineAsync(BuiltEngine engine, TimeSpan delta)
    {
        TimeSpan elapsed = _timing.CurrentTiming(_player.PlaybackSession.Position).Elapsed;
        if (_timing.ComputeSkipTarget(elapsed, delta) is not { } target)
        {
            return;
        }

        (int generation, CancellationToken newToken) = BeginGeneration();
        _chunkEnded?.TrySetResult(false); // release the ordered loop if mid-chunk
        _player.Pause();
        ClearSource();
        _timing.SeekTo(target);
        RaiseTiming(TimeSpan.Zero);

        await SpeakChunksAsync(engine, _chunks, target.ChunkIndex, Volatile.Read(ref _voiceId), generation, newToken);
    }

    // Synthesizes the chunks from startIndex onward (capped concurrency) with the given voice and plays
    // them strictly in order. The voice is a parameter, not the mutable field, so a change can't
    // half-apply to chunks already queued: the switch happens cleanly at the next chunk boundary
    // (Decision 29), so heard text is neither repeated nor skipped.
    private async Task SpeakChunksAsync(
        BuiltEngine engine, IReadOnlyList<SpeechChunkPlan> chunks, int startIndex, string? voiceId,
        int generation, CancellationToken token)
    {
        _chunkCount = chunks.Count;
        _currentChunkIndex = startIndex;
        ProgressChanged?.Invoke(this, chunks.Count == 0 ? 0 : (double)startIndex / chunks.Count);

        List<Task<string>> generations = StartGenerations(engine, chunks, startIndex, voiceId, token);
        for (int offset = 0; offset < generations.Count; offset++)
        {
            int index = startIndex + offset;
            if (!await PlayGeneratedChunkAsync(generations[offset], chunks, index, generation, token))
            {
                return;
            }

            if (await RestartForVoiceSwapAsync(engine, chunks, index, voiceId))
            {
                return;
            }
        }

        if (IsCurrent(generation))
        {
            UpdateState(PlaybackState.Idle);
            DeleteReadFiles();
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }

    // Kicks off synthesis of every remaining chunk; the first runs straight away and the rest wait for
    // it, so the chunk the listener waits on is never starved of CPU.
    private List<Task<string>> StartGenerations(
        BuiltEngine engine, IReadOnlyList<SpeechChunkPlan> chunks, int startIndex, string? voiceId,
        CancellationToken token)
    {
        VoiceInfo voice = ResolveVoice(engine, voiceId);
        var generations = new List<Task<string>>();
        Task<string>? firstChunk = null;
        for (int index = startIndex; index < chunks.Count; index++)
        {
            Task<string> chunkTask = index == startIndex
                ? GenerateChunkAsync(engine, chunks[index].Text, index, voice, token)
                : GenerateAfterAsync(firstChunk!, engine, chunks[index].Text, index, voice, token);
            firstChunk ??= chunkTask;
            generations.Add(chunkTask);
        }

        return generations;
    }

    // Waits for one chunk's audio and plays it. False when the read was cancelled, stopped or
    // superseded, so the caller stops consuming chunks.
    private async Task<bool> PlayGeneratedChunkAsync(
        Task<string> generated, IReadOnlyList<SpeechChunkPlan> chunks, int index, int generation,
        CancellationToken token)
    {
        string path;
        try
        {
            path = await generated;
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
        ChunkChanged?.Invoke(this, new ReadChunk(index, chunks.Count, chunk.Text, chunk.SourceStart, chunk.SourceEnd));
        bool endedNaturally = await PlayChunkAsync(path, token);
        if (!endedNaturally || !IsCurrent(generation))
        {
            return false;
        }

        _timing.AdvancePastChunk(index);
        return true;
    }

    // When the selected voice changed during the chunk just played, continue the remaining chunks in
    // the new voice (cancelling the old-voice synthesis already in flight). Voices are compared by id
    // within the read's own model, so a language or pack change never restarts a read. True when it
    // restarted (the restarted loop finishes the read, so the caller returns).
    private async Task<bool> RestartForVoiceSwapAsync(
        BuiltEngine engine, IReadOnlyList<SpeechChunkPlan> chunks, int index, string? voiceId)
    {
        string? selected = Volatile.Read(ref _voiceId);
        bool sameVoice = ResolveVoice(engine, selected).Id == ResolveVoice(engine, voiceId).Id;
        if (sameVoice || index + 1 >= chunks.Count)
        {
            return false;
        }

        (int nextGeneration, CancellationToken nextToken) = BeginGeneration();
        _systemLog.Info(
            $"voice changed mid-read; continuing at chunk {index + 2}/{chunks.Count} in the new voice",
            _readActivityId);
        await SpeakChunksAsync(engine, chunks, index + 1, selected, nextGeneration, nextToken);
        return true;
    }

    // The selected voice if the read's model offers it, else the voice last picked for this engine
    // family, else the model's default: a Piper pick for another language never displaces the
    // Supertonic choice.
    private VoiceInfo ResolveVoice(BuiltEngine engine, params string?[] ids)
    {
        _voiceByKind.TryGetValue(engine.Model.Kind, out string? remembered);
        string?[] candidates = [.. ids, Volatile.Read(ref _voiceId), remembered];
        return VoiceChoices.Resolve(engine.Model, candidates);
    }

    // Builds the engine and runs one discarded synthesis to warm the ONNX graph, off the UI thread, so
    // the first real read is near-instant. Idempotent; best-effort, with AcquireEngine in SpeakAsync as
    // the fallback. Called again whenever a pack becomes ready (ReadAloudService.OnVoiceModelReady).
    public Task WarmUpAsync() => Task.Run(WarmUp);

    private void WarmUp()
    {
        try
        {
            _sentences.LoadIndex(); // so the first read never indexes the cache folder on its own thread
            if (AcquireEngine() is not { } engine)
            {
                return;
            }

            try
            {
                SherpaEngineFactory.Generate(engine, WarmUpText, ResolveVoice(engine).SpeakerId).Dispose();
            }
            finally
            {
                engine.Release();
            }
        }
        catch
        {
            // Best-effort: any real error surfaces on the first read.
        }
    }

    // Defers a chunk's synthesis until the first chunk has finished, so the first chunk (which gates
    // time-to-first-audio) runs without competing for CPU. The gate's own outcome surfaces where it is
    // consumed in order; faults here just release this chunk, which then honours the token itself.
    private async Task<string> GenerateAfterAsync(
        Task gate, BuiltEngine engine, string chunk, int index, VoiceInfo voice, CancellationToken token)
    {
        try
        {
            await gate;
        }
        catch
        {
            // Swallowed: the first chunk's real outcome surfaces on its own await.
        }

        return await GenerateChunkAsync(engine, chunk, index, voice, token);
    }

    // Builds one chunk's WAV from its cached or synthesized sentences and writes it to the read's temp
    // chunk file (Decision 49). tts.Generate is a synchronous native call that cannot be interrupted, so
    // the token is checked again before any shared state or file is touched: a superseded generation
    // must not record a stale duration or write a stale chunk file (Slice 35).
    private async Task<string> GenerateChunkAsync(
        BuiltEngine engine, string chunk, int index, VoiceInfo voice, CancellationToken token)
    {
        int? activityId = _readActivityId;
        long started = Stopwatch.GetTimestamp();
        byte[] wav = await _sentences.JoinChunkAsync(engine, chunk, voice, activityId, token);
        token.ThrowIfCancellationRequested();

        RecordChunkDuration(index, WavFile.TryGetDuration(wav) ?? TimeSpan.Zero);
        string path = _audioPaths.ChunkPath(activityId ?? 0, index);
        await File.WriteAllBytesAsync(path, wav, token);
        _systemLog.Debug(
            $"chunk {index + 1}/{_chunkCount} ({chunk.Length} chars) ready in " +
            $"{Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms",
            activityId);
        return path;
    }

    // Records a chunk's real duration (known as soon as its audio lands) and, the instant that
    // completes the read's total, raises TimingChanged right away instead of on the next throttled tick.
    private void RecordChunkDuration(int index, TimeSpan duration)
    {
        bool totalWasUnknown = _timing.CurrentTiming(TimeSpan.Zero).Total is null;
        _timing.RecordChunkDuration(index, duration);
        if (totalWasUnknown && _timing.CurrentTiming(TimeSpan.Zero).Total is not null)
        {
            RaiseTiming(_player.PlaybackSession.Position);
        }
    }

    // Plays one chunk from its temp WAV file and awaits its natural end (true) or a stop/supersede (false).
    private async Task<bool> PlayChunkAsync(string path, CancellationToken token)
    {
        var ended = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _chunkEnded = ended;
        StorageFile file = await StorageFile.GetFileFromPathAsync(path);
        SwapSource(MediaSource.CreateFromStorageFile(file));
        using (token.Register(() => ended.TrySetResult(false)))
        {
            return await ended.Task;
        }
    }

    public void Pause() => _player.Pause();

    public void Resume() => _player.Play();

    public void Stop()
    {
        lock (_gate)
        {
            _synthCts?.Cancel();
            _generation++;
            _activeRead = null; // no read in progress from this moment, whatever the old one is still unwinding
        }

        _chunkEnded?.TrySetResult(false); // release the playback loop if mid-chunk
        _player.Pause();
        ClearSource();
        _chunkCount = 0;
        ProgressChanged?.Invoke(this, 0);
        _timing.Reset();
        RaiseTiming(TimeSpan.Zero);
        UpdateState(PlaybackState.Idle);
        DeleteReadFiles();
    }

    // Deletes the read's temp chunk folder once it's genuinely done - natural completion or Stop()
    // (Decision 49). Not called from Skip or the voice-change continuation, which restart the same read.
    // Only play files live there: the sentence cache is a separate folder and is never touched.
    private void DeleteReadFiles()
    {
        if (_readActivityId is { } id)
        {
            _audioPaths.DeleteRead(id);
        }
    }

    public void SetSpeed(PlaybackRate speed)
    {
        _playbackRate = speed.Value;
        _player.PlaybackSession.PlaybackRate = _playbackRate;
    }

    // Records the selected voice. While idle it applies to the next read; during a read the loop notices
    // at the next chunk boundary (Decision 29).
    public void SetVoice(string voiceId)
    {
        if (VoiceChoices.KindOf(voiceId) is { } kind)
        {
            _voiceByKind[kind] = voiceId;
        }

        Volatile.Write(ref _voiceId, voiceId);
    }

    // Returns the engine for the language's current model with a reference taken for the caller, who
    // must Release() it. A different pack or engine kind rebuilds the OfflineTts; the same pack under
    // another language reuses it with the new model. The superseded OfflineTts is disposed once the
    // last read or generation holding it lets go.
    private BuiltEngine? AcquireEngine()
    {
        if (_model.Paths is { } paths && ModelKey(paths) == Volatile.Read(ref _failedModelKey))
        {
            // This exact model already failed to load: don't retry the multi-hundred-MB build on every
            // read. A different pack folder, engine or version clears the block.
            throw new NeuralEngineUnavailableException();
        }

        try
        {
            return AcquireEngineCore();
        }
        catch (Exception ex) when (ex is not NeuralEngineUnavailableException)
        {
            if (_model.Paths is { } failed)
            {
                Volatile.Write(ref _failedModelKey, ModelKey(failed));
            }

            // A pack that is "ready" but cannot be loaded (files gone, native failure): the composite
            // reads with the Windows voice, and the model re-checks the installed packs.
            _systemLog.Warning("the neural engine could not be built", null, ex);
            RefreshModelInBackground();
            throw new NeuralEngineUnavailableException(ex);
        }
    }

    private static string ModelKey(VoiceModelPaths paths) => $"{paths.RootDir}|{paths.Kind}|{paths.PackVersion}";

    private void RefreshModelInBackground() =>
        _ = _model.InitializeAsync().ContinueWith(
            t => _systemLog.Warning("voice model refresh failed", null, t.Exception),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

    private BuiltEngine? AcquireEngineCore()
    {
        if (_model.Paths is not { } paths)
        {
            return null;
        }

        lock (_ttsGate)
        {
            if (_disposed)
            {
                return null;
            }

            if (_engine is null || _engine.Model != paths)
            {
                _engine = SwapEngine(_engine, paths);
            }

            return TryAcquireCurrent(paths);
        }
    }

    // Takes the caller's reference on the current engine. A refused reference means the engine was
    // already disposed, so it is rebuilt once; if that is refused too the read is not ready.
    // Called under _ttsGate.
    private BuiltEngine? TryAcquireCurrent(VoiceModelPaths paths)
    {
        if (_engine!.TryAcquire())
        {
            return _engine;
        }

        _engine = SwapEngine(null, paths);
        return _engine.TryAcquire() ? _engine : null;
    }

    private static BuiltEngine SwapEngine(BuiltEngine? current, VoiceModelPaths paths)
    {
        if (current is { Tts.IsAlive: true } && current.Model.RootDir == paths.RootDir && current.Model.Kind == paths.Kind)
        {
            return new BuiltEngine(current.Tts, paths);
        }

        var fresh = new BuiltEngine(new RefCountedTts(SherpaEngineFactory.Build(paths, SynthesisThreads)), paths);
        current?.Release();
        return fresh;
    }

    // Head start (Slice 47/56): synthesizes the sentences of the text's first chunks into the cache in
    // the voice that would read them now, without playing. A newer prepare, the start of a read,
    // CancelPrepare and Dispose all cancel it; audio already cached stays.
    public Task PrepareAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.CompletedTask;
        }

        // The token is taken here, synchronously and in call order, so a later call always supersedes an
        // earlier one and CancelPrepare reaches a prepare still queued on the pool.
        return BeginPrepare() is { } token
            ? Task.Run(() => PrepareOffCallerAsync(text, token))
            : Task.CompletedTask;
    }

    // Runs on the thread pool, so acquiring the engine (which may build the model), planning the chunks
    // and indexing the cache never block the thread that asked.
    private async Task PrepareOffCallerAsync(string text, CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            return;
        }

        BuiltEngine? engine = null;
        try
        {
            engine = AcquireEngine();
            if (engine is null)
            {
                return;
            }

            token.ThrowIfCancellationRequested();
            await PrepareChunksAsync(engine, text, token);
        }
        catch (OperationCanceledException)
        {
            // Superseded or cancelled: nothing to clean up.
        }
        catch (Exception ex)
        {
            _systemLog.Warning("head start failed", null, ex);
        }
        finally
        {
            engine?.Release();
        }
    }

    private async Task PrepareChunksAsync(BuiltEngine engine, string text, CancellationToken token)
    {
        VoiceInfo voice = ResolveVoice(engine, Volatile.Read(ref _voiceId));
        string prefix = text.Length > PrepareMaxChars ? text[..PrepareMaxChars] : text;
        IEnumerable<string> sentences = SpeechChunkPlanner.ForText(prefix, CachedHead)
            .Take(PrepareMaxChunks)
            .SelectMany(chunk => SentenceUnits.Split(chunk.Text));

        await _sentences.PrepareAsync(engine, sentences, voice, token);
    }

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

    // A newer prepare supersedes the one in flight; null when a read is in progress, which must never
    // share the engine with a prepare. Checked under the same lock that BeginRead takes.
    private CancellationToken? BeginPrepare()
    {
        lock (_gate)
        {
            if (_activeRead is not null)
            {
                return null;
            }

            CancelPrepareLocked();
            _prepareCts = new CancellationTokenSource();
            return _prepareCts.Token;
        }
    }

    // Opens a new generation, cancelling the previous synthesis so a superseded long synth stops wasting
    // CPU and never reaches the player.
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

    private void SwapSource(MediaSource source)
    {
        MediaSource? previous = _currentSource;
        _currentSource = source;
        _player.Source = source;
        previous?.Dispose();
    }

    private void ClearSource()
    {
        _player.Source = null;
        MediaSource? previous = _currentSource;
        _currentSource = null;
        previous?.Dispose();
    }

    private void OnMediaOpened(MediaPlayer sender, object args)
    {
        // The first chunk opening is the moment audio is about to start - log time-to-first-audio once.
        if (_firstAudioPending)
        {
            _firstAudioPending = false;
            _systemLog.Debug(
                $"first audio after {Stopwatch.GetElapsedTime(_readStartTicks).TotalMilliseconds:F0} ms",
                _readActivityId);
        }

        sender.PlaybackSession.PlaybackRate = _playbackRate;
        sender.Play();
    }

    // A chunk finished; let the ordered playback loop advance to the next one (or complete).
    private void OnMediaEnded(MediaPlayer sender, object args) => _chunkEnded?.TrySetResult(true);

    // Reports best-effort read-through progress as the current chunk plays. Ignored when nothing is loaded.
    private void OnPositionChanged(MediaPlaybackSession sender, object args)
    {
        // Snapshot once: Stop() can zero _chunkCount on another thread between a guard and the division.
        int chunkCount = Volatile.Read(ref _chunkCount);
        int chunkIndex = Volatile.Read(ref _currentChunkIndex);
        if (chunkCount == 0)
        {
            return;
        }

        double withinChunk = sender.NaturalDuration > TimeSpan.Zero
            ? sender.Position / sender.NaturalDuration
            : 0;
        double fraction = (chunkIndex + Math.Clamp(withinChunk, 0, 1)) / chunkCount;
        ProgressChanged?.Invoke(this, Math.Clamp(fraction, 0, 1));

        if (Stopwatch.GetElapsedTime(_lastTimingRaiseTicks) >= TimingRaiseInterval)
        {
            RaiseTiming(sender.Position);
        }
    }

    private void RaiseTiming(TimeSpan positionInCurrentChunk)
    {
        _lastTimingRaiseTicks = Stopwatch.GetTimestamp();
        TimingChanged?.Invoke(this, _timing.CurrentTiming(positionInCurrentChunk));
    }

    private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
    {
        UpdateState(sender.PlaybackState switch
        {
            MediaPlaybackState.Playing => PlaybackState.Playing,
            MediaPlaybackState.Paused => PlaybackState.Paused,
            _ => _state,
        });
    }

    private void UpdateState(PlaybackState next)
    {
        if (next == _state)
        {
            return;
        }

        _state = next;
        StateChanged?.Invoke(this, next);
    }

    public void Dispose()
    {
        CancelPrepare();
        _synthCts?.Cancel();
        _synthCts?.Dispose();
        _player.MediaOpened -= OnMediaOpened;
        _player.MediaEnded -= OnMediaEnded;
        _player.PlaybackSession.PlaybackStateChanged -= OnPlaybackStateChanged;
        _player.PlaybackSession.PositionChanged -= OnPositionChanged;
        _player.Dispose();
        _currentSource?.Dispose();
        lock (_ttsGate)
        {
            _disposed = true;
            _engine?.Release();
            _engine = null;
        }
    }
}
