using System.Text.Json;

namespace DinoReader;

/// <summary>Where a book's high-res upgrade is (Slice 375 D): waiting for Wi-Fi, or downloaded and waiting for the shelf to swap it in. "None" is the absence of an entry; "applied" removes it.</summary>
public enum HighResPhase
{
    Pending,
    Staged,
}

/// <summary>How one high-res attempt ended (Slice 375 D, outcome rule). <see cref="HighResUpgradeTable.Apply"/> is the only place an outcome becomes a state change.</summary>
public enum HighResOutcome
{
    /// <summary>Swapped in (or the installed copy already is high-res): the entry is removed.</summary>
    Done,

    /// <summary>A real 404 for the hd package, or the catalogue lists this version without an hd package: the entry is removed.</summary>
    Gone,

    /// <summary>Purchase or entitlement refusal (402/403): entry kept, no failure counted; the caller skips this book for the rest of the run and goes on.</summary>
    Refused,

    /// <summary>No or limited network, timeout, cancellation, IO or storage error, a failed catalogue fetch, the book missing from the catalogue, a standard update still on its way: entry kept, no failure counted, the next trigger retries.</summary>
    Transient,

    /// <summary>The downloaded or staged hd package fails its own validation: a failure is counted, the staged file is deleted, the entry is dropped after <see cref="HighResUpgradeTable.MaxFailures"/>.</summary>
    BadPackage,
}

/// <summary>One book's entry: the version still waiting for its high-res pack, where it is, and how many staged copies were bad.</summary>
public sealed record HighResUpgrade(int Version, HighResPhase Phase, int Failures = 0);

/// <summary>
/// Slice 375 D: the pure state machine of <c>HighResUpgrades</c>, a book id to <see cref="HighResUpgrade"/> table with immutable transitions (each returns a new table) and a JSON form. Plain .NET, so
/// the transitions are tested on the real file. States: none, <c>pending(v)</c> (the book was installed standard by a StandardThenHigh choice), <c>staged(v)</c> (the high-res package is in
/// StagedBookUpdates), applied (entry removed).
/// </summary>
public sealed class HighResUpgradeTable
{
    /// <summary>A bad staged copy is retried this many times, then the upgrade is dropped (the book keeps its standard pictures).</summary>
    public const int MaxFailures = 3;

    public static readonly HighResUpgradeTable Empty = new(new Dictionary<string, HighResUpgrade>(StringComparer.OrdinalIgnoreCase));

    private readonly IReadOnlyDictionary<string, HighResUpgrade> _entries;

    private HighResUpgradeTable(IReadOnlyDictionary<string, HighResUpgrade> entries) => _entries = entries;

    public int Count => _entries.Count;

    public HighResUpgrade? Get(string id) => _entries.GetValueOrDefault(id);

    public IEnumerable<KeyValuePair<string, HighResUpgrade>> Entries => _entries;

    /// <summary>A standard copy of version <paramref name="version"/> was installed or updated in by a StandardThenHigh choice: the book waits for its high-res pack. Replaces whatever the book had.</summary>
    public HighResUpgradeTable MarkPending(string id, int version) => With(id, new HighResUpgrade(version, HighResPhase.Pending));

    /// <summary>An install choice (outcome rule: not an attempt outcome): the book now has, or will have, version <paramref name="version"/>; <paramref name="upgradeLater"/> (a StandardThenHigh choice) makes it wait for its high-res pack, any other choice (standard for good, or high-res at once) removes an older entry.</summary>
    public HighResUpgradeTable Install(string id, int version, bool upgradeLater) => upgradeLater ? MarkPending(id, version) : Remove(id);

    /// <summary>A book delete (outcome rule: not an attempt outcome): the book has no entry any more.</summary>
    public HighResUpgradeTable Remove(string id) => _entries.ContainsKey(id) ? Without(id) : this;

    /// <summary>The high-res package of <paramref name="version"/> is fully staged. Only a pending entry of that same version moves on; anything else is left as it is.</summary>
    public HighResUpgradeTable MarkStaged(string id, int version) =>
        Get(id) is { Phase: HighResPhase.Pending } entry && entry.Version == version ? With(id, entry with { Phase = HighResPhase.Staged }) : this;

    /// <summary>The shelf swapped the staged package in: the entry is removed (only a staged entry of that version counts).</summary>
    public HighResUpgradeTable MarkApplied(string id, int version) =>
        Get(id) is { Phase: HighResPhase.Staged } entry && entry.Version == version ? Without(id) : this;

    /// <summary>A staged copy turned out bad (or its file was lost): back to pending with one more failure; after <see cref="MaxFailures"/> the entry is dropped.</summary>
    public HighResUpgradeTable MarkFailed(string id, int version)
    {
        if (Get(id) is not { } entry || entry.Version != version)
        {
            return this;
        }

        return entry.Failures + 1 >= MaxFailures ? Without(id) : With(id, new HighResUpgrade(version, HighResPhase.Pending, entry.Failures + 1));
    }

