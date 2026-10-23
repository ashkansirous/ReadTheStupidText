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

    // The model (folder, engine kind, pack version) whose engine build last failed; reads skip straight
    // to the Windows voice until the installed model changes.
    private string? _failedModelKey;

    // The built engines (at most two, keyed by model folder and pack version). Each read captures one
    // BuiltEngine and uses its Model for everything; a loaded engine is shared by whoever asks for it next.
    private readonly EngineCache<BuiltEngine> _engines = new(CreateEngine);

    // The read now playing, with the engine it plays on and how many call stacks (the original
    // SpeakAsync, then each Skip that continued it) still own it. Replaced by the next read and cleared
    // by Stop, both under _gate; a superseded read's late cleanup compares the reference and does nothing.
    private ActiveRead? _activeRead;
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
    private ChunkFeed _feed = new([], 0, complete: true);

    // The activity id of the open streamed read (Slice 75), so a late append for a read that has been
    // superseded can never land in the next one. Null when no streamed read is open.
    private int? _streamActivityId;
    private bool _streamOpen;

    // Appends and completion that arrive while the first block is still being planned (the feed does not
    // exist yet); non-null only during that window, under _gate.
    private PendingStreamBlocks? _pendingStream;

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

    public bool SupportsStreaming => true;

    public Task SpeakAsync(string text, int? activityId = null) =>
        ReadAsync(activityId, open: false, async (engine, voiceId) =>
            (PlanChunks(text, await ChooseHeadPolicyAsync(engine, text, voiceId), activityId), text.Length));

    // Slice 75: a read that grows. The first block is planned like any read; AppendText adds the rest
    // while it plays, and CompleteStream lets it finish.
    public Task SpeakStreamAsync(string firstText, int? activityId = null) =>
        ReadAsync(activityId, open: true, async (engine, voiceId) =>
            (PlanChunks(firstText, await ChooseHeadPolicyAsync(engine, firstText, voiceId), activityId), firstText.Length));

    public void AppendText(string text, int? activityId = null)
    {
        lock (_gate)
        {
            if (!IsOpenStreamOf(activityId))
            {
                return;
            }

            if (_pendingStream is not null)
            {
                _pendingStream.Add(text); // the head is still being planned: applied when the feed is created
                return;
            }

            // The timing grows under the feed's lock, before the playback loop can see (and record a duration
            // for) a new chunk, so RecordChunkDuration never indexes past the timing's chunk list.
            _feed.Append(text, ExtendTiming);
        }
    }

    public void CompleteStream(int? activityId = null)
    {
        lock (_gate)
        {
            if (!IsOpenStreamOf(activityId))
            {
                return;
            }

            _streamOpen = false;
            if (_pendingStream is not null)
            {
                _pendingStream.MarkComplete();
                return;
            }

            _feed.Complete();
            _timing.SetOpen(false);
        }

        RaiseTiming(_player.PlaybackSession.Position);
    }

    private void ExtendTiming(int count)
    {
        _timing.Extend(count);
        _chunkCount = count;
    }

    // Called under _gate.
    private bool IsOpenStreamOf(int? activityId) => _streamOpen && _streamActivityId == activityId;

    // Slice 67 (Dino Reader on Windows): each segment is chunked on its own and each chunk is synthesized
    // in its segment's voice, so a character keeps one voice for the whole read. A fixed feed, complete at
    // once: the same ordered-consume loop, skip and timing as a plain read.
    public bool SupportsSegments => true;

    public Task SpeakSegmentsAsync(IReadOnlyList<VoicedSegment> segments, int? activityId = null) =>
        ReadAsync(activityId, open: false, (_, _) =>
        {
            IReadOnlyList<SpeechChunkPlan> plan = SpeechChunkPlanner.ForSegments(segments, SpeechTextChunker.HeadPolicy.Default);
            return Task.FromResult((plan, plan.Count == 0 ? 0 : plan[^1].SourceEnd));
        });

    // The read lifecycle every entry point shares: take the engine, make this the current read, plan its
    // chunks (the planner runs after the generation starts, so a Stop during planning cancels the read)
    // and play them.
    private async Task ReadAsync(
        int? activityId, bool open,
        Func<BuiltEngine, string?, Task<(IReadOnlyList<SpeechChunkPlan> Chunks, int TextLength)>> plan)
    {
        // A cold model build takes seconds: never on the caller's (UI) thread.
        BuiltEngine? owned = await Task.Run(() => AcquireEngine()) ?? throw new NeuralEngineUnavailableException();
        BuiltEngine engine = owned;
        ActiveRead read = BeginRead(engine);
        try
        {
            (int generation, CancellationToken token) = BeginGeneration();
            string? voiceId = Volatile.Read(ref _voiceId);
            OpenStreamWindow(activityId, open);
            (IReadOnlyList<SpeechChunkPlan> chunks, int textLength) = await plan(engine, voiceId);
            if (!IsCurrent(generation))
            {
                return; // stopped or superseded while the cache was consulted
            }

            _readActivityId = activityId;
            _readStartTicks = Stopwatch.GetTimestamp();
            _firstAudioPending = true;

            ChunkFeed feed = CreateFeed(chunks, textLength, open, generation);

            owned = null; // the playback loop owns the reference from here and gives it back when it moves on
            await SpeakChunksAsync(engine, feed, 0, voiceId, generation, token);
        }
        finally
        {
            LeaveRead(read);
            owned?.Release();
        }
    }

    // Makes this activity the one stream appends are accepted for, from before the head is planned, so a
    // block arriving during planning is buffered rather than dropped and a superseded id is ignored.
    private void OpenStreamWindow(int? activityId, bool open)
    {
        lock (_gate)
        {
            _streamActivityId = activityId;
            _streamOpen = open;
            _pendingStream = open ? new PendingStreamBlocks() : null;
        }
    }

    // Creates the read's feed and replays what was buffered while it was being planned. All under _gate,
    // so no append lands between the drain and the switch to appending directly.
    private ChunkFeed CreateFeed(IReadOnlyList<SpeechChunkPlan> chunks, int textLength, bool open, int generation)
    {
        ChunkFeed feed;
        lock (_gate)
        {
            feed = new ChunkFeed(chunks, textLength, complete: !open);
            _feed = feed;
            _timing.Start(chunks.Count);
            _timing.SetOpen(open);
            if (_pendingStream is not null)
            {
                bool completed = _pendingStream.DrainInto(feed, ExtendTiming);
                _timing.SetOpen(!completed);
                _pendingStream = null;
            }
        }

        RaiseTiming(TimeSpan.Zero);
        return feed;
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
            BuiltEngine engine = read.Engine; // captured: a voice swap may replace read.Engine mid-await
            if (engine.TryAcquire())
            {
                await SkipOnEngineAsync(engine, delta); // takes over the reference taken here
            }
        }
        finally
        {
            LeaveRead(read);
        }
    }

    // Tears down the playing chunk (generation counter, as for a mid-read voice change) and restarts the
    // ordered playback loop at the target chunk found by the tracker (Decision 32). Owns the engine
    // reference it is given: the playback loop gives it back, and so does a skip with nowhere to go.
    private async Task SkipOnEngineAsync(BuiltEngine engine, TimeSpan delta)
    {
        TimeSpan elapsed = _timing.CurrentTiming(_player.PlaybackSession.Position).Elapsed;
        if (_timing.ComputeSkipTarget(elapsed, delta) is not { } target)
        {
            engine.Release();
            return;
        }

        (int generation, CancellationToken newToken) = BeginGeneration();
        _chunkEnded?.TrySetResult(false); // release the ordered loop if mid-chunk
        _player.Pause();
        ClearSource();
        _timing.SeekTo(target);
        RaiseTiming(TimeSpan.Zero);

        await SpeakChunksAsync(engine, _feed, target.ChunkIndex, Volatile.Read(ref _voiceId), generation, newToken);
    }

    // Synthesizes the chunks from startIndex onward (capped concurrency) with the given voice and plays
    // them strictly in order. The voice is a parameter, not the mutable field, so a change can't
    // half-apply to chunks already queued: the switch happens cleanly at the next chunk boundary
    // (Decision 29), so heard text is neither repeated nor skipped. Owns the engine reference it is
    // given and gives it back as soon as a voice change moves the read to another model, so a long
    // read that toggles voices keeps at most two models resident.
    private async Task SpeakChunksAsync(
        BuiltEngine engine, ChunkFeed feed, int startIndex, string? voiceId, int generation, CancellationToken token)
    {
        try
        {
            while (true)
            {
                _chunkCount = feed.Count;
                _currentChunkIndex = startIndex;
                ProgressChanged?.Invoke(this, feed.Count == 0 ? 0 : (double)startIndex / feed.Count);

                (RunEnd end, VoiceSwap? swap) = await RunChunksAsync(engine, feed, startIndex, voiceId, generation, token);
                if (end == RunEnd.Stopped)
                {
                    return;
                }

                if (swap is null)
                {
                    break;
                }

                (engine, startIndex, voiceId, generation, token) = TakeOver(engine, swap);
            }
        }
        finally
        {
            engine.Release();
        }

        if (token.IsCancellationRequested)
        {
            return; // stopped while waiting for more text
        }

        if (IsCurrent(generation))
        {
            UpdateState(PlaybackState.Idle);
            DeleteReadFiles();
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

    private async Task<(RunEnd End, VoiceSwap? Swap)> RunChunksAsync(
        BuiltEngine engine, ChunkFeed feed, int startIndex, string? voiceId, int generation, CancellationToken token)
    {
        var run = new GenerationRun(this, engine, feed, startIndex, voiceId, token);
        try
        {
            return await PlayRunAsync(run, engine, feed, startIndex, voiceId, generation, token);
        }
        finally
        {
            run.Close();
        }
    }

    // Plays the run's chunks in order. Stopped when the read stopped or was superseded; Swapped when the
    // voice changed and the read goes on in the swap's voice (and model); Played when every chunk played.
    private async Task<(RunEnd End, VoiceSwap? Swap)> PlayRunAsync(
        GenerationRun run, BuiltEngine engine, ChunkFeed feed, int startIndex, string? voiceId,
        int generation, CancellationToken token)
    {
        _ = run.PumpAsync();
        for (int index = startIndex; ; index++)
        {
            if (!await WaitForChunkAsync(feed, index, token))
            {
                return (RunEnd.Played, null);
            }

            run.StartPending();
            if (!await PlayGeneratedChunkAsync(run.TaskAt(index), feed, index, generation, token))
            {
                return (RunEnd.Stopped, null);
            }

            if (await RestartForVoiceSwapAsync(engine, feed, index, voiceId, generation) is { } swap)
            {
                return (RunEnd.Swapped, swap);
            }
        }
    }

    private enum RunEnd
    {
        Played,
        Swapped,
        Stopped,
    }

    // A voice change the read goes on in: the engine for the new voice (the run's own when the model is the
    // same), the chunk to resume at and the generation that replaces the old one.
    private sealed record VoiceSwap(BuiltEngine Engine, int NextIndex, string? Voice, int Generation, CancellationToken Token);

    // True when the chunk at index exists (or arrives); false when the read has no more text or was stopped.
    private static async Task<bool> WaitForChunkAsync(ChunkFeed feed, int index, CancellationToken token)
    {
        try
        {
            while (feed.Count <= index)
            {
                if (feed.IsComplete)
                {
                    return false;
                }

                await feed.WaitForMoreAsync(index, token);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    // Starts synthesis of every chunk of the feed as it appears: the first runs straight away and the rest
    // wait for it, so the chunk the listener waits on is never starved of CPU. A streamed read's appended
    // chunks start the moment they arrive (the pump), not when the previous chunk has finished playing.
    private sealed class GenerationRun
    {
        private readonly SupertonicSpeechReader _owner;
        private readonly BuiltEngine _engine;
        private readonly ChunkFeed _feed;
        private readonly int _startIndex;
        private readonly string? _voiceId;
        private readonly CancellationToken _token;
        private readonly object _gate = new();
        private readonly List<Task<string>> _tasks = [];
        private bool _closed;

        public GenerationRun(
            SupertonicSpeechReader owner, BuiltEngine engine, ChunkFeed feed, int startIndex, string? voiceId, CancellationToken token)
        {
            _owner = owner;
            _engine = engine;
            _feed = feed;
            _startIndex = startIndex;
            _voiceId = voiceId;
            _token = token;
        }

        private bool IsClosed
        {
            get
            {
                lock (_gate)
                {
                    return _closed;
                }
            }
        }

        public void StartPending()
        {
            lock (_gate)
            {
                if (_closed)
                {
                    return; // the run is over: nothing may start (or load an engine) any more
                }

                for (int index = _startIndex + _tasks.Count; index < _feed.Count; index++)
                {
                    _tasks.Add(GenerateAsync(_tasks.Count == 0 ? null : _tasks[0], _feed[index], index));
                }
            }
        }

        // Defers every chunk after the first until the first has finished (the first gates time-to-first-audio).
        // The gate's own outcome surfaces where it is consumed in order; a fault here just releases this chunk.
        // A chunk whose voice lives in another model folder of the pack (a cast of pack voices, Slice 89) waits
        // for that engine to load off-thread and holds a reference on it while it synthesizes; if it cannot
        // load, the chunk uses the run's engine.
        private async Task<string> GenerateAsync(Task? gate, SpeechChunkPlan chunk, int index)
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

            BuiltEngine? extra = await ExtraEngineAsync(chunk.VoiceId);
            try
            {
                BuiltEngine chunkEngine = extra ?? _engine;
                VoiceInfo voice = _owner.ResolveVoice(chunkEngine, chunk.VoiceId, _voiceId);
                return await _owner.GenerateChunkAsync(chunkEngine, chunk.Text, index, voice, _token);
            }
            finally
            {
                extra?.Release();
            }
        }

        // The engine of another model folder the chunk's voice needs, or null when the run's own serves it
        // (or the model cannot load, which is logged).
        private async Task<BuiltEngine?> ExtraEngineAsync(string? chunkVoiceId)
        {
            VoiceInfo wanted = _owner.PickVoice(_engine.Model, chunkVoiceId, _voiceId);
            return wanted.ModelDir == _engine.Model.ModelDir ? null : await _owner.LoadVoiceEngineAsync(wanted.Id);
        }

        // Ends the run. The pump and later StartPending calls do nothing from here on; generations still
        // running hold their own reference on their engine, so a model is disposed only once they return.
        public void Close()
        {
            lock (_gate)
            {
                _closed = true;
            }
        }

        public Task<string> TaskAt(int index)
        {
            lock (_gate)
            {
                return _tasks[index - _startIndex];
            }
        }

        public async Task PumpAsync()
        {
            try
            {
                while (true)
                {
                    StartPending();
                    int started;
                    lock (_gate)
                    {
                        started = _startIndex + _tasks.Count;
                    }

                    if (IsClosed || (_feed.IsComplete && started >= _feed.Count))
                    {
                        return;
                    }

                    await _feed.WaitForMoreAsync(started, _token);
                }
            }
            catch (OperationCanceledException)
            {
                // Stopped or superseded: the pump has nothing left to start.
            }
        }
    }

    // Loads, off the caller's thread and under no gate, the engine for a voice in another model folder of the
    // pack, with a reference for the caller; null (with a warning) when it cannot.
    private async Task<BuiltEngine?> LoadVoiceEngineAsync(string voiceId)
    {
        BuiltEngine? built = await Task.Run(() => TryAcquireForVoice(voiceId));
        if (built is null)
        {
            _systemLog.Warning($"voice {voiceId} could not be loaded; its lines use the read's voice");
        }

        return built;
    }

    // Waits for one chunk's audio and plays it. False when the read was cancelled, stopped or
    // superseded, so the caller stops consuming chunks.
    private async Task<bool> PlayGeneratedChunkAsync(
        Task<string> generated, ChunkFeed feed, int index, int generation,
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
        SpeechChunkPlan chunk = feed[index];
        ChunkChanged?.Invoke(this, new ReadChunk(index, feed.Count, chunk.Text, chunk.SourceStart, chunk.SourceEnd));
        bool endedNaturally = await PlayChunkAsync(path, token);
        if (!endedNaturally || !IsCurrent(generation))
        {
            return false;
        }

        _timing.AdvancePastChunk(index);
        return true;
    }

    // When the selected voice changed during the chunk just played, continue the remaining chunks in
    // the new voice (a chunk with its own voice, in a segment read, never follows the picker),
    // cancelling the old-voice synthesis already in flight. Voices are compared by id within the read's
    // own model, so a language or pack change never restarts a read. Returns the swap the playback loop
    // goes on with, or null when the read carries on as it was.
    private async Task<VoiceSwap?> RestartForVoiceSwapAsync(
        BuiltEngine engine, ChunkFeed feed, int index, string? voiceId, int generation)
    {
        string? selected = Volatile.Read(ref _voiceId);
        VoiceInfo target = PickVoice(engine.Model, selected);
        bool sameVoice = target.Id == PickVoice(engine.Model, voiceId).Id;
        bool lastChunk = index + 1 >= feed.Count && feed.IsComplete;
        if (sameVoice || lastChunk || (index + 1 < feed.Count && feed[index + 1].VoiceId is not null))
        {
            return null;
        }

        // A voice with its own model folder needs its own engine (Slice 89), loaded off the playback loop
        // and under no gate. The engine this read came in on is given back by the loop once the new one is
        // in place (generations still running hold their own reference). If the new model cannot load, the
        // read goes on in the current voice.
        BuiltEngine next = engine;
        if (target.ModelDir != engine.Model.ModelDir)
        {
            next = await Task.Run(() => TryAcquireForVoice(selected)) ?? engine;
        }

        if (!IsCurrent(generation))
        {
            ReleaseUnlessSame(next, engine); // stopped or superseded while the model loaded
            return null;
        }

        (int nextGeneration, CancellationToken nextToken) = BeginGeneration();
        _systemLog.Info(
            $"voice changed mid-read; continuing at chunk {index + 2}/{feed.Count} in the new voice",
            _readActivityId);
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

    private BuiltEngine? TryAcquireForVoice(string? voiceId)
    {
        try
        {
            return AcquireEngine(voiceId);
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

    // The selected voice if the read's model offers it, else the voice last picked for this engine
    // family, else the model's default: a Piper pick for another language never displaces the
    // Supertonic choice.
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
            _streamOpen = false; // a streamed read cannot grow once stopped
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
    // must Release() it. A different pack folder, version or engine kind is another engine (a pack update
    // is a new folder, so it is picked up here); the same folder under another language reuses the model.
    // An engine nobody asks for any more is disposed once the last read or generation holding it lets go.
    private BuiltEngine? AcquireEngine(string? voiceId = null)
    {
        VoiceModelPaths? paths = SelectedPaths(voiceId);
        if (paths is not null && ModelKey(paths) == Volatile.Read(ref _failedModelKey))
        {
            // This exact model already failed to load: don't retry the multi-hundred-MB build on every
            // read. A different pack folder, engine or version clears the block.
            throw new NeuralEngineUnavailableException();
        }

        try
        {
            return paths is null ? null : AcquireEngineCore(paths);
        }
        catch (Exception ex) when (ex is not NeuralEngineUnavailableException)
        {
            if (paths is not null)
            {
                Volatile.Write(ref _failedModelKey, ModelKey(paths));
            }

            // A pack that is "ready" but cannot be loaded (files gone, native failure): the composite
            // reads with the Windows voice, and the model re-checks the installed packs.
            _systemLog.Warning("the neural engine could not be built", null, ex);
            RefreshModelInBackground();
            throw new NeuralEngineUnavailableException(ex);
        }
    }

    private static string ModelKey(VoiceModelPaths paths) => $"{paths.RootDir}|{paths.Kind}|{paths.PackVersion}|{paths.ModelDir}";

    // The language's current model aimed at the voice that will speak (the given id, else the selected
    // one): voices of one pack can each have their own model folder (Slice 89).
    private VoiceModelPaths? SelectedPaths(string? voiceId) =>
        _model.Paths is { } paths ? paths.ForVoice(PickVoice(paths, voiceId)) : null;

    private void RefreshModelInBackground() =>
        _ = _model.InitializeAsync().ContinueWith(
            t => _systemLog.Warning("voice model refresh failed", null, t.Exception),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

    private BuiltEngine? AcquireEngineCore(VoiceModelPaths paths) => _engines.Acquire(paths);

    // The engine for a model folder: the live one of the same folder and pack version re-labelled for the
    // new language (it shares the loaded model), else a freshly built one.
    private static BuiltEngine CreateEngine(BuiltEngine? previous, VoiceModelPaths paths) =>
        previous is not null && previous.Tts.TryAcquire()
            ? new BuiltEngine(previous.Tts, paths)
            : new BuiltEngine(new RefCountedTts(SherpaEngineFactory.Build(paths, SynthesisThreads)), paths);

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
        _engines.Dispose();
    }
}
