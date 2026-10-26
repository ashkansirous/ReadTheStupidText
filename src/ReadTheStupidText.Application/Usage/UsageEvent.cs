namespace ReadTheStupidText.Application.Usage;

/// <summary>
/// One anonymous count from the phone (Slice 137, Decisions 143-145): a name plus closed-list values. It carries no id of the
/// install or the device, no read text and no file name, and nothing finer than the day. A field the event does not use stays null
/// and is left out of the JSON; the Worker (<c>worker/events.ts</c>) drops any count whose fields are outside its lists.
/// </summary>
/// <param name="Name">A <see cref="UsageEventName"/>.</param>
/// <param name="Version">The app version, like "1.4.0".</param>
/// <param name="Platform">"android", "ios" or "windows".</param>
/// <param name="Day">The UTC day, "yyyy-MM-dd".</param>
/// <param name="SampleRate">The share of launches that count; the server weighs each count by its inverse.</param>
/// <param name="Language">The language the count is about: the app's (app_opened), the one read or translated into, or the voice's.</param>
/// <param name="SourceLanguage">The detected language of the text, when known.</param>
/// <param name="Mode">A <see cref="UsageMode"/>.</param>
/// <param name="Engine">A <see cref="UsageEngine"/>.</param>
/// <param name="VoiceDefault">Whether the voice is the untouched default.</param>
/// <param name="LengthBucket">A <see cref="UsageBuckets.TextLength"/> value.</param>
/// <param name="OsMajor">The operating system's major version.</param>
/// <param name="Stage">A <see cref="UsageStage"/>.</param>
/// <param name="Outcome">A <see cref="UsageOutcome"/>.</param>
/// <param name="DurationBucket">A <see cref="UsageBuckets.Duration"/> value.</param>
/// <param name="Source">A <see cref="UsageSource"/>: what started a Windows read (Slice 140); the phone sends none.</param>
public sealed record UsageEvent(
    string Name,
    string Version,
    string Platform,
    string Day,
    double SampleRate,
    string? Language = null,
    string? SourceLanguage = null,
    string? Mode = null,
    string? Engine = null,
    bool? VoiceDefault = null,
    string? LengthBucket = null,
    int? OsMajor = null,
    string? Stage = null,
    string? Outcome = null,
    string? DurationBucket = null,
    string? Source = null);

/// <summary>The closed set of counts (the Worker's <c>RTST_EVENT_RULES</c> must match).</summary>
public static class UsageEventName
{
    public const string AppOpened = "app_opened";
    public const string ReadStarted = "read_started";
    public const string TranslateOffered = "translate_offered";
    public const string TranslateChosen = "translate_chosen";
    public const string VoiceChanged = "voice_changed";
    public const string Timing = "timing";
}

/// <summary>Whether a read speaks the text as it is or a translation of it.</summary>
public static class UsageMode
{
    public const string Original = "original";
    public const string Translation = "translation";
}

/// <summary>The voice engines a count may name.</summary>
public static class UsageEngine
{
    public const string Phone = "phone";
    public const string Supertonic = "supertonic";
    public const string Piper = "piper";
    public const string Melo = "melo";
    public const string Kokoro = "kokoro";

    /// <summary>The Windows speech voice that reads before a pack is installed, or by choice (Slice 140).</summary>
    public const string Windows = "windows";
}

/// <summary>What started a Windows read (Slice 140, Decision 143): the closed set, never a window title or an app name.</summary>
public static class UsageSource
{
    public const string Selection = "selection";
    public const string Clipboard = "clipboard";
    public const string Hotkey = "hotkey";
    public const string Panel = "panel";
}

/// <summary>The waits that are timed.</summary>
public static class UsageStage
{
    public const string ModelDownload = "model_download";
    public const string Translate = "translate";
    public const string FirstAudio = "first_audio";
}

/// <summary>How a timed wait ended.</summary>
public static class UsageOutcome
{
    public const string Done = "done";
    public const string Cancelled = "cancelled";
    public const string Failed = "failed";
}
