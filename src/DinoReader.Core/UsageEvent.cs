using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeviceContext = ReadTheStupidText.Application.Usage.DeviceContext;

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
    public const string StoryLanguagesSet = "story_languages_set";
    public const string StoryLanguageAdded = "story_language_added";
    public const string StoryLanguageSwitched = "story_language_switched";
    public const string VoicePackDownload = "voice_pack_download";

    // Slice 250: the playback events. Each names its book; read_stopped's detail is a ReadStopReason name (ReadPlaybackNames).
    public const string ReadStarted = "read_started";
    public const string ReadPaused = "read_paused";
    public const string ReadResumed = "read_resumed";
    public const string ReadStopped = "read_stopped";
    public const string BookLeft = "book_left";

    // Slice 288: a reading that reached 30 s of played audio (one per reading). read_paused and read_stopped also carry Seconds.
    public const string BookListened = "book_listened";

    // Slice 267: which voice a read used (detail: AudioPackUsage.ReadVoice), and how one pre-made audio pack download went (detail: started / done / failed:<kind>).
    public const string ReadVoice = "read_voice";
    public const string AudioPackDownload = "audio_pack_download";

    // Slice 273: a feature fell back to something else (detail: FallbackUsage.Detail, "<area>:<reason>"; Count: how many times in the session).
    public const string Fallback = "fallback";

    // Slice 313: a shelf book was behind the store (detail: BookUpdateUsage.Detail, "<result>:<from>:<to>", versions only).
    public const string BookUpdate = "book_update";

    // Slice 352: where the app was and what was pressed. All are about no book. screen: UiUsageDetail.Screen ("<screen>:<action>"), tap: UiUsageDetail.Tap ("<screen>:<control>"),
    // setting_changed: UiUsageDetail.Setting ("<setting>:<value>"), app_backgrounded / app_resumed: the screen the app was on.
    public const string Screen = "screen";
    public const string Tap = "tap";
    public const string SettingChanged = "setting_changed";
    public const string AppBackgrounded = "app_backgrounded";
    public const string AppResumed = "app_resumed";
}

/// <summary>Slice 228: the one rule for a demo read (the intro's read step), which is not a real reading of a book and so counts no book open, finish or (Slice 250) playback event.</summary>
public static class DemoReadCounting
{
    private static readonly HashSet<string> BookCounts =
    [
        UsageEventName.BookOpened, UsageEventName.BookFinished, UsageEventName.BookLeft,
        UsageEventName.ReadStarted, UsageEventName.ReadPaused, UsageEventName.ReadResumed, UsageEventName.ReadStopped, UsageEventName.ReadVoice, UsageEventName.BookListened,
    ];

    /// <summary>Whether the count <paramref name="eventName"/> may be recorded for a read that is (<paramref name="demoRead"/>) or is not a demo.</summary>
    public static bool Counts(string eventName, bool demoRead) => !demoRead || !BookCounts.Contains(eventName);
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
    public const string KindDocx = "docx";
    public const string KindTxt = "txt";
    public const string KindMixed = "mixed";

    /// <summary>A book that came back from a backup restore (Slice 191), one count per restored book.</summary>
    public const string KindRestore = "restore";

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

    public const string LanguagesFromIntro = "intro";
    public const string LanguagesFromSettings = "settings";

    public const string PackDone = "done";
    public const string PackFailed = "failed";
    public const string PackCancelled = "cancelled";

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

    /// <summary>The detail of a story-languages count (Slice 173): how many story languages there are now (1 to 3) and where the parent set them (<see cref="LanguagesFromIntro"/> or <see cref="LanguagesFromSettings"/>), like <c>2:settings</c>.</summary>
    public static string StoryLanguages(int count, string where) => string.Concat(count.ToString(CultureInfo.InvariantCulture), WaitSeparator, where);

    /// <summary>The detail of an own-book count (Slice 178): how the book came in (a <c>Kind</c> value) and its page-count bucket, like <c>pdf:6-10</c>.</summary>
    public static string OwnBookAdded(string kind, int pageCount) => string.Concat(kind, WaitSeparator, PagesBucket(pageCount));

