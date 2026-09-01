using System.Diagnostics;
using Android.OS;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using AndroidTts = Android.Speech.Tts.TextToSpeech;
using AndroidVoice = Android.Speech.Tts.Voice;
using JavaLocale = Java.Util.Locale;
using QueueMode = Android.Speech.Tts.QueueMode;
using OperationResult = Android.Speech.Tts.OperationResult;
using UtteranceProgressListener = Android.Speech.Tts.UtteranceProgressListener;

// Deliberately not "ReadTheStupidText.Mobile.Platforms.Android" — a namespace
// segment literally named "Android" shadows the top-level Android.* bindings
// namespace for unqualified lookups inside this file (matches MainActivity.cs's
// own namespace, which sidesteps the same trap).
namespace ReadTheStupidText.Mobile;

/// <summary>
/// <see cref="ISpeechReader"/> backed by Android's built-in <see cref="TextToSpeech"/>
/// engine (Slice 30). The neural Supertonic voice is ported in Slice 31 — this is the
/// safety-net-grade engine used until then, mirroring how <c>SpeechReader</c> (WinRT)
/// is the Windows fallback behind the neural <c>SupertonicSpeechReader</c>.
/// </summary>
/// <remarks>
/// Android's TTS has no native pause/resume or seek: <see cref="Pause"/> stops the
/// engine but remembers the character offset last reported by
/// <c>OnRangeStart</c>; <see cref="Resume"/>, <see cref="SkipForward"/> and
/// <see cref="SkipBackward"/> all re-speak the remaining text from an offset —
/// best-effort, snapped to the nearest word boundary, matching the "best-effort,
/// not sample-accurate" contract the Windows chunked engines already promise.
/// </remarks>
public sealed class AndroidSpeechReader : Java.Lang.Object, ISpeechReader, AndroidTts.IOnInitListener
{
    private const string UtteranceId = "read-the-stupid-text";

    // Android's SetSpeechRate(1.0f) is its "normal" speed, matching PlaybackRate's
    // own 1.0 default, so the mapping is a direct pass-through. This is a rough
    // average speaking rate at that setting, used only to size a ~10s skip.
    private const float CharsPerSecondAtNormalRate = 15f;

    private readonly Android.Content.Context _context;
    private AndroidTts? _tts;
    private TaskCompletionSource<bool>? _engineReady;

    private string _text = string.Empty;

    // Absolute character offset into _text where the utterance currently playing
    // (or last played) began — the base that OnRangeStart's utterance-relative
    // `start` is added to.
    private int _spokenFromOffset;

    // Absolute character offset last reported by OnRangeStart (or _spokenFromOffset
    // if nothing has been reported yet for the current utterance).
    private int _lastReportedOffset;

    private float _rate = 1.0f;
    private long _speakStartTimestamp;

    public AndroidSpeechReader(Android.Content.Context context)
    {
        _context = context;
    }

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler? Completed;
    public event EventHandler<double>? ProgressChanged;
    public event EventHandler<ReadTiming>? TimingChanged;

    public PlaybackState State { get; private set; } = PlaybackState.Idle;

    public Task WarmUpAsync()
    {
        if (_tts is not null)
        {
            return _engineReady?.Task ?? Task.CompletedTask;
        }

        _engineReady = new TaskCompletionSource<bool>();
        _tts = new AndroidTts(_context, this);
        return _engineReady.Task;
    }

    public async Task SpeakAsync(string text, int? activityId = null)
    {
        await WarmUpAsync();

        _text = text;
        SpeakFrom(0);
    }

    public void Pause()
    {
        if (State != PlaybackState.Playing || _tts is null)
        {
            return;
        }

        int resumeAt = _lastReportedOffset;
        _tts.Stop();
        _spokenFromOffset = resumeAt;
        _lastReportedOffset = resumeAt;
        SetState(PlaybackState.Paused);
    }

    public void Resume()
    {
        if (State != PlaybackState.Paused)
        {
            return;
        }

        SpeakFrom(_spokenFromOffset);
    }

