using System.Text;

namespace ReadTheStupidText.Application.Crashes;

/// <summary>
/// The crash reports waiting on the phone to be sent, one JSON file each in one folder, and the run marker (Slice 227). Plain files so the crash handler
/// can write one synchronously with no network and no preferences; at most <see cref="MaxPending"/> are kept, the oldest dropped first. Every method
/// is safe to call from a crash handler: none throws.
/// </summary>
public sealed class PendingCrashStore(string folder)
{
    public const int MaxPending = 10;
    private const string FilePrefix = "pending-";
    private const string FileSuffix = ".json";
    private const string TempSuffix = ".tmp";
    private const string RunMarkerName = "running.marker";

    private string RunMarkerPath => Path.Combine(folder, RunMarkerName);

    /// <summary>Writes <paramref name="report"/> as a new pending file and drops the oldest beyond <see cref="MaxPending"/>. Returns whether it was kept.</summary>
    public bool Save(CrashReport report, DateTimeOffset now)
    {
        try
        {
            Directory.CreateDirectory(folder);
            string name = $"{FilePrefix}{now.UtcTicks:D20}-{Guid.NewGuid():N}{FileSuffix}";
            string path = Path.Combine(folder, name);
            string temp = path + TempSuffix;
            File.WriteAllText(temp, CrashReportJson.ToJson(report), new UTF8Encoding(false));
            File.Move(temp, path);
            TrimToLimit();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>The pending files, oldest first.</summary>
    public IReadOnlyList<string> List()
    {
        try
        {
            if (!Directory.Exists(folder))
            {
                return [];
            }

            return Directory.GetFiles(folder, FilePrefix + "*" + FileSuffix).OrderBy(Path.GetFileName, StringComparer.Ordinal).ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>The report in a pending file, or null when it cannot be read or is damaged.</summary>
    public CrashReport? Read(string path)
    {
        try
        {
            return CrashReportJson.Parse(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Every readable pending report with the file it is in, oldest first.</summary>
    public IReadOnlyList<(string Path, CrashReport Report)> ReadAll() =>
        List().Select(path => (Path: path, Report: Read(path))).Where(item => item.Report is not null).Select(item => (item.Path, item.Report!)).ToList();

    /// <summary>
    /// The text of a pending file as it will be posted, or null when it cannot be read. The crash handler writes no log tail (it does the least it can), so a report
    /// of kind <c>managed</c> and an empty tail gets <paramref name="logTail"/> here: the end of the previous run's rolling log, read at the next launch. Other kinds
    /// are built with their own tail (or none, for a diagnostic the system delivers a day late), so they are posted as they are.
    /// </summary>
    public string? ReadBody(string path, string logTail = "")
    {
        try
        {
            if (CrashReportJson.Parse(File.ReadAllText(path, Encoding.UTF8)) is not { } report)
            {
                return null;
            }

            return CrashReportJson.ToJson(report.Kind == CrashKind.Managed && report.LogTail.Length == 0 && logTail.Length > 0 ? report with { LogTail = logTail } : report);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// The newest pending file whose report is of kind <c>managed</c>, or null. Only that one is given the previous run's log tail when sent: the tail describes the last
    /// run, so it would point an older report at the wrong run.
    /// </summary>
    public string? NewestManagedPath() => ReadAll().LastOrDefault(item => item.Report.Kind == CrashKind.Managed).Path;

    public void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception)
        {
            // A file that cannot be deleted is tried again at the next launch.
        }
    }

    /// <summary>Deletes every pending file (the user's switch or the remote config says off).</summary>
    public void Clear()
    {
        foreach (string path in List())
        {
            Delete(path);
        }
    }

    /// <summary>Notes that the app is in front. If the app dies before <see cref="MarkNotRunning"/>, the next launch sees the marker.</summary>
    public void MarkRunning()
    {
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(RunMarkerPath, string.Empty);
        }
        catch (Exception)
        {
            // Without the marker an unclean exit goes unseen; nothing else changes.
        }
    }

    /// <summary>Notes that the app went to the background.</summary>
    public void MarkNotRunning() => Delete(RunMarkerPath);

    /// <summary>When the app last came to the front, once, if the last run left its marker behind (it did not reach the background); null otherwise. The marker is removed.</summary>
    public DateTimeOffset? TakeUncleanExit()
    {
        try
        {
            DateTimeOffset? left = File.Exists(RunMarkerPath) ? new DateTimeOffset(File.GetLastWriteTimeUtc(RunMarkerPath), TimeSpan.Zero) : null;
            Delete(RunMarkerPath);
            return left;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void TrimToLimit()
    {
        IReadOnlyList<string> files = List();
        foreach (string old in files.Take(Math.Max(0, files.Count - MaxPending)))
        {
            Delete(old);
        }
    }
}