    /// <summary>
    /// The detail of the count for a book a backup restore put back (Slice 191), like <c>restore:2-5</c>, or null when the book is not the grown-up's own:
    /// a restored starter or store book has a <see cref="Story.SourceId"/> and is not counted (the store download already was).
    /// </summary>
    public static string? RestoredBook(Story restored) =>
        restored.SourceId is null ? OwnBookAdded(KindRestore, restored.Pages.Count) : null;

    /// <summary>How many pages of a book have a parent recording, as a bucket (never the exact count).</summary>
    public static string PagesBucket(int recordedPages) => recordedPages switch
    {
        < TwoPages => PagesOne,
        <= FivePages => PagesTwoToFive,
        <= TenPages => PagesSixToTen,
        _ => PagesMore,
    };
}

/// <summary>
/// Names an own book's kind from where each of its saved pages came in (Slice 178): all pages from one source give that kind, pages from
/// more than one source, or pages written in the wizard beside an import, give <see cref="UsageDetail.KindMixed"/>, and a book with
/// no import at all is <see cref="UsageDetail.KindTyped"/>.
/// </summary>
public static class OwnBookKind
{
    /// <summary>The kind for pages that came in by <paramref name="origins"/> (a <see cref="UsageDetail"/> kind each; null is a page written by hand).</summary>
    public static string Of(IEnumerable<string?> origins)
    {
        List<string> kinds = origins.Select(o => o ?? UsageDetail.KindTyped).Distinct().ToList();
        return kinds.Count switch
        {
            0 => UsageDetail.KindTyped,
            1 => kinds[0],
            _ => UsageDetail.KindMixed,
        };
    }
}

/// <summary>The first-run intro's steps in order (Slice 155, Decision 160): (Slice 228) the two places and a read step, then recording a book, adding an own book, and (Slice 173) the story languages. Slice 261 took the store step out (the first-run book picker replaced it). The index is what is saved, so a restart resumes at the same step.</summary>
public static class IntroSteps
{
    public const string Places = "places";
    public const string Read = "read";
    public const string Record = "record";
    public const string Languages = "languages";

    /// <summary>The retired add-your-own-book step (Slice 278): no longer shown, but apps released before it still send <c>add:tried</c> and <c>add:skipped</c>, so the Worker keeps the name.</summary>
    public const string Add = "add";

    /// <summary>The retired store step (Slice 261): no longer shown, but apps released before it still send <c>store:tried</c>, so the Worker keeps accepting the name.</summary>
    public const string Store = "store";

    /// <summary>How many steps the intro had before Slice 228 put <see cref="Places"/> and <see cref="Read"/> in front.</summary>
    private const int LegacyCount = 4;
    private const int StepsAdded = 2;
    private const int SixStepCount = 6;
    private const int StoreIndexInSix = 2;
    private const int NotRunning = -1;

    private static readonly string[] Order = [Places, Read, Record, Languages];

    /// <summary>The step an intro saved before Slice 228 (4 steps: store, record, add, languages) resumes at in today's four: the store step lands on record, the add step on languages; -1 when that saved value was not a step.</summary>
    public static int FromLegacy(int legacyIndex) => legacyIndex >= 0 && legacyIndex < LegacyCount ? FromSixSteps(legacyIndex + StepsAdded) : NotRunning;

    /// <summary>The step an intro saved by a six-step build (Slice 228 to 260: places, read, store, record, add, languages) resumes at: the store step lands on record, later steps move one place back, and the add step (Slice 278) lands on languages; -1 when that saved value was not a step.</summary>
    public static int FromSixSteps(int savedIndex)
    {
        if (savedIndex < 0 || savedIndex >= SixStepCount)
        {
            return NotRunning;
        }

        return Clamp(savedIndex <= StoreIndexInSix ? savedIndex : savedIndex - 1);
    }

