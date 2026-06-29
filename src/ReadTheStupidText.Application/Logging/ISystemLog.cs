namespace ReadTheStupidText.Application.Logging;

/// <summary>
/// The diagnostic system log — every action, warning and exception in the read
/// pipeline, written to the per-day <c>*-system.log</c> file. Each call may carry
/// the <paramref name="activityId"/> of the activity-log entry it relates to, so a
/// line can be joined back to the matching row in the input log. Info/Debug carry
/// extra detail; Warning/Error also carry the exception when there is one.
/// </summary>
public interface ISystemLog
{
    void Info(string message, int? activityId = null);

    void Debug(string message, int? activityId = null);

    void Warning(string message, int? activityId = null, Exception? error = null);

    void Error(string message, int? activityId = null, Exception? error = null);
}
