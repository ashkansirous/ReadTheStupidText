namespace DinoReader.Core;

/// <summary>The store's age chips (Slice 112, Decision 116): Google Play's bands, plus "All ages" for no filter.</summary>
public enum StoreAgeBand
{
    All,
    UpTo5,
    From6To8,
    From9To12,
}

/// <summary>The result of <see cref="StoreFilter.Resolve"/>: the band and author in force, the bands that have a story, whether the chips show, and the authors the picker offers.</summary>
public sealed record StoreFilterState(
    StoreAgeBand Band,
    string? Author,
    IReadOnlyList<StoreAgeBand> VisibleBands,
    bool ChipsShown,
    IReadOnlyList<string> AuthorsInBand,
    bool AuthorRowShown);

/// <summary>Filters the fetched store list on the phone (Slice 112): by age band and by author, so it works on the cached list offline.</summary>
public static class StoreFilter
{
    private const int UpTo5Max = 5;
    private const int From6Min = 6;
    private const int From6Max = 8;
    private const int From9Min = 9;
    private const int From9Max = int.MaxValue; // the mock's "9–12" chip is open-ended, so 13+ stories still show under it

    /// <summary>The items in the band and by the author, in list order. <see cref="StoreAgeBand.All"/> and a null author each filter nothing.</summary>
    public static IReadOnlyList<StoreItem> Apply(IEnumerable<StoreItem> items, StoreAgeBand band, string? author) => Apply(items, i => i, band, author);

    /// <summary>The same filter over any rows that carry a <see cref="StoreItem"/>, so the page and the tests share one rule.</summary>
    public static IReadOnlyList<T> Apply<T>(IEnumerable<T> rows, Func<T, StoreItem> itemOf, StoreAgeBand band, string? author) =>
        rows.Where(r => InBand(itemOf(r), band) && (author is null || SameAuthor(itemOf(r).Author, author))).ToList();

    /// <summary>Authors are the same when they differ only in letter case.</summary>
    public static bool SameAuthor(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the item's range overlaps the band ("9–12" is open-ended upward); an item with no range shows under every band.</summary>
    public static bool InBand(StoreItem item, StoreAgeBand band)
    {
        if (band == StoreAgeBand.All || item.AgeMin is not { } min || item.AgeMax is not { } max)
        {
            return true;
        }

        (int bandMin, int bandMax) = Range(band);
        return min <= bandMax && max >= bandMin;
    }

    /// <summary>The bands (never <see cref="StoreAgeBand.All"/>) that hold at least one story with an age range, in chip order. A story with no range shows under every band, so it does not count (Slice 394).</summary>
    public static IReadOnlyList<StoreAgeBand> BandsWithBooks(IEnumerable<StoreItem> items)
    {
        StoreItem[] ranged = items.Where(i => i.AgeMin is not null && i.AgeMax is not null).ToArray();
        return Enum.GetValues<StoreAgeBand>().Where(b => b != StoreAgeBand.All && ranged.Any(i => InBand(i, b))).ToList();
    }

    /// <summary>The selected band, or <see cref="StoreAgeBand.All"/> when it holds no story any more (Slice 394).</summary>
    public static StoreAgeBand EffectiveBand(StoreAgeBand selected, IReadOnlyCollection<StoreAgeBand> bandsWithBooks) =>
        selected == StoreAgeBand.All || bandsWithBooks.Contains(selected) ? selected : StoreAgeBand.All;

    /// <summary>What the store page shows for a requested band and author (Slice 394): the one place that decides it, so chips, author row and list always agree.</summary>
    public static StoreFilterState Resolve(IReadOnlyList<StoreItem> items, StoreAgeBand requestedBand, string? requestedAuthor)
    {
        IReadOnlyList<StoreAgeBand> bands = BandsWithBooks(items);
        StoreAgeBand band = EffectiveBand(requestedBand, bands);
        IReadOnlyList<string> authors = Authors(Apply(items, band, null));
        bool authorRowShown = authors.Count >= 2; // one author filters nothing
        string? author = authorRowShown ? authors.FirstOrDefault(a => SameAuthor(a, requestedAuthor)) : null;
        return new StoreFilterState(band, author, bands, bands.Count >= 2, authors, authorRowShown);
    }

    /// <summary>The distinct authors in the list (letter case ignored), sorted; items without one add none.</summary>
    public static IReadOnlyList<string> Authors(IEnumerable<StoreItem> items) =>
        items.Select(i => i.Author).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();

    private static (int Min, int Max) Range(StoreAgeBand band) => band switch
    {
        StoreAgeBand.UpTo5 => (0, UpTo5Max),
        StoreAgeBand.From6To8 => (From6Min, From6Max),
        StoreAgeBand.From9To12 => (From9Min, From9Max),
        _ => throw new ArgumentOutOfRangeException(nameof(band)),
    };
}
