using System.Diagnostics;
using System.Net;
using ReadTheStupidText.Application.Crashes;

namespace ReadTheStupidText.Mobile.Shared.Crashes;

/// <summary>How one "Report a problem" press ended.</summary>
public enum UserReportOutcome
{
    /// <summary>The Worker kept the report: show the code.</summary>
    Sent,

    /// <summary>It could not be sent now, and no copy is queued (this install sends nothing, the Worker refused the report, or it could not be saved): only Try again helps.</summary>
    Failed,

    /// <summary>It could not be sent now; a copy is queued on the phone for the crash queue's own retry, so it really will be sent later.</summary>
    FailedKept,

    /// <summary>The person cancelled the wait: nothing is kept.</summary>
    Cancelled,
}

/// <summary>The "Report a problem" side of crash reporting (Slice 249): the report a grown-up sends on purpose, with a code to quote.</summary>
public static partial class CrashReporter
{
    /// <summary>A fresh reference code for one report (Debug: the code a scripted check fixed with <c>report-code</c>, so a flow can look for it on screen).</summary>
    public static string NewReportCode()
    {
#if DEBUG
        if (CrashDebug.ForcedReportCode is { } forced && ReportCode.IsValid(forced))
        {
            return forced;
        }
#endif
        return ReportCode.Generate();
    }

    /// <summary>
    /// Sends one user report with <paramref name="code"/> (a <see cref="ReportCode"/>) to <paramref name="baseAddress"/> (the app's own host): the build facts and the end of the
    /// rolling log, nothing typed. This is a deliberate press, so it goes even while the usage switch is off (the log tail is then empty) and does not wait for the remote config
    /// (the Worker answers 503 while it is off, which is a failure here). A failure keeps the report in the pending queue to be sent later. Never throws.
    /// </summary>
    public static async Task<UserReportOutcome> SendUserReportAsync(string code, string baseAddress, CancellationToken cancel)
    {
        try
        {
            if (!BuildEligible())
            {
                Note($"user report not sent (this build or install sends nothing: {CrashGate.Reason}), code {code}");
                return UserReportOutcome.Failed;
            }

            string tail = await ReadTailAsync().ConfigureAwait(false);
            CrashReport? report = context is null ? null : UserReportBuilder.Build(ContextNow(context), code, tail, CreatedAtFor(code)) is { } built ? built with { Session = InThisLaunch(context).Session } : null;
            if (report is null)
            {
                Note("user report not made (this install's channel sends nothing)");
                return UserReportOutcome.Failed;
            }

            return await PostUserReportAsync(report, code, UserReportAddress(baseAddress), cancel).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // no-fallback: crash reporting must never report into itself; the Failed outcome is shown to the person
            Debug.WriteLine($"Crash reporting could not send a user report: {ex.Message}");
            return UserReportOutcome.Failed;
        }
    }

    private static readonly object CreatedGate = new();
    private static string? createdCode;
    private static DateTimeOffset createdAt;

    // A report's time is when its code was first sent: a retry of the same code carries the same minute, so the Worker (which files a user report under its creation day)
    // never stores one report twice when the first answer was lost and the retry comes after midnight.
    private static DateTimeOffset CreatedAtFor(string code)
    {
        lock (CreatedGate)
        {
            if (createdCode != code)
            {
                createdCode = code;
                createdAt = DateTimeOffset.UtcNow;
            }

            return createdAt;
        }
    }

    private static async Task<string> ReadTailAsync() =>
        CrashLog.Current is { } log ? await log.TailInBackground().ConfigureAwait(false) : string.Empty;

    private static string UserReportAddress(string baseAddress)
    {
#if DEBUG
        if (CrashDebug.StubAddress is { } stub)
        {
            return stub;
        }
#endif
        return baseAddress;
    }

    // Deletes the pending user reports that carry <paramref name="code"/>.
    private static void DropQueuedCopies(string code)
    {
        foreach ((string path, CrashReport queued) in store?.ReadAll() ?? [])
        {
            if (queued.Kind == CrashKind.UserReport && ReportCode.FromMessage(queued.Message) == code)
            {
                store?.Delete(path);
            }
        }
    }

    private static async Task<UserReportOutcome> PostUserReportAsync(CrashReport report, string code, string address, CancellationToken cancel)
    {
        HttpStatusCode? status = await CrashSender.PostBodyAsync(CrashReportJson.ToJson(report), address, cancel).ConfigureAwait(false);
        int? number = status is null ? null : (int)status.Value;
        if (number is >= 200 and < 300)
        {
            // A 2xx wins even when Cancel was pressed as it arrived: the Worker has the report, so the person gets the code.
            DropQueuedCopies(code); // an earlier failed try of this code must not be posted again later
            Note($"user report sent (status {number}), code {code}");
            return UserReportOutcome.Sent;
        }

        if (cancel.IsCancellationRequested)
        {
            Note($"user report cancelled, code {code}");
            return UserReportOutcome.Cancelled;
        }

        DropQueuedCopies(code); // one queued copy per code: a retry replaces the last one
        bool kept = !CrashSendPolicy.DeleteAfter(number) && store?.Save(report, DateTimeOffset.UtcNow) == true;
        Note($"user report failed (status {(number is null ? "none" : number)}), {(kept ? "kept for retry" : "not kept")}, code {code}");
        return kept ? UserReportOutcome.FailedKept : UserReportOutcome.Failed;
    }
}
