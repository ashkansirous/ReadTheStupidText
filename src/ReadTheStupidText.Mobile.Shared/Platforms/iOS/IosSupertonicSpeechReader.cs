using System.Collections.Concurrent;
using System.Diagnostics;
using AVFoundation;
using Foundation;
using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Speaks text with the local neural engine on iOS (Slices 49 and 55), the iOS counterpart of
/// <c>AndroidSupertonicSpeechReader</c>: the same sherpa-onnx synthesis (Supertonic, Piper and
/// Melo packs), <see cref="SpeechTextChunker"/> chunking, sentence cache and head start, ordered
/// playback, chunk-boundary voice swap and skip logic, with <see cref="AVAudioPlayer"/> as the
/// playback primitive.
/// </summary>
/// <remarks>
/// Each chunk is joined from its cached or synthesized sentences, written to a temp WAV file and
/// played by a fresh <see cref="AVAudioPlayer"/> (rate-adjustable, pitch-preserving). Read-through
/// progress and timing come from a short poll loop that samples the player's position on the main
/// thread. The logic is deliberately a copy of the Android reader rather than a shared base class
/// (see Slice 49): folding the two together is a follow-up now that both have landed. Player calls
/// happen on the main thread (AVFoundation delivers its callbacks there). The playback half is in
/// <c>IosSupertonicSpeechReader.Playback.cs</c>, the head start in <c>...HeadStart.cs</c>.
/// </remarks>
public sealed partial class IosSupertonicSpeechReader : ISpeechReader, IHeadStart, IDisposable
{
    private static readonly int MaxSynthesisConcurrency = IosTtsEngine.MaxSynthesisConcurrency;

    private const string WarmUpText = "Ready.";

    // Slice 47: sentence audio is cached under CacheDirectory/sentences, keyed by sentence + voice
    // + engine + language (SentenceCacheKey). A chunk is played from a temp file joined out of its
    // cached sentences, so a cached file is never opened by the player or deleted by playback.
    private const string SentenceFolderName = "sentences";
    private const string ChunkFilePrefix = "read-chunk-";
    private const string ChunkFileExtension = ".wav";
    private const string ChunkFilePattern = ChunkFilePrefix + "*" + ChunkFileExtension;

    // The cache makes a two-sentence head cheap (prepared ahead); the shared chunker's default
    // stays one short sentence for a reader without a cache.
    private static readonly SpeechTextChunker.HeadPolicy HeadPolicy = SpeechTextChunker.HeadPolicy.Cached;

    // Silence between the sentences of one chunk (each was synthesized on its own).
    private static readonly TimeSpan SentenceGap = TimeSpan.FromMilliseconds(150);

    private static readonly TimeSpan TimingRaiseInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SkipAmount = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

    private readonly IVoiceModelService _model;
    private readonly ISystemLog _log;
    private readonly SentenceAudioSource _sentences;
    private readonly ReadTimingTracker _timing = new();
    private readonly NSObject _interruptionObserver;

    // One engine-level pool of native synthesis slots shared by reads and head-start prepares,
    // so their combined concurrency never exceeds MaxSynthesisConcurrency.
    private readonly SemaphoreSlim _slots = new(MaxSynthesisConcurrency);

    private readonly object _gate = new();
    private readonly object _ttsGate = new();

    // The engine plus the model it was built from, swapped as one object under _ttsGate
    // (Slice 51: engine kind, language, voices). Each read captures one engine and uses its Model
    // for everything, so a language or pack change mid-read can't mix models.
    private IosBuiltEngine? _engine;
    private bool _disposed;
    private string? _voiceId;
    private readonly ConcurrentDictionary<VoiceEngineKind, string> _voiceByKind = new();
    private int _warmedUp;

    private AVAudioPlayer? _player;
    private string? _currentChunkFile;
    private string? _previousChunkFile;

    private double _playbackRate = PlaybackRate.Default.Value;
    private PlaybackState _state = PlaybackState.Idle;
    private bool _pausedByInterruption;
    private bool _playerFinished;

    // A chunk whose player started while the reader was paused: announced when Resume plays it.
    private ReadChunk? _unannouncedChunk;

    private TaskCompletionSource<bool>? _chunkEnded;
    private CancellationTokenSource? _pollCts;

    // The poll loop's last sample of the playing chunk, in ticks. Written on the main thread,
    // read from the synthesis threads (timing) and the UI (skip), so no native player is
    // touched off the main thread.
    private long _positionTicks;
    private long _durationTicks;

    private int _generation;
    private CancellationTokenSource? _synthCts;
    private CancellationTokenSource? _prepareCts;

