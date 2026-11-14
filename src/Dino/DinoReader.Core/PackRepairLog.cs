namespace DinoReader.Core;

/// <summary>
/// The in-memory record of every pack try in this launch (Slice 360, the pack repair state): per (book, language) the last attempt, its outcome and, while a download runs, its percent. Nothing is
/// persisted: a killed app starts the next launch with everything missing due. Thread-safe; the pure half of the App's <c>PackRepair</c>.
/// </summary>
public sealed class PackRepairLog
{
    private readonly object _gate = new();
    private readonly Dictionary<string, PackRepairEntry> _entries = [];
    private readonly Dictionary<string, int> _percent = [];
    private readonly Dictionary<string, (string BookId, string Language)> _ids = [];

    /// <summary>The key of one pack: the book id (any case) and the language.</summary>
    public static string Key(string bookId, string language) => $"{bookId.ToLowerInvariant()}/{language}";

    public PackRepairEntry? EntryOf(string bookId, string language)
    {
        lock (_gate)
        {
            return _entries.GetValueOrDefault(Key(bookId, language));
        }
    }

    /// <summary>Records the end of a try: its time, outcome and the listed version it asked for. The percent of that pack is forgotten.</summary>
    public void Record(string bookId, string language, PackOutcome outcome, DateTimeOffset now, int? listedVersion)
    {
        lock (_gate)
        {
            _entries[Key(bookId, language)] = new PackRepairEntry(now, outcome, listedVersion);
            _ids[Key(bookId, language)] = (bookId, language);
            _percent.Remove(Key(bookId, language));
        }
    }

    /// <summary>Puts back what this log held for the pack before a try (null: nothing, the entry goes): a try that ended by a cancel leaves no outcome of its own.</summary>
    public void Restore(string bookId, string language, PackRepairEntry? earlier)
    {
        lock (_gate)
        {
            if (earlier is null)
            {
                RemoveLocked(Key(bookId, language));
            }
            else
            {
                _entries[Key(bookId, language)] = earlier;
                _ids[Key(bookId, language)] = (bookId, language);
            }

            _percent.Remove(Key(bookId, language));
        }
    }

    /// <summary>Removes every entry whose pack is no longer needed (<paramref name="stillNeeded"/> false: installed, no listing yet, no pack by design, language gone), so nothing that no run will plan is ever due.</summary>
    public void Prune(Func<string, string, bool> stillNeeded)
    {
        List<(string Key, string BookId, string Language, PackRepairEntry Entry)> snapshot;
        lock (_gate)
        {
            snapshot = _entries.Select(pair => (pair.Key, _ids[pair.Key].BookId, _ids[pair.Key].Language, pair.Value)).ToList();
        }

        List<(string Key, PackRepairEntry Entry)> unneeded = snapshot.Where(item => !stillNeeded(item.BookId, item.Language)).Select(item => (item.Key, item.Entry)).ToList();
        lock (_gate)
        {
            foreach ((string key, PackRepairEntry seen) in unneeded)
            {
                if (_entries.TryGetValue(key, out PackRepairEntry? now) && now == seen)
                {
                    RemoveLocked(key); // only an entry unchanged since the snapshot: a try that ended meanwhile (a fresh Offline or Failed) is kept
                }
            }
        }
    }
    private void RemoveLocked(string key)
    {
        _entries.Remove(key);
        _ids.Remove(key);
    }

    /// <summary>A download of this pack is running at <paramref name="percent"/> (0 to 100).</summary>
    public void SetPercent(string bookId, string language, int percent)
    {
        lock (_gate)
        {
            _percent[Key(bookId, language)] = Math.Clamp(percent, 0, 100);
        }
    }

    /// <summary>A try ended without an outcome (a cancel): the percent goes, the last outcome stays.</summary>
    public void ClearPercent(string bookId, string language)
    {
        lock (_gate)
        {
            _percent.Remove(Key(bookId, language));
        }
    }

    /// <summary>The percent of a running download of this pack, or null when none runs.</summary>
    public int? PercentOf(string bookId, string language)
    {
        lock (_gate)
        {
            return _percent.TryGetValue(Key(bookId, language), out int percent) ? percent : null;
        }
    }

    /// <summary>Whether this pack should be tried now: <see cref="PackRepairDue.IsDue"/> on what this log holds.</summary>
    public bool IsDue(string bookId, string language, DateTimeOffset now, int? listedVersion = null, bool networkBack = false) =>
        PackRepairDue.IsDue(EntryOf(bookId, language), now, listedVersion, networkBack);

    /// <summary>Whether some tried pack whose last try ended Failed, Missing404 or Offline was tried an hour ago or more (what the tick asks before it starts any work).</summary>
    public bool AnyDue(DateTimeOffset now)
    {
        lock (_gate)
        {
            return _entries.Values.Any(entry => entry.LastOutcome != PackOutcome.Ok && now - entry.LastAttemptUtc >= PackRepairDue.Interval);
        }
    }

    /// <summary>Whether some pack's last try ended <see cref="PackOutcome.Offline"/> (what the network coming back asks before it starts any work).</summary>
    public bool AnyOffline()
    {
        lock (_gate)
        {
            return _entries.Values.Any(entry => entry.LastOutcome == PackOutcome.Offline);
        }
    }

    /// <summary>The percent of a download, 0 to 100, from the bytes so far and the listed size; 0 when the size is not known.</summary>
    public static int PercentOfBytes(long bytesSoFar, long? listedBytes) =>
        listedBytes is > 0 ? (int)Math.Clamp(bytesSoFar * 100 / listedBytes.Value, 0, 100) : 0;
}
