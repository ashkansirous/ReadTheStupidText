namespace ReadTheStupidText.Application.Reading;

/// <summary>How a run of a resumable pack ended (Slice 182).</summary>
public enum PackRunEnd
{
    /// <summary>The pack finished, or ended in a way retrying cannot fix. Forgotten.</summary>
    Finished,

    /// <summary>The user pressed Cancel. Forgotten.</summary>
    Cancelled,

    /// <summary>No network. Kept; restarts on every network or foreground trigger.</summary>
    Offline,

    /// <summary>Failed. Kept; one automatic retry per process, then it needs the user's Retry.</summary>
    Failed,
}

/// <summary>What the coordinator holds for one pack, for the UI to read.</summary>
public enum PackResumeState
{
    /// <summary>Nothing is waiting.</summary>
    None,

    /// <summary>Waiting for a network (or an automatic retry); it carries on by itself.</summary>
    Waiting,

    /// <summary>The automatic retry failed; only the user's Retry starts it again.</summary>
    NeedsUserRetry,
}

/// <summary>
/// Where every run of a resumable pack reports how it ended, exactly once (Slice 182). Installers never add or remove
/// an entry themselves: the coordinator alone decides, from the reported end.
/// </summary>
public interface IPackResume
{
    /// <summary>
    /// A run ended. <paramref name="resume"/> restarts that exact pack (same language and so on) and returns true when it
    /// started a run, false when another run of the pack or installer was busy.
    /// </summary>
    /// <param name="epoch">The <see cref="EpochOf"/> read when this run started; an Offline or Failed end of a run older than the last Cancel is dropped.</param>
    void Report(string key, PackRunEnd end, Func<Task<bool>> resume, string? packId = null, bool downloadNow = false, long? epoch = null);

    /// <summary>Counts the Cancels of the key; a run reads it when it starts and reports it back with its end, so a Cancel is final for older runs.</summary>
    long EpochOf(string key);

    /// <summary>What the coordinator holds for the key.</summary>
    PackResumeState StateOf(string key);

    /// <summary>The maps of <see cref="PackDownloadResult"/> to the end the coordinator understands.</summary>
    static PackRunEnd EndOf(PackDownloadResult result) => result switch
    {
        PackDownloadResult.Offline => PackRunEnd.Offline,
        PackDownloadResult.Failed => PackRunEnd.Failed,
        _ => PackRunEnd.Finished,
    };
}

/// <summary>
/// Holds the stopped downloads and restarts the eligible ones when <see cref="Trigger"/> says the network came back or the
/// app returned to the foreground. Offline stays until it finishes; Failed gets one automatic retry per process and then
/// waits for the user's Retry; Finished and Cancelled leave. A trigger never removes an entry: a restart that finds the pack
/// busy, or throws, leaves it, and the run that is active reports its own end.
/// </summary>
public sealed class PackResumeCoordinator : IPackResume
{
    private sealed record Entry(PackRunEnd End, Func<Task<bool>> Resume, string? PackId, bool DownloadNow, bool NeedsUserRetry);

    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new();
    private readonly HashSet<string> _retrySpent = new(); // keys whose one automatic retry after Failed was started (per app process)
    private readonly Dictionary<string, long> _epochs = new(); // Cancels per key

    public int TrackedCount
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public PackResumeState StateOf(string key)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out Entry? entry))
            {
                return PackResumeState.None;
            }

            return entry.NeedsUserRetry ? PackResumeState.NeedsUserRetry : PackResumeState.Waiting;
        }
    }

    public long EpochOf(string key)
    {
        lock (_gate)
        {
            return _epochs.GetValueOrDefault(key);
        }
    }

    public void Report(string key, PackRunEnd end, Func<Task<bool>> resume, string? packId = null, bool downloadNow = false, long? epoch = null)
    {
        lock (_gate)
        {
            bool resumable = end is PackRunEnd.Offline or PackRunEnd.Failed;
            if (resumable && epoch is { } started && started < _epochs.GetValueOrDefault(key))
            {
                return; // a run from before the last Cancel: Cancel is final
            }

            switch (end)
            {
                case PackRunEnd.Cancelled:
                    _epochs[key] = _epochs.GetValueOrDefault(key) + 1;
                    _entries.Remove(key);
                    _retrySpent.Remove(key);
                    break;
                case PackRunEnd.Offline:
                    _retrySpent.Remove(key); // it was only the network: a later Failed gets its one retry again
                    _entries[key] = new Entry(end, resume, packId, downloadNow, false);
                    break;
                case PackRunEnd.Failed:
                    _entries[key] = new Entry(end, resume, packId, downloadNow, _retrySpent.Contains(key));
                    break;
                default:
                    _entries.Remove(key);
                    _retrySpent.Remove(key);
                    break;
            }
        }
    }

    /// <summary>Restarts every tracked download the policy allows; returns how many were offered a restart. <paramref name="offsetOf"/> reads the partial file for the log line.</summary>
    public int Trigger(PackResumeReason reason, bool online, IReadOnlyCollection<NetworkProfile> profiles, Func<string, long>? offsetOf = null)
    {
        List<Due> due = DueEntries(online, profiles);
        foreach (Due item in due)
        {
            PackDownloadLog.Resume(reason, item.Entry.PackId ?? item.Key, item.Entry.PackId is { } id && offsetOf is not null ? offsetOf(id) : 0);
            _ = RunAsync(item);
        }

        return due.Count;
    }

    private sealed record Due(string Key, Entry Entry, long Epoch, bool SpentRetry);

    // The Failed retry is spent here, under the lock, so two triggers at once never both take it.
    private List<Due> DueEntries(bool online, IReadOnlyCollection<NetworkProfile> profiles)
    {
        lock (_gate)
        {
            List<Due> due = [];
            foreach ((string key, Entry entry) in _entries)
            {
                bool allowed = !entry.NeedsUserRetry
                    && PackResumePolicy.ShouldResume(PackResumePolicy.ResultOf(entry.End), false, entry.DownloadNow, online, profiles, false);
                bool failed = entry.End == PackRunEnd.Failed;
                if (allowed && (!failed || _retrySpent.Add(key)))
                {
                    due.Add(new Due(key, entry, _epochs.GetValueOrDefault(key), failed));
                }
            }

            return due;
        }
    }

    // Busy, a throw or a Cancel since the trigger leaves the entry as it is. Only the trigger that spent the Failed retry gives it back,
    // and only when its restart started no run.
    private async Task RunAsync(Due item)
    {
        bool started = false;
        try
        {
            started = !Cancelled(item) && await Task.Run(item.Entry.Resume).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Pack resume failed: {ex.Message}");
        }

        if (item.SpentRetry && !started)
        {
            GiveBackRetry(item);
        }
    }

    private bool Cancelled(Due item)
    {
        lock (_gate)
        {
            return _epochs.GetValueOrDefault(item.Key) != item.Epoch;
        }
    }

    private void GiveBackRetry(Due item)
    {
        lock (_gate)
        {
            if (_epochs.GetValueOrDefault(item.Key) == item.Epoch)
            {
                _retrySpent.Remove(item.Key);
            }
        }
    }
}
