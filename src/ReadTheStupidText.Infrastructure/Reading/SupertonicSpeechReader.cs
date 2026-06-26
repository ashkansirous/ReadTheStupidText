using System.Runtime.InteropServices.WindowsRuntime;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using SherpaOnnx;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;

namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// Speaks text with the local Supertonic-3 neural engine (sherpa-onnx). Synthesis
/// produces raw PCM, wrapped as an in-memory WAV stream and played through a
/// <see cref="MediaPlayer"/> — the same pipeline as the WinRT reader, so speed
/// stays live and pitch-corrected via <see cref="MediaPlaybackSession.PlaybackRate"/>
/// (synthesis runs at 1x; the player applies the rate). The engine is built
/// lazily on first speak, once the model files are present.
///
/// Long text is split into chunks (<see cref="SpeechTextChunker"/>) that synthesize
/// concurrently (up to <see cref="MaxSynthesisConcurrency"/>) but play strictly in
/// order, so playback starts after the first chunk instead of after the whole text.
/// </summary>
public sealed class SupertonicSpeechReader : ISpeechReader, IDisposable
{
    private const int SynthesisThreads = 2;

    // Long text is synthesized as several small chunks generated concurrently (this
    // many at once) and played strictly in order, so playback can start after the
    // first chunk instead of waiting for the whole text to synthesize.
    private const int MaxSynthesisConcurrency = 3;

    // A throwaway utterance synthesized once at startup to JIT/warm the ONNX graph;
    // its audio is discarded, so it never reaches the player.
    private const string WarmUpText = "Ready.";

    private readonly IVoiceModelService _model;
    private readonly MediaPlayer _player = new() { AutoPlay = false };

    // Serializes the generation counter / synthesis-cancellation swap, which can be
    // touched concurrently by an in-flight SpeakAsync, a newer one, and Stop.
    private readonly object _gate = new();

    // Serializes the one-time engine build so an eager warm-up and a lazy first read
    // racing each other build a single OfflineTts (one ~145 MB model load), not two.
    private readonly object _ttsGate = new();

    private OfflineTts? _tts;
    private MediaSource? _currentSource;
    private double _playbackRate = PlaybackRate.Default.Value;
    private int _speakerId = SupertonicVoiceTable.DefaultSpeakerId;
    private PlaybackState _state = PlaybackState.Idle;

    // Completes when the current chunk finishes playing (true) or playback is
    // stopped/superseded (false), so the ordered playback loop can advance.
    private TaskCompletionSource<bool>? _chunkEnded;

    // Incremented for every utterance and every Stop. A synthesis only reaches the
    // player while its generation is still current, so a superseded (slow) synth
    // can never play after the one that replaced it.
    private int _generation;
    private CancellationTokenSource? _synthCts;

    // Read-through progress is approximated by weighting each chunk equally: the
    // overall fraction is (completed chunks + position within the current chunk) /
    // total chunks. Exact per-chunk durations would be more precise but each chunk
    // is independently synthesized — equal weighting is the best-effort model.
    private int _chunkCount;
    private int _currentChunkIndex;

    public SupertonicSpeechReader(IVoiceModelService model)
    {
        _model = model;
        _player.MediaOpened += OnMediaOpened;
        _player.MediaEnded += OnMediaEnded;
        _player.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
        _player.PlaybackSession.PositionChanged += OnPositionChanged;
    }

    public event EventHandler<PlaybackState>? StateChanged;

    public event EventHandler? Completed;

    public event EventHandler<double>? ProgressChanged;

    public PlaybackState State => _state;

    public async Task SpeakAsync(string text)
    {
        OfflineTts? tts = EnsureTts();
        if (tts is null)
        {
            return;
        }

        (int generation, CancellationToken token) = BeginGeneration();

        // Split long text and start synthesizing the chunks concurrently (capped),
        // each task acquiring a slot from the semaphore. The tasks are kept in order.
        IReadOnlyList<string> chunks = SpeechTextChunker.Split(text);
        _chunkCount = chunks.Count;
        _currentChunkIndex = 0;
        ProgressChanged?.Invoke(this, 0);

        // Not disposed: a superseded read can leave orphaned generation tasks that
        // still Release(); SemaphoreSlim needs no disposal unless its wait handle is
        // accessed (it isn't), so letting it be collected avoids that race.
        var slots = new SemaphoreSlim(MaxSynthesisConcurrency);
        List<Task<IRandomAccessStream>> generations =
            chunks.Select(chunk => GenerateChunkAsync(tts, chunk, slots, token)).ToList();

        // Consume in order: await each chunk, then play it to its end before the
        // next. A superseded read bails as soon as it notices it is no longer current.
        for (int i = 0; i < generations.Count; i++)
        {
            IRandomAccessStream stream;
            try
            {
                stream = await generations[i];
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!IsCurrent(generation))
            {
                return;
            }

            _currentChunkIndex = i;
            bool endedNaturally = await PlayChunkAsync(stream, token);
            if (!endedNaturally || !IsCurrent(generation))
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

    // Builds the engine and runs one discarded synthesis to warm the ONNX graph, off
    // the UI thread, so the first real read is near-instant. Idempotent and safe to
    // race the lazy build in SpeakAsync; best-effort, with EnsureTts() as the fallback.
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
            // Warm-up is best-effort: if it fails, the lazy EnsureTts() in SpeakAsync
            // remains the safety net and any real error surfaces on the first read.
        }
    }

