using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Routes speech to the local Supertonic-3 neural engine once its model is ready,
/// and to Android's built-in <c>TextToSpeech</c> until then (or if the model is
/// missing), so the app is never mute — same routing shape as Windows'
/// <c>CompositeSpeechReader</c> (Decision 39), not a shared class since the two
/// engines it wraps are entirely platform-specific.
/// </summary>
public sealed class CompositeSpeechReader : ISpeechReader, IDisposable
{
    private readonly AndroidSupertonicSpeechReader _neural;
    private readonly AndroidSpeechReader _fallback;
    private readonly IVoiceModelService _model;
    private ISpeechReader _active;

    public CompositeSpeechReader(AndroidSupertonicSpeechReader neural, AndroidSpeechReader fallback, IVoiceModelService model)
    {
        _neural = neural;
        _fallback = fallback;
        _model = model;
        _active = fallback;

        _neural.StateChanged += (_, state) => Forward(_neural, state);
        _fallback.StateChanged += (_, state) => Forward(_fallback, state);
        _neural.Completed += (_, _) => ForwardCompleted(_neural);
        _fallback.Completed += (_, _) => ForwardCompleted(_fallback);
        _neural.ProgressChanged += (_, p) => ForwardProgress(_neural, p);
        _fallback.ProgressChanged += (_, p) => ForwardProgress(_fallback, p);
        _neural.TimingChanged += (_, t) => ForwardTiming(_neural, t);
        _fallback.TimingChanged += (_, t) => ForwardTiming(_fallback, t);
    }

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler? Completed;
    public event EventHandler<double>? ProgressChanged;
    public event EventHandler<ReadTiming>? TimingChanged;

    public PlaybackState State => _active.State;

    public Task SpeakAsync(string text, int? activityId = null)
    {
        _active = _model.IsReady ? _neural : _fallback;
        return _active.SpeakAsync(text, activityId);
    }

    // Only the neural engine has a heavy model to warm; the built-in engine is instant.
    public Task WarmUpAsync() => _neural.WarmUpAsync();

    public void Pause() => _active.Pause();

    public void Resume() => _active.Resume();

    public void Stop() => _active.Stop();

    public void SetSpeed(PlaybackRate speed)
    {
        _neural.SetSpeed(speed);
        _fallback.SetSpeed(speed);
    }

    // Voice selection is neural-only; the fallback always uses the system default.
    public void SetVoice(string voiceId) => _neural.SetVoice(voiceId);

    public Task SkipForward() => _active.SkipForward();

    public Task SkipBackward() => _active.SkipBackward();

    private void Forward(ISpeechReader source, PlaybackState state)
    {
        if (ReferenceEquals(source, _active))
        {
            StateChanged?.Invoke(this, state);
        }
    }

    private void ForwardCompleted(ISpeechReader source)
    {
        if (ReferenceEquals(source, _active))
        {
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ForwardProgress(ISpeechReader source, double progress)
    {
        if (ReferenceEquals(source, _active))
        {
            ProgressChanged?.Invoke(this, progress);
        }
    }

    private void ForwardTiming(ISpeechReader source, ReadTiming timing)
    {
        if (ReferenceEquals(source, _active))
        {
            TimingChanged?.Invoke(this, timing);
        }
    }

    public void Dispose()
    {
        _neural.Dispose();
    }
}
