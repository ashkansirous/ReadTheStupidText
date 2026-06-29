using ReadTheStupidText.Application.Activity;
using ReadTheStupidText.Domain.Activity;

namespace ReadTheStupidText.Infrastructure.Logging;

/// <summary>
/// Mirrors <see cref="IActivityLog"/> to the per-day input-log file. Every entry
/// add and every state change appends a <em>new</em> row (the file is append-only
/// — it never rewrites a prior row), so the file is the full transition history,
/// not just the latest state. The text logged is whatever the entry holds, which
/// upstream has already been redacted by the sanitizer (Slice 20).
/// </summary>
public sealed class ActivityInputLog : IDisposable
{
    private readonly IActivityLog _log;
    private readonly LogPaths _paths;
    private readonly object _gate = new();

    public ActivityInputLog(IActivityLog log, LogPaths paths)
    {
        _log = log;
        _paths = paths;
        _log.EntryAdded += OnEntry;
        _log.EntryChanged += OnEntry;
    }

    private void OnEntry(object? sender, ActivityEntry entry) => Append(entry);

    // Serialised so concurrent UIA / clipboard / playback threads can't interleave
    // partial lines; the day's file is chosen per write so it rolls at midnight.
    private void Append(ActivityEntry entry)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        string path = _paths.InputLogPathFor(now);
        string line = InputLogRow.Format(now, entry);

        lock (_gate)
        {
            bool fresh = !File.Exists(path);
            using var writer = new StreamWriter(path, append: true);
            if (fresh)
            {
                writer.WriteLine(InputLogRow.Header);
            }

            writer.WriteLine(line);
        }
    }

    public void Dispose()
    {
        _log.EntryAdded -= OnEntry;
        _log.EntryChanged -= OnEntry;
    }
}
