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
/// </summary>
public sealed class SupertonicSpeechReader : ISpeechReader, IDisposable
{
    private const int SynthesisThreads = 2;

    private readonly IVoiceModelService _model;
    private readonly MediaPlayer _player = new() { AutoPlay = false };

    private OfflineTts? _tts;
    private MediaSource? _currentSource;
    private double _playbackRate = PlaybackRate.Default.Value;
    private int _speakerId = SupertonicVoiceTable.DefaultSpeakerId;
    private PlaybackState _state = PlaybackState.Idle;

    public SupertonicSpeechReader(IVoiceModelService model)
    {
        _model = model;
        _player.MediaOpened += OnMediaOpened;
        _player.MediaEnded += OnMediaEnded;
        _player.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
    }

    public event EventHandler<PlaybackState>? StateChanged;

    public PlaybackState State => _state;

    public async Task SpeakAsync(string text)
    {
        OfflineTts? tts = EnsureTts();
        if (tts is null)
        {
            return;
        }

        // Generation is CPU-bound and synchronous; keep it off the UI thread.
        var audio = await Task.Run(() => tts.Generate(text, 1.0f, _speakerId));
        IRandomAccessStream stream = await BuildWavStreamAsync(audio.Samples, audio.SampleRate);
        SwapSource(MediaSource.CreateFromStream(stream, "audio/wav"));
    }

    public void Pause() => _player.Pause();

    public void Resume() => _player.Play();

    public void SetSpeed(PlaybackRate speed)
    {
        _playbackRate = speed.Value;
        _player.PlaybackSession.PlaybackRate = _playbackRate;
    }

    public void SetVoice(string voiceId) => _speakerId = SupertonicVoiceTable.SpeakerIdFor(voiceId);

    private OfflineTts? EnsureTts()
    {
        if (_tts is not null)
        {
            return _tts;
        }

        if (_model.Paths is not { } paths)
        {
            return null;
        }

        string dir = paths.RootDir;
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
        _tts = new OfflineTts(config);
        return _tts;
    }

    private void SwapSource(MediaSource source)
    {
        MediaSource? previous = _currentSource;
        _currentSource = source;
        _player.Source = source;
        previous?.Dispose();
    }

    private void OnMediaOpened(MediaPlayer sender, object args)
    {
        sender.PlaybackSession.PlaybackRate = _playbackRate;
        sender.Play();
    }

    private void OnMediaEnded(MediaPlayer sender, object args) => UpdateState(PlaybackState.Idle);

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
        _player.Dispose();
        _currentSource?.Dispose();
        _tts?.Dispose();
    }
}
