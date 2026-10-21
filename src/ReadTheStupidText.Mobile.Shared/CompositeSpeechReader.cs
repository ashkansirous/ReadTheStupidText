using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Routes speech to the local Supertonic-3 neural engine once its downloaded voice
/// pack is ready, and to the platform's built-in engine (Android <c>TextToSpeech</c>)
/// until then (or if the pack is missing, or the user picked "Phone voice"), so the app
/// is never mute — same routing shape as Windows' <c>CompositeSpeechReader</c>
/// (Decision 39). The two engines come from <see cref="IPlatformServices"/>, so this
/// class names no platform type (Slice 44).
/// </summary>
public sealed class CompositeSpeechReader : ISpeechReader, IDisposable
{
    private readonly ISpeechReader _neural;
    private readonly ISpeechReader _fallback;
    private readonly IVoiceModelService _model;
    private ISpeechReader _active;
    private bool _useSystemVoice;
    private const string LastNeuralVoiceKey = "LastNeuralVoiceId";

    public CompositeSpeechReader(ISpeechReader neural, ISpeechReader fallback, IVoiceModelService model)
    {
        _neural = neural;
        _fallback = fallback;
        _model = model;
        _active = fallback;

        // The persisted choice may be "Phone voice" at startup; the neural engine still
        // needs the voice the user last picked for when they switch back (or the default).
        if (Preferences.Default.Get<string?>(LastNeuralVoiceKey, null) is { } lastNeural)
        {
            _neural.SetVoice(lastNeural);
        }

        Subscribe(_neural);

        // A platform may hand back one reader for both slots (iOS, until the neural
        // engine lands): subscribe once, or every event would be forwarded twice.
        if (!ReferenceEquals(_neural, _fallback))
        {
            Subscribe(_fallback);
        }
    }

    private void Subscribe(ISpeechReader reader)
    {
        reader.StateChanged += (_, state) => Forward(reader, state);
        reader.Completed += (_, _) => ForwardCompleted(reader);
        reader.ProgressChanged += (_, p) => ForwardProgress(reader, p);
        reader.TimingChanged += (_, t) => ForwardTiming(reader, t);
        reader.ChunkChanged += (_, c) => ForwardChunk(reader, c);
    }

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler? Completed;
    public event EventHandler<double>? ProgressChanged;
    public event EventHandler<ReadTiming>? TimingChanged;
    public event EventHandler<ReadChunk>? ChunkChanged;

    public PlaybackState State => _active.State;

    public Task SpeakAsync(string text, int? activityId = null)
    {
        _active = _model.IsReady && !_useSystemVoice ? _neural : _fallback;
        return _active.SpeakAsync(text, activityId);
    }

    // Always true: a segment read is handled either way (neural, or the joined-text fallback).
    public bool SupportsSegments => true;

    // Per-voice reading needs a neural engine that supports it and a ready pack. Otherwise
    // (pack not ready, "Phone voice" picked, or a platform whose neural slot is just the
    // built-in engine, as on iOS) the built-in engine reads the whole story in its one
    // voice rather than failing, so a child is never left with silence (Slice 60).
    public Task SpeakSegmentsAsync(IReadOnlyList<VoicedSegment> segments, int? activityId = null)
    {
        if (_model.IsReady && !_useSystemVoice && _neural.SupportsSegments)
        {
            _active = _neural;
            return _neural.SpeakSegmentsAsync(segments, activityId);
        }

        _active = _fallback;
        return _fallback.SpeakAsync(VoicedSegment.JoinText(segments), activityId);
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

    // Neural voices are chosen by id; the labelled "Phone voice" (SystemVoice.Id) routes
    // reads to the fallback even after the neural pack is installed. The fallback always
    // uses the system default voice. Routing is decided when a read STARTS (SpeakAsync),
    // so switching to or from "Phone voice" mid-read applies to the next read only; the
    // neural-to-neural switch is the one that takes effect at the next chunk boundary
    // (Slice 23). The last neural voice is remembered separately so choosing "Phone
    // voice" never forgets it.
    public void SetVoice(string voiceId)
    {
        _useSystemVoice = voiceId == SystemVoice.Id;
        if (!_useSystemVoice)
        {
            _neural.SetVoice(voiceId);
            Preferences.Default.Set(LastNeuralVoiceKey, voiceId);
        }
    }

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

    private void ForwardChunk(ISpeechReader source, ReadChunk chunk)
    {
        if (ReferenceEquals(source, _active))
        {
            ChunkChanged?.Invoke(this, chunk);
        }
    }

    public void Dispose()
    {
        (_neural as IDisposable)?.Dispose();
        if (!ReferenceEquals(_neural, _fallback))
        {
            (_fallback as IDisposable)?.Dispose();
        }
    }
}
