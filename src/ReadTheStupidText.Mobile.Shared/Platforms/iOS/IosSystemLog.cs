using ReadTheStupidText.Application.Logging;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// iOS's <see cref="ISystemLog"/> (Slice 55): lines go to the unified log (visible in Console.app
/// or Xcode under the app's process) with one tag, so the engine's cache hit/miss and first-audio
/// timings can be read the way Android's logcat tag is. Local only, like the Windows system log:
/// nothing is written to disk or sent anywhere.
/// </summary>
public sealed class IosSystemLog : ISystemLog
{
    private const string Tag = "RTST";

    public void Info(string message, int? activityId = null) => Write("I", message, activityId, null);

    public void Debug(string message, int? activityId = null) => Write("D", message, activityId, null);

    public void Warning(string message, int? activityId = null, Exception? error = null) =>
        Write("W", message, activityId, error);

    public void Error(string message, int? activityId = null, Exception? error = null) =>
        Write("E", message, activityId, error);

    private static void Write(string level, string message, int? activityId, Exception? error)
    {
        string id = activityId is { } value ? $"[{value}] " : string.Empty;
        string detail = error is null ? string.Empty : $" ({error})";
        Console.WriteLine($"{Tag} {level} {id}{message}{detail}");
    }
}
