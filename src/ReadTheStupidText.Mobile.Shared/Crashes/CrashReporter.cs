using System.Diagnostics;
using ReadTheStupidText.Application.Crashes;
using ReadTheStupidText.Application.Usage;
using ReadTheStupidText.Mobile.Shared.Usage;

namespace ReadTheStupidText.Mobile.Shared.Crashes;

/// <summary>
/// Crash and error reports from the phone apps (Slice 227), built once for Dino Reader and Read The Stupid Text, Android and iOS. <see cref="Start"/> runs first
/// thing at start-up: the rolling log, the last-chance exception hook (it writes one small pending file and lets the crash continue), the run marker
/// check and the system's own crash history. <see cref="BeginSending"/> then posts what is pending, after the first screen. Nothing here may throw into
/// the app, and the hook does no network and takes no lock. Reports follow the person's anonymous-usage switch and the remote config's <c>crashReports.enabled</c>;
/// while the switch is off or the build may not send, the rolling log, the run marker and the pending reports are not kept at all.
/// One crash gives one report: a system report that repeats a managed one (Android's native abort, iOS MetricKit, the run marker) is dropped.
/// </summary>
public static class CrashReporter
{
    private const string RootFolder = "crashes";
    private const string PendingFolder = "pending";
    private const string LogFolder = "log";
    private const string LedgerName = "reported.txt";
    private const string ExitSeenKey = "crash.exit.lastSeen";
    private const string LogName = "crash reports";
    private const string UncleanType = "unclean_exit";
    private static readonly TimeSpan GateRecheck = TimeSpan.FromSeconds(5);

    private static readonly object StartGate = new();
    private static PendingCrashStore? store;
    private static ReportedCrashLedger? ledger;
    private static CrashContext? context;
    private static string? userSwitchKey;
    private static Exception? lastReported;
    private static bool started;
    private static Task<string> previousRunTail = Task.FromResult(string.Empty);
    private static Task recorded = Task.CompletedTask;

    // What the crash handler and the log read instead of the preferences: refreshed at start, on activation and at most every few seconds by the log.
    private static volatile bool reportingAllowed;
    private static long gateCheckedAt;

    /// <summary>
    /// Starts reporting for <paramref name="app"/> (a <see cref="CrashApp"/> value); <paramref name="usageSwitchKey"/> is the preference key of the person's
    /// anonymous-usage switch (on by default). Call once, first thing in <c>CreateMauiApp</c>. Never throws.
    /// </summary>
    public static void Start(string app, string usageSwitchKey)
    {
        try
        {
            if (DeviceInfo.Platform != DevicePlatform.Android && DeviceInfo.Platform != DevicePlatform.iOS)
            {
                return; // Slice 227 covers the phone apps; the Windows head reports nothing
            }

            lock (StartGate)
            {
                if (started)
                {
                    return;
                }

                started = true;
            }

#if DEBUG
            Note("start");
#endif
            userSwitchKey = usageSwitchKey;
            string root = Path.Combine(FileSystem.AppDataDirectory, RootFolder);
            CrashLog.Use(new RollingLogFile(Path.Combine(root, LogFolder)), LogMayBeWritten);
            store = new PendingCrashStore(Path.Combine(root, PendingFolder));
            ledger = new ReportedCrashLedger(Path.Combine(root, LedgerName));
            context = ReadContext(app);
            StartReporting();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Crash reporting could not start: {ex.Message}");
        }
    }

    private static void StartReporting()
    {
        // The hooks are always installed: they write only while reportingAllowed, so a closed gate keeps them silent, and a gate that opens later in the run
        // (the Debug stub set by the scripted check, see GateChanged) needs them already in place.
        InstallHandlers();
        if (!BuildEligible())
        {
            DiscardAll();
            return; // a build that may not send keeps nothing
        }

        if (!RefreshAllowed())
        {
            DiscardAll(); // the switch is off: nothing from before is kept either
        }

        RecordWhatTheLastRunLeft();
    }

