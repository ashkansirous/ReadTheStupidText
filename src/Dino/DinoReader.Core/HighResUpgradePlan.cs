using System.Text.Json;

namespace DinoReader.Core;

/// <summary>How one high-res attempt ended (Slice 375 D). <see cref="HighResLedger.Apply"/> is the only place an outcome becomes a state change.</summary>
public enum HighResOutcome
{
    /// <summary>Swapped in (or the installed copy already is high-res): nothing is left to do for the book.</summary>
    Done,

    /// <summary>The copy was edited (the swap leaves it alone): no more attempts for this version.</summary>
    Gone,

    /// <summary>No or limited network, a refused or unsettled purchase, a timeout, IO or storage error, a standard update still on its way: nothing is recorded, the next pass derives the book again.</summary>
    Transient,

    /// <summary>The downloaded hd package fails its own parse or verification: a failure is counted, dropped after <see cref="HighResLedger.MaxFailures"/>.</summary>
    BadPackage,
}

/// <summary>One book's failure record: the store version it is about and how many bad hd packages of that version were seen. Failures at <see cref="HighResLedger.MaxFailures"/> mean "no more attempts".</summary>
public sealed record HighResFailure(int Version, int Failures);

/// <summary>
/// Slice 375 D (design change after five review rounds): the only thing about a book's high-res upgrade that is persisted besides which size is installed. There is no journal of "pending" books: the
/// set to upgrade is derived at the start of each pass (<see cref="HighResUpgradePlan.Derive"/>), so a kill, a failed install or an unsettled purchase cannot leave a record that disagrees with the shelf.
/// This ledger only remembers content faults and give-ups per (book, version), which is what stops a book that can never be upgraded from being retried for ever.
/// </summary>
public sealed class HighResLedger
{
    /// <summary>A bad hd package is fetched again this many times in all; then the book keeps its standard pictures for that version.</summary>
    public const int MaxFailures = 3;

    public static readonly HighResLedger Empty = new(new Dictionary<string, HighResFailure>(StringComparer.OrdinalIgnoreCase));

    private readonly IReadOnlyDictionary<string, HighResFailure> _entries;

    private HighResLedger(IReadOnlyDictionary<string, HighResFailure> entries) => _entries = entries;

    public int Count => _entries.Count;

    public HighResFailure? Get(string id) => _entries.GetValueOrDefault(id);

    /// <summary>Whether another attempt for <paramref name="version"/> of the book may be made: a record of another version says nothing about this one.</summary>
    public bool Allows(string id, int version) => Get(id) is not { } entry || entry.Version != version || entry.Failures < MaxFailures;

    /// <summary>
    /// The outcome rule: how an attempt for <paramref name="version"/> ended. Done clears the book; Gone ends the attempts for this version; BadPackage counts one failure (a record of another version starts again at one);
    /// Transient changes nothing, so an unsettled purchase or a lost connection is never counted however many passes it lasts.
    /// </summary>
    public HighResLedger Apply(string id, int version, HighResOutcome outcome) => outcome switch
    {
        HighResOutcome.Done => Remove(id),
        HighResOutcome.Gone => With(id, new HighResFailure(version, MaxFailures)),
        HighResOutcome.BadPackage => With(id, new HighResFailure(version, Get(id) is { } entry && entry.Version == version ? Math.Min(entry.Failures + 1, MaxFailures) : 1)),
        _ => this,
    };

    /// <summary>The book left the shelf (or was installed afresh): it has no record.</summary>
    public HighResLedger Remove(string id) => _entries.ContainsKey(id) ? Without(id) : this;

    public string ToJson() => JsonSerializer.Serialize(_entries.ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase));

    /// <summary>Reads a saved ledger; a missing, empty or damaged text is an empty ledger, an entry with a bad version or count is left out (an upgrade is only a convenience).</summary>
    public static HighResLedger FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Empty;
        }

        try
        {
            Dictionary<string, HighResFailure>? saved = JsonSerializer.Deserialize<Dictionary<string, HighResFailure>>(json);
            var kept = new Dictionary<string, HighResFailure>(StringComparer.OrdinalIgnoreCase);
            foreach ((string id, HighResFailure entry) in saved ?? [])
            {
                if (!string.IsNullOrWhiteSpace(id) && entry is { Version: >= 1, Failures: >= 0 })
                {
                    kept[id] = entry;
                }
            }

            return new HighResLedger(kept);
        }
        catch (JsonException)
        {
            // no-fallback: a damaged saved file means no failures are remembered; at worst a bad package is fetched a few more times
            return Empty;
        }
    }

    private HighResLedger With(string id, HighResFailure entry) => new(new Dictionary<string, HighResFailure>(_entries, StringComparer.OrdinalIgnoreCase) { [id] = entry });

    private HighResLedger Without(string id)
    {
        var copy = new Dictionary<string, HighResFailure>(_entries, StringComparer.OrdinalIgnoreCase);
        copy.Remove(id);
        return new HighResLedger(copy);
    }
}