    /// <summary>
    /// The outcome rule (Slice 375 D): the one function that turns how an attempt for <paramref name="version"/> ended into a state change. An entry of another version is left alone.
    /// Done and Gone remove the entry; BadPackage counts a failure (back to pending, dropped after <see cref="MaxFailures"/>); Refused and Transient change nothing.
    /// </summary>
    public HighResUpgradeTable Apply(string id, int version, HighResOutcome outcome)
    {
        if (Get(id) is not { } entry || entry.Version != version)
        {
            return this;
        }

        return outcome switch
        {
            HighResOutcome.Done or HighResOutcome.Gone => Without(id),
            HighResOutcome.BadPackage => MarkFailed(id, version),
            _ => this,
        };
    }

    /// <summary>
    /// What a staged high-res package at apply time means (null: swap it in). Not staged for this version, or the book left the shelf: Gone (the file is dropped). The installed copy already
    /// high-res: Done. The installed copy older than the entry while the matching standard update is still staged (its swap failed retryably): Transient, so the entry and file wait for it.
    /// Any other installed version: Gone.
    /// </summary>
    public static HighResOutcome? OutcomeBeforeSwap(bool stagedForVersion, int? installed, int entryVersion, int? stagedStandardVersion, bool onShelf, bool installedIsHigh)
    {
        if (!stagedForVersion || !onShelf)
        {
            return HighResOutcome.Gone;
        }

        if (installed == entryVersion)
        {
            return installedIsHigh ? HighResOutcome.Done : null;
        }

        return installed < entryVersion && stagedStandardVersion >= entryVersion ? HighResOutcome.Transient : HighResOutcome.Gone;
    }

    /// <summary>A staged entry's file was lost (a kill between marking and renaming, a cleared cache): back to pending with no failure counted, since the package was never found bad.</summary>
    public HighResUpgradeTable MarkLost(string id, int version) =>
        Get(id) is { Phase: HighResPhase.Staged } entry && entry.Version == version ? With(id, entry with { Phase = HighResPhase.Pending }) : this;

    /// <summary>
    /// Drops every entry that can no longer be right: the book is not on the shelf, its installed version is newer than the entry's (updated meanwhile: the new install's own choice
    /// marked it again), it is older and no staged update of the entry's version is on its way (the update was dropped), or the listing no longer offers a high-res package (a listing that is
    /// unknown, <paramref name="offersHighRes"/> null, drops nothing for that reason). An entry whose book is older only because its update is staged stays: the update's own choice made it.
    /// </summary>
    public HighResUpgradeTable Reconcile(Func<string, int?> installedVersion, Func<string, int?> stagedUpdateVersion, Func<string, bool?> offersHighRes)
    {
        HighResUpgradeTable result = this;
        foreach ((string id, HighResUpgrade entry) in _entries)
        {
            int? installed = installedVersion(id);
            bool updateOnItsWay = installed < entry.Version && stagedUpdateVersion(id) >= entry.Version;
            bool stale = installed is null || (installed != entry.Version && !updateOnItsWay) || offersHighRes(id) == false;
            result = stale ? result.Without(id) : result;
        }

        return result;
    }

    /// <summary>The next book to fetch: a pending entry not in <paramref name="skip"/> (one at a time, in id order so a run is predictable), or null when none is pending.</summary>
    public KeyValuePair<string, HighResUpgrade>? NextPending(ISet<string>? skip = null) =>
        _entries.Where(e => e.Value.Phase == HighResPhase.Pending && skip?.Contains(e.Key) != true).OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase).Select(e => (KeyValuePair<string, HighResUpgrade>?)e).FirstOrDefault();

    public string ToJson() => JsonSerializer.Serialize(_entries.ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase));

    /// <summary>Reads a saved table; a missing, empty or unreadable text, and an entry with a bad version or phase, is an empty table or is left out (an upgrade is only a convenience).</summary>
    public static HighResUpgradeTable FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Empty;
        }

        try
        {
            Dictionary<string, HighResUpgrade>? saved = JsonSerializer.Deserialize<Dictionary<string, HighResUpgrade>>(json);
            var kept = new Dictionary<string, HighResUpgrade>(StringComparer.OrdinalIgnoreCase);
            foreach ((string id, HighResUpgrade entry) in saved ?? [])
            {
                if (!string.IsNullOrWhiteSpace(id) && entry is { Version: >= 1 } && Enum.IsDefined(entry.Phase))
                {
                    kept[id] = entry;
                }
            }

            return new HighResUpgradeTable(kept);
        }
        catch (JsonException)
        {
            // no-fallback: a damaged saved file means no upgrades waiting; the books keep their standard pictures
            return Empty;
        }
    }

    private HighResUpgradeTable With(string id, HighResUpgrade entry)
    {
        var copy = new Dictionary<string, HighResUpgrade>(_entries, StringComparer.OrdinalIgnoreCase) { [id] = entry };
        return new HighResUpgradeTable(copy);
    }

    private HighResUpgradeTable Without(string id)
    {
        var copy = new Dictionary<string, HighResUpgrade>(_entries, StringComparer.OrdinalIgnoreCase);
        copy.Remove(id);
        return new HighResUpgradeTable(copy);
    }
}
