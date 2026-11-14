namespace DinoReader.Core;

/// <summary>Whether the store sells a book (Slice 346): the listing's <c>access</c>. A list without the field (an older Worker) is all free.</summary>
public enum BookSale
{
    Free,
    Paid,
}

/// <summary>Where a listed book stands for this phone (Slice 346).</summary>
public enum BookState
{
    /// <summary>Not sold: a free book, or the paid-books switch is off.</summary>
    Free,

    /// <summary>Sold, and the store account that is signed in owns a product that unlocks it.</summary>
    Owned,

    /// <summary>Sold, not owned, not on the shelf: it can be bought.</summary>
    Buyable,

    /// <summary>Sold, a purchase is waiting for approval (Android pending purchase, iOS Ask to Buy).</summary>
    Pending,

    /// <summary>Sold, not owned, but already on the shelf from before the sale began: it stays readable, gets no updates.</summary>
    KeptFromBefore,
}

/// <summary>Where a book of an old (schema 1) backup is taken from by a restore (Slice 346).</summary>
public enum LegacyBookSource
{
    /// <summary>Download it from the store (free, or owned).</summary>
    Store,

    /// <summary>Install the book file the backup holds: the store cannot supply it and it is not a sold book the account lacks.</summary>
    File,

    /// <summary>Sold and not owned: list it as a book to get back; the backup's file is never installed.</summary>
    ListOnly,
}

/// <summary>What the store card offers for a book (Slice 346).</summary>
public enum StoreAction
{
    /// <summary>The usual Get button: download it.</summary>
    Get,

    /// <summary>The "Downloaded" mark: it is on the shelf.</summary>
    OnShelf,

    /// <summary>The Buy button with the store's own price.</summary>
    Buy,

    /// <summary>A paid book whose price is not known because the platform store has not answered (offline, no store, a timeout): a calm "connect to buy" note, no button.</summary>
    ConnectToBuy,

    /// <summary>Slice 358: a paid book with no price although the platform store did answer (product unknown or inactive, not for this account): a "can't be bought right now" note, no button.</summary>
    NotForSaleNow,

    /// <summary>The purchase is waiting; shown as waiting, re-read on the next open.</summary>
    Waiting,
}

/// <summary>How the latest read of the platform store went (Slice 358); the one input that decides between the "connect to the internet" strip and the "can't be bought right now" strip.</summary>
public enum StoreAnswer
{
    /// <summary>The store could not be reached (offline, no store, a timeout): the sand "connect to the internet" strip.</summary>
    NotReached,

    /// <summary>The store was reached but its price query ended in an error: the red strip, and a billing_error event.</summary>
    PriceQueryFailed,

    /// <summary>The device forbids purchases (iOS Screen Time): the red strip, no event.</summary>
    Restricted,

    /// <summary>The store answered the price query; a book with no price is not for sale now (red strip, prices_missing event).</summary>
    Answered,
}

/// <summary>A listed book's state and the card it gets; <see cref="Price"/> is the store's own localised string, only for <see cref="StoreAction.Buy"/>.</summary>
public sealed record StoreCardPlan(BookState State, StoreAction Action, string? Price);

/// <summary>
/// The pure rules for selling books (Slice 346, Decision 4): which listed books are sold, what the store page shows for one, and when a book on the shelf may be updated or
/// asked for with a purchase proof. Nothing is sold while the remote switch <c>paidBooks.enabled</c> is off, so every book is then <see cref="BookState.Free"/>. The store
/// owns the purchase state: <c>owned</c> and <c>pending</c> are product ids read from the platform store in memory, never kept.
/// </summary>
public static class BookAccess
{
    /// <summary>True when the book is sold right now: the switch is on and the listing says paid.</summary>
    public static bool IsSold(bool paidBooksOn, StoreItem item) => paidBooksOn && item.Sale == BookSale.Paid;

    /// <summary>The owned product that unlocks the book (the proof to send), or null when none does.</summary>
    public static string? OwnedProduct(StoreItem item, IReadOnlySet<string> owned) =>
        item.Products.FirstOrDefault(owned.Contains);

    /// <summary>Whether a request for the book's files must carry a purchase proof.</summary>
    public static bool NeedsProof(bool paidBooksOn, StoreItem item) => IsSold(paidBooksOn, item);

