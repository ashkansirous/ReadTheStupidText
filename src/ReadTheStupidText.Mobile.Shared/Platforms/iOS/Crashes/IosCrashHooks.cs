using Foundation;
using MetricKit;

namespace ReadTheStupidText.Mobile.Shared.Crashes;

/// <summary>
/// iOS's side of crash reporting (Slice 227): MetricKit, which hands the app, on a later launch, the system's own crash and hang diagnostics. There is no
/// <c>Runtime.MarshalManagedException</c> hook: it also fires for exceptions that something upstream catches, so it would report handled errors.
/// An unhandled managed exception is caught by <c>AppDomain.UnhandledException</c>, and a crash that hook misses by the run marker and MetricKit.
/// </summary>
internal static class IosCrashHooks
{
    private const int MaxJsonChars = 16 * 1024;
    private const string NoValue = "unknown";
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
            System.Diagnostics.Debug.WriteLine($"MetricKit could not start: {ex.Message}");
        }
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
            System.Diagnostics.Debug.WriteLine($"MetricKit payload unreadable: {ex.Message}");
        }
    }

    private void DeliverCrashes(MXDiagnosticPayload payload)
    {
        foreach (MXCrashDiagnostic crash in payload.CrashDiagnostics ?? [])
        {
            string type = $"{CrashType} exception={IosCrashHooks.Describe(crash.ExceptionType)} signal={IosCrashHooks.Describe(crash.Signal)}";
            string message = $"code={IosCrashHooks.Describe(crash.ExceptionCode)} {crash.TerminationReason}";
            deliver(new NativeCrash("native", type, message, IosCrashHooks.Cap(crash.CallStackTree?.JsonRepresentation?.ToString()), TimeOf(payload), StartOf(payload)));
        }
    }

    private void DeliverHangs(MXDiagnosticPayload payload)
    {
        foreach (MXHangDiagnostic hang in payload.HangDiagnostics ?? [])
        {
            string message = $"duration {hang.HangDuration}";
            deliver(new NativeCrash("hang", HangType, message, IosCrashHooks.Cap(hang.CallStackTree?.JsonRepresentation?.ToString()), TimeOf(payload)));
        }
    }

    private static DateTimeOffset TimeOf(MXDiagnosticPayload payload) =>
        payload.TimeStampEnd is { } end ? new DateTimeOffset((DateTime)end, TimeSpan.Zero) : DateTimeOffset.UtcNow;

    private static DateTimeOffset? StartOf(MXDiagnosticPayload payload) =>
        payload.TimeStampBegin is { } begin ? new DateTimeOffset((DateTime)begin, TimeSpan.Zero) : null;
}
