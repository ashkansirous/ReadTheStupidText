namespace DinoReader.Core;

/// <summary>What the worker does with a job once it is next (<see cref="BackgroundJobRules.Verdict"/>).</summary>
public enum JobVerdict
{
    Run,
    Drop,
    Fail,
}

/// <summary>Slice 459 (check pass #3): which files in the download folder are the <c>.part</c> of one book. The store client names a part <c>{id}-{guid:N}.part</c>.</summary>
public static class LeftoverParts
{
    private const string PartExtension = ".part";
    private const int GuidDigits = 32;

    /// <summary>True when <paramref name="fileName"/> is exactly <c>{bookId}-</c>, 32 hex digits and <c>.part</c>: the book <c>story</c> never matches the part of <c>story-fa</c>.</summary>
    public static bool IsPartOf(string fileName, string bookId)
    {
        string prefix = bookId + "-";
        if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !fileName.EndsWith(PartExtension, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string middle = fileName[prefix.Length..^PartExtension.Length];
        return middle.Length == GuidDigits && middle.All(Uri.IsHexDigit);
    }
}

/// <summary>Slice 459, "Concurrency, one design": when a background job is dropped, failed or retried, as pure rules.</summary>
public static class BackgroundJobRules
{
    /// <summary>A job that failed is tried again at most this many launches, then dropped.</summary>
    public const int MaxRetryLaunches = 3;

    /// <summary>True while the job has failed no more often than the first try plus <see cref="MaxRetryLaunches"/> retries.</summary>
    public static bool WithinRetryBudget(BackgroundJob job) => job.Attempts <= MaxRetryLaunches;

    /// <summary>
    /// What to do with the next job. A book missing from a listing read live this launch is gone: dropped. A book missing from no live listing (a saved one that is stale, or a hidden one) is unknown, not gone: the job
    /// fails, so it counts one attempt under the retry budget and Retry keeps it. A package of a book already on the shelf has nothing left to fetch: dropped. A pack of a book that is not on the shelf is dropped
    /// (the book was deleted), unless that book's package is still queued or failed: then the pack waits as a failed job.
    /// </summary>
    public static JobVerdict Verdict(BackgroundJob job, bool listed, bool liveListingRead, bool onShelf, bool packageQueued)
    {
        if (!listed)
        {
            return liveListingRead ? JobVerdict.Drop : JobVerdict.Fail;
        }

        if (job.Kind == BackgroundJobKind.Package)
        {
            return onShelf ? JobVerdict.Drop : JobVerdict.Run;
        }

        return onShelf ? JobVerdict.Run : packageQueued ? JobVerdict.Fail : JobVerdict.Drop;
    }

    /// <summary>
    /// The result that counts once a job ended: a job of an old generation (the grown-up cancelled) is Cancelled whatever it returned and is forgotten; a Cancelled result of a current job (a joined download another owner
    /// cancelled) is a failure, so the banner shows it and the queue goes on.
    /// </summary>
    public static BackgroundResult Settle(BackgroundResult result, bool stale) =>
        stale ? BackgroundResult.Cancelled : result == BackgroundResult.Cancelled ? BackgroundResult.Failed : result;
}

/// <summary>
/// Slice 459, "Concurrency, one design": the state of the one serial background worker, pure and not thread-safe (the owner holds one lock around every call). Jobs live in one list; <see cref="Add"/> and
/// <see cref="Retry"/> only add and never drop a job; <see cref="Cancel"/> raises the generation number, forgets the waiting and failed jobs and marks the running job stale, so it is dropped when it ends while jobs
/// added after the Cancel go on.
/// </summary>
public sealed class BackgroundQueueModel
{
    private readonly List<BackgroundJob> _pending = [];
    private readonly List<BackgroundJob> _failed = [];
    private BackgroundJob? _current;
    private int _currentGeneration;
    private int _generation;

    /// <summary>The number the last Cancel raised; a job taken earlier is stale.</summary>
    public int Generation => _generation;

    /// <summary>Jobs that failed and wait for Retry (or the next launch).</summary>
    public int Failed => _failed.Count;

    /// <summary>The job being run, null between jobs.</summary>
    public BackgroundJob? Current => _current;

    /// <summary>Jobs not yet finished, the running one included unless a Cancel made it stale.</summary>
    public int Remaining => _pending.Count + (RunningIsLive ? 1 : 0);

    /// <summary>Package jobs not yet finished (the shelf's "N more books").</summary>
    public int BooksRemaining => _pending.Count(IsPackage) + (RunningIsLive && IsPackage(_current!) ? 1 : 0);

    /// <summary>True when the running job was taken before the last Cancel.</summary>
    public bool CurrentIsStale => _current is not null && _currentGeneration != _generation;

    private bool RunningIsLive => _current is not null && !CurrentIsStale;

    private static bool IsPackage(BackgroundJob job) => job.Kind == BackgroundJobKind.Package;

    /// <summary>Adds the jobs not already waiting or running (a failed twin moves back to waiting); returns how many were added.</summary>
    public int Add(IEnumerable<BackgroundJob> jobs)
    {
        int added = 0;
        foreach (BackgroundJob job in jobs)
        {
            if (_pending.Any(p => BackgroundJobStore.SameJob(p, job)) || (RunningIsLive && BackgroundJobStore.SameJob(_current!, job)))
            {
                continue;
            }

            _failed.RemoveAll(f => BackgroundJobStore.SameJob(f, job));
            _pending.Add(job);
            added++;
        }

        return added;
    }

    /// <summary>The failed jobs go back to waiting (after the ones already waiting); returns how many.</summary>
    public int Retry()
    {
        int moved = _failed.Count;
        _pending.AddRange(_failed);
        _failed.Clear();
        return moved;
    }

    /// <summary>Takes the next waiting job as the running one, or null when none waits.</summary>
    public BackgroundJob? TakeNext()
    {
        if (_pending.Count == 0)
        {
            return null;
        }

        _current = _pending[0];
        _currentGeneration = _generation;
        _pending.RemoveAt(0);
        return _current;
    }

    /// <summary>The running job ended with <paramref name="result"/>: it is done, failed (kept with one more attempt) or, when stale, forgotten. Returns the result that counted.</summary>
    public BackgroundResult Settle(BackgroundResult result)
    {
        if (_current is not { } job)
        {
            return result;
        }

        BackgroundResult counted = BackgroundJobRules.Settle(result, CurrentIsStale);
        _current = null;
        if (counted == BackgroundResult.Failed)
        {
            _failed.Add(job with { Attempts = job.Attempts + 1 });
        }

        return counted;
    }

    /// <summary>The grown-up's Cancel: forgets the waiting and failed jobs and makes the running one stale.</summary>
    public void Cancel()
    {
        _generation++;
        _pending.Clear();
        _failed.Clear();
    }

    /// <summary>True when a package job of <paramref name="bookId"/> waits, is running or failed.</summary>
    public bool PackageQueued(string bookId) =>
        _pending.Concat(_failed).Concat(LiveCurrent()).Any(job => IsPackage(job) && string.Equals(job.BookId, bookId, StringComparison.OrdinalIgnoreCase));

    /// <summary>What a kill must not lose, in order: the failed jobs, the running one, the waiting ones.</summary>
    public IReadOnlyList<BackgroundJob> Snapshot() => _failed.Concat(LiveCurrent()).Concat(_pending).ToList();

    private IEnumerable<BackgroundJob> LiveCurrent() => RunningIsLive ? [_current!] : Enumerable.Empty<BackgroundJob>();
}