    // The read between its start and its end (changed under _gate): longer than PlaybackState,
    // which is Idle while the first chunk is still being synthesized. It ends only when its last
    // owner leaves, so a Skip-continued playback keeps it alive after the original call returns.
    // A head-start prepare never runs while it is set.
    private ActiveRead? _activeRead;

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

    // Raised once each chunk has started playing, with its real source range (Slice 60).
    public event EventHandler<ReadChunk>? ChunkChanged;

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

    public IosSupertonicSpeechReader(IVoiceModelService model, ISystemLog? log = null)
    {
        _model = model;
        _log = log ?? new IosSystemLog();
        string cacheRoot = FileSystem.Current.CacheDirectory;
        _sentences = new SentenceAudioSource(
            new SentenceAudioCache(new FileSentenceStore(Path.Combine(cacheRoot, SentenceFolderName))), _log);
        PurgeOrphanedChunkFiles(cacheRoot);
        _interruptionObserver = IosAudioSession.ObserveInterruptions(OnInterruptionBegan, OnInterruptionEnded);
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

    // On a cold start the model is only resolved after a background scan, so a warm-up asked for
    // before that found nothing to warm: warm once, the first time the model reports ready.
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
        IosBuiltEngine engine = AcquireEngine() ?? throw new NeuralEngineUnavailableException();

        ActiveRead? read = null;
        try
        {
            read = BeginRead(engine);

            // Once per read (and again after an interruption ends), not once per chunk.
            await MainThread.InvokeOnMainThreadAsync(() => IosAudioSession.Activate(this));
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
            if (read is not null)
            {
                LeaveRead(read);
            }

            engine.Release();
        }
    }

    // Makes this the active read and cancels any prepare, atomically with a prepare's own check,
    // so a prepare either finished its start before the read or never starts.
    private ActiveRead BeginRead(IosBuiltEngine engine)
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

        IosBuiltEngine engine = read.Engine;
        if (!engine.TryAcquire())
        {
            LeaveRead(read);
            return;
        }

