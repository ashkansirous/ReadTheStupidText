using ReadTheStupidText.Application.Usage;

namespace ReadTheStupidText.Application.Crashes;

/// <summary>The closed list of apps a report can come from (the Worker's list must match).</summary>
public static class CrashApp
{
    public const string Dino = "dino";
    public const string Rtst = "rtst";
}

/// <summary>The closed list of report kinds (the Worker's list must match).</summary>
public static class CrashKind
{
    /// <summary>An unhandled managed exception, written by the crash handler.</summary>
    public const string Managed = "managed";

    /// <summary>A native crash seen by the system: iOS MetricKit <c>MXCrashDiagnostic</c>, Android <c>REASON_CRASH_NATIVE</c>.</summary>
    public const string Native = "native";

    /// <summary>The run marker was still there at the next launch and no crash report was written (a native crash, a memory kill, the battery).</summary>
    public const string UncleanExit = "unclean_exit";

    /// <summary>Android <c>REASON_ANR</c>.</summary>
    public const string Anr = "anr";

    /// <summary>Android <c>REASON_LOW_MEMORY</c>.</summary>
    public const string LowMemory = "low_memory";

    /// <summary>iOS MetricKit hang diagnostic.</summary>
    public const string Hang = "hang";

    /// <summary>A report a grown-up sent on purpose from "Report a problem" (Slice 249): the log tail and the build facts, no exception. Its message is <c>user report &lt;code&gt;</c> (the code is the reference to quote).</summary>
    public const string UserReport = "user_report";
}

/// <summary>
/// One crash or error report (Slice 227): what the phone posts to <c>POST /api/crash</c> and the Worker keeps in R2. No identifier of any kind
/// (the Worker names the stored object itself); the device is the same coarse buckets as the usage counts (Slice 166).
/// </summary>
/// <param name="App">A <see cref="CrashApp"/> value.</param>
/// <param name="Platform">"android" or "ios".</param>
/// <param name="Version">The app version (a stamped release).</param>
/// <param name="Channel">"store" or "testflight" (<see cref="InstallChannels.NameOf"/>).</param>
/// <param name="Kind">A <see cref="CrashKind"/> value.</param>
/// <param name="ExceptionType">The exception's type name, or the signal or reason for a native report; empty when unknown.</param>
/// <param name="Message">The exception message, capped at <see cref="CrashReportLimits.MessageBytes"/>; never user text on purpose (see <see cref="LogLineFormatter"/>).</param>
/// <param name="Stack">The stack, capped at <see cref="CrashReportLimits.StackBytes"/>.</param>
/// <param name="LogTail">The last lines of the rolling log, capped at <see cref="CrashReportLimits.LogTailBytes"/>.</param>
/// <param name="Minute">The UTC time of the crash to the minute, "yyyy-MM-ddTHH:mmZ".</param>
/// <param name="Device">The coarse device buckets, or null when they could not be read.</param>
/// <param name="Session">Slice 250: Dino's usage session id (32 hex digits, random per launch, in memory and on disk only inside unsent queued counts) of the launch the report was made in, so its usage rows can be found; null for Read The Stupid Text, and for a report about an earlier launch (native, unclean exit).</param>
public sealed record CrashReport(
    string App, string Platform, string Version, string Channel, string Kind,
    string ExceptionType, string Message, string Stack, string LogTail, string Minute, DeviceContext? Device = null, string? Session = null);

/// <summary>The size caps of a report, in UTF-8 bytes (the Worker caps the same).</summary>
public static class CrashReportLimits
{
    public const int MessageBytes = 1024;
    public const int StackBytes = 16 * 1024;
    public const int LogTailBytes = 32 * 1024;
    public const int ExceptionTypeBytes = 200;

    /// <summary>The Worker refuses a larger request body.</summary>
    public const int BodyBytes = 64 * 1024;
}
