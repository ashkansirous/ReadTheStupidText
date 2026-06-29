using ReadTheStupidText.Application.Logging;
using Windows.Storage;

namespace ReadTheStupidText.Infrastructure.Logging;

/// <summary>
/// Resolves and owns the on-disk log folder under the package's TemporaryFolder
/// (created on first use). Hands out the Serilog rolling-file template for the
/// system log, the per-day path for the input log, and a retention sweep. Also
/// satisfies <see cref="ILogFolder"/> so the UI can open the folder.
/// </summary>
public sealed class LogPaths : ILogFolder
{
    private const string LogsSubfolder = "logs";

    // Serilog inserts the day (yyyyMMdd) before the extension, so this prefix
    // yields files like "system-20260629.log"; the input log matches with "input-".
    private const string SystemLogPrefix = "system-";
    private const string InputLogPrefix = "input-";
    private const string LogExtension = ".log";

    public LogPaths()
    {
        Path = System.IO.Path.Combine(ApplicationData.Current.TemporaryFolder.Path, LogsSubfolder);
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    /// <summary>The Serilog rolling-file path template for the system log.</summary>
    public string SystemLogTemplate => System.IO.Path.Combine(Path, SystemLogPrefix + LogExtension);

    /// <summary>The input-log file for a given day (one file per calendar day).</summary>
    public string InputLogPathFor(DateTimeOffset day) =>
        System.IO.Path.Combine(Path, $"{InputLogPrefix}{day:yyyyMMdd}{LogExtension}");

    /// <summary>Deletes log files last written before now minus <paramref name="maxAge"/>,
    /// so the temp folder stays bounded. Best-effort — a locked file is skipped.</summary>
    public void PurgeOlderThan(TimeSpan maxAge)
    {
        DateTime cutoff = DateTime.Now - maxAge;
        foreach (string file in Directory.EnumerateFiles(Path, "*" + LogExtension))
        {
            if (File.GetLastWriteTime(file) < cutoff)
            {
                TryDelete(file);
            }
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
