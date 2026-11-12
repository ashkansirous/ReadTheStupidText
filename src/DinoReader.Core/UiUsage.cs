namespace DinoReader.Core;

/// <summary>
/// Slice 352: the screens Dino counts. A screen is a place the app can be (a wizard page, the child's shelf, a tab of the grown-ups' corner, a settings page). The Worker keeps the same closed list
/// (<c>SCREENS</c> in worker/events.ts; <c>UiUsageTests</c> keeps the two equal). The current screen is also on every count as <c>blob20</c>.
/// </summary>
public static class UiScreens
{
    public const string Intro1 = "intro_1";
    public const string Intro2 = "intro_2";
    public const string Intro3 = "intro_3";
    public const string Intro4 = "intro_4";
    public const string FirstRun = "first_run";
    public const string FirstBooks = "first_books";
    public const string Shelf = "shelf";
    public const string Reading = "reading";
    public const string CornerBooks = "corner_books";
    public const string CornerStore = "corner_store";
    public const string CornerSettings = "corner_settings";
    public const string Gate = "gate";
    public const string StoryLanguages = "story_languages";
    public const string Language = "language";
    public const string ReadingSpeed = "reading_speed";
    public const string AnimalSounds = "animal_sounds";
    public const string RecordIntro = "record_intro";
    public const string Record = "record";
    public const string Backup = "backup";
    public const string UsageCounts = "usage_counts";
    public const string About = "about";
    public const string ReportProblem = "report_problem";
    public const string Update = "update";
    public const string CloseQuestion = "close_question";
    public const string DropLanguage = "drop_language";

    /// <summary>The value a count carries when no screen is known (also the worker's "none").</summary>
    public const string None = "none";

    /// <summary>Every screen, in the order the Worker lists them.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        Intro1, Intro2, Intro3, Intro4, FirstRun, FirstBooks, Shelf, Reading, CornerBooks, CornerStore, CornerSettings, Gate, StoryLanguages, Language, ReadingSpeed, AnimalSounds,
        RecordIntro, Record, Backup, UsageCounts, About, ReportProblem, Update, CloseQuestion, DropLanguage,
    ];

    /// <summary>The wizard screen for the 0-based step <paramref name="step"/> (<see cref="IntroSteps"/>), or null outside the four.</summary>
    public static string? IntroOf(int step) => step switch
    {
        0 => Intro1,
        1 => Intro2,
        2 => Intro3,
        3 => Intro4,
        _ => null,
    };
}

/// <summary>Slice 352: what a <c>screen</c> count says happened on a screen (closed list, equal to the Worker's <c>SCREEN_ACTIONS</c>).</summary>
public static class UiScreenActions
{
    public const string Shown = "shown";
    public const string Next = "next";
    public const string Back = "back";
    public const string Skip = "skip";
    public const string Close = "close";

    public static readonly IReadOnlyList<string> All = [Shown, Next, Back, Skip, Close];
}

/// <summary>Slice 352: the controls a <c>tap</c> count names (closed list, equal to the Worker's <c>CONTROLS</c>). One flat list: the screen in the count says where it was.</summary>
public static class UiControls
{
    public const string Play = "play";
    public const string Pause = "pause";
    public const string Stop = "stop";
    public const string NextPage = "next_page";
    public const string PreviousPage = "previous_page";
    public const string Sentence = "sentence";
    public const string Back = "back";
    public const string Close = "close";
    public const string Done = "done";
    public const string Cancel = "cancel";
    public const string Confirm = "confirm";
    public const string Next = "next";
    public const string Skip = "skip";
    public const string SkipAll = "skip_all";
    public const string TryIt = "try_it";
    public const string Continue = "continue";
    public const string Retry = "retry";
    public const string Save = "save";
    public const string Restore = "restore";
    public const string Send = "send";
    public const string CopyCode = "copy_code";
    public const string Update = "update";
    public const string Download = "download";
    public const string Delete = "delete";
    public const string Filter = "filter";
    public const string Language = "language";
    public const string Voice = "voice";
    public const string Speed = "speed";
    public const string Mode = "mode";
    public const string Book = "book";
    public const string BookMore = "book_more";
    public const string ReadNow = "read_now";
    public const string Record = "record";
    public const string OpenCorner = "open_corner";
    public const string TabBooks = "tab_books";
    public const string TabStore = "tab_store";
    public const string TabSettings = "tab_settings";
    public const string SettingsRow = "settings_row";
    public const string Toggle = "toggle";
    public const string Reorder = "reorder";
    public const string Pick = "pick";
    public const string Dismiss = "dismiss";
    public const string GateKey = "gate_key";
    public const string SeeStories = "see_stories";

