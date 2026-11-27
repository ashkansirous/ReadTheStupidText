using Foundation;
using MetricKit;
using Sirous.Reading.Application.Crashes;

namespace Sirous.Reading.Mobile.Shared.Crashes;

/// <summary>
/// iOS's side of crash reporting (Slice 227): MetricKit, which hands the app, on a later launch, the system's own crash and hang diagnostics. There is no
/// <c>Runtime.MarshalManagedException</c> hook: it also fires for exceptions that something upstream catches, so it would report handled errors.
/// An unhandled managed exception is caught by <c>AppDomain.UnhandledException</c>, and a crash that hook misses by the run marker and MetricKit.
/// </summary>
internal static class IosCrashHooks
{
    private const int MaxJsonChars = 16 * 1024;
    private const string NoValue = "unknown";
    private const string ExitCountsKey = "crash.exit.metrics";
    private const string ExitCountsEndKey = "crash.exit.metrics.end";
    private static readonly TimeSpan MaxCountsAge = TimeSpan.FromDays(2); // MetricKit's daily payload: older counts are not about the launch that just died
    private static IosMetricSubscriber? subscriber;

    /// <summary>Subscribes to MetricKit; each crash and hang diagnostic is handed to <paramref name="deliver"/> on a background queue. MetricKit sends them at the next launch, up to a day after the crash.</summary>
    public static void StartMetricKit(Action<NativeCrash> deliver)
    {
        try
        {
            subscriber = new IosMetricSubscriber(deliver);
            MXMetricManager.SharedManager.Add(subscriber);
        }
        catch (Exception ex)
        {
            // no-fallback: crash-hook code; without MetricKit no native crash report is sent
            System.Diagnostics.Debug.WriteLine($"MetricKit could not start: {ex.Message}");
        }
    }

    /// <summary>Slice 424 item 3: the abnormal-exit counts of MetricKit's last daily period as report text (see <see cref="CrashReportBuilder.ExitCountsText"/>), or null when none arrived yet or none were abnormal.</summary>
    public static string? LastExitCounts()
    {
        string text = Preferences.Default.Get(ExitCountsKey, string.Empty);
        long endTicks = Preferences.Default.Get(ExitCountsEndKey, 0L);
        bool recent = endTicks > 0 && DateTime.UtcNow - new DateTime(endTicks, DateTimeKind.Utc) <= MaxCountsAge;
        return text.Length == 0 || !recent ? null : text; // counts of an old period would read as this death's cause
    }

    /// <summary>Slice 424: forgets the stored exit counts; called when crash reporting is turned off, so old counts never reach a later report.</summary>
    public static void ForgetExitCounts() => RememberExitCounts(null, DateTime.MinValue);

    internal static void RememberExitCounts(string? text, DateTime periodEnd)
    {
        if (text is null)
        {
            Preferences.Default.Remove(ExitCountsKey);
            Preferences.Default.Remove(ExitCountsEndKey);
            return;
        }

        Preferences.Default.Set(ExitCountsKey, text);
        Preferences.Default.Set(ExitCountsEndKey, periodEnd.ToUniversalTime().Ticks);
    }

    internal static string Cap(string? json) => json is null ? string.Empty : json.Length <= MaxJsonChars ? json : json[..MaxJsonChars];

    internal static string Describe(NSNumber? value) => value?.ToString() ?? NoValue;
}

/// <summary>The MetricKit subscriber (Slice 227): converts each <c>MXCrashDiagnostic</c> and <c>MXHangDiagnostic</c> to a <see cref="NativeCrash"/>.</summary>
internal sealed class IosMetricSubscriber(Action<NativeCrash> deliver) : NSObject, IMXMetricManagerSubscriber
{
    private const string CrashType = "MXCrashDiagnostic";
    private const string HangType = "MXHangDiagnostic";

    [Export("didReceiveDiagnosticPayloads:")]
    public void DidReceiveDiagnosticPayloads(MXDiagnosticPayload[] payloads)
    {
        try
        {
            foreach (MXDiagnosticPayload payload in payloads)
            {
                DeliverCrashes(payload);
                DeliverHangs(payload);
            }
        }
        catch (Exception ex)
        {
            // no-fallback: crash-hook code; an unreadable payload is dropped
            System.Diagnostics.Debug.WriteLine($"MetricKit payload unreadable: {ex.Message}");
        }
    }

