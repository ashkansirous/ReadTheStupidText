using System.Collections.Concurrent;
using System.Diagnostics;
using AVFoundation;
using Foundation;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using SherpaOnnx;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Speaks text with the local Supertonic-3 neural engine on iOS (Slice 49), the iOS
/// counterpart of <c>AndroidSupertonicSpeechReader</c>: the same sherpa-onnx synthesis,
/// <see cref="SpeechTextChunker"/> chunking, ordered playback, chunk-boundary voice swap and
/// skip logic, with <see cref="AVAudioPlayer"/> as the playback primitive.
/// </summary>
/// <remarks>
/// Each synthesized chunk is written to a temp WAV file and played by a fresh
/// <see cref="AVAudioPlayer"/> (rate-adjustable, pitch-preserving). Read-through progress
/// and timing come from a short poll loop that samples the player's position on the main
/// thread. The logic is deliberately a copy of the Android reader rather than a shared base
/// class: Slice 60 is editing the Android reader and the shared reading code in parallel, so
/// extracting a base now would conflict; folding the two together is a follow-up once both
/// have landed. Player calls happen on the main thread (AVFoundation delivers its callbacks
/// there).
/// </remarks>
public sealed class IosSupertonicSpeechReader : ISpeechReader, IDisposable
{
    private static readonly int SynthesisThreads = Math.Clamp(Environment.ProcessorCount / 2, 2, 4);

    private static readonly int MaxSynthesisConcurrency =
        Math.Clamp(Environment.ProcessorCount / SynthesisThreads, 2, 4);

    private const string WarmUpText = "Ready.";

    private static readonly TimeSpan TimingRaiseInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SkipAmount = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

    private readonly IVoiceModelService _model;
    private readonly ReadTimingTracker _timing = new();
    private readonly NSObject _interruptionObserver;

    private readonly object _gate = new();
    private readonly object _ttsGate = new();

    private OfflineTts? _tts;
    private AVAudioPlayer? _player;
    private string? _currentChunkFile;
    private string? _previousChunkFile;

    private double _playbackRate = PlaybackRate.Default.Value;
    private int _speakerId = SupertonicVoiceTable.DefaultSpeakerId;
    private PlaybackState _state = PlaybackState.Idle;
    private bool _pausedByInterruption;
    private bool _playerFinished;

    private TaskCompletionSource<bool>? _chunkEnded;
    private CancellationTokenSource? _pollCts;

    // The poll loop's last sample of the playing chunk, in ticks. Written on the main thread,
    // read from the synthesis threads (timing) and the UI (skip), so no native player is
    // touched off the main thread.
    private long _positionTicks;
    private long _durationTicks;

    private int _generation;
    private CancellationTokenSource? _synthCts;

    private int _chunkCount;
    private int _currentChunkIndex;
    private IReadOnlyList<string> _chunks = [];

    private long _lastTimingRaiseTicks;

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler? Completed;
    public event EventHandler<double>? ProgressChanged;
    public event EventHandler<ReadTiming>? TimingChanged;
    public event EventHandler<ReadChunk>? ChunkChanged;

    public PlaybackState State => _state;

    public IosSupertonicSpeechReader(IVoiceModelService model)
    {
        _model = model;
        _interruptionObserver = IosAudioSession.ObserveInterruptions(OnInterruptionBegan, OnInterruptionEnded);
    }