    /// <summary>Posts the pending reports a little after start-up, once the remote config says so (Slice 227). Never throws.</summary>
    public static void BeginSending(CrashSendOptions options)
    {
        try
        {
            if (store is not { } pending || !BuildEligible())
            {
                return;
            }

            var sender = new CrashSender(pending, options, SwitchOn, MayReport, DiscardAll, () => previousRunTail, Note);
            _ = Task.Run(async () =>
            {
                await recorded.ConfigureAwait(false); // what the last run left is on disk before anything is posted
                await sender.RunAfterStartAsync().ConfigureAwait(false);
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Crash reporting could not begin sending: {ex.Message}");
        }
    }

    /// <summary>Marks the app as running while a window of it is in front, and clears the mark when it goes to the background (the unclean-exit check). Never throws.</summary>
    public static void AttachTo(Window window)
    {
        window.Activated += (_, _) => SetRunning(true);
        window.Stopped += (_, _) => SetRunning(false);
    }

    /// <summary>Re-reads the gate now (Debug: the scripted check has just set or cleared the stub mid-run). Never throws.</summary>
    internal static void GateChanged()
    {
        try
        {
            bool allowed = RefreshAllowed();
#if DEBUG
            Note($"gate re-read, allowed {allowed}, switch {SwitchOn()}, build {BuildEligible()}");
#else
            _ = allowed;
#endif
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Crash reporting could not re-read the gate: {ex.Message}");
        }
    }

    private static void SetRunning(bool running)
    {
        try
        {
            if (running && RefreshAllowed())
            {
                store?.MarkRunning();
            }
            else
            {
                store?.MarkNotRunning();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Crash reporting could not update the run marker: {ex.Message}");
        }
    }

    private static CrashContext? ReadContext(string app)
    {
        try
        {
            return new CrashContext(app, UsageEnvironment.Platform, UsageEnvironment.InstalledVersion, CrashGate.Channel, DeviceProbe.Current);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Crash reporting could not read the build facts: {ex.Message}");
            return null;
        }
    }

    private static void InstallHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => OnUnhandled(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => LogUnobserved(e.Exception);
        // Only AppDomain.UnhandledException: it fires when nothing handled the exception. Android's AndroidEnvironment.UnhandledExceptionRaiser also fires for
        // exceptions a handler marks handled afterwards, which would write a report and clear the run marker for an app that is still running (review pass 2).
    }

    // An unobserved task exception goes into the log tail as an Error line; the handler runs on the finalizer thread, so it must never throw.
    private static void LogUnobserved(Exception error)
    {
        try
        {
            RollingFileSystemLog.ToCurrent().Error("unobserved task exception", null, error);
        }
        catch (Exception)
        {
            // Losing one log line is better than an exception on the finalizer thread.
        }
    }

    // The last-chance hook: build one small report (type, message, stack, time, kind; the log tail is added by the sender at the next launch from the log file on disk)
    // and write it as one pending file. No log write, no flush, no lock and no preference read, so it cannot wait on another thread; the crash goes on.
    private static void OnUnhandled(Exception? error)
    {
        try
        {
#if DEBUG
            Note($"unhandled hook entered (error {error is not null}, store {store is not null}, context {context is not null}, allowed {reportingAllowed})");
#endif
            if (error is null || ReferenceEquals(error, lastReported) || store is null || context is null || !reportingAllowed)
            {
                return;
            }

            lastReported = error;
            Save(CrashReportBuilder.FromException(ContextNow(context), CrashKind.Managed, error, string.Empty, DateTimeOffset.UtcNow));
            store.MarkNotRunning();
        }
        catch (Exception hookFailure)
        {
            // A hook that throws would hide the real crash.
#if DEBUG
            Note($"unhandled hook failed: {hookFailure.GetType().Name}: {hookFailure.Message}");
#else
            _ = hookFailure;
#endif
        }
    }

    // At start: the previous run's system-described crashes (skipping any a managed report already covers), then an unclean exit if it left its run marker
    // and nothing else explains it. The exit-reason watermark moves on even while reporting is off, so nothing from that time is reported later.
    private static void RecordWhatTheLastRunLeft()
    {
        if (store is null || context is null)
        {
            return;
        }

        DateTimeOffset? frontSince = store.TakeUncleanExit();
        IReadOnlyList<NativeCrash> history = ReadHistoryAndAdvance();
        if (!reportingAllowed)
        {
            CrashPlatform.StartNativeDelivery(OnNativeDelivered); // consumes MetricKit's payloads; OnNativeDelivered drops them while reporting is off
            return;
        }

        IReadOnlyList<CrashReport> pending = store.ReadAll().Select(item => item.Report).ToList();
        bool needsTail = pending.Count > 0 || frontSince is not null || history.Count > 0;

        // The file extents are taken here, before this run logs anything; the end is read on a background thread so the UI thread never waits on the disk.
        previousRunTail = needsTail ? CrashLog.Current?.TailInBackground() ?? Task.FromResult(string.Empty) : Task.FromResult(string.Empty);
        Task<string> tailTask = previousRunTail;
        recorded = Task.Run(() => FinishRecordingAsync(tailTask, history, pending, frontSince));
    }

    // The part of the start-up check that needs the previous run's log tail.
    private static async Task FinishRecordingAsync(Task<string> tailTask, IReadOnlyList<NativeCrash> history, IReadOnlyList<CrashReport> pending, DateTimeOffset? frontSince)
    {
        try
        {
            string tail = await tailTask.ConfigureAwait(false);
            SaveHistory(history, pending, tail);
            bool explained = history.Count > 0 || (frontSince is { } since && CrashDedupe.ManagedReportSince(pending, since));
            if (frontSince is not null && !explained && context is not null)
            {
                Save(CrashReportBuilder.FromDescription(ContextNow(context), CrashKind.UncleanExit, UncleanType, "The run marker was left behind", string.Empty, tail, DateTimeOffset.UtcNow));
            }

            RememberReportedCrashes();
            CrashPlatform.StartNativeDelivery(OnNativeDelivered);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Crash reporting could not record the last run: {ex.Message}");
        }
    }

    private static void SaveHistory(IReadOnlyList<NativeCrash> history, IReadOnlyList<CrashReport> pending, string tail)
    {
        foreach (NativeCrash crash in history)
        {
            if (crash.Kind == CrashKind.Native && CrashDedupe.ManagedReportCovers(pending, crash.Time))
            {
                Note("native exit repeats a managed report, skipped");
                continue;
            }

            Save(FromNative(crash, tail));
        }
    }

    // The exit reasons since the last one seen; with none seen yet (this build's first launch) nothing from before is sent. Always moves the watermark on.
    private static IReadOnlyList<NativeCrash> ReadHistoryAndAdvance()
    {
        IReadOnlyList<NativeCrash> history = CrashPlatform.ReadHistory(ReadExitSeen());
        WriteExitSeen(history.Count > 0 ? history.Max(c => c.Time) : DateTimeOffset.UtcNow);
        return history;
    }

    // iOS: the managed and unclean-exit reports on the phone are noted, so MetricKit's later diagnostic for the same crash is dropped (one crash, one report).
    private static void RememberReportedCrashes()
    {
        foreach ((string path, CrashReport report) in store?.ReadAll() ?? [])
        {
            if (report.Kind is CrashKind.Managed or CrashKind.UncleanExit)
            {
                ledger?.Record(Path.GetFileName(path), CrashDedupe.TimeOf(report) ?? DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            }
        }
    }

    // MetricKit's diagnostic, up to a day after the crash, on a background thread.
    private static void OnNativeDelivered(NativeCrash crash)
    {
        try
        {
            if (!reportingAllowed)
            {
                return;
            }

            if (crash.Kind == CrashKind.Native && crash.PeriodStart is { } begin && ledger?.TryClaim(begin, crash.Time, DateTimeOffset.UtcNow) == true)
            {
                Note("native diagnostic repeats a report already made, dropped");
                return;
            }

            Save(FromNative(crash, string.Empty));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Crash reporting could not take a native diagnostic: {ex.Message}");
        }
    }

    private static CrashReport? FromNative(NativeCrash crash, string tail) =>
        context is null ? null : CrashReportBuilder.FromDescription(ContextNow(context), crash.Kind, crash.Type, crash.Message, crash.Stack, tail, crash.Time);

    // The build facts a report is made with. A Debug build is a dev install, which sends nothing; with the scripted check's stub set it reports as a store
    // install so the whole path (handler, file, sender, Worker checks) runs. Release builds use the facts as read.
    private static CrashContext ContextNow(CrashContext facts)
    {
#if DEBUG
        if (CrashDebug.StubAddress is not null && facts.Channel == InstallChannel.Dev)
        {
            return facts with { Channel = InstallChannel.Store };
        }
#endif
        return facts;
    }

    private static void Save(CrashReport? report)
    {
        if (report is null || store is null)
        {
            Note(report is null ? "report not made (this install's channel sends nothing)" : "report not saved (no store)");
            return;
        }

        bool kept = store.Save(report, DateTimeOffset.UtcNow);
        Note($"{report.Kind} report {(kept ? "saved" : "could not be saved")}");
    }

    private static DateTimeOffset ReadExitSeen()
    {
        long ticks = Preferences.Default.Get(ExitSeenKey, 0L);
        return ticks == 0 ? DateTimeOffset.UtcNow : new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    private static void WriteExitSeen(DateTimeOffset time) => Preferences.Default.Set(ExitSeenKey, time.UtcTicks);

    // The person's anonymous-usage switch (on by default); an unreadable preference counts as on, the Worker and the remote config still decide.
    private static bool SwitchOn()
    {
        try
        {
            return userSwitchKey is null || Preferences.Default.Get(userSwitchKey, true);
        }
        catch (Exception)
        {
            return true;
        }
    }

    // Whether this build and install may send at all (Debug builds only with the scripted check's stub).
    private static bool BuildEligible()
    {
#if DEBUG
        if (CrashDebug.StubAddress is not null)
        {
            return true;
        }
#endif
        return CrashGate.Reason == UsageSkipReason.None;
    }

    // Reports may be kept on the phone only while the switch is on and the build and install may send.
    private static bool MayReport() => SwitchOn() && BuildEligible();

    // The log's gate: the cached answer, looked at again every few seconds, so a switch turned off is noticed without reading a preference per log line.
    private static bool LogMayBeWritten() =>
        Environment.TickCount64 - Interlocked.Read(ref gateCheckedAt) < GateRecheck.TotalMilliseconds ? reportingAllowed : RefreshAllowed();

    // Re-reads the gate; when it has just closed, everything kept on the phone is deleted.
    private static bool RefreshAllowed()
    {
        bool allowed = MayReport();
        bool was = reportingAllowed;
        reportingAllowed = allowed;
        Interlocked.Exchange(ref gateCheckedAt, Environment.TickCount64);
        if (was && !allowed)
        {
            DiscardAll();
        }

        return allowed;
    }

    /// <summary>Deletes everything crash reporting keeps on the phone: the pending reports, the rolling log, the run marker and the list of reported crashes.</summary>
    private static void DiscardAll()
    {
        try
        {
            store?.Clear();
            store?.MarkNotRunning();
            CrashLog.Current?.DeleteFiles();
            ledger?.Clear();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Crash reporting could not clear its files: {ex.Message}");
        }
    }

    // One line per branch, for a device check to read: Debug builds only (a marker line for the scripted flows too).
    private static void Note(string message)
    {
        try
        {
            Debug.WriteLine($"{LogName}: {message}");
#if DEBUG
            Testing.TestHookLog.Write($"CRASH_REPORT {message}");
#endif
        }
        catch (Exception)
        {
            // A note is a trace for a device check; it must never throw out of the crash handler.
        }
    }
}
