namespace ReadTheStupidText.Mobile.Shared.Crashes;

/// <summary>
/// The wording of the rolling log's trace lines (Slice 243 item 5, built in Slice 249): what screen was shown or left, Back, reading started or stopped and why, the app going to
/// the background, memory warnings, the synthesis queue. One place so Android and iOS, Dino Reader and Read The Stupid Text write the same words, and a test can pin them.
/// Pure: a line carries only a kind, a type name, a number or a fixed reason, never typed or read text, a book title or a file name (the log's tail goes out with every report).
/// </summary>
public static class TraceLines
{
    public const string BackPressed = "back pressed";
    public const string AppBackground = "app background";
    public const string AppForeground = "app foreground";

    /// <summary>The verbs of a reading line.</summary>
    public static class ReadingEvent
    {
        public const string Started = "started";
        public const string Paused = "paused";
        public const string Resumed = "resumed";
        public const string Stopped = "stopped";
        public const string Detached = "detached";
        public const string Stalled = "stalled";
        public const string Unstalled = "unstalled";
    }

    /// <summary>The fixed reasons a reading line may give (never free text).</summary>
    public static class ReadingReason
    {
        public const string User = "user";
        public const string PageLeft = "page left";
        public const string Superseded = "superseded";
        public const string Finished = "finished";
        public const string AppBackground = "app background";
        public const string Failed = "failed";
        public const string Skipped = "skipped";
        public const string LanguageChange = "language change";
        public const string Other = "other";
    }

    private static string Clean(string typeName) => string.IsNullOrWhiteSpace(typeName) ? "unknown" : typeName.Trim();

    public static string PageShown(string pageTypeName) => $"page shown {Clean(pageTypeName)}";

    public static string PageLeft(string pageTypeName) => $"page left {Clean(pageTypeName)}";

    public static string PagePopped(string pageTypeName) => $"page popped {Clean(pageTypeName)}";

    public static string MemoryWarning(string level) => $"memory warning {Clean(level)}";

    public static string Reading(string verb, string? reason = null) => string.IsNullOrEmpty(reason) ? $"reading {verb}" : $"reading {verb} ({reason})";

    public static string SynthesisQueue(int length) => $"synthesis queue {Math.Max(0, length)}";
}

/// <summary>
/// Decides which changes of the synthesis queue length are worth a log line (Slice 243 item 5): a queue of one chunk goes up and down for every chunk read, which would fill the
/// 300-line tail, so only a new high of two or more and the return to empty after such a build-up are written. Thread-safe; pure, so a test pins it.
/// </summary>
public sealed class SynthesisQueueTrace
{
    private const int FirstStep = 2;
    private readonly object gate = new();
    private int high;

    /// <summary>The line to write when the queue has just grown to <paramref name="length"/>, or null.</summary>
    public string? Entered(int length)
    {
        lock (gate)
        {
            if (length <= high)
            {
                return null;
            }

            high = length;
            return length >= FirstStep ? TraceLines.SynthesisQueue(length) : null;
        }
    }

    /// <summary>The line to write when the queue has just shrunk to <paramref name="length"/>, or null.</summary>
    public string? Left(int length)
    {
        lock (gate)
        {
            if (length > 0)
            {
                return null;
            }

            bool built = high >= FirstStep;
            high = 0;
            return built ? TraceLines.SynthesisQueue(0) : null;
        }
    }
}