        try
        {
            await SkipOnEngineAsync(engine, delta);
        }
        finally
        {
            engine.Release();
            LeaveRead(read);
        }
    }

    private async Task SkipOnEngineAsync(IosBuiltEngine engine, TimeSpan delta)
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

    // Same ordered-consume design as the Windows and Android readers. The voice is a parameter
    // (not the mutable field) so a mid-read voice change can't half-apply to already-queued
    // chunks. The chunk files of this generation are deleted when it ends, however it ends.
    private async Task SpeakChunksAsync(
        IosBuiltEngine engine, IReadOnlyList<SpeechChunkPlan> chunks, int startIndex, string? voiceId, int generation,
        CancellationToken token)
    {
        _chunkCount = chunks.Count;
        _currentChunkIndex = startIndex;
        ProgressChanged?.Invoke(this, chunks.Count == 0 ? 0 : (double)startIndex / chunks.Count);

        var files = new ConcurrentQueue<string>();
        List<Task<string>> generations = StartGenerations(engine, chunks, startIndex, voiceId, files, token);
        bool drained = false;
        try
        {
            for (int offset = 0; offset < generations.Count; offset++)
            {
                int index = startIndex + offset;
                if (!await PlayGeneratedChunkAsync(generations[offset], chunks, index, generation, token))
                {
                    return;
                }

                if (await RestartForVoiceSwapAsync(engine, chunks, index, voiceId, generations, files))
                {
                    drained = true; // the restart drained this generation before it ran on
                    return;
                }
            }

            if (IsCurrent(generation))
            {
                await FinishAsync(generation);
            }
        }
        finally
        {
            if (!drained)
            {
                _ = DrainAsync(generations, files);
            }
        }
    }

    // Natural end of a read, as AvSpeechReader.Finish does: stop polling, free the player, delete
    // the last chunk file and hand the audio session back, then leave the same state Stop() does
    // (progress and timer back at zero, so a stale player never feeds the next read's timer), all
    // before Completed.
    private async Task FinishAsync(int generation)
    {
        StopPolling();
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ReleasePlayer();
            DeletePreviousChunkFile();
            DeleteCurrentChunkFile();
            IosAudioSession.Deactivate(this);
        });

        if (!IsCurrent(generation))
        {
            return;
        }

        ResetProgress();
        UpdateState(PlaybackState.Idle);
        Completed?.Invoke(this, EventArgs.Empty);
    }

    private void ResetProgress()
    {
        _chunkCount = 0;
        Volatile.Write(ref _positionTicks, 0);
        Volatile.Write(ref _durationTicks, 0);
        ProgressChanged?.Invoke(this, 0);
        _timing.Reset();
        RaiseTiming(TimeSpan.Zero);
    }

    // Kicks off synthesis of every remaining chunk; the first runs straight away and the rest
    // wait for it, so the chunk the listener waits on is never starved.
    private List<Task<string>> StartGenerations(
        IosBuiltEngine engine, IReadOnlyList<SpeechChunkPlan> chunks, int startIndex, string? voiceId,
        ConcurrentQueue<string> files, CancellationToken token)
    {
        var generations = new List<Task<string>>();
        Task<string>? firstChunk = null;
        for (int index = startIndex; index < chunks.Count; index++)
        {
            VoiceInfo voice = ResolveVoice(engine, chunks[index].VoiceId, voiceId);
            Task<string> chunkTask = index == startIndex
                ? GenerateChunkAsync(engine, chunks[index].Text, index, voice, files, token)
                : GenerateAfterAsync(firstChunk!, engine, chunks[index].Text, index, voice, files, token);
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

    // A chunk with its own voice (a segment read) never follows the narrator picker; a plain read
    // restarts at the next chunk when the selected voice changed mid-read. Voices are compared by
    // id within the read's own model, so a language or pack change never restarts a read. True
    // when it restarted (the restarted loop finishes the read, so the caller returns). The superseded
    // generation's chunk files are drained before the restarted loop runs, not after it: the
    // restarted loop plays the rest of the read, and the old files would sit on disk until then.
    private async Task<bool> RestartForVoiceSwapAsync(
        IosBuiltEngine engine, IReadOnlyList<SpeechChunkPlan> chunks, int index, string? voiceId,
        List<Task<string>> generations, ConcurrentQueue<string> files)
    {
        string? selected = Volatile.Read(ref _voiceId);
        bool sameVoice = ResolveVoice(engine, selected).Id == ResolveVoice(engine, voiceId).Id;
        if (sameVoice || index + 1 >= chunks.Count || chunks[index + 1].VoiceId is not null)
        {
            return false;
        }

        (int nextGeneration, CancellationToken nextToken) = BeginGeneration(); // cancels the old synthesis
        _ = DrainAsync(generations, files);
        await SpeakChunksAsync(engine, chunks, index + 1, selected, nextGeneration, nextToken);
        return true;
    }

    // The first of the given ids the read's model offers (a chunk's own voice, then the selected
    // one), then the voice last picked for this engine family, then the model's default: a Piper
    // pick for another language never displaces the Supertonic choice.
    private VoiceInfo ResolveVoice(IosBuiltEngine engine, params string?[] ids)
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
            IosBuiltEngine? engine = AcquireEngine();
            if (engine is null)
            {
                return;
            }

            try
            {
                Volatile.Write(ref _warmedUp, 1);
                int speaker = ResolveVoice(engine).SpeakerId;
                IosTtsEngine.Generate(engine, WarmUpText, speaker).Dispose();
            }
            finally
            {
                engine.Release();
            }
        }
        catch
        {
            // Best-effort: AcquireEngine() in StartReadAsync remains the safety net.
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

    public void SetSpeed(PlaybackRate speed)
    {
        _playbackRate = speed.Value;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_player is { } player)
            {
                player.Rate = (float)_playbackRate;
            }
        });
    }

    public void Stop()
    {
        lock (_gate)
        {
            _synthCts?.Cancel();
            _generation++;
            _activeRead = null;
        }

        _pausedByInterruption = false;
        _chunkEnded?.TrySetResult(false);
        StopPolling();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ReleasePlayer();
            DeletePreviousChunkFile();
            DeleteCurrentChunkFile();
            IosAudioSession.Deactivate(this);
        });
        ResetProgress();
        UpdateState(PlaybackState.Idle);
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

    public void Dispose()
    {
        _model.ReadyChanged -= OnModelReadyChanged;
        _interruptionObserver.Dispose();
        StopPolling();
        CancelPrepare();
        _synthCts?.Cancel();
        _synthCts?.Dispose();
        ReleasePlayer();
        DeletePreviousChunkFile();
        DeleteCurrentChunkFile();
        lock (_ttsGate)
        {
            _disposed = true;
            _engine?.Release();
            _engine = null;
        }
    }

    /// <summary>The read in progress: its engine and who still owns it (the original read and
    /// any Skip that continued its playback).</summary>
    private sealed record ActiveRead(IosBuiltEngine Engine, ReadOwnership Owners);
}
