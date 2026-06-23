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

    private MediaSource? _currentSource;
    private double _playbackRate = ReadingSpeedExtensions.Default.ToPlaybackRate();
    private PlaybackState _state = PlaybackState.Idle;

    public SpeechReader()
    {
        _player.MediaOpened += OnMediaOpened;
        _player.MediaEnded += OnMediaEnded;
        _player.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
    }

    public event EventHandler<PlaybackState>? StateChanged;

    public PlaybackState State => _state;

    public async Task SpeakAsync(string text)
    {
        SpeechSynthesisStream stream = await _synthesizer.SynthesizeTextToStreamAsync(text);
        SwapSource(MediaSource.CreateFromStream(stream, stream.ContentType));
    }

    public void Pause() => _player.Pause();

    public void Resume() => _player.Play();

    public void SetSpeed(ReadingSpeed speed)
    {
        _playbackRate = speed.ToPlaybackRate();
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

    public void Dispose()
    {
        _player.Dispose();
        _currentSource?.Dispose();
        _synthesizer.Dispose();
    }
}
