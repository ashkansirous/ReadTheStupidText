#if DEBUG
using ReadTheStupidText.Application.AppConfig;
using ReadTheStupidText.Application.Usage;
using ReadTheStupidText.Mobile.Shared.Testing;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Debug test command of the forced update (Slice 217): <c>update-test mode=forced|soft|clear</c> gives the app a built-in test config for its
/// own platform, so a scripted check reaches the "Update needed" page (<c>forced</c>: <c>minVersion</c> far above any build) or the reader's card
/// (<c>soft</c>: only <c>latestVersion</c> is above) without the live config file, which a test never reads or changes. <c>clear</c> removes
/// it. Chained after any command registered before it; Release has none of it. Outcome line: <c>HOOK update-test ok</c>, then <c>UPDATE state=..</c>.
/// </summary>
internal static class UpdateTestHook
{
    private const string Name = "update-test";
    private const string ModeKey = "mode";
    private const string Forced = "forced";
    private const string Soft = "soft";
    private const string Clear = "clear";
    private const string FarVersion = "99.0.0";

    /// <summary>Registers the command; call once from the app's start-up, after the other Debug commands registered themselves.</summary>
    internal static void Install()
    {
        Func<TestHookCommand, string?>? previous = TestHookDispatcher.AppCommands;
        TestHookDispatcher.AppCommands = command => command.Name == Name ? Run(command) : previous?.Invoke(command);
        TestHookDispatcher.AppLaunchKeys = [.. TestHookDispatcher.AppLaunchKeys, ModeKey];
    }

    private static string Run(TestHookCommand command)
    {
        string mode = command.Get(ModeKey, string.Empty);
        if (mode is not (Forced or Soft or Clear))
        {
            return TestHookDispatcher.Failed;
        }

        AppUpdate.Shared.SetTestConfig(mode switch
        {
            Forced => ConfigWith(FarVersion, FarVersion),
            Soft => ConfigWith(PlatformVersions.NoVersion, FarVersion),
            _ => null,
        });
        return TestHookDispatcher.Ok;
    }

    private static UsageConfig ConfigWith(string min, string latest)
    {
        var versions = new PlatformVersions(min, latest);
        return new UsageConfig(TelemetrySettings.Off, versions, versions, versions);
    }
}
#endif
