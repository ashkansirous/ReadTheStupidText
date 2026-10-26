using System.Diagnostics;
using AVFoundation;
using Foundation;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// <see cref="ISpeechReader"/> backed by iOS's built-in <see cref="AVSpeechSynthesizer"/>
/// (Slice 48), the iOS counterpart of <c>AndroidSpeechReader</c>. It is the system-voice fallback
/// beside the neural reader (Slice 49).
/// </summary>
/// <remarks>
/// <c>AVSpeechSynthesizer</c> pauses and resumes natively. It cannot seek, so skip re-speaks
/// the rest of the text from a character offset, snapped to a word boundary: best-effort,
/// like the other engines. Delegate callbacks for an utterance that has since been replaced
/// are ignored (<see cref="_current"/>), because a cancel callback for the old utterance
/// arrives after the new one has started.
/// </remarks>
public sealed class AvSpeechReader : ISpeechReader, IReadingLanguage, IDisposable
{
    private readonly AVSpeechSynthesizer _synthesizer = new();
    private readonly SynthesizerDelegate _delegate;

    private AVSpeechUtterance? _current;
    private string _text = string.Empty;
    private int _spokenFromOffset;
    private int _lastReportedOffset;
    private double _speed = 1.0;
    private AVSpeechSynthesisVoice? _voice;
    private string? _readingLanguage;
    private long _speakStartTimestamp;
    private readonly NSObject _interruptionObserver;
    private bool _pausedByInterruption;

    // Bumped by every new read and every Stop, so a Skip that was computed for an earlier
    // read can never restart text the user has since stopped or replaced.
    private int _generation;

    public AvSpeechReader()
    {
        _delegate = new SynthesizerDelegate(this);
        _synthesizer.Delegate = _delegate;
        _interruptionObserver = IosAudioSession.ObserveInterruptions(OnInterruptionBegan, OnInterruptionEnded);
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
        IosAudioSession.Activate(this);
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
            _pausedByInterruption = false;
            SetState(PlaybackState.Playing);
        }
    }

    public void Stop()
    {
        _pausedByInterruption = false;
        _generation++;
        _current = null;
        _synthesizer.StopSpeaking(AVSpeechBoundary.Immediate);
        _text = string.Empty;
        _spokenFromOffset = 0;
        _lastReportedOffset = 0;
        IosAudioSession.Deactivate(this);
        SetState(PlaybackState.Idle);
    }

    // Applies to the next utterance; a skip or a new read picks it up. No live change.
    public void SetSpeed(PlaybackRate speed) => _speed = (double)speed.Value;

    // "Phone voice" (SystemVoice.Id) and any id iOS doesn't know (a Supertonic id carried over
    // from the shared picker, a voice uninstalled since) both mean the system default voice.
    public void SetVoice(string voiceId) =>
        _voice = voiceId == SystemVoice.Id ? null : AVSpeechSynthesisVoice.FromIdentifier(voiceId);

    // Slice 82: with no voice chosen, the default voice for the text's language reads, not the device's.
    public void SetReadingLanguage(string languageCode) => _readingLanguage = languageCode;

    /// <summary>The primary language codes of the voices installed on this device.</summary>
    public static IReadOnlyCollection<string> InstalledLanguages() =>
        AVSpeechSynthesisVoice.GetSpeechVoices().Select(v => v.Language).Where(l => !string.IsNullOrEmpty(l)).ToArray();

    public Task SkipForward() => SkipBy(OffsetSkip.SkipChars(_speed));

    public Task SkipBackward() => SkipBy(-OffsetSkip.SkipChars(_speed));

    public void Dispose()
    {
        _generation++;
        _current = null;
        _interruptionObserver.Dispose();
        _synthesizer.StopSpeaking(AVSpeechBoundary.Immediate);
        IosAudioSession.Deactivate(this);
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
            Voice = _voice ?? LanguageVoice(), // null = the system default voice
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

    // A call, an alarm or Siri took the audio: pause, and carry on when the system says it is
    // fine to. A pause the user made is never undone.
    private void OnInterruptionBegan()
    {
        if (State == PlaybackState.Playing)
        {
            Pause();
            _pausedByInterruption = State == PlaybackState.Paused;
        }
    }

    private void OnInterruptionEnded(bool shouldResume)
    {
        if (_pausedByInterruption && shouldResume && State == PlaybackState.Paused && IosAudioSession.Activate(this))
        {
            Resume();
        }
    }

    // Only a language other than the device's needs a voice picked; otherwise null keeps the
    // system default (the user's region and chosen voice). The device language is read from the OS,
    // not CurrentUICulture, which the app overrides with its own language. Among the installed
    // voices of the wanted language an exact-locale match wins, then enhanced/premium quality, then
    // any; no match keeps the default.
    private AVSpeechSynthesisVoice? LanguageVoice()
    {
        if (_readingLanguage is null)
        {
            return null;
        }

        string wanted = VoiceAvailability.Primary(_readingLanguage);
        if (wanted == DeviceLanguage())
        {
            return null;
        }

        return AVSpeechSynthesisVoice.GetSpeechVoices()
            .Where(v => VoiceAvailability.Primary(v.Language) == wanted)
            .OrderByDescending(v => IsExactLocale(v, _readingLanguage))
            .ThenByDescending(v => (int)v.Quality)
            .FirstOrDefault();
    }

    private static string DeviceLanguage() =>
        VoiceAvailability.Primary(NSLocale.PreferredLanguages.FirstOrDefault() ?? NSLocale.CurrentLocale.LanguageCode ?? string.Empty);

    private static bool IsExactLocale(AVSpeechSynthesisVoice voice, string language) =>
        string.Equals(voice.Language?.Replace('_', '-'), language.Replace('_', '-'), StringComparison.OrdinalIgnoreCase);

    private void Finish()
    {
        IosAudioSession.Deactivate(this);
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

    // A stopped utterance has finished winding down: a deactivation that failed while it was
    // still speaking can succeed now.
    private void OnCancelled() => IosAudioSession.RetryPendingDeactivate();

    // Callbacks arrive on the main thread. A cancelled (superseded or stopped) utterance
    // is deliberately not reported as Completed.
    private sealed class SynthesizerDelegate(AvSpeechReader owner) : AVSpeechSynthesizerDelegate
    {
        public override void WillSpeakRangeOfSpeechString(AVSpeechSynthesizer synthesizer, NSRange characterRange, AVSpeechUtterance utterance) =>
            owner.OnWillSpeak(utterance, characterRange);

        public override void DidFinishSpeechUtterance(AVSpeechSynthesizer synthesizer, AVSpeechUtterance utterance) =>
            owner.OnFinished(utterance);

        public override void DidCancelSpeechUtterance(AVSpeechSynthesizer synthesizer, AVSpeechUtterance utterance) =>
            owner.OnCancelled();
    }
}
