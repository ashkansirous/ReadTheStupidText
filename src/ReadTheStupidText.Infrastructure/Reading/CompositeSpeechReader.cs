using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// Routes speech to the local neural engine once a voice pack serving the user's language is ready,
/// and to the built-in WinRT "Windows voice" until then, or when the user picked it (Slice 56,
/// Decision 61), so the app is never mute. Speed applies to both engines; neural voice selection
/// targets the neural engine, while the Windows voice id routes reads to the fallback. Only one engine
/// plays at a time; state is surfaced from whichever is active. Also forwards the head start (Slice 47)
/// when the neural engine would be the one to read.
/// </summary>
public sealed class CompositeSpeechReader : ISpeechReader, IHeadStart, IDisposable
{
    private readonly SupertonicSpeechReader _neural;
    private readonly SpeechReader _fallback;
    private readonly IVoiceModelService _model;
    private ISpeechReader _active;
    private bool _useSystemVoice;
    private volatile bool _streamUnavailable;

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
        _neural.TimingChanged += (_, t) => ForwardTiming(_neural, t);
        _fallback.TimingChanged += (_, t) => ForwardTiming(_fallback, t);
        _neural.ChunkChanged += (_, c) => ForwardChunk(_neural, c);
        _fallback.ChunkChanged += (_, c) => ForwardChunk(_fallback, c);
        _neural.StallChanged += (_, stalled) => ForwardStall(_neural, stalled);
    }

    public event EventHandler<PlaybackState>? StateChanged;

    public event EventHandler? Completed;

    public event EventHandler<double>? ProgressChanged;

    public event EventHandler<ReadTiming>? TimingChanged;

    public event EventHandler<ReadChunk>? ChunkChanged;

    public event EventHandler<bool>? StallChanged;

    public PlaybackState State => _active.State;

    public async Task SpeakAsync(string text, int? activityId = null)
    {
        if (IsNothingToRead(text))
        {
            FinishNothingToRead();
            return;
        }

        if (UseNeural)
        {
            _active = _neural;
            try
            {
                await _neural.SpeakAsync(text, activityId);
                return;
            }
            catch (NeuralEngineUnavailableException)
            {
                // The model went away after the readiness check: read with the Windows voice.
            }
        }

        _active = _fallback;
        await _fallback.SpeakAsync(text, activityId);
    }

    // Slice 176: text with nothing to speak (blank, whitespace, invisible characters) never reaches an engine, which
    // could fail or never finish on it. Like any new speak it replaces the active read (stopped), then ends at once
    // with Idle and Completed, so a caller waiting on Completed (Dino's ReadingSession, VoicePreview) is not left reading.
    private void FinishNothingToRead()
    {
        _active.Stop();
        StateChanged?.Invoke(this, PlaybackState.Idle);
        Completed?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsNothingToRead(string text) => BlankText.IsBlank(text);

    private bool UseNeural => _model.IsReady && !_useSystemVoice;

    // Slice 75: only the neural engine reads text that arrives in blocks; the Windows voice falls back to
    // translating the whole text first. Once the neural engine failed to start a streamed read it reports
    // false, so the service speaks the whole text through the Windows voice instead.
    public bool SupportsStreaming => UseNeural && !_streamUnavailable;

    public async Task SpeakStreamAsync(string firstText, int? activityId = null)
    {
        _active = _neural;
        try
        {
            await _neural.SpeakStreamAsync(firstText, activityId);
        }
        catch (NeuralEngineUnavailableException ex)
        {
            _streamUnavailable = true;
            _active = _fallback;
            throw new StreamingUnavailableException(ex);
        }
    }

    public void AppendText(string text, int? activityId = null) => _neural.AppendText(text, activityId);

    public void CompleteStream(int? activityId = null) => _neural.CompleteStream(activityId);

    public bool SupportsSegments => false;

    // Neither Windows engine reads per-voice segments (Slice 60: Dino Reader is Android
    // only). Fault without touching _active, so a read already in progress keeps its
    // routing for Pause/Stop.
    public Task SpeakSegmentsAsync(IReadOnlyList<VoicedSegment> segments, int? activityId = null) =>
        Task.FromException(new NotSupportedException("The Windows readers do not read segments in separate voices."));

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

    // Neural voices are chosen by id; the labelled "Windows voice" (SystemVoice.Id) routes reads to the
    // fallback even after a pack is installed. Routing is decided when a read STARTS, so switching to or
    // from the Windows voice mid-read applies to the next read only. The fallback always uses the system
    // default voice.
    public void SetVoice(string voiceId)
    {
        _useSystemVoice = voiceId == SystemVoice.Id;
        if (!_useSystemVoice)
        {
            _neural.SetVoice(voiceId);
        }
    }

    // Slice 47/56: only the neural engine has audio worth preparing, and only when it would be the one
    // to read; otherwise there is nothing to do.
    public Task PrepareAsync(string text) => UseNeural ? _neural.PrepareAsync(text) : HeadStartRun.Skipped;

    public bool ReadInProgress => _neural.ReadInProgress;

    public void CancelPrepare() => _neural.CancelPrepare();

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

    // A stall ending is forwarded whichever reader raised it, so a swap mid-stall can never leave the overlay up.
    private void ForwardStall(ISpeechReader source, bool stalled)
    {
        if (!stalled || ReferenceEquals(source, _active))
        {
            StallChanged?.Invoke(this, stalled);
        }
    }

    public void Dispose()
    {
        _neural.Dispose();
        _fallback.Dispose();
    }
}