    public static readonly IReadOnlyList<string> All =
    [
        Play, Pause, Stop, NextPage, PreviousPage, Sentence, Back, Close, Done, Cancel, Confirm, Next, Skip, SkipAll, TryIt, Continue, Retry, Save, Restore, Send, CopyCode, Update, Download, Delete,
        Filter, Language, Voice, Speed, Mode, Book, BookMore, ReadNow, Record, OpenCorner, TabBooks, TabStore, TabSettings, SettingsRow, Toggle, Reorder, Pick, Dismiss, GateKey, SeeStories,
    ];
}

/// <summary>
/// Slice 352: the settings a <c>setting_changed</c> count names and the closed values each may take (the Worker's <c>SETTING_VALUES</c>). A value is never typed text: a switch is <c>on</c>/<c>off</c>,
/// a language is one of the 15 codes, a speed is a bucket.
/// </summary>
public static class UiSettings
{
    public const string UsageCounts = "usage_counts";
    public const string AnimalSounds = "animal_sounds";
    public const string ReadingSpeed = "reading_speed";
    public const string ModeSetting = "reading_mode";
    public const string AppLanguage = "app_language";

    public const string On = "on";
    public const string Off = "off";

    /// <summary>The reading-speed buckets a speed is counted as (never the exact number).</summary>
    public static readonly IReadOnlyList<string> SpeedBuckets = ["slow", "normal", "fast"];

    /// <summary>The reading-mode names (<c>ReadingMode</c>: text, picture only, picture and text).</summary>
    public static readonly IReadOnlyList<string> Modes = ["text", "picture", "picture_text"];

    private static readonly IReadOnlyList<string> Switch = [On, Off];

    /// <summary>Each setting and the values it may take; the app languages are <see cref="UiLanguage.Supported"/>.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Values { get; } = new Dictionary<string, IReadOnlyList<string>>
    {
        [UsageCounts] = Switch,
        [AnimalSounds] = Switch,
        [ReadingSpeed] = SpeedBuckets,
        [ModeSetting] = Modes,
        [AppLanguage] = UiLanguage.Supported,
    };

    /// <summary>The bucket for a reading speed multiplier: under 0.95 is slow, up to 1.05 normal, above that fast.</summary>
    public static string SpeedBucketOf(double rate) => rate < 0.95 ? SpeedBuckets[0] : rate <= 1.05 ? SpeedBuckets[1] : SpeedBuckets[2];

    /// <summary>True when a speed moved from one bucket to another; a drag or step inside one bucket is not a changed setting.</summary>
    public static bool SpeedBucketChanged(double before, double after) => SpeedBucketOf(before) != SpeedBucketOf(after);

    /// <summary>The name of a reading mode, as counted.</summary>
    public static string ModeOf(ReadingMode mode) => mode switch
    {
        ReadingMode.Text => Modes[0],
        ReadingMode.PictureOnly => Modes[1],
        _ => Modes[2],
    };

    /// <summary>The value of a switch.</summary>
    public static string OnOff(bool on) => on ? On : Off;
}

/// <summary>Slice 352: where a read was started from (closed list, equal to the Worker's <c>READ_ORIGINS</c>). It is the detail of <c>read_started</c> and <c>book_opened</c>.</summary>
public static class ReadOrigins
{
    public const string Wizard = "wizard";
    public const string Shelf = "shelf";
    public const string Corner = "corner";

    /// <summary>A book picked up again where it was left; no path in the app starts one yet, the name is kept for the list the Worker accepts.</summary>
    public const string Resume = "resume";

    /// <summary>A book that follows the one just finished; no path in the app starts one yet.</summary>
    public const string NextBook = "next_book";

    public static readonly IReadOnlyList<string> All = [Wizard, Shelf, Corner, Resume, NextBook];
}

/// <summary>Slice 352: the detail strings of the new counts (each half from a closed list; the Worker checks both).</summary>
public static class UiUsageDetail
{
    private const char Separator = ':';

    /// <summary>The detail of a <c>screen</c> count: <c>intro_2:next</c>.</summary>
    public static string Screen(string screen, string action) => string.Concat(screen, Separator, action);

    /// <summary>The detail of a <c>tap</c> count: <c>shelf:book</c>.</summary>
    public static string Tap(string screen, string control) => string.Concat(screen, Separator, control);

    /// <summary>The detail of a <c>setting_changed</c> count: <c>reading_speed:fast</c>.</summary>
    public static string Setting(string setting, string value) => string.Concat(setting, Separator, value);
}
