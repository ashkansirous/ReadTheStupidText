using System.Collections.Concurrent;
using System.Diagnostics;

namespace DinoReader;

/// <summary>
/// Slice 346 E3: remembers (in memory only) which products became owned through a purchase made in this run, so a Worker 403 soon after is read as the store lagging (<see cref="PurchaseLagRetry"/>).
/// A product is stamped only when the purchase flow ends Bought, or when a Pending purchase of this run is later reported owned by a read (the store's listener). A Restore or a sync that finds an
/// older purchase stamps nothing, so a refunded or revoked book is refused at once instead of being retried for minutes.
/// </summary>
public sealed class PurchaseStamps(Func<long>? clock = null)
{
    private readonly Func<long> _clock = clock ?? Stopwatch.GetTimestamp;
    private readonly ConcurrentDictionary<string, long> _boughtAt = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> _pendingThisRun = new(StringComparer.Ordinal);

    /// <summary>The purchase flow of this run ended Bought: the product is as fresh as a purchase gets.</summary>
    public void NoteBought(string product)
    {
        _pendingThisRun.TryRemove(product, out _);
        _boughtAt[product] = _clock();
    }

    /// <summary>The purchase flow of this run ended Pending (a slow payment): a later read that finds the product owned is its completion.</summary>
    public void NotePending(string product) => _pendingThisRun[product] = _clock();

    /// <summary>
    /// A read of the store answered: a Pending purchase of this run that is now owned is stamped. One that is not owned yet stays remembered until <see cref="PurchaseLagRetry.Window"/> after it ended Pending: a single read that lists it
    /// as neither owned nor pending is the store lagging, not a decline. Anything else owned stamps nothing.
    /// </summary>
    public void NoteRead(IReadOnlySet<string> owned, IReadOnlySet<string> pending)
    {
        foreach ((string product, long since) in _pendingThisRun)
        {
            if (Stopwatch.GetElapsedTime(since, _clock()) > PurchaseLagRetry.Window)
            {
                _pendingThisRun.TryRemove(product, out _); // check2 #8: a pending stamp ends at the window from the purchase start, owned, pending or neither
            }
            else if (owned.Contains(product))
            {
                NoteBought(product);
            }
        }
    }

    /// <summary>How long ago this run saw a purchase of any of the products complete, or null when it did not or the last one is older than <see cref="PurchaseLagRetry.Window"/>.</summary>
    public TimeSpan? SinceBought(IEnumerable<string> products)
    {
        TimeSpan? latest = null;
        foreach (string product in products)
        {
            if (!_boughtAt.TryGetValue(product, out long at))
            {
                continue;
            }

            TimeSpan since = Stopwatch.GetElapsedTime(at, _clock());
            if (since > PurchaseLagRetry.Window)
            {
                _boughtAt.TryRemove(product, out _); // past the window a 403 is taken at its word, so the stamp is dropped
                continue;
            }

            latest = latest is { } known && known < since ? known : since;
        }

        return latest;
    }
}
