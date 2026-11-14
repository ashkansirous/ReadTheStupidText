using System.Text;
using System.Text.Json;

namespace Sirous.Reading.Application.Crashes;

/// <summary>
/// The build one launch of the app ran (Slice 243 findings 1-5): version, build number and executable UUID, each null when not known. A report is made with the facts of the launch it is
/// about, never another launch's: the running launch holds its own in memory and in <see cref="LaunchFactsFile"/>, the previous launch's are in that file's previous copy, a MetricKit
/// diagnostic carries its own. <paramref name="Since"/> is when the launch started (set by <see cref="LaunchFactsFile"/>), so a system exit record can be matched to it.
/// </summary>
public sealed record LaunchFacts(string? Version, string? Build, string? BinaryUuid, DateTimeOffset? Since = null, bool Foreground = false)
{
    /// <summary><paramref name="now"/> made about the launch these facts describe: its version, build number and UUID, and no memory, state or session (those describe only the running launch). Null facts leave all three null.</summary>
    public static CrashContext About(CrashContext now, LaunchFacts? launch) =>
        launch is null
            ? now with { Version = null, Session = null, Facts = null }
            : now with { Version = launch.Version, Session = null, Facts = new CrashFacts(launch.Build, launch.BinaryUuid) };
}

/// <summary>
/// Which build facts a report gets (Slice 243 findings 1-7, design changed after review check 2). Pure. A report about an earlier launch takes that launch's facts: the ones the system's
/// own description carries (MetricKit), else the previous launch's file copy when the exit happened during it; never the running launch's, except its UUID when the build is the same.
/// </summary>
public static class LaunchFactsChoice
{
    /// <summary>The facts for a system-described exit at <paramref name="exitTime"/>: <paramref name="reported"/> if the system named any, else <paramref name="previous"/> when the exit is not older than that launch's start, else null.</summary>
    public static LaunchFacts? ForNativeExit(DateTimeOffset? exitTime, LaunchFacts? reported, LaunchFacts? previous, LaunchFacts? running)
    {
        if (IsEmpty(reported))
        {
            // exitTime is null when the system gave no time of the crash itself (a MetricKit period end is not one): no guess, no previous facts
            // the exit must fall inside the previous launch: not before it started, and before the running launch started (a later one is of an unkept launch or this one)
            return exitTime is { } time && previous?.Since is { } since && running?.Since is { } runningSince && time >= since && time < runningSince ? FillUuid(previous, running) : null;
        }

        return FillUuid(reported, running);
    }

    /// <summary>The facts for an unclean exit: the previous launch's (its marker is the one left behind), UUID filled from the running binary when the build is the same.</summary>
    public static LaunchFacts? ForUncleanExit(LaunchFacts? previous, LaunchFacts? running) => IsEmpty(previous) ? null : FillUuid(previous, running);

    /// <summary>A launch with no UUID, whose build number equals the running build's, ran the same binary: it takes the running binary's UUID.</summary>
    public static LaunchFacts? FillUuid(LaunchFacts? launch, LaunchFacts? running)
    {
        bool sameBuild = launch?.Build is { } build && running?.Build == build;
        return launch is { BinaryUuid: null } && sameBuild && running?.BinaryUuid is { } uuid ? launch with { BinaryUuid = uuid } : launch;
    }

    private static bool IsEmpty(LaunchFacts? launch) => launch is null || (launch.Version is null && launch.Build is null && launch.BinaryUuid is null);
}

/// <summary>
/// <c>launch-facts.json</c> in the crash folder (design changed after review check 2): the build number, version, UUID and start time of the running launch. At start the existing file
/// becomes <c>launch-facts.prev.json</c> and the current launch's is written at once; the UUID, known a moment later, rewrites only <c>launch-facts.json</c>. It is not the run marker and
/// is not deleted when the app goes to the background. Every method swallows I/O errors: a missing or damaged file has no facts (null), never another launch's.
/// </summary>
public sealed class LaunchFactsFile(string folder)
{
    private const string CurrentName = "launch-facts.json";
    private const string PreviousName = "launch-facts.prev.json";
    private const int MaxDepth = 4;

    private static readonly JsonSerializerOptions Options = new() { MaxDepth = MaxDepth };

    private string CurrentPath => Path.Combine(folder, CurrentName);

    private string PreviousPath => Path.Combine(folder, PreviousName);

    /// <summary>
    /// Keeps the existing file as the previous launch's and writes <paramref name="current"/> (with its start time set to <paramref name="now"/>); returns what was written. The existing file
    /// is kept only if its launch reached the foreground (the only launches that can leave an unclean-exit marker) or there is no previous copy yet; a launch that stayed in the background
    /// is overwritten, so the last foreground launch's facts stay as the previous copy.
    /// </summary>
    public LaunchFacts Begin(LaunchFacts current, DateTimeOffset now)
    {
        LaunchFacts stamped = current with { Since = now, Foreground = false };
        try
        {
            Directory.CreateDirectory(folder);
            if (File.Exists(CurrentPath) && (ReadCurrent()?.Foreground == true || !File.Exists(PreviousPath)))
            {
                File.Move(CurrentPath, PreviousPath, true);
            }
        }
        catch (Exception)
        {
            // no-fallback: a launch whose facts file cannot be kept has no previous facts (null), and its reports then carry none.
        }

        Write(stamped);
        return stamped;
    }

    /// <summary>Rewrites only the current launch's file (the UUID arrived).</summary>
    public void Write(LaunchFacts current)
    {
        try
        {
            Directory.CreateDirectory(folder);
            string temp = CurrentPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(new Stored(current.Version, current.Build, current.BinaryUuid, current.Since, current.Foreground), Options), new UTF8Encoding(false));
            File.Move(temp, CurrentPath, true);
        }
        catch (Exception)
        {
            // no-fallback: the facts file is a best-effort extra; a failed write leaves the report without those facts.
        }
    }

    /// <summary>The facts the previous launch left (null when none or unreadable).</summary>
    public LaunchFacts? ReadPrevious() => Read(PreviousPath);

    /// <summary>The facts this launch wrote (null when none or unreadable).</summary>
    public LaunchFacts? ReadCurrent() => Read(CurrentPath);

    /// <summary>Deletes both files (reporting was switched off: nothing from before is kept).</summary>
    public void Clear()
    {
        foreach (string path in new[] { CurrentPath, PreviousPath, CurrentPath + ".tmp" })
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception)
            {
                // no-fallback: a file that cannot be deleted now is deleted by the next clear.
            }
        }
    }

    /// <summary>Parses the file text; null when empty, not JSON, or without any field.</summary>
    public static LaunchFacts? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            Stored? stored = JsonSerializer.Deserialize<Stored>(text, Options);
            return stored is null || (stored.Version is null && stored.Build is null && stored.BinaryUuid is null) ? null : new LaunchFacts(stored.Version, stored.Build, stored.BinaryUuid, stored.Since, stored.Foreground);
        }
        catch (JsonException)
        {
            // no-fallback: a damaged facts file has no facts (null), never another launch's.
            return null;
        }
    }

    private LaunchFacts? Read(string path)
    {
        try
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path, Encoding.UTF8)) : null;
        }
        catch (Exception)
        {
            // no-fallback: an unreadable facts file has no facts (null).
            return null;
        }
    }

    private sealed record Stored(string? Version, string? Build, string? BinaryUuid, DateTimeOffset? Since, bool Foreground = false);
}
