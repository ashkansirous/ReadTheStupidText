namespace DinoReader.Core;

/// <summary>Why a book on the shelf is not updated in this run (Slice 181).</summary>
public enum BookUpdateSkip
{
    /// <summary>The recorded version is at or above the store's.</summary>
    UpToDate,

    /// <summary>The grown-up changed the book's page text, so it is never overwritten.</summary>
    Edited,

    /// <summary>A catalogue book no longer on the shelf (deleted); it is not brought back.</summary>
    Deleted,

    /// <summary>The store does not list the book (any more, or in a format this app cannot read).</summary>
    NotInStore,
}

/// <summary>What the updater does with one book.</summary>
public enum BookUpdateAction
{
    /// <summary>Fetch the store's version and swap it in.</summary>
    Update,

    /// <summary>The book's original text was never recorded: fetch the installed version once and record its hash, then plan again.</summary>
    NeedsBaseline,

    /// <summary>Leave the book alone; <see cref="BookUpdateDecision.Skip"/> says why.</summary>
    Skip,
}

/// <summary>The store's newest version of one book.</summary>
public sealed record StoreBookVersion(string Id, int Version);

/// <summary>
/// What the app recorded for a catalogue book: its installed version (null when none was recorded), the hash of the page text it arrived with (null when none),
/// and the newest store version found while the copy was edited (null when none), so an edited book is not downloaded again until the store has a newer one.
/// </summary>
public sealed record InstalledBookRecord(string Id, int? Version, string? TextHash, int? SeenVersion = null);

/// <summary>One copy of a book on the shelf (found by its store id; there can be several after a half-finished swap): the hash of its page text now and, when known, of its text plus pictures (<see cref="StarterUpdate.ContentHash"/>).</summary>
public sealed record ShelfBook(string Id, string CurrentTextHash, string? ContentHash = null);

/// <summary>
/// The journal line of a swap in progress (Slice 181): "swapping <c>Id</c> to v<c>Version</c>", written before the new copy is imported (no <see cref="FreshId"/> yet) and rewritten with the
/// new copy's id once it holds the carried-over choices, cleared after the old copy is deleted and the version and hash are recorded. A line left at launch is finished by <c>StarterStories</c>.
/// </summary>
public sealed record SwapMarker(string Id, int Version, string OldId, string? FreshId = null)
{
    private const char Separator = '|';

    public string Format() => string.Join(Separator, Id, Version, OldId, FreshId ?? string.Empty);

    public static SwapMarker? TryParse(string? text)
    {
        string[] parts = (text ?? string.Empty).Split(Separator);
        return parts.Length == 4 && parts[0].Length > 0 && parts[2].Length > 0 && int.TryParse(parts[1], out int version)
            ? new SwapMarker(parts[0], version, parts[2], parts[3].Length > 0 ? parts[3] : null)
            : null;
    }
}

public sealed record BookUpdateDecision(string Id, BookUpdateAction Action, BookUpdateSkip? Skip = null);

/// <summary>
/// Which books of the shelf to refresh from the store (Slice 181): every catalogue book (starters and downloaded) whose recorded version is below the store's,
/// unless the grown-up edited its text or it was deleted. Pure: the app gathers the three lists and acts on the result. It judges against the library as it is, so it is used
/// twice: when the background run decides what to download, and again at the next cold start (before any screen is open) when the downloaded packages are applied.
/// </summary>
public static class BookUpdatePlan
{
    /// <summary>
    /// One decision per installed record, in its order (a shelf book with no record is not ours to update). Several shelf copies of one id are judged together:
    /// the book is edited only when no copy still has the recorded text.
    /// </summary>
    public static IReadOnlyList<BookUpdateDecision> Decide(
        IReadOnlyList<StoreBookVersion> store, IReadOnlyList<InstalledBookRecord> installed, IReadOnlyList<ShelfBook> shelf)
    {
        var storeById = store.GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Max(s => s.Version), StringComparer.OrdinalIgnoreCase);
        var shelfById = shelf.GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        return installed.Select(record => DecideOne(record, storeById, shelfById)).ToList();
    }

    private static BookUpdateDecision DecideOne(
        InstalledBookRecord record, Dictionary<string, int> storeById, Dictionary<string, List<ShelfBook>> shelfById)
    {
        if (!shelfById.TryGetValue(record.Id, out List<ShelfBook>? book))
        {
            return Skipped(record.Id, BookUpdateSkip.Deleted);
        }

        if (!storeById.TryGetValue(record.Id, out int storeVersion))
        {
            return Skipped(record.Id, BookUpdateSkip.NotInStore);
        }

        if (!StarterUpdate.NeedsUpdate(record.Version, storeVersion))
        {
            return Skipped(record.Id, BookUpdateSkip.UpToDate);
        }

        if (record.SeenVersion is { } seen && storeVersion <= seen)
        {
            return Skipped(record.Id, BookUpdateSkip.Edited);
        }

        if (record.TextHash is null)
        {
            return new BookUpdateDecision(record.Id, BookUpdateAction.NeedsBaseline);
        }

        return JudgeContent(record, record.TextHash, book);
    }

    // A hash that covers the pictures is compared with each copy's text plus pictures. An older text-only hash can say a text was edited, but not that the pictures were not:
    // a copy with that text needs its baseline fetched again (and recorded in the new form) before it is ever replaced. When unsure, the copy is left alone.
    private static BookUpdateDecision JudgeContent(InstalledBookRecord record, string recorded, List<ShelfBook> copies)
    {
        if (StarterUpdate.IsContentHash(recorded))
        {
            return copies.Any(c => c.ContentHash == recorded)
                ? new BookUpdateDecision(record.Id, BookUpdateAction.Update)
                : Skipped(record.Id, BookUpdateSkip.Edited);
        }

        return copies.Any(c => c.CurrentTextHash == recorded)
            ? new BookUpdateDecision(record.Id, BookUpdateAction.NeedsBaseline)
            : Skipped(record.Id, BookUpdateSkip.Edited);
    }

    private static BookUpdateDecision Skipped(string id, BookUpdateSkip why) => new(id, BookUpdateAction.Skip, why);
}

/// <summary>When the background book check runs: at most once every <see cref="Interval"/> after the last finished check (Slice 181).</summary>
public static class BookUpdateSchedule
{
    public static readonly TimeSpan Interval = TimeSpan.FromDays(7);

    /// <summary>Due when no check ever finished, when the interval has passed, or when the clock moved back before the last check (a time in the future cannot be trusted).</summary>
    public static bool IsDue(DateTimeOffset? lastFinished, DateTimeOffset now) =>
        lastFinished is not { } last || last > now || now - last >= Interval;
}