    /// <summary>A saved step made valid: -1 (not running) stays, any other negative value is not a step (-1), and a value past the last step resumes at the last step. A step saved by a five-step build (add at 3, languages at 4) therefore lands on languages (Slice 278).</summary>
    public static int Clamp(int saved) => saved < 0 ? NotRunning : Math.Min(saved, Order.Length - 1);

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
/// <see cref="Session"/> and <see cref="Seq"/> (Slice 250) are the per-launch random session id (32 hex digits, in memory and on disk only inside unsent queued counts, never reused for a later launch) and the count's order inside it; null for a count made before they existed.
/// <see cref="Count"/> and <see cref="DeviceCount"/> (Slice 273) are two optional whole numbers: a <c>fallback</c> count's number of times in the session, and a <c>read_voice</c> count's pieces read by clips (<see cref="Count"/>) and by the device voice (<see cref="DeviceCount"/>); left out of the JSON when null.
/// <see cref="Screen"/> (Slice 352) is the screen the app was on when the count was made (a <see cref="UiScreens"/> name; the Worker keeps it as <c>blob20</c>), left out of the JSON when null.
/// <see cref="Seconds"/> (Slice 288) is the whole seconds of audio played since the previous paused or stopped row of the reading, on <c>read_paused</c> and <c>read_stopped</c> (always, 0 allowed; never on <c>book_listened</c>); left out of the JSON when null.
/// </summary>
public sealed record UsageEvent(string Name, string Book, string Language, string Source, bool HasRecording, string Version, string Platform, string Day, double SampleRate, string Detail = "", string? Session = null, int? Seq = null, int? Count = null, int? DeviceCount = null, int? Seconds = null, string? UiLang = null, string? InstallAge = null, string? LaunchNo = null, int? SinceLaunch = null, string? Screen = null)
{
    private const int PackageIdLength = 32;
    private const string DayFormat = "yyyy-MM-dd";

