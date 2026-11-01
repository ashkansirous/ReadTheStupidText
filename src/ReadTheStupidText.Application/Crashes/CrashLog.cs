using ReadTheStupidText.Application.Logging;

namespace ReadTheStupidText.Application.Crashes;

/// <summary>
/// Holds the app's one <see cref="RollingLogFile"/> (Slice 227), set at start-up, so every system log on the phone writes to the same file. Lines are written only
/// while the gate set by <see cref="Use"/> allows it (crash reporting could send: an eligible build and the person's switch on), so nothing is kept otherwise.
/// </summary>
public static class CrashLog
{
    private static volatile RollingLogFile? current;
    private static volatile Func<bool>? mayWrite;

    /// <summary>The rolling file in use (to read its tail or delete it), or null before start-up (and always null in the Windows tray app).</summary>
    public static RollingLogFile? Current => current;

    /// <summary>The file to write a line to: <see cref="Current"/> while the gate allows writing, otherwise null.</summary>
    public static RollingLogFile? Writer => mayWrite is { } gate && !gate() ? null : current;

    /// <param name="file">The file to use.</param>
    /// <param name="gate">True while a line may be written; read at each call, so it must be cheap.</param>
    public static void Use(RollingLogFile file, Func<bool> gate)
    {
        mayWrite = gate;
        current = file;
    }
}

/// <summary>
/// An <see cref="ISystemLog"/> that writes each call as one line to the rolling log file (Slice 227): the part of the phone's system logs that
/// feeds the crash report's tail. It formats with <see cref="LogLineFormatter"/> and flushes on Warning and Error. Never throws.
/// </summary>
/// <param name="file">The file to write to, read at each call (so it can be set after the log is created); null drops the line.</param>
public sealed class RollingFileSystemLog(Func<RollingLogFile?> file) : ISystemLog
{
    private const char InfoLevel = 'I';
    private const char DebugLevel = 'D';
    private const char WarningLevel = 'W';
    private const char ErrorLevel = 'E';

    public void Info(string message, int? activityId = null) => Write(InfoLevel, message, activityId, null, false);

    public void Debug(string message, int? activityId = null) => Write(DebugLevel, message, activityId, null, false);

    public void Warning(string message, int? activityId = null, Exception? error = null) => Write(WarningLevel, message, activityId, error, true);

    public void Error(string message, int? activityId = null, Exception? error = null) => Write(ErrorLevel, message, activityId, error, true);

    /// <summary>The platform logs' shared sink: writes to <see cref="CrashLog.Writer"/>.</summary>
    public static RollingFileSystemLog ToCurrent() => new(() => CrashLog.Writer);

    private void Write(char level, string message, int? activityId, Exception? error, bool flush)
    {
        try
        {
            file()?.Append(LogLineFormatter.Format(DateTimeOffset.UtcNow, level, message, activityId, error), flush);
        }
        catch (Exception)
        {
            // A broken log must not break the caller.
        }
    }
}
