using ReadTheStupidText.Application.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace ReadTheStupidText.Infrastructure.Logging;

/// <summary>
/// <see cref="ISystemLog"/> backed by a Serilog rolling-file sink (one file per
/// day). The related activity id is baked into the line as "#&lt;id&gt;" so the
/// system log joins to the input log, and the message is passed as a property —
/// never as a template — so braces in redacted text can't break formatting.
/// </summary>
public sealed class SerilogSystemLog : ISystemLog, IDisposable
{
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

    private readonly Logger _logger;

    public SerilogSystemLog(LogPaths paths)
    {
        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                paths.SystemLogTemplate,
                rollingInterval: RollingInterval.Day,
                outputTemplate: OutputTemplate)
            .CreateLogger();
    }

    public void Info(string message, int? activityId = null) =>
        Write(LogEventLevel.Information, message, activityId, null);

    public void Debug(string message, int? activityId = null) =>
        Write(LogEventLevel.Debug, message, activityId, null);

    public void Warning(string message, int? activityId = null, Exception? error = null) =>
        Write(LogEventLevel.Warning, message, activityId, error);

    public void Error(string message, int? activityId = null, Exception? error = null) =>
        Write(LogEventLevel.Error, message, activityId, error);

    private void Write(LogEventLevel level, string message, int? activityId, Exception? error)
    {
        string line = activityId is { } id ? $"#{id} {message}" : message;
        _logger.Write(level, error, "{Detail}", line);
    }

    public void Dispose() => _logger.Dispose();
}
