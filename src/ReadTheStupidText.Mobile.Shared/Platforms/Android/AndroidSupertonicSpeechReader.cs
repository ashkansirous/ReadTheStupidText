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

    private static readonly TimeSpan TimingRaiseInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SkipAmount = TimeSpan.FromSeconds(10);

    // How often the poll loop checks MediaPlayer's position while a chunk plays.
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

    private readonly IVoiceModelService _model;
    private readonly ReadTimingTracker _timing = new();

    private readonly object _gate = new();
    private readonly object _ttsGate = new();

    private OfflineTts? _tts;
    private AndroidMedia.MediaPlayer? _player;
    private string? _currentChunkFile;
    private string? _previousChunkFile;

    private double _playbackRate = PlaybackRate.Default.Value;
    private int _speakerId = SupertonicVoiceTable.DefaultSpeakerId;
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

    public AndroidSupertonicSpeechReader(IVoiceModelService model) => _model = model;

    public Task SpeakAsync(string text, int? activityId = null) =>
        StartReadAsync(SpeechChunkPlanner.ForText(text));

    // Slice 60: each segment is chunked on its own and each chunk is synthesized in its
    // segment's voice, so a character keeps one voice for the whole read.
    public Task SpeakSegmentsAsync(IReadOnlyList<VoicedSegment> segments, int? activityId = null) =>
        StartReadAsync(SpeechChunkPlanner.ForSegments(segments));

    private async Task StartReadAsync(IReadOnlyList<SpeechChunkPlan> chunks)
    {
        OfflineTts? tts = EnsureTts();
        if (tts is null)
        {
            return;
        }

        (int generation, CancellationToken token) = BeginGeneration();

        _chunks = chunks;
        _timing.Start(chunks.Count);
        RaiseTiming(TimeSpan.Zero);

        await SpeakChunksAsync(tts, chunks, 0, Volatile.Read(ref _speakerId), generation, token);
    }

    public Task SkipForward() => SkipByAsync(SkipAmount);

    public Task SkipBackward() => SkipByAsync(-SkipAmount);

    private async Task SkipByAsync(TimeSpan delta)
    {
        OfflineTts? tts = Volatile.Read(ref _tts);
        if (tts is null || _state == PlaybackState.Idle)
        {
            return;
        }

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

        await SpeakChunksAsync(tts, _chunks, target.ChunkIndex, Volatile.Read(ref _speakerId), generation, newToken);
    }

    // Same ordered-consume design as the Windows reader — see that file for the
    // full rationale. Speaker is a parameter (not the mutable field) so a
    // mid-read voice change can't half-apply to already-queued chunks.
    private async Task SpeakChunksAsync(
        OfflineTts tts, IReadOnlyList<SpeechChunkPlan> chunks, int startIndex, int speakerId, int generation,
        CancellationToken token)
    {
        _chunkCount = chunks.Count;
        _currentChunkIndex = startIndex;
        ProgressChanged?.Invoke(this, chunks.Count == 0 ? 0 : (double)startIndex / chunks.Count);

        List<Task<string>> generations = StartGenerations(tts, chunks, startIndex, speakerId, token);
        for (int offset = 0; offset < generations.Count; offset++)
        {
            int index = startIndex + offset;
            if (!await PlayGeneratedChunkAsync(generations[offset], chunks, index, generation, token))
            {
                return;
            }

            if (await RestartForVoiceSwapAsync(tts, chunks, index, speakerId))
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
        OfflineTts tts, IReadOnlyList<SpeechChunkPlan> chunks, int startIndex, int speakerId,
        CancellationToken token)
    {
        var slots = new SemaphoreSlim(MaxSynthesisConcurrency);
        var generations = new List<Task<string>>();
        Task<string>? firstChunk = null;
        for (int index = startIndex; index < chunks.Count; index++)
        {
            int speaker = SpeakerFor(chunks[index], speakerId);
            Task<string> chunkTask = index == startIndex
                ? GenerateChunkAsync(tts, chunks[index].Text, index, speaker, slots, token)
                : GenerateAfterAsync(firstChunk!, tts, chunks[index].Text, index, speaker, slots, token);
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
    // read restarts at the next chunk when the selected voice changed mid-read. True when it
    // restarted (the restarted loop finishes the read, so the caller returns).
    private async Task<bool> RestartForVoiceSwapAsync(
        OfflineTts tts, IReadOnlyList<SpeechChunkPlan> chunks, int index, int speakerId)
    {
        int selected = Volatile.Read(ref _speakerId);
        if (selected == speakerId || index + 1 >= chunks.Count || chunks[index + 1].VoiceId is not null)
        {
            return false;
        }

        (int nextGeneration, CancellationToken nextToken) = BeginGeneration();
        await SpeakChunksAsync(tts, chunks, index + 1, selected, nextGeneration, nextToken);
        return true;
    }

    // A chunk's own voice (segment reads) wins; a plain read uses the selected speaker.
    private static int SpeakerFor(SpeechChunkPlan chunk, int selectedSpeakerId) =>
        chunk.VoiceId is { } voiceId ? SupertonicVoiceTable.SpeakerIdFor(voiceId) : selectedSpeakerId;

    public Task WarmUpAsync() => Task.Run(WarmUp);

    private void WarmUp()
    {
        try
        {
            OfflineTts? tts = EnsureTts();
            _ = tts?.Generate(WarmUpText, 1.0f, _speakerId);
        }
        catch
        {
            // Best-effort: EnsureTts() in SpeakAsync remains the safety net.
        }
    }

    private async Task<string> GenerateAfterAsync(
        Task gate, OfflineTts tts, string chunk, int index, int speakerId, SemaphoreSlim slots, CancellationToken token)
    {
        try
        {
            await gate;
        }
        catch
        {
            // Swallowed: the first chunk's real outcome surfaces on its own await.
        }

        return await GenerateChunkAsync(tts, chunk, index, speakerId, slots, token);
    }

    private async Task<string> GenerateChunkAsync(
        OfflineTts tts, string chunk, int index, int speakerId, SemaphoreSlim slots, CancellationToken token)
    {
        await slots.WaitAsync(token);
        try
        {
            OfflineTtsGeneratedAudio audio = await Task.Run(() => tts.Generate(chunk, 1.0f, speakerId), token);
            RecordChunkDuration(index, audio);
            return await WriteWavFileAsync(audio.Samples, audio.SampleRate);
        }
        finally
        {
            slots.Release();
        }
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

    public void SetVoice(string voiceId) =>
        Volatile.Write(ref _speakerId, SupertonicVoiceTable.SpeakerIdFor(voiceId));

    private OfflineTts? EnsureTts()
    {
        OfflineTts? existing = Volatile.Read(ref _tts);
        if (existing is not null)
        {
            return existing;
        }

        if (_model.Paths is not { } paths)
        {
            return null;
        }

        lock (_ttsGate)
        {
            if (_tts is not null)
            {
                return _tts;
            }

            OfflineTts built = BuildTts(paths.RootDir);
            Volatile.Write(ref _tts, built);
            return built;
        }
    }

    private static OfflineTts BuildTts(string dir)
    {
        var config = new OfflineTtsConfig();
        config.Model.Supertonic.DurationPredictor = Path.Combine(dir, SupertonicFiles.DurationPredictor);
        config.Model.Supertonic.TextEncoder = Path.Combine(dir, SupertonicFiles.TextEncoder);
        config.Model.Supertonic.VectorEstimator = Path.Combine(dir, SupertonicFiles.VectorEstimator);
        config.Model.Supertonic.Vocoder = Path.Combine(dir, SupertonicFiles.Vocoder);
        config.Model.Supertonic.TtsJson = Path.Combine(dir, SupertonicFiles.TtsJson);
        config.Model.Supertonic.UnicodeIndexer = Path.Combine(dir, SupertonicFiles.UnicodeIndexer);
        config.Model.Supertonic.VoiceStyle = Path.Combine(dir, SupertonicFiles.VoiceStyle);
        config.Model.NumThreads = SynthesisThreads;
        config.Model.Provider = "cpu";
        return new OfflineTts(config);
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
        StopPolling();
        _synthCts?.Cancel();
        _synthCts?.Dispose();
        _player?.Release();
        _player = null;
        _playerReady = false;
        DeletePreviousChunkFile();
        DeleteCurrentChunkFile();
        _tts?.Dispose();
    }
}
