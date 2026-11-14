namespace DinoReader.Core;

/// <summary>
/// Why a read stopped (Slice 250): the closed set the Worker accepts as the detail of a <c>read_stopped</c> count. The reading session takes one at every stop,
/// and maps it to its trace text and to this detail only at the boundary, so no stop carries free text.
/// </summary>
public enum ReadStopReason
{
    /// <summary>The page was left (Back, a pop): the book is no longer open.</summary>
    LeftBook,

    /// <summary>The read reached the end of the book.</summary>
    Finished,

    /// <summary>The grown-up or child cancelled it (Cancel on the wait, Play while loading).</summary>
    Cancelled,

    /// <summary>A new story language was picked, which restarts the read.</summary>
    LanguageChanged,

    /// <summary>A new read replaced the running one (read again from the top).</summary>
    Restarted,

    /// <summary>The Windows window went away or closed.</summary>
    WindowStopped,

    /// <summary>A skip while paused dropped the paused read.</summary>
    Skipped,

    /// <summary>The read failed (the voice or the engine could not make it).</summary>
    Failed,

    /// <summary>Any other internal stop (an inherited read cleared at open, a test command).</summary>
    Other,
}

/// <summary>The four playback verbs a reading session raises; each is one usage count (Slice 250).</summary>
public enum ReadingEventKind
{
    Started,
    Paused,
    Resumed,
    Stopped,
}

/// <summary>Maps the closed playback sets to the closed names the Worker accepts.</summary>
public static class ReadPlaybackNames
{
    public const string LeftBook = "left_book";
    public const string Finished = "finished";
    public const string Cancelled = "cancelled";
    public const string LanguageChanged = "language_changed";
    public const string Restarted = "restarted";
    public const string WindowStopped = "window_stopped";
    public const string Skipped = "skipped";
    public const string Failed = "failed";
    public const string Other = "other";

    /// <summary>The <see cref="UsageEvent.Detail"/> of a <c>read_stopped</c> count for <paramref name="reason"/>.</summary>
    public static string DetailOf(ReadStopReason reason) => reason switch
    {
        ReadStopReason.LeftBook => LeftBook,
        ReadStopReason.Finished => Finished,
        ReadStopReason.Cancelled => Cancelled,
        ReadStopReason.LanguageChanged => LanguageChanged,
        ReadStopReason.Restarted => Restarted,
        ReadStopReason.WindowStopped => WindowStopped,
        ReadStopReason.Skipped => Skipped,
        ReadStopReason.Failed => Failed,
        _ => Other,
    };

    /// <summary>The <see cref="UsageEventName"/> of the count for the verb <paramref name="kind"/>.</summary>
    public static string EventOf(ReadingEventKind kind) => kind switch
    {
        ReadingEventKind.Started => UsageEventName.ReadStarted,
        ReadingEventKind.Paused => UsageEventName.ReadPaused,
        ReadingEventKind.Resumed => UsageEventName.ReadResumed,
        _ => UsageEventName.ReadStopped,
    };
}
