using ReadTheStupidText.Application.Logging;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Android's <see cref="ISystemLog"/> (Slice 47): lines go to logcat under one tag, so the
/// engine's cache hit/miss and first-audio timings can be read with <c>adb logcat -s RTST</c>.
/// Local only, like the Windows system log: nothing is written to disk or sent anywhere.
/// </summary>
public sealed class AndroidSystemLog : ISystemLog
{
    private const string Tag = "RTST";

    public void Info(string message, int? activityId = null) =>
        global::Android.Util.Log.Info(Tag, Format(message, activityId));

    public void Debug(string message, int? activityId = null) =>
        global::Android.Util.Log.Debug(Tag, Format(message, activityId));

    public void Warning(string message, int? activityId = null, Exception? error = null) =>
        global::Android.Util.Log.Warn(Tag, Format(message, activityId) + FormatError(error));

    public void Error(string message, int? activityId = null, Exception? error = null) =>
        global::Android.Util.Log.Error(Tag, Format(message, activityId) + FormatError(error));

    private static string Format(string message, int? activityId) =>
        activityId is { } id ? $"[{id}] {message}" : message;

    private static string FormatError(Exception? error) => error is null ? string.Empty : $" ({error})";
}
