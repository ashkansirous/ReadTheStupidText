using System.Text;
using ReadTheStupidText.Application.Usage;

namespace ReadTheStupidText.Application.Crashes;

/// <summary>What every report of one run says about the build (Slice 227).</summary>
/// <param name="App">A <see cref="CrashApp"/> value.</param>
/// <param name="Platform">"android" or "ios".</param>
/// <param name="Version">The app version.</param>
/// <param name="Channel">The install channel; a <see cref="InstallChannel.Dev"/> install builds no report.</param>
/// <param name="Device">The coarse device buckets, or null.</param>
public sealed record CrashContext(string App, string Platform, string Version, InstallChannel Channel, DeviceContext? Device);

/// <summary>Builds <see cref="CrashReport"/>s from an exception or from a native description (Slice 227). Pure.</summary>
public static class CrashReportBuilder
{
    private const string UnknownType = "unknown";
    private const int MaxInnerExceptions = 3;
    private const string InnerHeader = "--- inner exception: ";

    /// <summary>A report for a managed exception; null when the install channel sends nothing. The message has its quoted text and file paths scrubbed and is cut to 200 characters; the stack is the exception's and its inner exceptions'.</summary>
    public static CrashReport? FromException(CrashContext context, string kind, Exception error, string logTail, DateTimeOffset now) =>
        Build(context, kind, error.GetType().FullName ?? UnknownType, LogLineFormatter.ScrubMessage(error.Message), StackOf(error), logTail, now);

    /// <summary>A report for a crash the system described (a signal, an exit reason, a MetricKit diagnostic); null when the install channel sends nothing.</summary>
    public static CrashReport? FromDescription(CrashContext context, string kind, string type, string message, string stack, string logTail, DateTimeOffset now) =>
        Build(context, kind, type, LogLineFormatter.ScrubMessage(message), stack, logTail, now);

    private static CrashReport? Build(CrashContext context, string kind, string type, string message, string stack, string logTail, DateTimeOffset now)
    {
        string? channel = InstallChannels.NameOf(context.Channel);
        if (channel is null)
        {
            return null;
        }

        return CrashReportJson.Capped(new CrashReport(
            context.App, context.Platform, context.Version, channel, kind, type, message, stack, logTail, CrashReportJson.MinuteOf(now), context.Device));
    }

    /// <summary>The stack trace of <paramref name="error"/>, then each inner exception's type and stack, so a wrapped failure keeps its origin.</summary>
    public static string StackOf(Exception error)
    {
        var text = new StringBuilder(error.StackTrace ?? string.Empty);
        Exception? inner = error.InnerException;
        for (int depth = 0; inner is not null && depth < MaxInnerExceptions; depth++, inner = inner.InnerException)
        {
            text.AppendLine().Append(InnerHeader).Append(inner.GetType().FullName).AppendLine().Append(inner.StackTrace);
        }

        return text.ToString();
    }
}