    public void Stop()
    {
        _tts?.Stop();
        _text = string.Empty;
        _spokenFromOffset = 0;
        _lastReportedOffset = 0;
        SetState(PlaybackState.Idle);
    }

    public void SetSpeed(PlaybackRate speed)
    {
        _rate = (float)speed.Value;
        _tts?.SetSpeechRate(_rate);
    }

    public void SetVoice(string voiceId)
    {
        AndroidVoice? match = _tts?.Voices?.FirstOrDefault(v => v.Name == voiceId);
        if (match is not null)
        {
            _tts!.SetVoice(match);
        }
    }

    public Task SkipForward()
    {
        if (State == PlaybackState.Idle)
        {
            return Task.CompletedTask;
        }

        int target = ClampOffset(_lastReportedOffset + SkipChars());
        SpeakFrom(SnapToWordStart(target));
        return Task.CompletedTask;
    }

    public Task SkipBackward()
    {
        if (State == PlaybackState.Idle)
        {
            return Task.CompletedTask;
        }

        int target = ClampOffset(_lastReportedOffset - SkipChars());
        SpeakFrom(SnapToWordStart(target));
        return Task.CompletedTask;
    }

    void AndroidTts.IOnInitListener.OnInit(OperationResult status)
    {
        if (status == OperationResult.Success && _tts is not null)
        {
            _tts.SetLanguage(JavaLocale.Default);
            _tts.SetSpeechRate(_rate);
            _tts.SetOnUtteranceProgressListener(new ProgressListener(this));
        }

        _engineReady?.TrySetResult(status == OperationResult.Success);
    }

    private void SpeakFrom(int offset)
    {
        if (_tts is null || string.IsNullOrEmpty(_text) || offset >= _text.Length)
        {
            SetState(PlaybackState.Idle);
            Completed?.Invoke(this, EventArgs.Empty);
            return;
        }

        _spokenFromOffset = offset;
        _lastReportedOffset = offset;
        _speakStartTimestamp = Stopwatch.GetTimestamp();

        using var parameters = new Bundle();
        _tts.Speak(_text[offset..], QueueMode.Flush, parameters, UtteranceId);
        SetState(PlaybackState.Playing);
        RaiseTiming();
    }

    private int SkipChars() => (int)(CharsPerSecondAtNormalRate * _rate * 10);

    private int ClampOffset(int offset) => Math.Clamp(offset, 0, Math.Max(_text.Length - 1, 0));

    private int SnapToWordStart(int offset)
    {
        if (offset <= 0 || offset >= _text.Length)
        {
            return offset;
        }

        int nextSpace = _text.IndexOf(' ', offset);
        return nextSpace < 0 ? _text.Length : nextSpace + 1;
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

    private void RaiseTiming()
    {
        TimeSpan elapsed = Stopwatch.GetElapsedTime(_speakStartTimestamp);
        TimingChanged?.Invoke(this, new ReadTiming(elapsed, null));
    }

    // Reports word-boundary progress and the natural end of an utterance. Runs on a
    // binder thread, not the UI thread — the ContentPage marshals to the UI thread
    // itself via MainThread.BeginInvokeOnMainThread before touching any control.
    private sealed class ProgressListener(AndroidSpeechReader owner) : UtteranceProgressListener
    {
        public override void OnStart(string? utteranceId)
        {
        }

        public override void OnDone(string? utteranceId)
        {
            owner.SetState(PlaybackState.Idle);
            owner.Completed?.Invoke(owner, EventArgs.Empty);
        }

#pragma warning disable CS0672 // the newer OnError(string, int) overload isn't abstract on this API level
        public override void OnError(string? utteranceId)
        {
            owner.SetState(PlaybackState.Idle);
        }
#pragma warning restore CS0672

        public override void OnRangeStart(string? utteranceId, int start, int end, int frame)
        {
            owner._lastReportedOffset = owner._spokenFromOffset + start;
            int total = owner._text.Length;
            double progress = total == 0 ? 0 : Math.Clamp((double)owner._lastReportedOffset / total, 0, 1);
            owner.ProgressChanged?.Invoke(owner, progress);
            owner.RaiseTiming();
        }
    }
}
