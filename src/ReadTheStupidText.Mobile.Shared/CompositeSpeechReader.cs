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
public sealed class CompositeSpeechReader : ISpeechReader, IHeadStart, IReadingLanguage, IReadScoped, IDisposable
{
    private readonly ISpeechReader _neural;
    private readonly ISpeechReader _fallback;
    private readonly IVoiceModelService _model;
    private ISpeechReader _active;
    private bool _useSystemVoice;
    private string? _appReadingLanguage;

    public CompositeSpeechReader(ISpeechReader neural, ISpeechReader fallback, IVoiceModelService model)
    {
        _neural = neural;
        _fallback = fallback;
        _model = model;
        _active = fallback;

        // The persisted choice may be "Phone voice" at startup; the neural engine still
        // needs the voice the user last picked for when they switch back (or the default).
        foreach (VoiceEngineKind kind in Enum.GetValues<VoiceEngineKind>())
        {
            if (NeuralVoiceMemory.Get(kind) is { } lastNeural)
            {
                _neural.SetVoice(lastNeural);
            }
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

    public async Task SpeakAsync(string text, int? activityId = null)
    {
        RestoreAppReadingLanguage();
        if (_model.IsReady && !_useSystemVoice)
        {
            _active = _neural;
            try
            {
                await _neural.SpeakAsync(text, activityId);
                return;
            }
            catch (NeuralEngineUnavailableException)
            {
                // The model went away after the readiness check: read with the phone voice.
            }
        }

        _active = _fallback;
        await _fallback.SpeakAsync(text, activityId);
    }

    public bool SupportsModelScope => _neural is IModelScopedReader;

    // Slice 90: a read in the text's own language. The page decided who reads (the phone voice, or the
    // natural pack for that language); the app-language model and the stored voice choice are untouched,
    // and the phone voice's language is the app's again at the next unscoped read (the reader may still
    // need this read's language to resume or skip, so it is not reset mid-read).
    public async Task SpeakScopedAsync(string text, ReadVoiceScope scope, int? activityId = null)
    {
        if (!scope.PhoneVoice && scope.Model is { } model && _neural is IModelScopedReader scoped)
        {
            _active = _neural;
            try
            {
                await scoped.SpeakAsync(text, model, activityId);
                return;
            }
            catch (NeuralEngineUnavailableException)
            {
                // The model went away after the page resolved it: read with the phone voice.
            }
        }

        _active = _fallback;
        (_fallback as IReadingLanguage)?.SetReadingLanguage(scope.Language);
        await _fallback.SpeakAsync(text, activityId);
    }

    private void RestoreAppReadingLanguage()
    {
        if (_appReadingLanguage is { } language)
        {
            (_fallback as IReadingLanguage)?.SetReadingLanguage(language);
        }
    }

    // Always true: a segment read is handled either way (neural, or the joined-text fallback).
    public bool SupportsSegments => true;

    // Per-voice reading needs a neural engine that supports it and a ready pack. Otherwise
    // (pack not ready, "Phone voice" picked, or a platform whose neural slot is just the
    // built-in engine, as on iOS) the built-in engine reads the whole story in its one
    // voice rather than failing, so a child is never left with silence (Slice 60).
    public async Task SpeakSegmentsAsync(IReadOnlyList<VoicedSegment> segments, int? activityId = null)
    {
        if (_model.IsReady && !_useSystemVoice && _neural.SupportsSegments)
        {
            _active = _neural;
            try
            {
                await _neural.SpeakSegmentsAsync(segments, activityId);
                return;
            }
            catch (NeuralEngineUnavailableException)
            {
                // The model went away after the readiness check: read with the phone voice.
            }
        }

        _active = _fallback;
        await _fallback.SpeakAsync(VoicedSegment.JoinText(segments), activityId);
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
            NeuralVoiceMemory.Remember(voiceId);
        }
    }

    // Slice 82: only the phone voice follows the text's language; a natural pack already is one language.
    // This is the app language (Slice 90): what every unscoped read and voice preview speaks in. A one-off
    // read in another language goes through SpeakScopedAsync and never changes it.
    public void SetReadingLanguage(string languageCode)
    {
        _appReadingLanguage = languageCode;
        (_fallback as IReadingLanguage)?.SetReadingLanguage(languageCode);
    }

    // Slice 47: only the neural engine has audio worth preparing, and only when it would be
    // the one to read (pack ready, "Phone voice" not chosen); otherwise there is nothing to do.
    public Task PrepareAsync(string text) =>
        _model.IsReady && !_useSystemVoice && _neural is IHeadStart neural
            ? neural.PrepareAsync(text)
            : Task.CompletedTask;

    public Task PrepareAsync(string text, ReadVoiceScope scope) =>
        !scope.PhoneVoice && scope.Model is { } model && _neural is IModelScopedReader scoped
            ? scoped.PrepareAsync(text, model)
            : Task.CompletedTask;

    public bool ReadInProgress => (_neural as IHeadStart)?.ReadInProgress ?? false;

    public void CancelPrepare() => (_neural as IHeadStart)?.CancelPrepare();

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
