using System.Globalization;

namespace ReadTheStupidText.Application.Crashes;

/// <summary>
/// A small file that remembers which crashes the app already reported itself (a <c>managed</c> or <c>unclean_exit</c> report), so a later iOS MetricKit crash
/// diagnostic for the same crash is dropped instead of reported again (Slice 227). MetricKit hands over a day's diagnostics with only the period they cover,
/// not the crash time, so the rule is by count: each entry can silence one crash diagnostic whose period covers its time. One line per entry,
/// <c>key|ticks|claimed</c>; entries older than <see cref="Keep"/> are dropped. Never throws.
/// </summary>
public sealed class ReportedCrashLedger(string path)
{
    /// <summary>MetricKit delivers within about a day; an entry is kept this long.</summary>
    public static readonly TimeSpan Keep = TimeSpan.FromDays(3);

    /// <summary>Slack around a diagnostic's period, because a report's time is only to the minute.</summary>
    public static readonly TimeSpan Slack = TimeSpan.FromMinutes(5);

    private const int MaxEntries = 30;
    private const char Separator = '|';
    private readonly object _gate = new();

    /// <summary>Notes a crash reported under <paramref name="key"/> (the pending file's name) at <paramref name="time"/>; the same key is noted once, even after it was claimed.</summary>
    public void Record(string key, DateTimeOffset time, DateTimeOffset now)
    {
        Change(entries =>
        {
            if (entries.Any(e => e.Key == key))
            {
                return;
            }

            entries.Add(new Entry(key, time.UtcTicks, false));
        }, now);
    }

    /// <summary>Uses up one unclaimed entry whose time falls inside the period (with slack). True when there was one: the diagnostic repeats a report already made.</summary>
    public bool TryClaim(DateTimeOffset periodStart, DateTimeOffset periodEnd, DateTimeOffset now)
    {
        bool claimed = false;
        Change(entries =>
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var time = new DateTimeOffset(entries[i].Ticks, TimeSpan.Zero);
                if (!entries[i].Claimed && time >= periodStart - Slack && time <= periodEnd + Slack)
                {
                    entries[i] = entries[i] with { Claimed = true };
                    claimed = true;
                    return;
                }
            }
        }, now);
        return claimed;
    }

    /// <summary>Deletes the ledger file.</summary>
    public void Clear()
    {
        try
        {
            lock (_gate)
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // A ledger that cannot be deleted holds only times and file names.
        }
    }

    private void Change(Action<List<Entry>> change, DateTimeOffset now)
    {
        try
        {
            lock (_gate)
            {
                List<Entry> entries = Read().Where(e => now - new DateTimeOffset(e.Ticks, TimeSpan.Zero) <= Keep).ToList();
                change(entries);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllLines(path, entries.TakeLast(MaxEntries).Select(e => string.Join(Separator, e.Key, e.Ticks.ToString(CultureInfo.InvariantCulture), e.Claimed ? "1" : "0")));
            }
        }
        catch (Exception)
        {
            // Without the ledger a duplicate may be reported; nothing else changes.
        }
    }

    private List<Entry> Read()
    {
        var entries = new List<Entry>();
        if (!File.Exists(path))
        {
            return entries;
        }

        foreach (string line in File.ReadAllLines(path))
        {
            string[] parts = line.Split(Separator);
            if (parts.Length == 3 && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long ticks) && ticks > 0)
            {
                entries.Add(new Entry(parts[0], ticks, parts[2] == "1"));
            }
        }

        return entries;
    }

    private sealed record Entry(string Key, long Ticks, bool Claimed);
}