    // Synthesizes one chunk to a WAV stream, throttled by the shared slot semaphore
    // so at most MaxSynthesisConcurrency run at once.
    private async Task<IRandomAccessStream> GenerateChunkAsync(
        OfflineTts tts, string chunk, SemaphoreSlim slots, CancellationToken token)
    {
        await slots.WaitAsync(token);
        try
        {
            OfflineTtsGeneratedAudio audio =
                await Task.Run(() => tts.Generate(chunk, 1.0f, _speakerId), token);
            return await BuildWavStreamAsync(audio.Samples, audio.SampleRate);
        }
        finally
        {
            slots.Release();
        }
    }

    // Plays one chunk and awaits its natural end (true) or a stop/supersede (false).
    private async Task<bool> PlayChunkAsync(IRandomAccessStream stream, CancellationToken token)
    {
        var ended = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _chunkEnded = ended;
        SwapSource(MediaSource.CreateFromStream(stream, "audio/wav"));
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
        }

        _chunkEnded?.TrySetResult(false); // release the playback loop if mid-chunk
        _player.Pause();
        ClearSource();
        _chunkCount = 0;
        ProgressChanged?.Invoke(this, 0);
        UpdateState(PlaybackState.Idle);
    }

    public void SetSpeed(PlaybackRate speed)
    {
        _playbackRate = speed.Value;
        _player.PlaybackSession.PlaybackRate = _playbackRate;
    }

    public void SetVoice(string voiceId) => _speakerId = SupertonicVoiceTable.SpeakerIdFor(voiceId);

    // Returns the engine, building it once on first use. Double-checked under _ttsGate
    // so a warm-up thread and a first read can't each construct one (the build loads
    // the ~145 MB model). Volatile pairs the lock-free fast path with the locked write.
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

    // Builds the Supertonic engine from the model files under the given directory
    // (no espeak/lexicon — Supertonic ships a unicode_indexer + voice.bin).
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

    // Opens a new generation, cancelling the previous synthesis so a superseded
    // long synth stops wasting CPU and never reaches the player.
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
        sender.PlaybackSession.PlaybackRate = _playbackRate;
        sender.Play();
    }

    // A chunk finished; let the ordered playback loop advance to the next one (or,
    // for the last chunk, complete). Completion/idle are signalled by SpeakAsync.
    private void OnMediaEnded(MediaPlayer sender, object args) => _chunkEnded?.TrySetResult(true);

    // Reports best-effort read-through progress as the current chunk plays: the
    // completed-chunk count plus the fraction through the current chunk, over the
    // total. Ignored when nothing is loaded (no chunks / unknown duration).
    private void OnPositionChanged(MediaPlaybackSession sender, object args)
    {
        // Snapshot once: Stop() can zero _chunkCount on another thread between a
        // guard and the division, and a superseded read can half-update the pair.
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

    private static async Task<IRandomAccessStream> BuildWavStreamAsync(float[] samples, int sampleRate)
    {
        byte[] wav = EncodeWav(samples, sampleRate);
        var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(wav.AsBuffer());
        stream.Seek(0);
        return stream;
    }

    // Encodes mono float samples as a 16-bit PCM WAV (RIFF) byte buffer.
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
        writer.Write(sampleRate * blockAlign); // byte rate
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

    public void Dispose()
    {
        _synthCts?.Cancel();
        _synthCts?.Dispose();
        _player.MediaOpened -= OnMediaOpened;
        _player.MediaEnded -= OnMediaEnded;
        _player.PlaybackSession.PlaybackStateChanged -= OnPlaybackStateChanged;
        _player.PlaybackSession.PositionChanged -= OnPositionChanged;
        _player.Dispose();
        _currentSource?.Dispose();
        _tts?.Dispose();
    }
}