    // Slice 424 item 3: MetricKit's daily payload carries how the app's launches ended in that period (foreground and background: memory limit, watchdog, bad access, ...). It is a period
    // count, not one launch's reason, so it is only kept as text for the next unclean exit report to quote.
    [Export("didReceiveMetricPayloads:")]
    public void DidReceiveMetricPayloads(MXMetricPayload[] payloads)
    {
        try
        {
            if (!CrashReporter.MayKeepLaunchFacts())
            {
                return; // reporting is off: nothing is saved (a daily payload must not bring the counts back after DiscardAll)
            }

            MXMetricPayload? newest = payloads.OrderBy(EndOf).LastOrDefault();
            if (newest?.ApplicationExitMetrics is { } exits)
            {
                IosCrashHooks.RememberExitCounts(CrashReportBuilder.ExitCountsText(CountsOf(exits)), EndOf(newest));
            }
        }
        catch (Exception ex)
        {
            // no-fallback: crash-hook code; unreadable exit metrics only mean the report has no exit counts
            System.Diagnostics.Debug.WriteLine($"MetricKit exit metrics unreadable: {ex.Message}");
        }
    }

    // A payload with no end date sorts first and counts as not recent; the conversion never touches a null NSDate.
    private static DateTime EndOf(MXMetricPayload payload) => payload.TimeStampEnd is { } end ? (DateTime)end : DateTime.MinValue;

    // Short names: the report message is cut at 200 characters and the counts come last.
    private const string ForeMemory = "fg memory";
    private const string ForeWatchdog = "fg watchdog";
    private const string ForeBadAccess = "fg bad access";
    private const string ForeAbnormal = "fg abnormal";
    private const string BackMemory = "bg memory";
    private const string BackPressure = "bg pressure";
    private const string BackWatchdog = "bg watchdog";
    private const string BackCpu = "bg cpu";
    private const string BackAbnormal = "bg abnormal";

    private static IEnumerable<(string Name, long Count)> CountsOf(MXAppExitMetric exits)
    {
        MXForegroundExitData? fore = exits.ForegroundExitData;
        MXBackgroundExitData? back = exits.BackgroundExitData;
        yield return (ForeMemory, Count(fore?.CumulativeMemoryResourceLimitExitCount));
        yield return (ForeWatchdog, Count(fore?.CumulativeAppWatchdogExitCount));
        yield return (ForeBadAccess, Count(fore?.CumulativeBadAccessExitCount));
        yield return (ForeAbnormal, Count(fore?.CumulativeAbnormalExitCount));
        yield return (BackMemory, Count(back?.CumulativeMemoryResourceLimitExitCount));
        yield return (BackPressure, Count(back?.CumulativeMemoryPressureExitCount));
        yield return (BackWatchdog, Count(back?.CumulativeAppWatchdogExitCount));
        yield return (BackCpu, Count(back?.CumulativeCpuResourceLimitExitCount));
        yield return (BackAbnormal, Count(back?.CumulativeAbnormalExitCount));
    }

    private static long Count(nuint? value) => value is { } count ? (long)count : 0;

    private void DeliverCrashes(MXDiagnosticPayload payload)
    {
        foreach (MXCrashDiagnostic crash in payload.CrashDiagnostics ?? [])
        {
            string type = $"{CrashType} exception={IosCrashHooks.Describe(crash.ExceptionType)} signal={IosCrashHooks.Describe(crash.Signal)}";
            string message = $"code={IosCrashHooks.Describe(crash.ExceptionCode)} {crash.TerminationReason}";
            string? tree = crash.CallStackTree?.JsonRepresentation?.ToString();
            deliver(new NativeCrash("native", type, message, IosCrashHooks.Cap(tree), TimeOf(payload), StartOf(payload), LaunchOf(crash, tree)));
        }
    }

    private void DeliverHangs(MXDiagnosticPayload payload)
    {
        foreach (MXHangDiagnostic hang in payload.HangDiagnostics ?? [])
        {
            string message = $"duration {hang.HangDuration}";
            string? tree = hang.CallStackTree?.JsonRepresentation?.ToString();
            deliver(new NativeCrash("hang", HangType, message, IosCrashHooks.Cap(tree), TimeOf(payload), null, LaunchOf(hang, tree)));
        }
    }

    // Slice 243 finding 3: the build the diagnostic is about comes from the diagnostic itself (MXDiagnostic.applicationVersion, MXMetaData.applicationBuildVersion, and the UUID of this
    // app's binary in the call-stack tree), never from the build installed now; a field the payload lacks is null.
    private static LaunchFacts LaunchOf(MXDiagnostic diagnostic, string? callStackTree) =>
        new(NullIfEmpty(diagnostic.ApplicationVersion), NullIfEmpty(diagnostic.MetaData?.ApplicationBuildVersion), MetricKitBinaries.UuidOf(callStackTree, IosRuntimeFacts.ExecutableName()));

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static DateTimeOffset TimeOf(MXDiagnosticPayload payload) =>
        payload.TimeStampEnd is { } end ? new DateTimeOffset((DateTime)end, TimeSpan.Zero) : DateTimeOffset.UtcNow;

    private static DateTimeOffset? StartOf(MXDiagnosticPayload payload) =>
        payload.TimeStampBegin is { } begin ? new DateTimeOffset((DateTime)begin, TimeSpan.Zero) : null;
}
