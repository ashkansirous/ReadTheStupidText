using System.Globalization;
using System.Text.Json;

namespace DinoReader.Core;

/// <summary>The few things Dino counts (Slice 133, Decision 140). The Worker accepts exactly these names.</summary>
public static class UsageEventName
{
    public const string BookOpened = "book_opened";
    public const string BookFinished = "book_finished";
    public const string StoreDownload = "store_download";
    public const string OwnBookAdded = "own_book_added";
    public const string RecordingMade = "recording_made";
    public const string VoiceChanged = "voice_changed";
    public const string ReadingLanguageChanged = "reading_language_changed";
    public const string AppOpened = "app_opened";
    public const string WaitShown = "wait_shown";
    public const string WaitCancelled = "wait_cancelled";
    public const string IntroStarted = "intro_started";
    public const string IntroStep = "intro_step";
    public const string IntroEnded = "intro_ended";
}

/// <summary>Where a counted book came from: Dino's own shelf (a starter or a store story) or a grown-up's own book.</summary>
public static class UsageSource
{
    public const string Shelf = "shelf";
    public const string Own = "own";

    /// <summary>The book and source of a count that is about no book (a voice change, an app launch).</summary>
    public const string None = "none";
}

/// <summary>
/// The closed values of <see cref="UsageEvent.Detail"/> (Slice 134, Decision 140): what else a count says besides its book and language. The Worker
/// accepts exactly these shapes per event name, so nothing a grown-up typed or picked from free text can be carried.
/// </summary>
public static class UsageDetail
{
    public const string StoreNoFilter = "none";
    public const string StoreAge = "age";
    public const string StoreAuthor = "author";
    public const string StoreAgeAndAuthor = "age+author";

    public const string KindPhoto = "photo";
    public const string KindPdf = "pdf";
    public const string KindScan = "scan";
    public const string KindTyped = "typed";

    public const string PagesOne = "1";
    public const string PagesTwoToFive = "2-5";
    public const string PagesSixToTen = "6-10";
    public const string PagesMore = "11+";

    public const string ChangeCharacterVoice = "character_voice";
    public const string ChangeFlavour = "flavour";
    public const string ChangeNarrator = "narrator";

    public const string IntroFirstRun = "first_run";
    public const string IntroReplay = "replay";
    public const string IntroFinished = "finished";
    public const string IntroSkippedAll = "skipped_all";
    public const string IntroTried = "tried";
    public const string IntroSkipped = "skipped";

    /// <summary>Separates a wait count's stage from its duration bucket in <c>detail</c> (Slice 148): <c>stalled:2-5</c>.</summary>
    public const char WaitSeparator = ':';

    private const int TwoPages = 2;
    private const int FivePages = 5;
    private const int TenPages = 10;

    /// <summary>Which store filters were in use when a story was downloaded: only whether each was set, never its value.</summary>
    public static string StoreFilters(bool ageSet, bool authorSet) => (ageSet, authorSet) switch
    {
        (true, true) => StoreAgeAndAuthor,
        (true, false) => StoreAge,
        (false, true) => StoreAuthor,
        _ => StoreNoFilter,
    };

    /// <summary>The detail of a wait count: the wait's stage (a <c>UsageWaitStage</c> name) and how long its overlay showed (a <c>UsageBuckets.Duration</c> bucket).</summary>
    public static string Wait(string stage, string durationBucket) => string.Concat(stage, WaitSeparator, durationBucket);

    /// <summary>The detail of an intro step count (Slice 155): the step (an <see cref="IntroSteps"/> name) and the action (<c>tried</c> or <c>skipped</c>), like <c>store:tried</c>.</summary>
    public static string IntroStepAction(string step, bool tried) => string.Concat(step, WaitSeparator, tried ? IntroTried : IntroSkipped);

    /// <summary>How many pages of a book have a parent recording, as a bucket (never the exact count).</summary>
    public static string PagesBucket(int recordedPages) => recordedPages switch
    {
        < TwoPages => PagesOne,
        <= FivePages => PagesTwoToFive,
        <= TenPages => PagesSixToTen,
        _ => PagesMore,
    };
}

/// <summary>The first-run intro's steps in order (Slice 155, Decision 160): the store, recording a book, adding an own book. The index is what is saved, so a restart resumes at the same step.</summary>
public static class IntroSteps
{
    public const string Store = "store";
    public const string Record = "record";
    public const string Add = "add";

    private static readonly string[] Order = [Store, Record, Add];

    public static int Count => Order.Length;

    /// <summary>The name of step <paramref name="index"/> (0-based), or null outside the list.</summary>
    public static string? NameAt(int index) => index >= 0 && index < Order.Length ? Order[index] : null;

    /// <summary>True while <paramref name="index"/> names a step; a saved value outside the list means the intro is not running.</summary>
    public static bool IsStep(int index) => NameAt(index) is not null;
}

