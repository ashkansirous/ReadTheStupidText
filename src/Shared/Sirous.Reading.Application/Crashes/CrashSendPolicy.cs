namespace Sirous.Reading.Application.Crashes;

/// <summary>What to do with a pending crash report once the Worker has answered (Slice 227). Pure, so the status table is tested.</summary>
public static class CrashSendPolicy
{
    /// <summary>
    /// True when the pending file is to be deleted: the Worker kept the report (2xx) or says the payload itself is bad and will never be accepted (400, 413, 422).
    /// Every other status keeps the report: 404 and 405 mean the Worker is not deployed yet (an app may ship before its Worker), 408, 429 and 5xx are temporary,
    /// and anything else (a redirect, 401, 403) is not a verdict on the report. A network failure (no status) keeps it too.
    /// </summary>
    public static bool DeleteAfter(int? statusCode) => statusCode is { } code && (code is >= 200 and < 300 or 400 or 413 or 422);

    /// <summary>
    /// True when a user report that failed to send is to be kept for a retry (Slice 300): only while the person's usage switch is on and the Worker did not
    /// give a final verdict (<see cref="DeleteAfter"/>). With the switch off the next start wipes the queue, so a copy saved now would be a false "kept".
    /// </summary>
    public static bool KeepFailedUserReport(int? statusCode, bool mayReport) => mayReport && !DeleteAfter(statusCode);

    /// <summary>Saves a failed user report when <see cref="KeepFailedUserReport"/> says so; true when a copy was saved.</summary>
    public static bool TryKeepFailedUserReport(PendingCrashStore? store, CrashReport report, int? statusCode, bool usageSwitchOn, DateTimeOffset now)
        => KeepFailedUserReport(statusCode, usageSwitchOn) && store?.Save(report, now) == true;
}
