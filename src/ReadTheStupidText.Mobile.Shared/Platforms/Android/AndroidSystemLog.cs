using ReadTheStupidText.Application.Crashes;
using ReadTheStupidText.Application.Logging;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// Android's <see cref="ISystemLog"/> (Slice 47): lines go to logcat under one tag, so the
/// engine's cache hit/miss and first-audio timings can be read with <c>adb logcat -s RTST</c>.
/// Slice 227: every line is also appended to the small rolling log file whose tail goes out with a crash report (<see cref="RollingFileSystemLog"/>); nothing else
/// is written to disk, and the file itself is never sent on its own.
/// </summary>
public sealed class AndroidSystemLog : ISystemLog
{
    private const string Tag = "RTST";

    private readonly RollingFileSystemLog _file = RollingFileSystemLog.ToCurrent();

    public void Info(string message, int? activityId = null)
    {
        global::Android.Util.Log.Info(Tag, Format(message, activityId));
        _file.Info(message, activityId);
    }

    public void Debug(string message, int? activityId = null)
    {
        global::Android.Util.Log.Debug(Tag, Format(message, activityId));
        _file.Debug(message, activityId);
    }

    public void Warning(string message, int? activityId = null, Exception? error = null)
    {
        global::Android.Util.Log.Warn(Tag, Format(message, activityId) + FormatError(error));
        _file.Warning(message, activityId, error);
    }

    public void Error(string message, int? activityId = null, Exception? error = null)
    {
        global::Android.Util.Log.Error(Tag, Format(message, activityId) + FormatError(error));
        _file.Error(message, activityId, error);
    }

    private static string Format(string message, int? activityId) =>
        activityId is { } id ? $"[{id}] {message}" : message;

    private static string FormatError(Exception? error) => error is null ? string.Empty : $" ({error})";
}
