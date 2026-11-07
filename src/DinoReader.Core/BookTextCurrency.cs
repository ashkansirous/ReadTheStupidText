namespace DinoReader.Core;

/// <summary>
/// Whether a shelf book's installed text is behind the store's (Slice 313), judged on every store listing the app fetches rather than weekly, so the text and the pre-made audio pack
/// of a book are never out of step. Pure: the app gathers the listing, the shelf ids and the recorded versions.
/// </summary>
public static class BookTextCurrency
{
    /// <summary>The store books on the shelf whose recorded version is below the store's (a book with no recorded version counts as the first release). The cheap pre-check before the full plan.</summary>
    /// <remarks>A book whose store version the grown-up's edit already settled (<paramref name="seenVersion"/> at or above it) is not behind: the plan would skip it as edited, so it must not force a plan on every run.</remarks>
    public static IReadOnlyList<StoreBookVersion> Behind(IEnumerable<StoreBookVersion> store, ISet<string> shelfIds, Func<string, int?> recordedVersion, Func<string, int?>? seenVersion = null) =>
        store.Where(s => shelfIds.Contains(s.Id) && StarterUpdate.NeedsUpdate(recordedVersion(s.Id), s.Version) && !(seenVersion?.Invoke(s.Id) is { } seen && s.Version <= seen)).ToList();

    /// <summary>
    /// The ids among <paramref name="candidates"/> whose text is still to be replaced after the plan: an update waiting (staged or not) or a baseline still to be fetched. A book the plan skips
    /// (edited, deleted, not in the store, already current) is not behind: its text will not change, so its pack is judged on its own.
    /// </summary>
    public static ISet<string> StillBehind(IReadOnlyList<BookUpdateDecision> decisions, IEnumerable<string> candidates)
    {
        var wanted = new HashSet<string>(candidates, StringComparer.OrdinalIgnoreCase);
        return decisions.Where(d => d.Action != BookUpdateAction.Skip && wanted.Contains(d.Id)).Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Slice 313: a book is given up for a store version only when the store has no usable package for that version, so its pack must not wait for a text that
/// cannot come. The stored note is that one version; a different (newer) store version is not given up, so it resets by itself. Every other failure (network, storage) is retried at the next listing pass and never given up.
/// </summary>
public static class BookTextGiveUp
{
    /// <summary>The note to store when the book is given up for this store version.</summary>
    public static string Note(int storeVersion) => storeVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>True only when the stored note is exactly this store version (nothing stored, another version or an unreadable note: not given up).</summary>
    public static bool IsGivenUp(string? stored, int storeVersion) => stored == Note(storeVersion);
}

/// <summary>How one behind book's update check ended (Slice 313), the first part of the <c>book_update</c> count's detail.</summary>
public static class BookUpdateResult
{
    /// <summary>The newer text is downloaded and waits to be swapped in.</summary>
    public const string Staged = "staged";

    /// <summary>The newer text is on the shelf.</summary>
    public const string Updated = "updated";

    /// <summary>The grown-up changed the book's text, so it is left as it is.</summary>
    public const string SkippedEdited = "skipped_edited";

    /// <summary>The download, the baseline or the swap failed; the next check tries again.</summary>
    public const string Failed = "failed";

    public static readonly IReadOnlyList<string> All = [Staged, Updated, SkippedEdited, Failed];
}

/// <summary>The detail of the <c>book_update</c> count (Slice 313): <c>&lt;result&gt;:&lt;from version&gt;:&lt;to version&gt;</c>, like <c>updated:5:6</c>. Versions only, never text or a title.</summary>
public static class BookUpdateUsage
{
    private const char Separator = ':';

    public static string Detail(string result, int fromVersion, int toVersion) =>
        string.Join(Separator, result, Math.Max(fromVersion, 0), Math.Max(toVersion, 0));
}
