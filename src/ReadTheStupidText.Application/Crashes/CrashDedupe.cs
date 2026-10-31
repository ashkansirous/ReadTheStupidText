using System.Globalization;

namespace ReadTheStupidText.Application.Crashes;

/// <summary>
/// One crash must give one report (Slice 227). The same crash can be seen twice: the managed hook writes a <c>managed</c> report and the system then also
/// describes the dying process (Android's native-abort exit reason), or the run marker shows an unclean exit for a crash a hook already reported. These pure rules
/// decide, from the time a report carries (to the minute), whether a system report repeats one already made. Android matches by time only (the exit reason's
/// timestamp against the managed report's minute); the report has no process id.
/// </summary>
public static class CrashDedupe
{
    /// <summary>How far apart a managed report's minute and the system's timestamp for the same crash may be.</summary>
    public static readonly TimeSpan MatchWindow = TimeSpan.FromMinutes(2);

    private const string MinuteFormat = "yyyy-MM-dd'T'HH:mm'Z'";

    /// <summary>The time a report carries, or null when it is not in the report format.</summary>
    public static DateTimeOffset? TimeOf(CrashReport report) =>
        DateTimeOffset.TryParseExact(report.Minute, MinuteFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset time)
            ? time
            : null;

    /// <summary>True when a managed report was written at about <paramref name="exitTime"/>: the system's native abort is the managed crash already reported.</summary>
    public static bool ManagedReportCovers(IEnumerable<CrashReport> pending, DateTimeOffset exitTime) =>
        pending.Any(r => r.Kind == CrashKind.Managed && TimeOf(r) is { } time && (exitTime - time).Duration() <= MatchWindow);

    /// <summary>True when a managed report was written after the app last came to the front (<paramref name="frontSince"/>), so the run marker it left explains nothing new.</summary>
    public static bool ManagedReportSince(IEnumerable<CrashReport> pending, DateTimeOffset frontSince) =>
        pending.Any(r => r.Kind == CrashKind.Managed && TimeOf(r) is { } time && time.AddMinutes(1) >= frontSince);
}