    /// <summary>The state of a listed book. A book on the shelf is never taken away: sold, not owned and on the shelf is <see cref="BookState.KeptFromBefore"/>.</summary>
    public static BookState StateOf(bool paidBooksOn, StoreItem item, bool onShelf, IReadOnlySet<string> owned, IReadOnlySet<string> pending)
    {
        if (!IsSold(paidBooksOn, item))
        {
            return BookState.Free;
        }

        if (OwnedProduct(item, owned) is not null)
        {
            return BookState.Owned;
        }

        if (onShelf)
        {
            return BookState.KeptFromBefore;
        }

        return item.Products.Any(pending.Contains) ? BookState.Pending : BookState.Buyable;
    }

    /// <summary>What the store card shows for the book; <paramref name="price"/> is the store's formatted price for the book's product, or null when not known; <paramref name="answer"/> is how the latest store read went (Slice 358): only a store that was not reached makes a missing price "connect to buy", every other way of having no price is "not for sale now".</summary>
    public static StoreCardPlan Plan(bool paidBooksOn, StoreItem item, bool onShelf, IReadOnlySet<string> owned, IReadOnlySet<string> pending, string? price, StoreAnswer answer = StoreAnswer.NotReached)
    {
        BookState state = StateOf(paidBooksOn, item, onShelf, owned, pending);
        StoreAction action = state switch
        {
            BookState.Free or BookState.Owned => onShelf ? StoreAction.OnShelf : StoreAction.Get,
            BookState.KeptFromBefore => StoreAction.OnShelf,
            BookState.Pending => StoreAction.Waiting,
            _ => BuyableAction(price, answer),
        };
        return new StoreCardPlan(state, action, action == StoreAction.Buy ? price : null);
    }

    // A device that forbids purchases never shows Buy, whatever price an older read left; otherwise a price means Buy, and no price depends on whether the store was reached.
    private static StoreAction BuyableAction(string? price, StoreAnswer answer)
    {
        if (answer == StoreAnswer.Restricted)
        {
            return StoreAction.NotForSaleNow;
        }

        if (HasPrice(price))
        {
            return StoreAction.Buy;
        }

        return answer == StoreAnswer.NotReached ? StoreAction.ConnectToBuy : StoreAction.NotForSaleNow;
    }

    /// <summary>The one definition of "has a price" (Slice 358): a blank or whitespace-only price is no price.</summary>
    public static bool HasPrice(string? price) => !string.IsNullOrWhiteSpace(price);

    /// <summary>The price the card shows for a book: the first non-blank price among its products (its own or a bundle's), or null.</summary>
    public static string? PriceOf(StoreItem item, IReadOnlyDictionary<string, string> prices) =>
        item.Products.Select(product => prices.GetValueOrDefault(product)).FirstOrDefault(HasPrice);

    /// <summary>Whether a book already on the shelf may be updated from the store: not sold, or owned. A kept old copy gets no updates (and no request that would be refused).</summary>
    public static bool MayUpdate(bool paidBooksOn, StoreItem item, IReadOnlySet<string> owned) =>
        !IsSold(paidBooksOn, item) || OwnedProduct(item, owned) is not null;

    /// <summary>The books the first-run picker offers: only books that are not sold (the four free ones), in the listing's order.</summary>
    public static IReadOnlyList<StoreItem> FirstRunOffer(bool paidBooksOn, IEnumerable<StoreItem> items) =>
        items.Where(item => !IsSold(paidBooksOn, item)).ToList();

    /// <summary>
    /// Whether the sale rules should assume books are sold: while no remote config has been read at all (a fresh install, offline) the switch is unknown, and the first-run picker and a restore
    /// then offer or fetch only books whose listing says free rather than guess "off" and ask for paid ones.
    /// </summary>
    public static bool AssumeSold(bool configKnown, bool paidBooksOn) => !configKnown || paidBooksOn;

    /// <summary>How a restore treats a schema 1 backup's book files: the store first only when the switch is known to be on; off or unknown is origin/main's behaviour, the files are imported.</summary>
    public static LegacyBookFiles LegacyFilesMode(bool configKnown, bool paidBooksOn) => configKnown && paidBooksOn ? LegacyBookFiles.StoreFirst : LegacyBookFiles.Restore;