    /// <summary>
    /// The count for <paramref name="name"/> about <paramref name="story"/>; <paramref name="language"/> is the language it was read in (Slice 173), the book's own when null. Only a book that came from the starters or the store
    /// (<paramref name="fromCatalogue"/>) with a usable package id is a shelf book; any other book, a re-imported own one included, counts as the grown-up's own.
    /// </summary>
    public static UsageEvent For(string name, Story story, bool fromCatalogue, double sampleRate, string version, AppPlatform platform, DateTimeOffset now, string detail = "", string? language = null)
    {
        string? shelfId = fromCatalogue ? ShelfId(story.SourceId) : null;
        return new UsageEvent(
            name,
            shelfId ?? UsageSource.Own,
            language ?? story.Language,
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

/// <summary>
/// The context every count carries (Slice 351): <see cref="UsageEvent.UiLang"/> (the app's screen language), <see cref="UsageEvent.InstallAge"/> and <see cref="UsageEvent.LaunchNo"/> (closed buckets from a first-launch date and a launch count that stay on the device, never sent)
/// and <see cref="UsageEvent.SinceLaunch"/> (whole seconds since this launch started). No install id: the buckets cannot tell two installs apart. The Worker keeps the same closed lists.
/// </summary>
public static class UsageContext
{
    public const string AgeFirst = "first";
    public const string AgeSameDay = "d0";
    public const string AgeFirstWeek = "d1_7";
    public const string AgeFirstMonth = "d8_30";
    public const string AgeLater = "d31_plus";

    public const string LaunchFirst = "1";
    public const string LaunchFewer = "2_5";
    public const string LaunchMany = "6_20";
    public const string LaunchMost = "21_plus";

    /// <summary>The cap of <see cref="UsageEvent.SinceLaunch"/>: one day.</summary>
    public const int MaxSinceLaunch = 86_400;

    private const int OneWeek = 7;
    private const int OneMonth = 30;
    private const int SecondLaunch = 2;
    private const int SixthLaunch = 6;
    private const int TwentyFirstLaunch = 21;
    private const int FifthLaunch = 5;
    private const int TwentiethLaunch = 20;

    /// <summary>The age bucket of an install whose first launch was on <paramref name="firstLaunch"/>, seen at a launch on <paramref name="today"/>; <paramref name="launchNumber"/> 1 is the first launch itself.</summary>
    public static string InstallAgeOf(DateOnly firstLaunch, DateOnly today, int launchNumber)
    {
        int days = Math.Max(0, today.DayNumber - firstLaunch.DayNumber);
        return (launchNumber, days) switch
        {
            (<= 1, 0) => AgeFirst,
            (_, 0) => AgeSameDay,
            (_, <= OneWeek) => AgeFirstWeek,
            (_, <= OneMonth) => AgeFirstMonth,
            _ => AgeLater,
        };
    }

    /// <summary>The launch-count bucket of the <paramref name="launchNumber"/>th launch (1 for the first).</summary>
    public static string LaunchNoOf(int launchNumber) => launchNumber switch
    {
        < SecondLaunch => LaunchFirst,
        <= FifthLaunch => LaunchFewer,
        <= TwentiethLaunch => LaunchMany,
        _ => LaunchMost,
    };

    /// <summary>Whole seconds since the launch started, from 0 up to <see cref="MaxSinceLaunch"/>.</summary>
    public static int SinceLaunchOf(TimeSpan elapsed) => (int)Math.Clamp(Math.Floor(elapsed.TotalSeconds), 0, MaxSinceLaunch);
}

/// <summary>
/// The two values kept on the device between launches (Slice 351): the day of the first launch and the number of launches so far. <see cref="Begin"/> counts a launch and
/// gives back the new values to save and the buckets this launch's counts carry. Pure, so a test pins it; the app saves the values in preferences and never sends them.
/// </summary>
public static class LaunchHistory
{
    private const string DayFormat = "yyyy-MM-dd";

    /// <summary>What a launch stamps on its counts, and what to save.</summary>
    public readonly record struct Launch(string FirstDay, int Number, string InstallAge, string LaunchNo);

    /// <summary>Counts a launch on <paramref name="today"/>; a missing or damaged <paramref name="storedFirstDay"/> or a count below 0 makes this the first launch.</summary>
    public static Launch Begin(string? storedFirstDay, int storedLaunches, DateOnly today)
    {
        bool known = DateOnly.TryParseExact(storedFirstDay, DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly first) && storedLaunches >= 0;
        int number = known ? storedLaunches + 1 : 1;
        DateOnly firstDay = known ? first : today;
        return new Launch(firstDay.ToString(DayFormat, CultureInfo.InvariantCulture), number, UsageContext.InstallAgeOf(firstDay, today, number), UsageContext.LaunchNoOf(number));
    }
}

/// <summary>The request body of <c>POST /api/events</c> and the rule for who is counted at all.</summary>
public static class UsageBatch
{
    /// <summary>The Worker refuses more than this in one request.</summary>
    public const int MaxEvents = 50;

    /// <summary>Slice 352: the app's queue sends as soon as this many counts wait, and in batches of this size, so a busy session never meets the Worker's 50-count / 32 KB cap.</summary>
    public const int FlushAt = 40;

    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The JSON body for <paramref name="events"/> (at most <see cref="MaxEvents"/>; send longer lists in slices) with the optional <paramref name="device"/> object (Slice 166), left out when null.</summary>
    public static string ToJson(IReadOnlyList<UsageEvent> events, DeviceContext? device = null) => JsonSerializer.Serialize(new { schemaVersion = SchemaVersion, device, events }, Options);

    /// <summary>The events in a stored queue, or none when <paramref name="json"/> is missing or damaged.</summary>
    public static List<UsageEvent> ParseQueue(string? json)
    {
        try
        {
            return string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<UsageEvent>>(json, Options) ?? [];
        }
        catch (JsonException)
        {
            // no-fallback: usage code never reports on itself; a damaged queue is dropped
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