/// <summary>
/// Slice 375 D: the pure rules of the high-res upgrade pass. <see cref="Derive"/> says which books to upgrade right now from facts that exist anyway (the shelf, the store catalogue, which size is
/// installed, the ledger); <see cref="OutcomeBeforeSwap"/> says what a staged hd package means at apply time.
/// </summary>
public static class HighResUpgradePlan
{
    /// <summary>
    /// The books to upgrade now, in id order: on the shelf at the catalogue's version, installed at standard size, offered in high-res, the device wanting it, no hd package already staged for that version,
    /// the ledger allowing another attempt, and fetchable by this account. A book the catalogue does not list, or one that is not fetchable right now (an unsettled purchase, a store read that failed), is
    /// simply not in the result this time: nothing is counted or dropped for it, so it is derived again by the pass that can fetch it.
    /// </summary>
    public static IReadOnlyList<StoreItem> Derive(
        bool deviceWantsHigh,
        IReadOnlyDictionary<string, int?> shelfVersions,
        IReadOnlyList<StoreItem> listed,
        IReadOnlySet<string> fetchableIds,
        IReadOnlyDictionary<string, int> installedHigh,
        IReadOnlyDictionary<string, int> stagedHigh,
        HighResLedger ledger)
    {
        if (!deviceWantsHigh)
        {
            return [];
        }

        return listed
            .Where(item => item.HasHighRes && fetchableIds.Contains(item.Id) && IsDue(item, shelfVersions, installedHigh, stagedHigh, ledger))
            .OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsDue(StoreItem item, IReadOnlyDictionary<string, int?> shelfVersions, IReadOnlyDictionary<string, int> installedHigh, IReadOnlyDictionary<string, int> stagedHigh, HighResLedger ledger) =>
        shelfVersions.TryGetValue(item.Id, out int? installed) && installed == item.Version
        && !(installedHigh.TryGetValue(item.Id, out int high) && high == item.Version)
        && !(stagedHigh.TryGetValue(item.Id, out int staged) && staged == item.Version)
        && ledger.Allows(item.Id, item.Version);

    /// <summary>
    /// What a staged high-res package at apply time means (null: swap it in). The book left the shelf: Gone (the file is dropped, nothing is recorded by the caller). The installed copy already
    /// high-res: Done. The installed copy older than the package while the matching standard update is still staged (its swap failed retryably): Transient, so the file waits for it. Any other installed version: Gone.
    /// </summary>
    public static HighResOutcome? OutcomeBeforeSwap(int? installed, int packageVersion, int? stagedStandardVersion, bool onShelf, bool installedIsHigh)
    {
        if (!onShelf)
        {
            return HighResOutcome.Gone;
        }

        if (installed == packageVersion)
        {
            return installedIsHigh ? HighResOutcome.Done : null;
        }

        return installed < packageVersion && stagedStandardVersion >= packageVersion ? HighResOutcome.Transient : HighResOutcome.Gone;
    }

    /// <summary>Review round 7 #6: a 404 for the hd package counts like a bad package (one failure of the ledger, given up after <see cref="HighResLedger.MaxFailures"/>), never a permanent Gone on the first hit: a transient edge or R2 404 just after publishing must not block the upgrade for good.</summary>
    public static HighResOutcome OutcomeForMissingPackage() => HighResOutcome.BadPackage;

    /// <summary>Review round 7 #5: whether the pass goes on to the next book after staging one failed with <paramref name="ex"/>. A fault of this book alone (a bad id) skips it; a storage fault stops the pass.</summary>
    public static bool PassContinuesAfterStageFault(Exception ex) => ex is not (IOException or UnauthorizedAccessException);

    /// <summary>Whether a swap fault says the staged package itself is bad (it will never parse) rather than the device being busy: such a fault ends as BadPackage, any other one is Transient.</summary>
    public static bool IsContentFault(Exception ex) => ex is InvalidDataException or JsonException or FormatException;
}
