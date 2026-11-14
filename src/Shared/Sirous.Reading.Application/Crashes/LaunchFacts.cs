namespace Sirous.Reading.Application.Crashes;

/// <summary>
/// The build one launch of the app ran (Slice 243 findings 1-5): version, build number and executable UUID, each null when not known. A report is made with the facts of the launch it is
/// about, never another launch's: the running launch holds its own in memory, a crashed one's are in its run marker (<see cref="RunMarker"/>), a MetricKit diagnostic carries its own.
/// </summary>
public sealed record LaunchFacts(string? Version, string? Build, string? BinaryUuid)
{
    /// <summary><paramref name="now"/> made about the launch these facts describe: its version, build number and UUID, and no memory, state or session (those describe only the running launch). Null facts leave all three null.</summary>
    public static CrashContext About(CrashContext now, LaunchFacts? launch) =>
        launch is null
            ? now with { Version = null, Session = null, Facts = null }
            : now with { Version = launch.Version, Session = null, Facts = new CrashFacts(launch.Build, launch.BinaryUuid) };
}

/// <summary>
/// The content of the run marker file: one tab-separated line, version, build and UUID of the launch that wrote it (Slice 243 findings 3 and 5), so an unclean exit, or an Android native
/// crash, is reported with the build that crashed and not the one now installed. Pure; a missing, empty or damaged marker has no facts (null), never another launch's.
/// </summary>
public static class RunMarker
{
    private const char Separator = '\t';
    private const int FieldCount = 3;

    /// <summary>The line to store for <paramref name="launch"/> (empty when there are no facts).</summary>
    public static string Format(LaunchFacts? launch) =>
        launch is null ? string.Empty : string.Join(Separator, Clean(launch.Version), Clean(launch.Build), Clean(launch.BinaryUuid));

    /// <summary>The facts in <paramref name="text"/>, or null when it is empty, damaged or all fields are empty.</summary>
    public static LaunchFacts? Parse(string? text)
    {
        string[] parts = (text ?? string.Empty).Trim('\r', '\n').Split(Separator);
        if (parts.Length != FieldCount)
        {
            return null;
        }

        var launch = new LaunchFacts(NullIfEmpty(parts[0]), NullIfEmpty(parts[1]), NullIfEmpty(parts[2]));
        return launch.Version is null && launch.Build is null && launch.BinaryUuid is null ? null : launch;
    }

    private static string Clean(string? value) => string.Concat((value ?? string.Empty).Select(c => char.IsControl(c) ? ' ' : c));

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;
}
