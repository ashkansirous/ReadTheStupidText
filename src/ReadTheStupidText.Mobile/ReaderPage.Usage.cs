using System.Diagnostics;
using ReadTheStupidText.Application.Usage;
using ReadTheStupidText.Mobile.Resources.Strings;
using ReadTheStupidText.Mobile.Shared.Usage;

namespace ReadTheStupidText.Mobile;

// Anonymous usage counts of a read (Slice 137): that it started, in which languages and with which kind of voice, and how long the
// first sound took. A count holds closed-list values only (UsageEvents); the text itself is never in it, only its length bucket.
public partial class ReaderPage
{
    // An install that updated from before the counts never saw the first-run language screen's line, so it is told once here (Decision 143).
    private async Task ShowCountsNoticeOnceAsync()
    {
        if (RtstUsage.TakeUnseenNotice())
        {
            await AlertAsync(AppResources.Settings_CountsTitle, AppResources.Language_CountsNote);
        }
    }

    // Running from the tap until the first sound, a stop or a failure; null when no read waits for its first sound.
    private Stopwatch? _firstAudioClock;
    private string? _firstAudioSource;
    private string? _firstAudioLanguage;
    private bool _firstAudioEarly;

    private void StartFirstAudioClock(string text)
    {
        _firstAudioClock = Stopwatch.StartNew();
        _firstAudioSource = null;
        _firstAudioLanguage = null;
        _firstAudioEarly = _earlyVoice.TakeEarly(text, ReadingLanguageFor(text)); // the head was prepared while the person decided (Slice 139)
    }

    // A text read as the translation made on the review page says nothing here about the language it came from (that count is translate_chosen).
    private void CountReadStarted(string text, VoicePlan plan)
    {
        bool translated = _original is not null;
        string? source = translated ? null : _detectedSource;
        string engine = UsageEvents.EngineFor(plan.PhoneReads, plan.Pack?.Kind);
        bool voiceDefault = UsageEvents.VoiceIsDefault(_settings.VoiceId, plan.Pack?.ResolveVoice(null).Id);
        _usage.Record(context => UsageEvents.ReadStarted(context, source, plan.Language, translated, engine, voiceDefault, text.Length));
        _firstAudioSource = source;
        _firstAudioLanguage = plan.Language;
    }

    // Counts the wait once: later calls find no clock. Done when the sound played, Cancelled when the read was stopped or replaced, Failed on an error.
    private void EndFirstAudioClock(string outcome)
    {
        Stopwatch? clock = _firstAudioClock;
        if (clock is null)
        {
            return;
        }

        _firstAudioClock = null;
        TimeSpan elapsed = clock.Elapsed;
        string? source = _firstAudioSource;
        string? language = _firstAudioLanguage;
        bool early = _firstAudioEarly;
        _usage.Record(context => UsageEvents.Timing(context, UsageStage.FirstAudio, outcome, elapsed, source, language, early));
    }
}