    /// <summary>
    /// Where a book that a schema 1 backup holds comes from when the paid-books switch is on or unknown (Slice 346). A sold book the account does not own is only listed (its file is never installed);
    /// otherwise the store supplies it when it can, else the backup's own file does. <paramref name="access"/> comes from <see cref="LegacyAccess"/> (a book the
    /// listing does not name counts as free, so its file restores it). The restore asks once with <paramref name="storeCanSupply"/> true and again with false after a failed download.
    /// </summary>
    public static LegacyBookSource LegacyBookChoice(BookSale access, bool owned, bool storeCanSupply)
    {
        if (access == BookSale.Paid && !owned)
        {
            return LegacyBookSource.ListOnly;
        }

        return storeCanSupply ? LegacyBookSource.Store : LegacyBookSource.File;
    }

    /// <summary>
    /// What a restore treats a backed-up book as (check7 #6, decided in review): a book the listing names is sold or free as the listing says; a book it does not name (delisted, or the listing could not be
    /// read) counts as free, so the backup's own files restore it and a family never loses a book it holds (the same as with the switch off). A sold book the account does not own is a listed one, and is only listed.
    /// </summary>
    public static BookSale LegacyAccess(bool listed, bool soldPerListing) => listed && soldPerListing ? BookSale.Paid : BookSale.Free;

    /// <summary>How the latest store read counts as an answer (Slice 358), by one rule on every platform: a device with no internet has not reached the store; with internet a failed price query is a failure, then a restricted device, else answered.</summary>
    public static StoreAnswer AnswerOf(bool hasInternet, bool pricesFailed, bool restricted) =>
        !hasInternet ? StoreAnswer.NotReached : pricesFailed ? StoreAnswer.PriceQueryFailed : restricted ? StoreAnswer.Restricted : StoreAnswer.Answered;

    /// <summary>The ids of the sold books none of whose products has a (non-blank) price in a reachable store's answer (Slice 358); each one's card says "can't be bought right now". A book with one priced product (its own or a bundle) is not listed, since its card shows Buy.</summary>
    public static string[] UnpricedBooks(IEnumerable<StoreItem> items, IReadOnlyDictionary<string, string> prices) =>
        [.. items.Where(item => item.Sale == BookSale.Paid && PriceOf(item, prices) is null).Select(item => item.Id)];

    /// <summary>The prices to show after a store read: the last known ones, overlaid with what the store just answered, so a failed or timed-out price query never blanks a card.</summary>
    public static IReadOnlyDictionary<string, string> KeepPrices(IReadOnlyDictionary<string, string> previous, IReadOnlyDictionary<string, string> fresh)
    {
        var merged = previous.Where(p => HasPrice(p.Value)).ToDictionary(p => p.Key, p => p.Value); // a blank price is no price: it neither is kept nor replaces a real one
        foreach ((string product, string price) in fresh.Where(p => HasPrice(p.Value)))
        {
            merged[product] = price;
        }

        return merged;
    }

    /// <summary>The header value for a request about a sold book: <c>&lt;store&gt;:&lt;productId&gt;:&lt;token&gt;</c>, or null when the book is not sold or no proof is held.</summary>
    public static string? ProofHeader(bool paidBooksOn, StoreItem item, IReadOnlyDictionary<string, string> proofs, string store)
    {
        if (!NeedsProof(paidBooksOn, item))
        {
            return null;
        }

        string? product = item.Products.FirstOrDefault(proofs.ContainsKey);
        return product is null ? null : $"{store}:{PurchaseProofs.ProductPart(store, product)}:{proofs[product]}";
    }
}

/// <summary>The header every request for a sold book's files carries (Slice 346). The Worker reads it as <c>&lt;store&gt;:&lt;productId&gt;:&lt;token&gt;</c>.</summary>
public static class PurchaseProofs
{
    public const string HeaderName = "X-Purchase-Proof";
    public const string AndroidStore = "android";
    public const string IosStore = "ios";
    public const string WindowsStore = "windows";

    /// <summary>Windows' Store ID key covers every product, so its proof names none; the others name the product the token was issued for.</summary>
    public static string ProductPart(string store, string productId) => store == WindowsStore ? string.Empty : productId;
}
