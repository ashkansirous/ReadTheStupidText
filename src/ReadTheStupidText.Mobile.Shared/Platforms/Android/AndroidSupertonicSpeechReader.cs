using System.Collections.Concurrent;
using System.Diagnostics;
using AndroidMedia = Android.Media;
using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using SherpaOnnx;

namespace ReadTheStupidText.Mobile;

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
public sealed class AndroidSupertonicSpeechReader : ISpeechReader, IHeadStart, IDisposable
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

    // Silence between the sentences of one chunk (each was synthesized on its own).
    private static readonly TimeSpan SentenceGap = TimeSpan.FromMilliseconds(150);

    // A head start prepares the opening of the text only: this many chunks (the two-sentence
    // head plus a few after it), not the whole file, so a long document costs no battery.
    private const int PrepareMaxChunks = 4;

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

    // One engine-level pool of native synthesis slots shared by reads and head-start prepares,
    // so their combined concurrency never exceeds MaxSynthesisConcurrency.
    private readonly SemaphoreSlim _slots = new(MaxSynthesisConcurrency);

    private readonly object _gate = new();
    private readonly object _ttsGate = new();

    // The engine plus the model it was built from, swapped as one object under _ttsGate
    // (Slice 51: engine kind, language, voices). Each read captures one BuiltEngine and uses
    // its Model for everything, so a language or pack change mid-read can't mix models.
    private BuiltEngine? _engine;
    private BuiltEngine? _activeEngine;
    private bool _disposed;
    private string? _voiceId;
    private readonly ConcurrentDictionary<VoiceEngineKind, string> _voiceByKind = new();
    private int _warmedUp;
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
    private bool _playerReady;

    private TaskCompletionSource<bool>? _chunkEnded;
    private CancellationTokenSource? _pollCts;

    private int _generation;
    private CancellationTokenSource? _synthCts;
    private CancellationTokenSource? _prepareCts;

    // Reads between their start and their end (changed under _gate): longer than
    // PlaybackState, which is Idle while the first chunk is still being synthesized. A counter,
    // not a flag, because a superseded read's end must not clear the read that replaced it.
    // A head-start prepare never runs while it is above zero.
    private int _readsInProgress;

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

    public PlaybackState State => _state;

    public bool SupportsSegments => true;

    public bool ReadInProgress
    {
        get
        {
            lock (_gate)
            {
                return _readsInProgress > 0;
            }
        }
    }

    public AndroidSupertonicSpeechReader(IVoiceModelService model, ISystemLog? log = null)
    {
        _model = model;
        _log = log ?? new AndroidSystemLog();
        string cacheRoot = FileSystem.Current.CacheDirectory;
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
    // for before that found nothing to warm: warm once, the first time the model reports ready.
    private void OnModelReadyChanged(object? sender, EventArgs e)
    {
        if (_model.IsReady && Volatile.Read(ref _warmedUp) == 0)
        {
            _ = WarmUpAsync();
        }
    }

    public Task SpeakAsync(string text, int? activityId = null) =>
        StartReadAsync(SpeechChunkPlanner.ForText(text, HeadPolicy));

    // Slice 60: each segment is chunked on its own and each chunk is synthesized in its
    // segment's voice, so a character keeps one voice for the whole read.
    public Task SpeakSegmentsAsync(IReadOnlyList<VoicedSegment> segments, int? activityId = null) =>
        StartReadAsync(SpeechChunkPlanner.ForSegments(segments, HeadPolicy));

    private async Task StartReadAsync(IReadOnlyList<SpeechChunkPlan> chunks)
    {
        BuiltEngine engine = AcquireEngine() ?? throw new NeuralEngineUnavailableException();

        try
        {
            Volatile.Write(ref _activeEngine, engine);
            BeginRead();
            (int generation, CancellationToken token) = BeginGeneration();
            _readStartTicks = Stopwatch.GetTimestamp();
            Volatile.Write(ref _firstAudioPending, 1);

            _chunks = chunks;
            _timing.Start(chunks.Count);
            RaiseTiming(TimeSpan.Zero);

            await SpeakChunksAsync(engine, chunks, 0, Volatile.Read(ref _voiceId), generation, token);
        }
        finally
        {
            Interlocked.CompareExchange(ref _activeEngine, null, engine);
            EndRead();
            engine.Release();
        }
    }

    // Marks the read in progress and cancels any prepare, atomically with a prepare's own
    // check, so a prepare either finished its start before the read or never starts.
    private void BeginRead()
    {
        lock (_gate)
        {
            _readsInProgress++;
            CancelPrepareLocked();
        }
    }

    // Paired with BeginRead: only called for a read that began.
    private void EndRead()
    {
        lock (_gate)
        {
            _readsInProgress--;
        }
    }

    public Task SkipForward() => SkipByAsync(SkipAmount);

    public Task SkipBackward() => SkipByAsync(-SkipAmount);

    private async Task SkipByAsync(TimeSpan delta)
    {
        BuiltEngine? engine = Volatile.Read(ref _activeEngine);
        if (engine is null || _state == PlaybackState.Idle || !engine.TryAcquire())
        {
            return;
        }

        try
        {
            await SkipOnEngineAsync(engine, delta);
        }
        finally
        {
            engine.Release();
        }
    }

    private async Task SkipOnEngineAsync(BuiltEngine engine, TimeSpan delta)
    {
        TimeSpan elapsed = _timing.CurrentTiming(CurrentChunkPosition()).Elapsed;
        if (_timing.ComputeSkipTarget(elapsed, delta) is not { } target)
        {
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
    // mid-read voice change can't half-apply to already-queued chunks.
    private async Task SpeakChunksAsync(
        BuiltEngine engine, IReadOnlyList<SpeechChunkPlan> chunks, int startIndex, string? voiceId, int generation,
        CancellationToken token)
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
            FinishNaturalEnd();
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }

    // A read that ends by itself leaves the same state Stop() does: the poll loop is stopped
    // and the timer and progress are back at zero, so a stale player never feeds the next
    // tick (the timer used to run on past the total, e.g. 00:09/00:04, and never reset).
    private void FinishNaturalEnd()
    {
        StopPolling();
        _chunkCount = 0;
        ProgressChanged?.Invoke(this, 0);
        _timing.Reset();
        RaiseTiming(TimeSpan.Zero);
        UpdateState(PlaybackState.Idle);
    }

    // Kicks off synthesis of every remaining chunk; the first runs straight away and the
    // rest wait for it, so the chunk the listener waits on is never starved.
    private List<Task<string>> StartGenerations(
        BuiltEngine engine, IReadOnlyList<SpeechChunkPlan> chunks, int startIndex, string? voiceId,
        CancellationToken token)
    {
        SemaphoreSlim slots = _slots;
        var generations = new List<Task<string>>();
        Task<string>? firstChunk = null;
        for (int index = startIndex; index < chunks.Count; index++)
        {
            VoiceInfo voice = ResolveVoice(engine, chunks[index].VoiceId, voiceId);
            Task<string> chunkTask = index == startIndex
                ? GenerateChunkAsync(engine, chunks[index].Text, index, voice, slots, token)
                : GenerateAfterAsync(firstChunk!, engine, chunks[index].Text, index, voice, slots, token);
            firstChunk ??= chunkTask;
            generations.Add(chunkTask);
        }

        return generations;
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
            wavPath = await generated;
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
    // restarts a read. True when it restarted (the restarted loop finishes the read, so the
    // caller returns).
    private async Task<bool> RestartForVoiceSwapAsync(
        BuiltEngine engine, IReadOnlyList<SpeechChunkPlan> chunks, int index, string? voiceId)
    {
        string? selected = Volatile.Read(ref _voiceId);
        bool sameVoice = ResolveVoice(engine, selected).Id == ResolveVoice(engine, voiceId).Id;
        if (sameVoice || index + 1 >= chunks.Count || chunks[index + 1].VoiceId is not null)
        {
            return false;
        }

        (int nextGeneration, CancellationToken nextToken) = BeginGeneration();
        await SpeakChunksAsync(engine, chunks, index + 1, selected, nextGeneration, nextToken);
        return true;
    }

    // The first of the given ids the read's model offers (a chunk's own voice, then the
    // selected one), then the voice last picked for this engine family, then the model's
    // default: a Piper pick for another language never displaces the Supertonic choice.
    private VoiceInfo ResolveVoice(BuiltEngine engine, params string?[] ids)
    {
        _voiceByKind.TryGetValue(engine.Model.Kind, out string? remembered);
        string?[] candidates = [.. ids, Volatile.Read(ref _voiceId), remembered];
        return VoiceChoices.Resolve(engine.Model, candidates);
    }

    public Task WarmUpAsync() => Task.Run(WarmUp);

    private void WarmUp()
    {
        try
        {
            BuiltEngine? engine = AcquireEngine();
            if (engine is null)
            {
                return;
            }

            try
            {
                Volatile.Write(ref _warmedUp, 1);
                int speaker = ResolveVoice(engine).SpeakerId;
                GenerateAudio(engine, WarmUpText, speaker).Dispose();
            }
            finally
            {
                engine.Release();
            }
        }
        catch
        {
            // Best-effort: AcquireEngine() in SpeakAsync remains the safety net.
        }
    }

    private async Task<string> GenerateAfterAsync(
        Task gate, BuiltEngine engine, string chunk, int index, VoiceInfo voice, SemaphoreSlim slots,
        CancellationToken token)
    {
        try
        {
            await gate;
        }
        catch
        {
            // Swallowed: the first chunk's real outcome surfaces on its own await.
        }

        return await GenerateChunkAsync(engine, chunk, index, voice, slots, token);
    }

    // A chunk is joined from its sentences' audio, each fetched from the cache or synthesized
    // (Slice 47). The sentences of one chunk are requested together; `slots` bounds how many
    // actually synthesize at once.
    private async Task<string> GenerateChunkAsync(
        BuiltEngine engine, string chunk, int index, VoiceInfo voice, SemaphoreSlim slots, CancellationToken token)
    {
        long started = Stopwatch.GetTimestamp();
        IReadOnlyList<string> units = SentenceUnits.Split(chunk);
        byte[] wav = await JoinSentencesAsync(engine, units, voice, slots, token);
        RecordChunkDuration(index, WavFile.TryGetDuration(wav) ?? TimeSpan.Zero);
        _log.Debug($"chunk {index} ({chunk.Length} chars, {units.Count} sentence(s)) ready in " +
            $"{Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms");
        return await WriteChunkFileAsync(wav);
    }

    // Fetches every sentence's clip and joins them. A cached clip that will not join (corrupt,
    // or a different sample rate) is a miss, not a failed read: the chunk's cached clips are
    // dropped and synthesized afresh. A second failure is real and propagates.
    private async Task<byte[]> JoinSentencesAsync(
        BuiltEngine engine, IReadOnlyList<string> units, VoiceInfo voice, SemaphoreSlim slots, CancellationToken token)
    {
        byte[][] clips = await GetClipsAsync(engine, units, voice, slots, token);
        try
        {
            return JoinClips(clips);
        }
        catch (InvalidDataException ex)
        {
            _log.Warning("cached sentence audio would not join; resynthesizing the chunk", error: ex);
            units.ToList().ForEach(u => _sentences.Invalidate(KeyFor(engine, u, voice)));
            return JoinClips(await GetClipsAsync(engine, units, voice, slots, token));
        }
    }

    private async Task<byte[][]> GetClipsAsync(
        BuiltEngine engine, IReadOnlyList<string> units, VoiceInfo voice, SemaphoreSlim slots, CancellationToken token)
    {
        byte[][] clips = await Task.WhenAll(units.Select(u => GetSentenceAsync(engine, u, voice, slots, token)));
        token.ThrowIfCancellationRequested();
        return clips;
    }

    private static byte[] JoinClips(byte[][] clips) =>
        clips.Length == 1 ? ValidClip(clips[0]) : WavFile.Concat(clips, SentenceGap);

    private static byte[] ValidClip(byte[] clip) =>
        WavFile.IsComplete(clip, clip.LongLength) ? clip : throw new InvalidDataException("Not a WAV written by this app.");

    // The key carries the voice, engine, language and model, so audio is never reused across
    // them; the engine and voice come from the read's own captured model.
    private Task<byte[]> GetSentenceAsync(
        BuiltEngine engine, string sentence, VoiceInfo voice, SemaphoreSlim slots, CancellationToken token)
    {
        SentenceCacheKey key = KeyFor(engine, sentence, voice);
        return _sentences.GetAsync(key, t => SynthesizeSentenceAsync(engine, sentence, voice.SpeakerId, slots, t), token);
    }

    private static SentenceCacheKey KeyFor(BuiltEngine engine, string sentence, VoiceInfo voice) =>
        new(sentence, engine.Model.Kind, engine.Model.Language, voice.Id, engine.Model.RootDir, engine.Model.PackVersion);

    // The native Generate call cannot be interrupted, and its result may be wanted by another
    // caller that joined this synthesis, so a cancelled caller only stops waiting for a slot;
    // a call already running finishes and its audio goes to whoever is still waiting.
    private static async Task<byte[]> SynthesizeSentenceAsync(
        BuiltEngine engine, string sentence, int speakerId, SemaphoreSlim slots, CancellationToken token)
    {
        await slots.WaitAsync(token);
        try
        {
            OfflineTtsGeneratedAudio audio = await GenerateGuardedAsync(engine, sentence, speakerId);
            try
            {
                return WavFile.Encode(audio.Samples, audio.SampleRate);
            }
            finally
            {
                audio.Dispose();
            }
        }
        finally
        {
            slots.Release();
        }
    }

    // The native Generate call keeps running after its caller is cancelled, so it holds its
    // own reference on the engine, released by the task itself when the call returns (success,
    // fault or abandonment alike, so the count stays balanced): a retired engine is disposed
    // only once it returns.
    private static Task<OfflineTtsGeneratedAudio> GenerateGuardedAsync(BuiltEngine engine, string chunk, int speakerId)
    {
        if (!engine.TryAcquire())
        {
            throw new ObjectDisposedException(nameof(AndroidSupertonicSpeechReader));
        }

        return Task.Run(() =>
        {
            try
            {
                return GenerateAudio(engine, chunk, speakerId);
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
        _chunkEnded = ended;

        AndroidMedia.MediaPlayer player = EnsurePlayer();
        player.Reset();
        _playerReady = false;
        DeletePreviousChunkFile();
        _previousChunkFile = _currentChunkFile;
        _currentChunkFile = wavPath;

        var prepared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnPrepared(object? s, EventArgs e) => prepared.TrySetResult();
        player.Prepared += OnPrepared;
        try
        {
            player.SetDataSource(wavPath);
            player.PrepareAsync();
            await prepared.Task;
        }
        finally
        {
            player.Prepared -= OnPrepared;
        }

        _playerReady = true;

        if (!token.IsCancellationRequested)
        {
            ApplyPlaybackRate(player);
            player.Start();
            LogFirstAudio();
            ChunkChanged?.Invoke(this, readChunk);
            UpdateState(PlaybackState.Playing);
            StartPolling(player, token);
        }

        using (token.Register(() => ended.TrySetResult(false)))
        {
            return await ended.Task;
        }
    }

    private void LogFirstAudio()
    {
        if (Interlocked.Exchange(ref _firstAudioPending, 0) == 1)
        {
            _log.Debug($"first audio after {Stopwatch.GetElapsedTime(_readStartTicks).TotalMilliseconds:F0} ms");
        }
    }

    public void Pause()
    {
        _player?.Pause();
        UpdateState(PlaybackState.Paused);
    }

    public void Resume()
    {
        if (_player is { } player)
        {
            player.Start();
            UpdateState(PlaybackState.Playing);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _synthCts?.Cancel();
            _generation++;
        }

        _chunkEnded?.TrySetResult(false);
        StopPolling();
        _player?.Reset();
        _playerReady = false;
        DeletePreviousChunkFile();
        DeleteCurrentChunkFile();
        _chunkCount = 0;
        ProgressChanged?.Invoke(this, 0);
        _timing.Reset();
        RaiseTiming(TimeSpan.Zero);
        UpdateState(PlaybackState.Idle);
    }

    public void SetSpeed(PlaybackRate speed)
    {
        _playbackRate = speed.Value;
        if (_playerReady && _player is { } player)
        {
            ApplyPlaybackRate(player);
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
    private static OfflineTtsGeneratedAudio GenerateAudio(BuiltEngine engine, string text, int speakerId)
    {
        VoiceModelPaths model = engine.Model;
        if (model.Kind != VoiceEngineKind.Supertonic)
        {
            return engine.Tts.Tts.Generate(text, 1.0f, speakerId);
        }

        var config = new OfflineTtsGenerationConfig { Sid = speakerId, Speed = 1.0f };
        config.Extra[SupertonicLanguageKey] = model.Language;
        return engine.Tts.Tts.GenerateWithConfig(text, config, KeepGenerating);
    }

    // Returning 1 tells sherpa-onnx to keep generating (0 would stop early).
    private static int KeepGenerating(IntPtr samples, int count, float progress, IntPtr arg) => 1;

    // Returns the engine for the language's current model with a reference taken for the
    // caller, who must Release() it. A different pack or engine kind rebuilds the OfflineTts;
    // the same pack under another language reuses it with the new model. The superseded
    // OfflineTts is disposed once the last read or generation holding it lets go.
    private BuiltEngine? AcquireEngine()
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

    // Takes the caller's reference on the current engine. A refused reference means the
    // engine was already disposed, so it is rebuilt once from scratch; if that is refused too
    // the read is not ready. A disposed engine is never handed out and a reference that was
    // not acquired is never released. Called under _ttsGate.
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

        var fresh = new BuiltEngine(new RefCountedTts(BuildTts(paths)), paths);
        current?.Release();
        return fresh;
    }

    private static OfflineTts BuildTts(VoiceModelPaths paths)
    {
        var config = new OfflineTtsConfig();
        switch (paths.Kind)
        {
            case VoiceEngineKind.Supertonic:
                ConfigureSupertonic(ref config, paths.RootDir);
                break;
            case VoiceEngineKind.Vits:
                ConfigureVits(ref config, paths.RootDir);
                break;
            case VoiceEngineKind.Melo:
                ConfigureMelo(ref config, paths.RootDir);
                break;
            default:
                throw new NotSupportedException($"No engine for voice model kind {paths.Kind}.");
        }

        config.Model.NumThreads = SynthesisThreads;
        config.Model.Provider = "cpu";
        RequireModelFiles(paths, config);
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
            _ => [config.Model.Vits.Model, config.Model.Vits.Tokens],
        };

        foreach (string file in required)
        {
            if (string.IsNullOrEmpty(file) || !File.Exists(file))
            {
                string message = $"Voice model file missing for {paths.Kind}: '{file}'.";
                System.Diagnostics.Debug.WriteLine(message);
                throw new FileNotFoundException(message, file);
            }
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
    private static void ConfigureVits(ref OfflineTtsConfig config, string dir)
    {
        config.Model.Vits.Model = Path.Combine(dir, VitsFiles.Model);
        config.Model.Vits.Tokens = Path.Combine(dir, VitsFiles.Tokens);
        config.Model.Vits.DataDir = Path.Combine(dir, VitsFiles.EspeakDataDir);
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
    public Task PrepareAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.CompletedTask;
        }

        // The token is taken here, synchronously and in call order, so a later call always
        // supersedes an earlier one and CancelPrepare reaches a prepare still queued on the pool.
        return BeginPrepare() is { } token
            ? Task.Run(() => PrepareOffCallerAsync(text, token))
            : Task.CompletedTask;
    }

    // Runs on the thread pool, so acquiring the engine (which may build the model), planning the
    // chunks and indexing the cache never block the UI thread that asked.
    private async Task PrepareOffCallerAsync(string text, CancellationToken token)
    {
        if (token.IsCancellationRequested || AcquireEngine() is not { } engine)
        {
            return;
        }

        try
        {
            token.ThrowIfCancellationRequested();
            await PrepareChunksAsync(engine, text, token);
        }
        catch (OperationCanceledException)
        {
            // Superseded or cancelled: nothing to clean up.
        }
        catch (Exception ex)
        {
            _log.Warning("head start failed", error: ex);
        }
        finally
        {
            engine.Release();
        }
    }

    private async Task PrepareChunksAsync(BuiltEngine engine, string text, CancellationToken token)
    {
        VoiceInfo voice = ResolveVoice(engine, Volatile.Read(ref _voiceId));
        string prefix = text.Length > PrepareMaxChars ? text[..PrepareMaxChars] : text;
        IEnumerable<string> sentences = SpeechChunkPlanner.ForText(prefix, HeadPolicy)
            .Take(PrepareMaxChunks)
            .SelectMany(chunk => SentenceUnits.Split(chunk.Text));

        await Task.WhenAll(sentences.Select(s => GetSentenceAsync(engine, s, voice, _slots, token)));
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

    // A newer prepare supersedes the one in flight; null when a read is in progress, which
    // must never share the engine with a prepare. Checked under the same lock that
    // BeginRead takes.
    private CancellationToken? BeginPrepare()
    {
        lock (_gate)
        {
            if (_readsInProgress > 0)
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
        if (_player is null)
        {
            _player = new AndroidMedia.MediaPlayer();
            _player.Completion += (_, _) => _chunkEnded?.TrySetResult(true);
        }

        return _player;
    }

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
        _pollCts = cts;
        _ = PollLoopAsync(player, cts.Token);
    }

    private void StopPolling()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
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
        if (chunkCount == 0)
        {
            return;
        }

        double withinChunk = player.Duration > 0 ? (double)player.CurrentPosition / player.Duration : 0;
        double fraction = (chunkIndex + Math.Clamp(withinChunk, 0, 1)) / chunkCount;
        ProgressChanged?.Invoke(this, Math.Clamp(fraction, 0, 1));

        if (Stopwatch.GetElapsedTime(_lastTimingRaiseTicks) >= TimingRaiseInterval)
        {
            RaiseTiming(CurrentChunkPosition());
        }
    }

    // Zero unless a chunk is actually playing (player prepared and the reader not Idle), so a
    // finished or reset player can never leak its position into the next read's timer.
    private TimeSpan CurrentChunkPosition() =>
        _playerReady && _state != PlaybackState.Idle && _player is { } player && player.Duration > 0
            ? TimeSpan.FromMilliseconds(player.CurrentPosition)
            : TimeSpan.Zero;

    private void RaiseTiming(TimeSpan positionInCurrentChunk)
    {
        _lastTimingRaiseTicks = Stopwatch.GetTimestamp();
        TimingChanged?.Invoke(this, _timing.CurrentTiming(positionInCurrentChunk));
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
            TryDelete(path);
            _previousChunkFile = null;
        }
    }

    private void DeleteCurrentChunkFile()
    {
        if (_currentChunkFile is { } path)
        {
            TryDelete(path);
            _currentChunkFile = null;
        }
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
        _player?.Release();
        _player = null;
        _playerReady = false;
        DeletePreviousChunkFile();
        DeleteCurrentChunkFile();
        lock (_ttsGate)
        {
            _disposed = true;
            _engine?.Release();
            _engine = null;
        }
    }

    /// <summary>An <see cref="OfflineTts"/> disposed when its last holder releases it. The
    /// reader holds one reference while the engine is current; each read and each in-flight
    /// generation holds another.</summary>
    private sealed class RefCountedTts(OfflineTts tts)
    {
        private int _references = 1;

        public OfflineTts Tts { get; } = tts;

        public bool IsAlive => Volatile.Read(ref _references) > 0;

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

    /// <summary>An engine and the model it was built from, never changed after creation.
    /// A language change within one pack shares the <see cref="RefCountedTts"/>.</summary>
    private sealed record BuiltEngine(RefCountedTts Tts, VoiceModelPaths Model)
    {
        public bool TryAcquire() => Tts.TryAcquire();

        public void Release() => Tts.Release();
    }
}
