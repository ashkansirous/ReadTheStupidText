using System.Diagnostics;
using AVFoundation;
using Foundation;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// <see cref="ISpeechReader"/> backed by iOS's built-in <see cref="AVSpeechSynthesizer"/>
/// (Slice 48), the iOS counterpart of <c>AndroidSpeechReader</c>. It is the only engine on
/// iOS until Slice 49 links the neural one.
/// </summary>
/// <remarks>
/// <c>AVSpeechSynthesizer</c> pauses and resumes natively. It cannot seek, so skip re-speaks
/// the rest of the text from a character offset, snapped to a word boundary: best-effort,
/// like the other engines. Delegate callbacks for an utterance that has since been replaced
/// are ignored (<see cref="_current"/>), because a cancel callback for the old utterance
/// arrives after the new one has started.
/// </remarks>
public sealed class AvSpeechReader : ISpeechReader, IDisposable
{
    private readonly AVSpeechSynthesizer _synthesizer = new();
    private readonly SynthesizerDelegate _delegate;

    private AVSpeechUtterance? _current;
    private string _text = string.Empty;
    private int _spokenFromOffset;
    private int _lastReportedOffset;
    private double _speed = 1.0;
    private AVSpeechSynthesisVoice? _voice;
    private long _speakStartTimestamp;

    // Bumped by every new read and every Stop, so a Skip that was computed for an earlier
    // read can never restart text the user has since stopped or replaced.
    private int _generation;

    public AvSpeechReader()
    {
        _delegate = new SynthesizerDelegate(this);
        _synthesizer.Delegate = _delegate;
    }

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler? Completed;
    public event EventHandler<double>? ProgressChanged;
    public event EventHandler<ReadTiming>? TimingChanged;
    public event EventHandler<ReadChunk>? ChunkChanged;

    public PlaybackState State { get; private set; } = PlaybackState.Idle;

    // Nothing to preload; the audio session is configured on the first read.
    public Task WarmUpAsync() => Task.CompletedTask;

    public Task SpeakAsync(string text, int? activityId = null)
    {
        ConfigureAudioSession();
        _generation++;
        _text = text;
        ChunkChanged?.Invoke(this, new ReadChunk(0, 1, text, 0, text.Length));
        SpeakFrom(0);
        return Task.CompletedTask;
    }

    public void Pause()
    {
        if (State == PlaybackState.Playing && _synthesizer.PauseSpeaking(AVSpeechBoundary.Immediate))
        {
            SetState(PlaybackState.Paused);
        }
    }

    public void Resume()
    {
        if (State == PlaybackState.Paused && _synthesizer.ContinueSpeaking())
        {
            SetState(PlaybackState.Playing);
        }
    }

    public void Stop()
    {
        _generation++;
        _current = null;
        _synthesizer.StopSpeaking(AVSpeechBoundary.Immediate);
        _text = string.Empty;
        _spokenFromOffset = 0;
        _lastReportedOffset = 0;
        DeactivateAudioSession();
        SetState(PlaybackState.Idle);
    }

    // Applies to the next utterance; a skip or a new read picks it up. No live change.
    public void SetSpeed(PlaybackRate speed) => _speed = (double)speed.Value;

    // "Phone voice" (SystemVoice.Id) and any id iOS doesn't know (a Supertonic id carried over
    // from the shared picker, a voice uninstalled since) both mean the system default voice.
    public void SetVoice(string voiceId) =>
        _voice = voiceId == SystemVoice.Id ? null : AVSpeechSynthesisVoice.FromIdentifier(voiceId);

    public Task SkipForward() => SkipBy(OffsetSkip.SkipChars(_speed));

    public Task SkipBackward() => SkipBy(-OffsetSkip.SkipChars(_speed));

    public void Dispose()
    {
        _generation++;
        _current = null;
        _synthesizer.StopSpeaking(AVSpeechBoundary.Immediate);
        DeactivateAudioSession();
        _synthesizer.Delegate = null;
        _synthesizer.Dispose();
    }

