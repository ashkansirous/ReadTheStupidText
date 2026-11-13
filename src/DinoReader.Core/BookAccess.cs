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

    /// <summary>A paid book whose price is not known (offline, or the store has not answered): a "connect to buy" note, no button.</summary>
    ConnectToBuy,

    /// <summary>The purchase is waiting; shown as waiting, re-read on the next open.</summary>
    Waiting,
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

    /// <summary>What the store card shows for the book; <paramref name="price"/> is the store's formatted price for the book's product, or null when not known.</summary>
    public static StoreCardPlan Plan(bool paidBooksOn, StoreItem item, bool onShelf, IReadOnlySet<string> owned, IReadOnlySet<string> pending, string? price)
    {
        BookState state = StateOf(paidBooksOn, item, onShelf, owned, pending);
        StoreAction action = state switch
        {
            BookState.Free or BookState.Owned => onShelf ? StoreAction.OnShelf : StoreAction.Get,
            BookState.KeptFromBefore => StoreAction.OnShelf,
            BookState.Pending => StoreAction.Waiting,
            _ => string.IsNullOrWhiteSpace(price) ? StoreAction.ConnectToBuy : StoreAction.Buy,
        };
        return new StoreCardPlan(state, action, action == StoreAction.Buy ? price : null);
    }

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

    /// <summary>The prices to show after a store read: the last known ones, overlaid with what the store just answered, so a failed or timed-out price query never blanks a card.</summary>
    public static IReadOnlyDictionary<string, string> KeepPrices(IReadOnlyDictionary<string, string> previous, IReadOnlyDictionary<string, string> fresh)
    {
        var merged = new Dictionary<string, string>(previous);
        foreach ((string product, string price) in fresh)
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

/// <summary>What the Worker's 402/403 answer for a sold book's files means (Slice 346, review): only a definitive refusal is permanent; anything else is tried again at the next check.</summary>
public static class StoreRefusal
{
    public const string PurchaseNotValidCode = "purchase-not-valid";
    public const string CannotVerifyCode = "cannot-verify";

    /// <summary>True only for the store's own "this purchase is not valid": the book is skipped for good. No proof yet, an unknown or unreadable code, or "cannot verify" all stay retryable.</summary>
    public static bool IsDefinitive(string? code) => code == PurchaseNotValidCode;

    /// <summary>The <c>code</c> of the Worker's JSON error body, or null when the body is not JSON, has no string <c>code</c>, or is empty. Never throws.</summary>
    public static string? CodeFrom(ReadOnlyMemory<byte> body)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(body);
            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && document.RootElement.TryGetProperty("code", out System.Text.Json.JsonElement code)
                && code.ValueKind == System.Text.Json.JsonValueKind.String
                    ? code.GetString()
                    : null;
        }
        catch (System.Text.Json.JsonException)
        {
            // no-fallback: an unreadable refusal body only means the code is unknown, which stays retryable; the caller reports the refusal
            return null;
        }
    }
}
