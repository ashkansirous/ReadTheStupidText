using System.Collections.Concurrent;
using System.Diagnostics;
using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Application.Usage;
using ReadTheStupidText.Domain.Activity;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

// Slice 140: the anonymous usage counts of a Windows read (Decisions 143-145): that it started and what started it, in which languages and
// with which kind of voice, and how long the translation, a translation-pack download and the first sound took. A count holds closed-list
// values only (UsageEvents): the text itself is never in it, only its length bucket. Every count is recorded through IUsageRecorder, which
// never throws and never makes the read wait.
public sealed partial class ReadAloudService
{
    private readonly IUsageRecorder? _usage;

    // The read whose wait for the first sound is being timed, with the languages it will be counted under, as one immutable value that is
    // swapped whole; null once the wait ended, so each wait is counted once.
    private sealed record FirstAudioWait(ActivityEntry Entry, long StartTicks, string? SourceLanguage, string? Language);

    private FirstAudioWait? _firstAudioWait;

    // The entry whose streamed start was already counted, so a stream that falls back to the whole-text path is not counted twice.
    private ActivityEntry? _streamCountedEntry;

    // When each translation-pack download began, keyed "source>target" (Progress events come from the download's own thread).
    private readonly ConcurrentDictionary<string, long> _packDownloadStarts = new();

    private static readonly TimeSpan PackDownloadStaleAfter = TimeSpan.FromMinutes(30);

    private void StartUsageTracking()
    {
        if (_usage is not null && _translation is not null)
        {
            _translation.PackProgress += OnPackProgressForUsage;
        }
    }

    private void StopUsageTracking()
    {
        if (_usage is not null && _translation is not null)
        {
            _translation.PackProgress -= OnPackProgressForUsage;
        }
    }

    // A wait still open when the next read begins never saw its first sound (a stop or an error outside Supersede ended it): it is counted
    // as cancelled, with its own start, instead of staying pending and being lost.
    private void BeginFirstAudioCount(ActivityEntry entry)
    {
        FirstAudioWait? stale = Interlocked.Exchange(ref _firstAudioWait, new FirstAudioWait(entry, _activeReadStartTicks, null, null));
        if (_usage is not null && stale is not null && !ReferenceEquals(stale.Entry, entry))
        {
            CountFirstAudio(stale, UsageOutcome.Cancelled);
        }
    }

    private void CountFirstAudio(FirstAudioWait wait, string outcome) =>
        CountTiming(UsageStage.FirstAudio, outcome, Stopwatch.GetElapsedTime(wait.StartTicks), wait.SourceLanguage, wait.Language);

    private void SetFirstAudioLanguages(ActivityEntry entry, string? sourceLanguage, string? language)
    {
        FirstAudioWait? wait = Volatile.Read(ref _firstAudioWait);
        if (wait is not null && ReferenceEquals(wait.Entry, entry))
        {
            Interlocked.CompareExchange(ref _firstAudioWait, wait with { SourceLanguage = sourceLanguage, Language = language }, wait);
        }
    }

    // Counts the wait once: later calls for the same read find it already ended.
    private void EndFirstAudioCount(ActivityEntry entry, string outcome)
    {
        FirstAudioWait? wait = Volatile.Read(ref _firstAudioWait);
        if (_usage is null || wait is null || !ReferenceEquals(wait.Entry, entry) || !ReferenceEquals(Interlocked.CompareExchange(ref _firstAudioWait, null, wait), wait))
        {
            return;
        }

        CountFirstAudio(wait, outcome);
    }

    // A text read as a translation says which language it came from; the language read in is the translation's target, else the language of the
    // voice that reads it, else the text's detected language, else the app's language setting.
    private void CountReadStarted(ActivityEntry entry, int textLength, string? sourceLanguage, bool translated, string? targetLanguage)
    {
        if (_usage is null || ReferenceEquals(Volatile.Read(ref _streamCountedEntry), entry))
        {
            return;
        }

        VoiceInfo? voice = CurrentVoice();
        string? language = translated ? targetLanguage : OriginalReadLanguage(voice, sourceLanguage);
        string engine = UsageEvents.EngineOf(voice);
        bool voiceDefault = UsageEvents.VoiceIsDefault(_settings.VoiceId, _voices.DefaultVoice?.Id);
        string source = UsageEvents.SourceOf(entry.Trigger);
        _usage.Record(context => UsageEvents.ReadStarted(context, sourceLanguage, language, translated, engine, voiceDefault, textLength, source));
        SetFirstAudioLanguages(entry, sourceLanguage, UsageEvents.Language(language));
    }

