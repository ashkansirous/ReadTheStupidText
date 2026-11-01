using ReadTheStupidText.Application.Crashes;
using ReadTheStupidText.Application.Logging;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// iOS's <see cref="ISystemLog"/> (Slice 55): lines go to the unified log (visible in Console.app
/// or Xcode under the app's process) with one tag, so the engine's cache hit/miss and first-audio
/// timings can be read the way Android's logcat tag is. Slice 227: every line is also appended to the small rolling log file whose tail goes
/// out with a crash report (<see cref="RollingFileSystemLog"/>); nothing else is written to disk, and the file itself is never sent on its own.
/// </summary>
public sealed class IosSystemLog : ISystemLog
{
    private const string Tag = "RTST";

    private readonly RollingFileSystemLog _file = RollingFileSystemLog.ToCurrent();

    public void Info(string message, int? activityId = null)
    {
        Write("I", message, activityId, null);
        _file.Info(message, activityId);
    }

    public void Debug(string message, int? activityId = null)
    {
        Write("D", message, activityId, null);
        _file.Debug(message, activityId);
    }

    public void Warning(string message, int? activityId = null, Exception? error = null)
    {
        Write("W", message, activityId, error);
        _file.Warning(message, activityId, error);
    }

    public void Error(string message, int? activityId = null, Exception? error = null)
    {
        Write("E", message, activityId, error);
        _file.Error(message, activityId, error);
    }

    private static void Write(string level, string message, int? activityId, Exception? error)
    {
        string id = activityId is { } value ? $"[{value}] " : string.Empty;
        string detail = error is null ? string.Empty : $" ({error})";
        Console.WriteLine($"{Tag} {level} {id}{message}{detail}");
    }
}