    private Task SkipBy(int deltaChars)
    {
        if (State == PlaybackState.Idle)
        {
            return Task.CompletedTask;
        }

        int generation = _generation;
        int target = OffsetSkip.SnapToWordStart(_text, OffsetSkip.ClampOffset(_text, _lastReportedOffset + deltaChars));
        if (generation == _generation)
        {
            SpeakFrom(target);
        }

        return Task.CompletedTask;
    }

    private void SpeakFrom(int offset)
    {
        if (string.IsNullOrEmpty(_text) || offset >= _text.Length)
        {
            Finish();
            return;
        }

        _spokenFromOffset = offset;
        _lastReportedOffset = offset;
        _speakStartTimestamp = Stopwatch.GetTimestamp();

        var utterance = new AVSpeechUtterance(_text[offset..])
        {
            Rate = ToUtteranceRate(_speed),
            Voice = _voice, // null = the system default voice
        };

        _current = utterance;
        _synthesizer.StopSpeaking(AVSpeechBoundary.Immediate);
        _synthesizer.SpeakUtterance(utterance);

        // A synthesizer paused before the skip stays paused for the new utterance: StopSpeaking
        // clears the queue but not the paused flag, so the skipped-to text would sit silent
        // while the state says Playing. A skip is a request to hear it, so resume.
        if (_synthesizer.Paused)
        {
            _synthesizer.ContinueSpeaking();
        }

        SetState(PlaybackState.Playing);
        RaiseTiming();
    }

    // AVSpeechUtterance.DefaultSpeechRate (0.5) is "normal": PlaybackRate 1.0 maps to it and
    // the 0.5-2.0 range to 0.25-1.0, inside the utterance's 0-1 scale.
    private static float ToUtteranceRate(double speed) =>
        Math.Clamp((float)speed * AVSpeechUtterance.DefaultSpeechRate, AVSpeechUtterance.MinimumSpeechRate, AVSpeechUtterance.MaximumSpeechRate);

    // Playback otherwise stays silent when the ringer switch is on; reading aloud must not.
    private static void ConfigureAudioSession()
    {
        AVAudioSession session = AVAudioSession.SharedInstance();
        session.SetCategory(AVAudioSession.CategoryPlayback, out _);
        session.SetActive(true, out _);
    }

    // Lets other apps' audio (music) come back once the read is over. Failure is harmless.
    private static void DeactivateAudioSession() =>
        AVAudioSession.SharedInstance().SetActive(false, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation, out _);

    private void Finish()
    {
        DeactivateAudioSession();
        SetState(PlaybackState.Idle);
        Completed?.Invoke(this, EventArgs.Empty);
    }

    private void SetState(PlaybackState state)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        StateChanged?.Invoke(this, state);
    }

    private void RaiseTiming() =>
        TimingChanged?.Invoke(this, new ReadTiming(Stopwatch.GetElapsedTime(_speakStartTimestamp), null));

    private void OnWillSpeak(AVSpeechUtterance utterance, NSRange range)
    {
        if (!ReferenceEquals(utterance, _current))
        {
            return;
        }

        _lastReportedOffset = _spokenFromOffset + (int)range.Location;
        double progress = _text.Length == 0 ? 0 : Math.Clamp((double)_lastReportedOffset / _text.Length, 0, 1);
        ProgressChanged?.Invoke(this, progress);
        RaiseTiming();
    }

    private void OnFinished(AVSpeechUtterance utterance)
    {
        if (ReferenceEquals(utterance, _current))
        {
            _current = null;
            Finish();
        }
    }

    // Callbacks arrive on the main thread. A cancelled (superseded or stopped) utterance
    // is deliberately not reported as Completed.
    private sealed class SynthesizerDelegate(AvSpeechReader owner) : AVSpeechSynthesizerDelegate
    {
        public override void WillSpeakRangeOfSpeechString(AVSpeechSynthesizer synthesizer, NSRange characterRange, AVSpeechUtterance utterance) =>
            owner.OnWillSpeak(utterance, characterRange);

        public override void DidFinishSpeechUtterance(AVSpeechSynthesizer synthesizer, AVSpeechUtterance utterance) =>
            owner.OnFinished(utterance);
    }
}