    public async Task SpeakAsync(string text, int? activityId = null)
    {
        OfflineTts? tts = EnsureTts();
        if (tts is null)
        {
            return;
        }

        // Once per read (and again after an interruption ends), not once per chunk.
        await MainThread.InvokeOnMainThreadAsync(() => IosAudioSession.Activate(this));
        (int generation, CancellationToken token) = BeginGeneration();

        IReadOnlyList<string> chunks = SpeechTextChunker.Split(text);
        _chunks = chunks;
        _timing.Start(chunks.Count);
        RaiseTiming(TimeSpan.Zero);
        ChunkChanged?.Invoke(this, new ReadChunk(0, chunks.Count, text, 0, text.Length));

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
        OfflineTts tts, IReadOnlyList<string> chunks, int startIndex, int speakerId, int generation, CancellationToken token)
    {
        _chunkCount = chunks.Count;
        _currentChunkIndex = startIndex;
        ProgressChanged?.Invoke(this, chunks.Count == 0 ? 0 : (double)startIndex / chunks.Count);

        var slots = new SemaphoreSlim(MaxSynthesisConcurrency);
        var generations = new List<Task<string>>();
        var files = new ConcurrentQueue<string>();
        Task<string>? firstChunk = null;
        for (int index = startIndex; index < chunks.Count; index++)
        {
            Task<string> chunkTask = index == startIndex
                ? GenerateChunkAsync(tts, chunks[index], index, speakerId, slots, files, token)
                : GenerateAfterAsync(firstChunk!, tts, chunks[index], index, speakerId, slots, files, token);
            firstChunk ??= chunkTask;
            generations.Add(chunkTask);
        }

        try
        {
            for (int offset = 0; offset < generations.Count; offset++)
            {
                string wavPath;
                try
                {
                    wavPath = await generations[offset];
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (!IsCurrent(generation))
                {
                    return;
                }

                int index = startIndex + offset;
                _currentChunkIndex = index;
                bool endedNaturally = await PlayChunkAsync(wavPath, token);
                if (!endedNaturally || !IsCurrent(generation))
                {
                    return;
                }

                _timing.AdvancePastChunk(index);

                int selected = Volatile.Read(ref _speakerId);
                if (selected != speakerId && index + 1 < chunks.Count)
                {
                    (int nextGeneration, CancellationToken nextToken) = BeginGeneration();
                    _ = DrainAsync(generations, files);
                    await SpeakChunksAsync(tts, chunks, index + 1, selected, nextGeneration, nextToken);
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
            _ = DrainAsync(generations, files);
        }
    }

    // Natural end of a read, as AvSpeechReader.Finish does: stop polling, free the player,
    // delete the last chunk file and hand the audio session back, all before Completed.
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

        if (IsCurrent(generation))
        {
            UpdateState(PlaybackState.Idle);
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }

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
        Task gate, OfflineTts tts, string chunk, int index, int speakerId, SemaphoreSlim slots,
        ConcurrentQueue<string> files, CancellationToken token)
    {
        try
        {
            await gate;
        }
        catch
        {
            // Swallowed: the first chunk's real outcome surfaces on its own await.
        }

        return await GenerateChunkAsync(tts, chunk, index, speakerId, slots, files, token);
    }

    // Checks the token before each synthesis starts and again before the file is written, so
    // a stopped, skipped or superseded read neither keeps synthesizing queued chunks nor leaves
    // a WAV behind. Every file written is recorded in `files` so the generation's drain can
    // delete it.
    private async Task<string> GenerateChunkAsync(
        OfflineTts tts, string chunk, int index, int speakerId, SemaphoreSlim slots,
        ConcurrentQueue<string> files, CancellationToken token)
    {
        await slots.WaitAsync(token);
        try
        {
            token.ThrowIfCancellationRequested();
            OfflineTtsGeneratedAudio audio = await Task.Run(() => tts.Generate(chunk, 1.0f, speakerId), token);
            token.ThrowIfCancellationRequested();
            RecordChunkDuration(index, audio);
            string path = await WriteWavFileAsync(audio.Samples, audio.SampleRate);
            files.Enqueue(path);
            return path;
        }
        finally
        {
            slots.Release();
        }
    }

    // Runs once a generation is over (finished, stopped, skipped or superseded). Waits for
    // its synthesis tasks, swallowing every outcome so none goes unobserved, then deletes
    // every chunk file the generation wrote. Deleting a file that is still playing is safe
    // (the open handle stays valid) and the player is released by whoever ended the read.
    private static async Task DrainAsync(List<Task<string>> generations, ConcurrentQueue<string> files)
    {
        foreach (Task<string> generation in generations)
        {
            try
            {
                await generation;
            }
            catch
            {
                // Cancelled or failed: a real failure already surfaced on the consumer's await.
            }
        }

        foreach (string path in files)
        {
            TryDelete(path);
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

    // Plays one chunk's WAV file and awaits its natural end (true) or a stop/supersede
    // (false). The previous chunk's temp file is deleted once this one has taken over the
    // player (it's no longer open by then).
    private async Task<bool> PlayChunkAsync(string wavPath, CancellationToken token)
    {
        var ended = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _chunkEnded = ended;

        await MainThread.InvokeOnMainThreadAsync(() => StartPlayer(wavPath, ended, token));

        using (token.Register(() => ended.TrySetResult(false)))
        {
            return await ended.Task;
        }
    }

    // Runs on the main thread. Replaces the previous chunk's player, then starts this chunk.
    // A chunk that cannot be decoded is skipped (treated as ended) rather than stalling the read.
    private void StartPlayer(string wavPath, TaskCompletionSource<bool> ended, CancellationToken token)
    {
        ReleasePlayer();
        DeletePreviousChunkFile();
        _previousChunkFile = _currentChunkFile;
        _currentChunkFile = wavPath;
        if (token.IsCancellationRequested)
        {
            return;
        }

        using var url = NSUrl.FromFilename(wavPath);
        AVAudioPlayer? player = AVAudioPlayer.FromUrl(url, out NSError? error);
        if (player is null || error is not null)
        {
            ended.TrySetResult(true);
            return;
        }

        player.EnableRate = true;
        player.Rate = (float)_playbackRate;
        player.FinishedPlaying += (_, _) =>
        {
            if (ReferenceEquals(_player, player))
            {
                _playerFinished = true;
            }

            ended.TrySetResult(true);
        };
        player.PrepareToPlay();
        _player = player;
        _playerFinished = false;
        Volatile.Write(ref _positionTicks, 0);
        Volatile.Write(ref _durationTicks, TimeSpan.FromSeconds(player.Duration).Ticks);

        // Paused (by the user or an interruption) between chunks: the next chunk is prepared
        // but stays silent, and the interruption flag is kept; Resume plays it.
        if (_state != PlaybackState.Paused)
        {
            if (!player.Play())
            {
                ReleasePlayer();
                ended.TrySetResult(true);
                return;
            }

            _pausedByInterruption = false;
            UpdateState(PlaybackState.Playing);
        }

        StartPolling(token);
    }

    public void Pause() => MainThread.BeginInvokeOnMainThread(PauseNow);

    // Main thread only.
    private void PauseNow()
    {
        if (_state != PlaybackState.Playing)
        {
            return;
        }

        _player?.Pause();
        UpdateState(PlaybackState.Paused);
    }

    public void Resume() => MainThread.BeginInvokeOnMainThread(ResumeNow);

    // Main thread only. A player that refuses to play leaves the state Paused. If the paused
    // chunk had already finished, replaying it would repeat audio: just flip the state, and the
    // next StartPlayer (state no longer Paused) plays the next chunk.
    private void ResumeNow()
    {
        if (_state != PlaybackState.Paused)
        {
            return;
        }

        if (_playerFinished || _player is null)
        {
            _pausedByInterruption = false;
            UpdateState(PlaybackState.Playing);
            return;
        }

        if (_player.Play())
        {
            _pausedByInterruption = false;
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
        _chunkCount = 0;
        Volatile.Write(ref _positionTicks, 0);
        Volatile.Write(ref _durationTicks, 0);
        ProgressChanged?.Invoke(this, 0);
        _timing.Reset();
        RaiseTiming(TimeSpan.Zero);
        UpdateState(PlaybackState.Idle);
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

    public void SetVoice(string voiceId) =>
        Volatile.Write(ref _speakerId, SupertonicVoiceTable.SpeakerIdFor(voiceId));

    // The audio session reports a call, an alarm or Siri taking the audio: pause, and carry on
    // when the system says it is fine to (a call ending). A pause the user made is never undone.
    private void OnInterruptionBegan()
    {
        if (_state == PlaybackState.Playing)
        {
            PauseNow();
            _pausedByInterruption = _state == PlaybackState.Paused;
        }
    }

    private void OnInterruptionEnded(bool shouldResume)
    {
        if (_pausedByInterruption && shouldResume && _state == PlaybackState.Paused && IosAudioSession.Activate(this))
        {
            ResumeNow();
        }
    }

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

            IosSherpaLibrary.EnsureResolver();
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

    // Main thread only. Stops and frees the current chunk's player.
    private void ReleasePlayer()
    {
        if (_player is { } player)
        {
            player.Stop();
            player.Dispose();
            _player = null;
            _playerFinished = false;
        }
    }

    private void StartPolling(CancellationToken stopToken)
    {
        StopPolling();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(stopToken);
        _pollCts = cts;
        _ = PollLoopAsync(cts.Token);
    }

    private void StopPolling()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
    }

    private async Task PollLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(PollInterval, token);
                await MainThread.InvokeOnMainThreadAsync(SamplePosition);
                ReportProgress();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on stop/skip/supersede.
        }
    }

    // Main thread only: the one place the native player's position is read.
    private void SamplePosition()
    {
        if (_player is { } player)
        {
            Volatile.Write(ref _positionTicks, TimeSpan.FromSeconds(player.CurrentTime).Ticks);
            Volatile.Write(ref _durationTicks, TimeSpan.FromSeconds(player.Duration).Ticks);
        }
    }

    private void ReportProgress()
    {
        int chunkCount = Volatile.Read(ref _chunkCount);
        int chunkIndex = Volatile.Read(ref _currentChunkIndex);
        if (chunkCount == 0)
        {
            return;
        }

        long duration = Volatile.Read(ref _durationTicks);
        double withinChunk = duration > 0 ? (double)Volatile.Read(ref _positionTicks) / duration : 0;
        double fraction = (chunkIndex + Math.Clamp(withinChunk, 0, 1)) / chunkCount;
        ProgressChanged?.Invoke(this, Math.Clamp(fraction, 0, 1));

        if (Stopwatch.GetElapsedTime(_lastTimingRaiseTicks) >= TimingRaiseInterval)
        {
            RaiseTiming(CurrentChunkPosition());
        }
    }

    private TimeSpan CurrentChunkPosition() =>
        Volatile.Read(ref _durationTicks) > 0 ? TimeSpan.FromTicks(Volatile.Read(ref _positionTicks)) : TimeSpan.Zero;

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
            // Best-effort cleanup; the cache directory is reclaimed by the OS regardless.
        }
    }

    public void Dispose()
    {
        _interruptionObserver.Dispose();
        StopPolling();
        _synthCts?.Cancel();
        _synthCts?.Dispose();
        ReleasePlayer();
        DeletePreviousChunkFile();
        DeleteCurrentChunkFile();
        _tts?.Dispose();
    }
}
