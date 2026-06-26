using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// Routes speech to the local Supertonic-3 neural engine once its model is ready,
/// and to the built-in WinRT voice until then (or if the model is missing), so
/// the app is never mute. Speed applies to both engines; voice selection targets
/// the neural engine (the fallback uses the system default voice). Only one
/// engine plays at a time; state is surfaced from whichever is active.
/// </summary>
public sealed class CompositeSpeechReader : ISpeechReader, IDisposable
{
    private readonly SupertonicSpeechReader _neural;
    private readonly SpeechReader _fallback;
    private readonly IVoiceModelService _model;
    private ISpeechReader _active;

    public CompositeSpeechReader(SupertonicSpeechReader neural, SpeechReader fallback, IVoiceModelService model)
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
    }

    public event EventHandler<PlaybackState>? StateChanged;

    public event EventHandler? Completed;

    public event EventHandler<double>? ProgressChanged;

    public PlaybackState State => _active.State;

    public Task SpeakAsync(string text)
    {
        _active = _model.IsReady ? _neural : _fallback;
        return _active.SpeakAsync(text);
    }

    // Only the neural engine has a heavy model to warm; the WinRT fallback is instant.
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

    public void Dispose()
    {
        _neural.Dispose();
        _fallback.Dispose();
    }
}