    private string? OriginalReadLanguage(VoiceInfo? voice, string? detectedLanguage) =>
        !string.IsNullOrEmpty(voice?.Language) ? voice.Language : (UsageEvents.Language(detectedLanguage) is { } detected ? detected : _settings.Language);

    // The streamed path: the first translated block is ready, so the read starts and the translation wait ends. The translate timing is
    // therefore the wait until reading can begin, as on the whole-text path (which times PrepareAsync); a failure after the first block is not a translate wait.
    private void CountStreamStarted(ActivityEntry entry, TranslationStream stream, int textLength, long translateStarted)
    {
        bool translated = stream.Outcome == TranslationOutcome.Translated;
        string outcome = translated ? UsageOutcome.Done : UsageOutcome.Failed;
        CountTiming(UsageStage.Translate, outcome, Stopwatch.GetElapsedTime(translateStarted), stream.Source, stream.Target);
        CountReadStarted(entry, textLength, stream.Source, translated, stream.Target);
        Volatile.Write(ref _streamCountedEntry, entry); // after the count: from here on the whole-text fallback must not count this read again
    }

    // Only an attempt that ran counts: a translated text is Done, a timeout, a missing pack or a failure is Failed; "same language",
    // "source unknown", "pair unsupported" and "pack downloading" did no translating, so there is nothing to time.
    private void CountTranslationTiming(ActivityEntry entry, ReadTranslationResult result, TimeSpan elapsed)
    {
        if (ReferenceEquals(Volatile.Read(ref _streamCountedEntry), entry))
        {
            return; // the streamed attempt already counted this read's translation
        }

        string? outcome = result.Outcome switch
        {
            TranslationOutcome.Translated => UsageOutcome.Done,
            TranslationOutcome.TimedOut or TranslationOutcome.Failed or TranslationOutcome.ModelMissing => UsageOutcome.Failed,
            _ => null,
        };
        if (outcome is not null)
        {
            CountTiming(UsageStage.Translate, outcome, elapsed, result.Source, result.Target);
        }
    }

    private void CountTiming(string stage, string outcome, TimeSpan elapsed, string? sourceLanguage, string? language) =>
        _usage?.Record(context => UsageEvents.Timing(context, stage, outcome, elapsed, sourceLanguage, language));

    // Turning translation on is the one count about the translate toggle; the text's language is not known yet, so only a picked source is sent.
    private void CountTranslateSwitchedOn()
    {
        string? source = _settings.TranslateSourceLanguage;
        string? target = _settings.Language;
        _usage?.Record(context => UsageEvents.TranslateSwitchedOn(context, source, target));
    }

    private void CountVoiceChanged(string voiceId)
    {
        if (_usage is null)
        {
            return;
        }

        VoiceInfo? voice = _voices.InstalledVoices.FirstOrDefault(v => v.Id == voiceId);
        if (voice is null)
        {
            return; // not an installed voice (a stale id, a pack still arriving): its engine is unknown, so it is not counted as a Windows voice
        }

        string? language = string.IsNullOrEmpty(voice.Language) ? _settings.Language : voice.Language;
        string engine = UsageEvents.EngineOf(voice);
        _usage.Record(context => UsageEvents.VoiceChanged(context, language, engine));
    }

    private VoiceInfo? CurrentVoice() => _voices.InstalledVoices.FirstOrDefault(v => v.Id == CurrentVoiceId);

    // The download of a translation pack is the longest wait a translated read can have; it is timed from the first progress report to the last.
    private void OnPackProgressForUsage(object? sender, TranslationPackProgress progress)
    {
        string key = $"{progress.Source}>{progress.Target}";
        if (progress.State == TranslationPackState.Downloading)
        {
            long now = Stopwatch.GetTimestamp();
            // A start older than the stale limit belonged to a download that ended without a final report: this is a new download.
            _packDownloadStarts.AddOrUpdate(key, now, (_, old) => Stopwatch.GetElapsedTime(old) > PackDownloadStaleAfter ? now : old);
            return;
        }

        if (_packDownloadStarts.TryRemove(key, out long started))
        {
            string outcome = progress.State == TranslationPackState.Ready ? UsageOutcome.Done : UsageOutcome.Failed;
            CountTiming(UsageStage.ModelDownload, outcome, Stopwatch.GetElapsedTime(started), progress.Source, progress.Target);
        }
    }
}
