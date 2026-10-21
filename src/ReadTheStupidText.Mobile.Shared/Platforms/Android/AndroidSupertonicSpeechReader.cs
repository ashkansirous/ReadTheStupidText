using System.Collections.Concurrent;
using System.Diagnostics;
using AndroidMedia = Android.Media;
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
public sealed class AndroidSupertonicSpeechReader : ISpeechReader, IDisposable
{
    // Same reasoning as the Windows reader: intra-op ONNX threads per synthesis,
    // scaled to the device but capped so a single Generate doesn't over-claim
    // cores that the concurrent chunks below also need.
    private static readonly int SynthesisThreads = Math.Clamp(Environment.ProcessorCount / 2, 2, 4);

    private static readonly int MaxSynthesisConcurrency =
        Math.Clamp(Environment.ProcessorCount / SynthesisThreads, 2, 4);

    private const string WarmUpText = "Ready.";

    // OfflineTtsGenerationConfig.Extra key that selects the Supertonic language.
    private const string SupertonicLanguageKey = "lang";

    private static readonly TimeSpan TimingRaiseInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SkipAmount = TimeSpan.FromSeconds(10);

    // How often the poll loop checks MediaPlayer's position while a chunk plays.
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

    private readonly IVoiceModelService _model;
    private readonly ReadTimingTracker _timing = new();

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

    public AndroidSupertonicSpeechReader(IVoiceModelService model)
    {
        _model = model;
        _model.ReadyChanged += OnModelReadyChanged;
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
        StartReadAsync(SpeechChunkPlanner.ForText(text));

    // Slice 60: each segment is chunked on its own and each chunk is synthesized in its
    // segment's voice, so a character keeps one voice for the whole read.
    public Task SpeakSegmentsAsync(IReadOnlyList<VoicedSegment> segments, int? activityId = null) =>
        StartReadAsync(SpeechChunkPlanner.ForSegments(segments));

    private async Task StartReadAsync(IReadOnlyList<SpeechChunkPlan> chunks)
    {
        BuiltEngine engine = AcquireEngine() ?? throw new NeuralEngineUnavailableException();

        try
        {
            Volatile.Write(ref _activeEngine, engine);
            (int generation, CancellationToken token) = BeginGeneration();

            _chunks = chunks;
            _timing.Start(chunks.Count);
            RaiseTiming(TimeSpan.Zero);

            await SpeakChunksAsync(engine, chunks, 0, Volatile.Read(ref _voiceId), generation, token);
        }
        finally
        {
            Interlocked.CompareExchange(ref _activeEngine, null, engine);
            engine.Release();
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
            UpdateState(PlaybackState.Idle);
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }

    // Kicks off synthesis of every remaining chunk; the first runs straight away and the
    // rest wait for it, so the chunk the listener waits on is never starved.
    private List<Task<string>> StartGenerations(
        BuiltEngine engine, IReadOnlyList<SpeechChunkPlan> chunks, int startIndex, string? voiceId,
        CancellationToken token)
    {
        var slots = new SemaphoreSlim(MaxSynthesisConcurrency);
        var generations = new List<Task<string>>();
        Task<string>? firstChunk = null;
        for (int index = startIndex; index < chunks.Count; index++)
        {
            int speaker = ResolveVoice(engine, chunks[index].VoiceId, voiceId).SpeakerId;
            Task<string> chunkTask = index == startIndex
                ? GenerateChunkAsync(engine, chunks[index].Text, index, speaker, slots, token)
                : GenerateAfterAsync(firstChunk!, engine, chunks[index].Text, index, speaker, slots, token);
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
        Task gate, BuiltEngine engine, string chunk, int index, int speakerId, SemaphoreSlim slots, CancellationToken token)
    {
        try
        {
            await gate;
        }
        catch
        {
            // Swallowed: the first chunk's real outcome surfaces on its own await.
        }

        return await GenerateChunkAsync(engine, chunk, index, speakerId, slots, token);
    }

    private async Task<string> GenerateChunkAsync(
        BuiltEngine engine, string chunk, int index, int speakerId, SemaphoreSlim slots, CancellationToken token)
    {
        await slots.WaitAsync(token);
        try
        {
            Task<OfflineTtsGeneratedAudio> generating = GenerateGuardedAsync(engine, chunk, speakerId);
            OfflineTtsGeneratedAudio audio = await WaitOrAbandonAsync(generating, token);
            try
            {
                RecordChunkDuration(index, audio);
                return await WriteWavFileAsync(audio.Samples, audio.SampleRate);
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

    // The native Generate call cannot be interrupted. When the caller is cancelled the call
    // is abandoned: a continuation observes its outcome, so a fault is never unobserved and
    // a result that arrives late is disposed instead of leaking its native buffer.
    private static async Task<OfflineTtsGeneratedAudio> WaitOrAbandonAsync(
        Task<OfflineTtsGeneratedAudio> generating, CancellationToken token)
    {
        try
        {
            return await generating.WaitAsync(token);
        }
        catch (OperationCanceledException)
        {
            _ = generating.ContinueWith(DisposeAbandoned, TaskScheduler.Default);
            throw;
        }
    }

    private static void DisposeAbandoned(Task<OfflineTtsGeneratedAudio> abandoned)
    {
        if (abandoned.IsCompletedSuccessfully)
        {
            abandoned.Result.Dispose();
        }
        else
        {
            _ = abandoned.Exception;
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

    private void RecordChunkDuration(int index, OfflineTtsGeneratedAudio audio)
    {
        TimeSpan duration = audio.SampleRate > 0
            ? TimeSpan.FromSeconds((double)audio.Samples.Length / audio.SampleRate)
            : TimeSpan.Zero;

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
            ChunkChanged?.Invoke(this, readChunk);
            UpdateState(PlaybackState.Playing);
            StartPolling(player, token);
        }

        using (token.Register(() => ended.TrySetResult(false)))
        {
            return await ended.Task;
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

    private TimeSpan CurrentChunkPosition() =>
        _player is { } player && player.Duration > 0
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

    private static async Task<string> WriteWavFileAsync(float[] samples, int sampleRate)
    {
        byte[] wav = EncodeWav(samples, sampleRate);
        string path = Path.Combine(FileSystem.Current.CacheDirectory, $"read-chunk-{Guid.NewGuid():N}.wav");
        await File.WriteAllBytesAsync(path, wav);
        return path;
    }

    // Encodes mono float samples as a 16-bit PCM WAV (RIFF) byte buffer —
    // identical to the Windows reader's encoder (pure C#, no WinRT types).
    private static byte[] EncodeWav(float[] samples, int sampleRate)
    {
        const int bitsPerSample = 16;
        const short channels = 1;
        int dataBytes = samples.Length * sizeof(short);
        int blockAlign = channels * bitsPerSample / 8;

        using var memory = new MemoryStream(44 + dataBytes);
        using var writer = new BinaryWriter(memory);

        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * blockAlign);
        writer.Write((short)blockAlign);
        writer.Write((short)bitsPerSample);
        writer.Write("data"u8.ToArray());
        writer.Write(dataBytes);

        foreach (float sample in samples)
        {
            writer.Write((short)(Math.Clamp(sample, -1f, 1f) * short.MaxValue));
        }

        writer.Flush();
        return memory.ToArray();
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
