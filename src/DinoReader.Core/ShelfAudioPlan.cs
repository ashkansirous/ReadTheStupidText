using System.Collections.Immutable;

namespace DinoReader.Core;

/// <summary>Where the fetch of a language's pre-made audio for the shelf books stands (Slice 262).</summary>
public enum ShelfAudioPhase
{
    Idle,
    Preparing,
    Running,
    WaitingForInternet,
    WaitingForWifi,
    Cancelled,
}

/// <summary>
/// What the Story languages row shows about one language's shelf audio: the phase, "<see cref="Done"/> of <see cref="Total"/>" books that needed a pack, the book in progress, and the books
/// the store has no pack for yet (they read with the reading voice), and how many books gave up after their tries (<see cref="Failed"/>). Immutable (the title list is an
/// <see cref="ImmutableArray{T}"/>), so the run thread and the main thread can share one record safely.
/// </summary>
public sealed record ShelfAudioState(
    ShelfAudioPhase Phase, int Done = 0, int Total = 0, string? CurrentTitle = null, ImmutableArray<string> NoAudioTitles = default, int Failed = 0)
{
    public static readonly ShelfAudioState Idle = new(ShelfAudioPhase.Idle);

    /// <summary>The books with no pack yet, in shelf order; empty when none.</summary>
    public IReadOnlyList<string> NoAudio => NoAudioTitles.IsDefault ? [] : NoAudioTitles;

    /// <summary>Whether the in-row card shows its bar and Cancel: a run is preparing, going or waiting.</summary>
    public bool IsActive => Phase is ShelfAudioPhase.Preparing or ShelfAudioPhase.Running or ShelfAudioPhase.WaitingForInternet or ShelfAudioPhase.WaitingForWifi;

    /// <summary>The bar's share, 0 to 1 (0 when nothing needed a pack). A book that gave up still moves the bar.</summary>
    public double Fraction => Total <= 0 ? 0 : Math.Clamp((double)(Done + Failed) / Total, 0, 1);

    /// <summary>The 1-based book in progress, never past the total.</summary>
    public int Position => Math.Min(Done + Failed + 1, Total);

    /// <summary>The same state with one more book done; the book in progress is cleared.</summary>
    public ShelfAudioState WithBookDone() => this with { Done = Math.Min(Total - Failed, Done + 1), CurrentTitle = null };

    /// <summary>The same state with one more book given up after its tries (not counted done); the book in progress is cleared.</summary>
    public ShelfAudioState WithBookFailed() => this with { Failed = Math.Min(Total - Done, Failed + 1), CurrentTitle = null };
}

/// <summary>How long to wait before the next try of something that failed (Slice 262): 15 s, 30 s, 60 s ... up to 5 minutes.</summary>
public static class ShelfAudioBackoff
{
    public static readonly TimeSpan First = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan Max = TimeSpan.FromMinutes(5);

    /// <summary>The wait after <paramref name="attempt"/> failed tries (0 = the first failure).</summary>
    public static TimeSpan Delay(int attempt)
    {
        int doublings = Math.Clamp(attempt, 0, 16);
        double seconds = First.TotalSeconds * Math.Pow(2, doublings);
        return TimeSpan.FromSeconds(Math.Min(seconds, Max.TotalSeconds));
    }
}

/// <summary>A shelf book that needs a pack in a language, and the listed pack to fetch.</summary>
public sealed record ShelfAudioPick(StoreItem Item, AudioPackInfo Pack);

/// <summary>The result of <see cref="ShelfAudioPlan.For"/>: the packs to fetch (shelf order) and the titles of shelf books the store has no pack for.</summary>
public sealed record ShelfAudioWork(IReadOnlyList<ShelfAudioPick> Needed, IReadOnlyList<string> NoAudioTitles);

/// <summary>
/// Which shelf books still lack their pre-made audio in a language (Slice 262), from the store listing, the ids on the shelf and what is installed. Pure: the plan is recomputed from
/// the files whenever it is needed, so a killed run resumes by asking again, with no queue to keep.
/// </summary>
public static class ShelfAudioPlan
{
    /// <summary>
    /// A shelf book is <b>needed</b> when the listing offers a pack for <paramref name="language"/> and none, or an older version, is installed; it has <b>no audio</b> when the listing offers
    /// none and none is installed. A book already holding a pack the listing no longer offers is neither. Books the shelf does not hold are ignored.
    /// </summary>
    /// <param name="installedVersion">The installed pack version of (book id, language), or null.</param>
    /// <param name="titleLanguage">The screen language the book titles are named in (the listing's title in it, else the story's own).</param>
    /// <param name="textBehind">Slice 313: the shelf books whose text is older than the store's. Their pack is made for the newer text, so it is not fetched yet (the text comes first, then the pack); an installed pack is kept.</param>
    public static ShelfAudioWork For(
        IEnumerable<StoreItem> listing, ISet<string> shelfIds, string language, Func<string, string, int?> installedVersion, string titleLanguage, ISet<string>? textBehind = null)
    {
        string key = BundledTranslations.Key(language);
        var needed = new List<ShelfAudioPick>();
        var none = new List<string>();
        foreach (StoreItem item in listing.Where(i => shelfIds.Contains(i.Id) && textBehind?.Contains(i.Id) != true))
        {
            int? have = installedVersion(item.Id, key);
            AudioPackInfo? pack = item.AudioIn(key);
            if (pack is not null && (have is null || have < pack.Version))
            {
                needed.Add(new ShelfAudioPick(item, pack));
            }
            else if (pack is null && have is null && BundledTranslations.Key(item.Language) != key) // a main language equal to the book's own needs no note: nothing is missing
            {
                none.Add(item.TitleIn(titleLanguage) ?? item.Title);
            }
        }

        return new ShelfAudioWork(needed, none);
    }
}
