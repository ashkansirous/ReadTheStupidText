using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// Speaks text using the WinRT speech synthesizer, played through a
/// <see cref="MediaPlayer"/> so speed changes apply live and pitch-corrected
/// via <see cref="MediaPlaybackSession.PlaybackRate"/>.
/// </summary>
public sealed class SpeechReader : ISpeechReader, IDisposable
{
    private readonly SpeechSynthesizer _synthesizer = new();
    private readonly MediaPlayer _player = new() { AutoPlay = false };
    private readonly object _gate = new();

    private MediaSource? _currentSource;
    private double _playbackRate = PlaybackRate.Default.Value;
    private PlaybackState _state = PlaybackState.Idle;

    // See SupertonicSpeechReader: a generation guard keeps a superseded synthesis
    // from reaching the shared player after a newer utterance replaced it.
    private int _generation;
    private CancellationTokenSource? _synthCts;

    public SpeechReader()
    {
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
        (int generation, CancellationToken token) = BeginGeneration();

        SpeechSynthesisStream stream;
        try
        {
            stream = await _synthesizer.SynthesizeTextToStreamAsync(text).AsTask(token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!IsCurrent(generation))
        {
            stream.Dispose();
            return;
        }

        SwapSource(MediaSource.CreateFromStream(stream, stream.ContentType));
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

        _player.Pause();
        ClearSource();
        ProgressChanged?.Invoke(this, 0);
        UpdateState(PlaybackState.Idle);
    }

    public void SetSpeed(PlaybackRate speed)
    {
        _playbackRate = speed.Value;
        _player.PlaybackSession.PlaybackRate = _playbackRate;
    }

    // Applies to the next SynthesizeTextToStreamAsync; a voice cannot be swapped
    // mid-utterance. An unknown id is ignored so the current voice is kept.
    public void SetVoice(string voiceId)
    {
        VoiceInformation? voice = SpeechSynthesizer.AllVoices.FirstOrDefault(v => v.Id == voiceId);
        if (voice is not null)
        {
            _synthesizer.Voice = voice;
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

    // Natural end of playback (not a Stop, which clears the source without ending).
    private void OnMediaEnded(MediaPlayer sender, object args)
    {
        UpdateState(PlaybackState.Idle);
        Completed?.Invoke(this, EventArgs.Empty);
    }

    // A single stream, so read-through progress is just position over duration.
    private void OnPositionChanged(MediaPlaybackSession sender, object args)
    {
        if (sender.NaturalDuration <= TimeSpan.Zero)
        {
            return;
        }

        ProgressChanged?.Invoke(this, Math.Clamp(sender.Position / sender.NaturalDuration, 0, 1));
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
        _synthCts?.Cancel();
        _synthCts?.Dispose();
        _player.MediaOpened -= OnMediaOpened;
        _player.MediaEnded -= OnMediaEnded;
        _player.PlaybackSession.PlaybackStateChanged -= OnPlaybackStateChanged;
        _player.PlaybackSession.PositionChanged -= OnPositionChanged;
        _player.Dispose();
        _currentSource?.Dispose();
        _synthesizer.Dispose();
    }
}
