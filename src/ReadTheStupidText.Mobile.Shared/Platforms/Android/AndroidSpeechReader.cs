using System.Diagnostics;
using Android.OS;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using AndroidTts = Android.Speech.Tts.TextToSpeech;
using AndroidVoice = Android.Speech.Tts.Voice;
using JavaLocale = Java.Util.Locale;
using LanguageAvailableResult = Android.Speech.Tts.LanguageAvailableResult;
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
public sealed class AndroidSpeechReader : Java.Lang.Object, ISpeechReader, IReadingLanguage, AndroidTts.IOnInitListener
{
    private const string UtteranceId = "read-the-stupid-text";
    private static readonly TimeSpan EngineStartTimeout = TimeSpan.FromSeconds(5);

    // One wait plus one retry, then the engine is treated as unavailable until ForgetLanguages.
    private const int MaxFailedStarts = 2;

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

    // The language of the text about to be read (Slice 82); null keeps the device locale.
    private string? _readingLanguage;
    private string? _chosenVoiceId;
    private IReadOnlyCollection<string>? _availableLanguages;
    private int _failedStarts;
    private long _speakStartTimestamp;

    public AndroidSpeechReader(Android.Content.Context context)
    {
        _context = context;
    }

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler? Completed;
    public event EventHandler<double>? ProgressChanged;
    public event EventHandler<ReadTiming>? TimingChanged;

    // No mobile reading text box yet (Batch 7's reading text box is Windows-only) —
    // raised once per read, for the whole utterance, purely so this engine
    // satisfies ISpeechReader like the Windows readers do.
    public event EventHandler<ReadChunk>? ChunkChanged;

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

        ApplyReadingLanguage();
        _text = text;
        ChunkChanged?.Invoke(this, new ReadChunk(0, 1, text, 0, text.Length));
        SpeakFrom(0);
    }

    public void SetReadingLanguage(string languageCode) => _readingLanguage = languageCode;

    /// <summary>
    /// The app languages the installed engine has a voice for (Slice 82), as primary codes. Waits
    /// for the engine to start; null when it did not (the phone is then unknown, not silent).
    /// </summary>
    public async Task<IReadOnlyCollection<string>?> AvailableLanguagesAsync()
    {
        if (_availableLanguages is not null)
        {
            return _availableLanguages;
        }

        if (!await EngineStartedAsync())
        {
            return null;
        }

        return _availableLanguages = Enum.GetValues<AppLanguage>()
            .Select(AppLanguages.Code)
            .Where(IsLanguageSpeakable)
            .ToArray();
    }

    /// <summary>Drops the cached languages (voice data may have been installed) and allows the
    /// engine start to be retried once more.</summary>
    public void ForgetLanguages()
    {
        _availableLanguages = null;
        _failedStarts = 0;
    }

    // False when the engine failed to start or did not within the timeout. A failed start is
    // retried once (a fresh engine if it had failed outright), then answered false at once, so a
    // broken engine never costs a wait on every read.
    private async Task<bool> EngineStartedAsync()
    {
        if (EngineIsUp())
        {
            return true;
        }

        if (_failedStarts >= MaxFailedStarts)
        {
            return false;
        }

        if (_engineReady is { Task.IsCompleted: true })
        {
            RestartEngine();
        }

        try
        {
            await WarmUpAsync().WaitAsync(EngineStartTimeout);
        }
        catch (TimeoutException)
        {
            // still pending: counted below
        }

        bool up = EngineIsUp();
        if (!up)
        {
            _failedStarts++;
        }

        return up;
    }

    private bool EngineIsUp() => _engineReady is { Task.IsCompletedSuccessfully: true } started && started.Task.Result;

    private void RestartEngine()
    {
        _tts?.Shutdown();
        _tts = null;
        _engineReady = null;
    }

    private bool IsLanguageSpeakable(string code)
    {
        using var locale = new JavaLocale(code);
        return _tts?.IsLanguageAvailable(locale) >= LanguageAvailableResult.Available;
    }

    // Switches the engine to the text's language before each read, so Google TTS loads that
    // language's voice instead of staying on the device locale and skipping other scripts.
    // When the engine refuses the language it keeps the previous one, so go back to the device
    // locale rather than read in whatever the last read left behind. SetLanguage may also drop a
    // chosen voice, so that voice is applied again once the language is set, on either path, when
    // it speaks the language now in force.
    private void ApplyReadingLanguage()
    {
        if (_readingLanguage is null || _tts is null)
        {
            return;
        }

        using var locale = new JavaLocale(_readingLanguage);
        if (_tts.SetLanguage(locale) < LanguageAvailableResult.Available)
        {
            _tts.SetLanguage(JavaLocale.Default);
            ReapplyChosenVoice(JavaLocale.Default.Language ?? string.Empty);
            return;
        }

        ReapplyChosenVoice(_readingLanguage);
    }

    private void ReapplyChosenVoice(string language)
    {
        AndroidVoice? chosen = _chosenVoiceId is null ? null : _tts?.Voices?.FirstOrDefault(v => v.Name == _chosenVoiceId);
        if (chosen is not null && chosen.Locale?.Language == language)
        {
            _tts!.SetVoice(chosen);
        }
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

    protected override void Dispose(bool disposing)
    {
        if (disposing && _tts is not null)
        {
            _tts.Stop();
            _tts.Shutdown();
            _tts = null;
        }

        base.Dispose(disposing);
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
            _chosenVoiceId = voiceId;
            _tts!.SetVoice(match);
        }
    }

    public Task SkipForward()
    {
        if (State == PlaybackState.Idle)
        {
            return Task.CompletedTask;
        }

        int target = OffsetSkip.ClampOffset(_text, _lastReportedOffset + OffsetSkip.SkipChars(_rate));
        SpeakFrom(OffsetSkip.SnapToWordStart(_text, target));
        return Task.CompletedTask;
    }

    public Task SkipBackward()
    {
        if (State == PlaybackState.Idle)
        {
            return Task.CompletedTask;
        }

        int target = OffsetSkip.ClampOffset(_text, _lastReportedOffset - OffsetSkip.SkipChars(_rate));
        SpeakFrom(OffsetSkip.SnapToWordStart(_text, target));
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
