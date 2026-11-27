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

    /// <summary>Slice 459: a prefetch in one of these states can be joined by Continue (a Failed or Cancelled one never is: Retry starts from zero).</summary>
    public static bool IsJoinable(PrefetchState state) => state is PrefetchState.Listing or PrefetchState.Downloading or PrefetchState.Landed;

    /// <summary>Slice 459: Continue joins the prefetch only when it is joinable and fetches the pack of the main language read at Continue time (else it is cancelled and the normal path runs).</summary>
    public static bool JoinsPrefetch(PrefetchState state, string? prefetchLanguage, string mainLanguage) =>
        IsJoinable(state) && string.Equals(prefetchLanguage, mainLanguage, StringComparison.OrdinalIgnoreCase);

    /// <summary>Slice 459: the first book is ready to open once it is on the shelf and its main-language pack has ended (installed, not offered, or failed: a pack failure keeps the book).</summary>
    public static bool IsFirstBookReady(bool bookOnShelf, bool mainPackEnded) => bookOnShelf && mainPackEnded;

    /// <summary>What the prefetch does when the grown-up unticks <paramref name="unticked"/> (Decision 1: "cancel and delete it"): only the prefetched book matters.</summary>
    public static PrefetchAction OnUntick(string? prefetchedId, PrefetchState state, string unticked, bool installedByRun)
    {
        if (prefetchedId is null || !string.Equals(prefetchedId, unticked, StringComparison.OrdinalIgnoreCase))
        {
            return PrefetchAction.Keep;
        }

        return state switch
        {
            PrefetchState.Listing or PrefetchState.Downloading => PrefetchAction.Cancel,
            PrefetchState.Landed => installedByRun ? PrefetchAction.CancelAndRemove : PrefetchAction.Cancel, // a book that was on the shelf before this run stays
            _ => PrefetchAction.Keep,
        };
    }

    /// <summary>What the prefetch does when the language step saves a pick: a live or landed run for another main language restarts with the new one; the same language changes nothing; a finished-badly or idle one starts fresh.</summary>
    public static PrefetchStart OnLanguageSaved(PrefetchState state, string? runLanguage, string newMain) =>
        state switch
        {
            PrefetchState.Listing or PrefetchState.Downloading or PrefetchState.Landed =>
                string.Equals(runLanguage, newMain, StringComparison.OrdinalIgnoreCase) ? PrefetchStart.Keep : PrefetchStart.Restart,
            _ => PrefetchStart.Begin,
        };

    /// <summary>Which line the shelf's queue banner shows: nothing with no job left, the book count while books are still coming, else the audio line (only packs are left).</summary>
    public static QueueLine LineFor(int jobsRemaining, int booksRemaining, int jobsFailed = 0) =>
        jobsRemaining <= 0 ? (jobsFailed > 0 ? QueueLine.Failed : QueueLine.Hidden)
        : booksRemaining <= 0 ? QueueLine.Audio
        : booksRemaining == 1 ? QueueLine.OneBook
        : QueueLine.ManyBooks;

    /// <summary>
    /// Slice 459, the Continue plan: what the wait screen finishes first and what runs in the background after it. <paramref name="tickedInOrder"/> are the ticked ids in tick order, <paramref name="languages"/> the family's
    /// languages with the main one first. The first book is the prefetched one when it is ticked and joinable, else the first ticked book; once it is on the shelf with its main pack the wait ends. The background list is the
    /// other ticked books not on the shelf (each with its main pack), then every other language's pack of every ticked book, language by language.
    /// </summary>
    public static FirstBookPlan PlanContinue(IReadOnlyList<string> tickedInOrder, ISet<string> onShelf, string? prefetchedId, PrefetchState prefetchState, IReadOnlyList<string> languages, string? prefetchLanguage)
    {
        if (tickedInOrder.Count == 0 || languages.Count == 0)
        {
            return new FirstBookPlan(null, []);
        }

        string? joined = prefetchedId is not null && JoinsPrefetch(prefetchState, prefetchLanguage, languages[0]) ? tickedInOrder.FirstOrDefault(id => string.Equals(id, prefetchedId, StringComparison.OrdinalIgnoreCase)) : null;
        string first = joined ?? tickedInOrder[0];
        FirstBookSource source = joined is not null ? FirstBookSource.JoinPrefetch : onShelf.Contains(first) ? FirstBookSource.OnShelf : FirstBookSource.Download;
        List<BackgroundJob> background = [];
        background.AddRange(tickedInOrder.Where(id => !string.Equals(id, first, StringComparison.OrdinalIgnoreCase) && !onShelf.Contains(id)).Select(id => new BackgroundJob(id, BackgroundJobKind.Package, null)));
        foreach (string language in languages.Skip(1))
        {
            background.AddRange(tickedInOrder.Select(id => new BackgroundJob(id, BackgroundJobKind.Pack, language)));
        }

        return new FirstBookPlan(new FirstBookStep(first, source, languages[0]), background);
    }
}

/// <summary>Slice 459: where the first book's prefetch is (one owner, <c>FirstBookPrefetch</c> in the app).</summary>
public enum PrefetchState
{
    Idle,
    Listing,
    Downloading,
    Landed,
    Failed,
    Cancelled,
}

public enum QueueLine
{
    Hidden,
    OneBook,
    ManyBooks,
    Audio,
    Failed,
}

public enum PrefetchAction
{
    Keep,
    Cancel,
    CancelAndRemove,
}

public enum PrefetchStart
{
    Keep,
    Begin,
    Restart,
}

public enum FirstBookSource
{
    JoinPrefetch,
    OnShelf,
    Download,
}

public enum BackgroundJobKind
{
    Package,
    Pack,
}

public enum BackgroundResult
{
    Done,
    Failed,
    Cancelled,
}

/// <summary>The one book the wait screen finishes first: where it comes from and the language of the pack it needs.</summary>
public sealed record FirstBookStep(string BookId, FirstBookSource Source, string MainLanguage);

/// <summary>One background fetch: a book's package (with its main pack) or one language's pack of a book on the shelf. <paramref name="Attempts"/> counts the times it failed (kept across launches; <see cref="BackgroundJobRules.MaxRetryLaunches"/>).</summary>
public sealed record BackgroundJob(string BookId, BackgroundJobKind Kind, string? Language, int Attempts = 0);

public sealed record FirstBookPlan(FirstBookStep? First, IReadOnlyList<BackgroundJob> Background);