/// <summary>
/// One anonymous count (Slice 133, Decision 140): a name plus context, and nothing that identifies a person or a device. The book is the
/// shelf book's package id (32 lower-case hex digits) or <see cref="UsageSource.Own"/> for a grown-up's own book, never a title; the day is
/// the UTC date, no finer. <see cref="SampleRate"/> is the share of launches counted when this was queued, so the Worker can weight each count by 1 / rate.
/// <see cref="Detail"/> (Slice 134) is the event's one extra closed value (see <see cref="UsageDetail"/>), empty for the reader events; a count about no book has <see cref="UsageSource.None"/> as book and source.
/// </summary>
public sealed record UsageEvent(string Name, string Book, string Language, string Source, bool HasRecording, string Version, string Platform, string Day, double SampleRate, string Detail = "")
{
    private const int PackageIdLength = 32;
    private const string DayFormat = "yyyy-MM-dd";

    /// <summary>
    /// The count for <paramref name="name"/> about <paramref name="story"/>. Only a book that came from the starters or the store
    /// (<paramref name="fromCatalogue"/>) with a usable package id is a shelf book; any other book, a re-imported own one included, counts as the grown-up's own.
    /// </summary>
    public static UsageEvent For(string name, Story story, bool fromCatalogue, double sampleRate, string version, AppPlatform platform, DateTimeOffset now, string detail = "")
    {
        string? shelfId = fromCatalogue ? ShelfId(story.SourceId) : null;
        return new UsageEvent(
            name,
            shelfId ?? UsageSource.Own,
            story.Language,
            shelfId is null ? UsageSource.Own : UsageSource.Shelf,
            StoryRecordings.Count(story) > 0,
            version,
            platform.ToString().ToLowerInvariant(),
            DayOf(now),
            sampleRate,
            detail);
    }

    /// <summary>A count about a store story (by its package id, <paramref name="storyId"/>) the user has not necessarily got yet; null when the id is not a package id.</summary>
    public static UsageEvent? ForShelfBook(string name, string storyId, string language, string detail, double sampleRate, string version, AppPlatform platform, DateTimeOffset now) =>
        ShelfId(storyId) is { } id
            ? new UsageEvent(name, id, language, UsageSource.Shelf, false, version, platform.ToString().ToLowerInvariant(), DayOf(now), sampleRate, detail)
            : null;

    /// <summary>A count that is about no book (a voice change, a launch): <paramref name="language"/> is the one that matters for it.</summary>
    public static UsageEvent ForNoBook(string name, string language, string detail, double sampleRate, string version, AppPlatform platform, DateTimeOffset now) =>
        new(name, UsageSource.None, language, UsageSource.None, false, version, platform.ToString().ToLowerInvariant(), DayOf(now), sampleRate, detail);

    /// <summary>The language code a count sends when it is about no language (a wait count, Slice 148); the Worker takes only this for those counts.</summary>
    public const string NoLanguage = "und";

    private static string DayOf(DateTimeOffset now) => now.UtcDateTime.ToString(DayFormat, CultureInfo.InvariantCulture);

    /// <summary>The package id in its stored form (hex digits, no dashes, lower case), or null when <paramref name="sourceId"/> is missing or not an id.</summary>
    public static string? ShelfId(string? sourceId)
    {
        string compact = (sourceId ?? string.Empty).Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        return compact.Length == PackageIdLength && compact.All(Uri.IsHexDigit) ? compact : null;
    }
}

/// <summary>The request body of <c>POST /api/events</c> and the rule for who is counted at all.</summary>
public static class UsageBatch
{
    /// <summary>The Worker refuses more than this in one request.</summary>
    public const int MaxEvents = 50;

    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>The JSON body for <paramref name="events"/> (at most <see cref="MaxEvents"/>; send longer lists in slices).</summary>
    public static string ToJson(IReadOnlyList<UsageEvent> events) => JsonSerializer.Serialize(new { schemaVersion = SchemaVersion, events }, Options);

    /// <summary>The events in a stored queue, or none when <paramref name="json"/> is missing or damaged.</summary>
    public static List<UsageEvent> ParseQueue(string? json)
    {
        try
        {
            return string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<UsageEvent>>(json, Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string QueueToJson(IReadOnlyList<UsageEvent> events) => JsonSerializer.Serialize(events, Options);

    /// <summary>
    /// True when a session with this <paramref name="roll"/> (a random number from 0 up to but not including 1) is counted at the config's
    /// <paramref name="telemetry"/>: the switch is on and the roll is under the sample rate. No id is involved, so the roll is made once per app launch.
    /// </summary>
    public static bool Samples(TelemetryConfig telemetry, double roll) => telemetry.Enabled && roll < telemetry.SampleRate;
}
