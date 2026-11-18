using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// What the device keeps about the subscription and about what opened each book (Slice 384): the store's last answer (<see cref="SubscriptionRecord"/>, so the books follow it with no internet) and one
/// <see cref="BookUnlock"/> per downloaded book. Both are small JSON texts in the app's preferences; the rules are all in Core (<see cref="SubscriptionAccess"/>, <see cref="BookUnlockRecords"/>).
/// Nothing here identifies the family: the subscription itself lives in the parent's store account.
/// </summary>
public sealed class SubscriptionStore
{
    private const string RecordKey = "dino.subscription.record";
    private const string UnlocksKey = "dino.book.unlocks";

    /// <summary>The one store of the app: <see cref="BookPurchases"/> uses it unless the host passes another.</summary>
    public static SubscriptionStore Shared { get; } = new();

    private readonly object _gate = new();
    private SubscriptionRecord? _record;
    private Dictionary<string, BookUnlock>? _unlocks;

    /// <summary>The store's last answer about the subscription; null when never read. Read once from preferences, then held.</summary>
    public SubscriptionRecord? Record
    {
        get
        {
            lock (_gate)
            {
                return _record ??= SubscriptionAccess.FromJson(Preferences.Default.Get(RecordKey, string.Empty));
            }
        }
    }

    /// <summary>True while the subscription opens paid books (<see cref="SubscriptionAccess.Grants"/>, at this moment).</summary>
    public bool Grants => SubscriptionAccess.Grants(Record, DateTimeOffset.UtcNow);

    /// <summary>Merges one answer into the record (<see cref="SubscriptionAccess.Merge"/>) and saves it, read and write under one lock so a late answer cannot overwrite a newer read; returns the record before and after.</summary>
    public (SubscriptionRecord? Before, SubscriptionRecord? After) Merge(SubscriptionAnswer answer)
    {
        lock (_gate)
        {
            SubscriptionRecord? before = Record;
            SubscriptionRecord? after = SubscriptionAccess.Merge(before, answer, DateTimeOffset.UtcNow);
            if (!ReferenceEquals(before, after))
            {
                Save(after);
            }

            return (before, after);
        }
    }

    private void Save(SubscriptionRecord? record)
    {
        lock (_gate)
        {
            _record = record;
            if (record is null)
            {
                Preferences.Default.Remove(RecordKey);
                return;
            }

            Preferences.Default.Set(RecordKey, SubscriptionAccess.ToJson(record));
        }
    }

    /// <summary>What opened the book when it was downloaded; null for a book with no record (it is never locked).</summary>
    public BookUnlock? UnlockOf(string bookId)
    {
        lock (_gate)
        {
            return Unlocks().TryGetValue(bookId, out BookUnlock unlock) ? unlock : null;
        }
    }

    /// <summary>The ids of the downloaded books the subscription opened (Slice 397: the Ended card counts the locked ones from the shelf's own records, not from the store listing).</summary>
    public IReadOnlyList<string> SubscriptionOpenedBooks()
    {
        lock (_gate)
        {
            return [.. Unlocks().Where(pair => pair.Value == BookUnlock.Subscription).Select(pair => pair.Key)];
        }
    }

    /// <summary>Writes the record for a downloaded book (<see cref="BookUnlockRecords.Merge"/>: the first record stands, a subscription one becomes bought).</summary>
    public void RecordUnlock(string bookId, BookUnlock now)
    {
        lock (_gate)
        {
            Dictionary<string, BookUnlock> all = Unlocks();
            BookUnlock merged = BookUnlockRecords.Merge(all.TryGetValue(bookId, out BookUnlock held) ? held : null, now);
            if (!all.TryGetValue(bookId, out BookUnlock before) || before != merged)
            {
                all[bookId] = merged;
                Persist(all);
            }
        }
    }

    /// <summary>The books the account now owns by purchase: any subscription record of theirs becomes bought, for good.</summary>
    public void UpgradeBought(IEnumerable<string> boughtBookIds)
    {
        lock (_gate)
        {
            Dictionary<string, BookUnlock> all = Unlocks();
            bool changed = false;
            foreach (string id in boughtBookIds)
            {
                if (all.TryGetValue(id, out BookUnlock held) && BookAccess.UpgradeUnlock(held, boughtNow: true) is { } upgraded && upgraded != held)
                {
                    all[id] = upgraded;
                    changed = true;
                }
            }

            if (changed)
            {
                Persist(all);
            }
        }
    }

    /// <summary>Forgets a deleted book's record.</summary>
    public void ForgetUnlock(string bookId)
    {
        lock (_gate)
        {
            if (Unlocks().Remove(bookId))
            {
                Persist(Unlocks());
            }
        }
    }

    private Dictionary<string, BookUnlock> Unlocks() => _unlocks ??= BookUnlockRecords.FromJson(Preferences.Default.Get(UnlocksKey, string.Empty));

    private void Persist(Dictionary<string, BookUnlock> all) => Preferences.Default.Set(UnlocksKey, BookUnlockRecords.ToJson(all));
}
