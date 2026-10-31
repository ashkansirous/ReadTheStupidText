#if DEBUG
using ReadTheStupidText.Mobile.Shared.Testing;

namespace ReadTheStupidText.Mobile.Shared.Crashes;

/// <summary>
/// The Debug-only stub address of the scripted crash check (Slice 227): a Debug build never reports (the gate drops it), unless a flow has set a stub
/// with <c>crash-stub url=http://10.0.2.2:8787</c>. The address survives the crash and the relaunch in a preference. Release has none of this.
/// </summary>
public static class CrashDebug
{
    private const string StubKey = "crash.debug.stub";

    /// <summary>The local stub's address, or null for no stub.</summary>
    public static string? StubAddress
    {
        get
        {
            string stub = Preferences.Default.Get(StubKey, string.Empty);
            return stub.Length == 0 ? null : stub;
        }
    }

    internal static void SetStub(string? address)
    {
        if (string.IsNullOrEmpty(address))
        {
            Preferences.Default.Remove(StubKey);
            CrashReporter.GateChanged();
            return;
        }

        Preferences.Default.Set(StubKey, address);
        CrashReporter.GateChanged(); // the crash hook reads a cached gate: without this the stub set mid-run would not open it until the next launch
    }
}

/// <summary>
/// The Debug-only crash test commands (Slice 227), chained onto the dispatcher's app commands:
/// <c>crash-stub url=&lt;base&gt;</c> sets (or with no url clears) the local stub the reports go to;
/// <c>crash-test kind=managed</c> throws an unhandled exception on a pool thread (the process dies and the report is sent at the next launch);
/// <c>crash-test kind=kill</c> kills the process while it is in front (the next launch reports <c>unclean_exit</c>).
/// Outcome lines: <c>HOOK crash-stub ok</c>, <c>HOOK crash-test started</c>.
/// </summary>
public static class CrashTestHook
{
    public const string StubCommand = "crash-stub";
    public const string TestCommand = "crash-test";
    public const string UrlKey = "url";
    public const string KindKey = "kind";
    private const string ManagedKind = "managed";
    private const string KillKind = "kill";
    private const string StartedOutcome = "started";
    private const int CrashDelayMilliseconds = 300;
    private const int KillExitCode = 137;

    /// <summary>Chains the commands onto whatever the app registered before (call after the app's own Debug hooks are set up).</summary>
    public static void Install()
    {
        Func<TestHookCommand, string?>? previous = TestHookDispatcher.AppCommands;
        TestHookDispatcher.AppCommands = command => Run(command) ?? previous?.Invoke(command);
        TestHookDispatcher.AppLaunchKeys = TestHookDispatcher.AppLaunchKeys.Concat([UrlKey, KindKey]).Distinct().ToList();
    }

    private static string? Run(TestHookCommand command)
    {
        if (command.Name == StubCommand)
        {
            CrashDebug.SetStub(command.Get(UrlKey, string.Empty));
            return TestHookDispatcher.Ok;
        }

        if (command.Name != TestCommand)
        {
            return null;
        }

        string kind = command.Get(KindKey, ManagedKind);
        if (kind == ManagedKind)
        {
            // A raw thread, not a Task: an exception thrown in a Task is only "unobserved", while one on a thread ends the process, which is what the check needs.
            ReadTheStupidText.Application.Crashes.RollingFileSystemLog.ToCurrent().Warning("crash test about to throw"); // a known line for the stub to find in the log tail (Warning: flushed to the file at once)
            new Thread(() =>
            {
                Thread.Sleep(CrashDelayMilliseconds);
                throw new InvalidOperationException("Slice 227 crash test");
            }).Start();
            return StartedOutcome;
        }

        if (kind == KillKind)
        {
            // Environment.Exit leaves the run marker behind, like a kill while in front (Process.Kill is not available on iOS).
            _ = Task.Run(async () =>
            {
                await Task.Delay(CrashDelayMilliseconds);
                Environment.Exit(KillExitCode);
            });
            return StartedOutcome;
        }

        return TestHookDispatcher.Failed;
    }
}
#endif
