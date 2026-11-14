namespace DinoReader.Core;

/// <summary>The page the first-books picker shows once Continue has ended.</summary>
public enum ContinueEnd
{
    KeepGetting,
    KeepOffline,
    ShowPicker,
}

/// <summary>
/// The first-books rules (Slice 261): a fresh install starts with an empty shelf and a grown-up picks at least <see cref="Minimum"/> books from the store before the
/// app goes on. The library on disk is the one owner of the state (<see cref="IsSatisfied"/>); the picker only decides what is ticked and what still has to be fetched.
/// Pure, so the picker's count line, its Continue button and the resume after a kill are unit-tested here.
/// </summary>
public static class FirstBooks
{
    /// <summary>How many books a fresh install must hold before the first run goes on.</summary>
    public const int Minimum = 2;

    /// <summary>True once the shelf holds enough books: the first-run book step is done and is never shown again.</summary>
    public static bool IsSatisfied(int booksOnShelf) => booksOnShelf >= Minimum;

    /// <summary>True when Continue is enabled: from <see cref="Minimum"/> ticks (a third or fourth tick is fine).</summary>
    public static bool CanContinue(int ticked) => ticked >= Minimum;

    /// <summary>How many more ticks are needed before Continue works ("Pick 2 books to start", "Pick 1 more"); 0 once enough.</summary>
    public static int StillNeeded(int ticked) => Math.Max(0, Minimum - Math.Max(0, ticked));

    /// <summary>The store's books as the picker lists them: tagged ones first (Slice 260), every group keeping the listing's order.</summary>
    public static IReadOnlyList<StoreItem> Order(IEnumerable<StoreItem> items) => StoreTags.TaggedFirst(items, item => item.TagList);

    /// <summary>The ids of listed books already on the shelf: after a kill mid-download they come back ticked and counted (the books that landed stay on disk).</summary>
    public static IReadOnlyList<string> AlreadyOnShelf(IEnumerable<StoreItem> items, ISet<string> shelfIds) =>
        items.Where(item => shelfIds.Contains(item.Id)).Select(item => item.Id).ToList();

    /// <summary>The ticked books still to fetch, in the picker's order: a ticked book already on the shelf is not fetched again.</summary>
    public static IReadOnlyList<StoreItem> ToDownload(IEnumerable<StoreItem> items, ISet<string> tickedIds, ISet<string> shelfIds) =>
        items.Where(item => tickedIds.Contains(item.Id) && !shelfIds.Contains(item.Id)).ToList();

    /// <summary>The summed package size of the ticked books, or null when any of them has no size in the listing (the count line then shows no size).</summary>
    public static long? TotalBytes(IEnumerable<StoreItem> items, ISet<string> tickedIds)
    {
        List<StoreItem> ticked = items.Where(item => tickedIds.Contains(item.Id)).ToList();
        return ticked.Count > 0 && ticked.All(item => item.Bytes is > 0) ? ticked.Sum(item => item.Bytes!.Value) : null;
    }

    /// <summary>
    /// Which page the picker shows when Continue ends (one table, one input that says whether the page really handed over): handed over (Completed returned) -> the getting page stays, the page is leaving;
    /// otherwise the offline page is up (the connection dropped meanwhile) -> it stays; anything else (cancel, failure, exception, shelf not satisfied, Completed threw) -> the picker.
    /// </summary>
    public static ContinueEnd EndOfContinue(bool handedOver, bool offlinePageShown) =>
        handedOver ? ContinueEnd.KeepGetting
        : offlinePageShown ? ContinueEnd.KeepOffline
        : ContinueEnd.ShowPicker;

    /// <summary>"N of M" for the download wait: the 1-based number of the book being fetched, never past the total.</summary>
    public static int BookNumber(int landed, int total) => Math.Min(landed + 1, Math.Max(total, 1));
}
