using System.Text;

namespace Sirous.Reading.Application.Crashes;

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

    private const string UncleanTailName = "last-unclean-tail.txt";

    private string RunMarkerPath => Path.Combine(folder, RunMarkerName);

    private string UncleanTailPath => Path.Combine(folder, UncleanTailName);

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
            // no-fallback: the crash report is not kept and the caller is told (false); crash storage is best effort.
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
            // no-fallback: an unreadable crash folder lists as empty; pending reports are retried at the next launch.
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
            // no-fallback: an unreadable pending crash file reads as null and is skipped.
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
            // no-fallback: an unreadable pending crash file has no body (null) and is not posted.
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
            // no-fallback: a file that cannot be deleted is tried again at the next launch.
        }
    }

    /// <summary>Deletes every pending file (the user's switch or the remote config says off) and the note of the last unclean exit's tail.</summary>
    public void Clear()
    {
        foreach (string path in List())
        {
            Delete(path);
        }

        Delete(UncleanTailPath);
    }

    /// <summary>
    /// Slice 424 item 6, one death one report: true when the unclean exit of the launch that started at <paramref name="launchStart"/> with this <paramref name="logTail"/> was already
    /// reported (the note on disk holds the same launch and tail, so the same death is not counted twice). A different launch with the same tail is a different death and is not a
    /// repeat; a launch of unknown start or an empty tail cannot be told apart, so it is never a repeat. A failure to read the note says false (a repeated report is better than a lost one).
    /// </summary>
    public bool IsUncleanReported(string logTail, DateTimeOffset? launchStart)
    {
        if (UncleanKey(logTail, launchStart) is not { } key)
        {
            return false;
        }

        try
        {
            return File.Exists(UncleanTailPath) && string.Equals(File.ReadAllText(UncleanTailPath, Encoding.UTF8).Trim(), key, StringComparison.Ordinal);
        }
        catch (Exception)
        {
            // no-fallback: an unreadable note means the death is reported (not a repeat), never silently dropped.
            return false;
        }
    }

    /// <summary>Remembers on disk that the unclean exit of this launch and tail was reported; call it only after the report is stored. Never throws.</summary>
    public void NoteUncleanReported(string logTail, DateTimeOffset? launchStart)
    {
        if (UncleanKey(logTail, launchStart) is not { } key)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(UncleanTailPath, key, new UTF8Encoding(false));
        }
        catch (Exception)
        {
            // no-fallback: without the note a repeat of this death is reported again (better than a lost report).
        }
    }

    private static string? UncleanKey(string logTail, DateTimeOffset? launchStart) =>
        logTail.Length == 0 || launchStart is not { } start
            ? null
            : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes($"{start.UtcTicks}|{logTail}")));

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
            // no-fallback: without the marker an unclean exit goes unseen; nothing else changes.
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
            // no-fallback: an unreadable run marker reads as a clean exit (null).
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
