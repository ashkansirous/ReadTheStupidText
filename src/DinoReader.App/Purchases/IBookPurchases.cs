namespace DinoReader;

/// <summary>How a Buy ended (Slice 346). Nothing changes on <see cref="Cancelled"/> and <see cref="Failed"/>; <see cref="Pending"/> is shown as waiting and re-read on the next open.</summary>
public enum BuyOutcome
{
    Bought,
    Cancelled,
    Pending,
    Failed,
}

/// <summary>
/// What the platform store said right now (Slice 346): the store's own formatted price per product, the products the signed-in store account owns, the ones waiting for approval, and a
/// purchase proof per owned product (the token the Worker checks with the store). Held in memory only; <see cref="Reachable"/> is false when the store could not be asked.
/// </summary>
public sealed record PurchaseSnapshot(
    IReadOnlyDictionary<string, string> Prices,
    IReadOnlySet<string> Owned,
    IReadOnlySet<string> Pending,
    IReadOnlyDictionary<string, string> Proofs,
    bool Reachable)
{
    public static PurchaseSnapshot Empty { get; } = new(
        new Dictionary<string, string>(), new HashSet<string>(), new HashSet<string>(), new Dictionary<string, string>(), false);
}

/// <summary>
/// The platform store behind paid books (Slice 346): Google Play Billing on Android, StoreKit on iOS, <c>Windows.Services.Store</c> on Windows. The store is the one owner of the purchase
/// state; an implementation keeps no purchase database. Every method returns instead of throwing for a store that cannot be reached.
/// </summary>
public interface IBookPurchases
{
    /// <summary>The store name the Worker expects in a purchase proof: <c>android</c>, <c>ios</c> or <c>windows</c> (<see cref="DinoReader.Core.PurchaseProofs"/>).</summary>
    string StoreName { get; }

    /// <summary>
    /// Asks the store for the prices of <paramref name="productIds"/> and for everything the account owns. <paramref name="interactive"/> is true when a grown-up opened the store page or pressed a
    /// button. No platform may prompt for a password here: iOS reads its kept list of seen purchases (StoreKit 1 has no silent owned list) and only <see cref="RestoreAsync"/> replays with Apple.
    /// </summary>
    Task<PurchaseSnapshot> RefreshAsync(IReadOnlyCollection<string> productIds, bool interactive, CancellationToken cancel);

    /// <summary>Opens the store's own purchase sheet for the product. The caller re-reads with <see cref="RefreshAsync"/> afterwards.</summary>
    Task<BuyOutcome> BuyAsync(string productId, CancellationToken cancel);

    /// <summary>Brings back what the store account owns (iOS asks Apple to restore; Android and Windows re-read): same answer as <see cref="RefreshAsync"/>.</summary>
    Task<PurchaseSnapshot> RestoreAsync(IReadOnlyCollection<string> productIds, CancellationToken cancel);

    /// <summary>Opens the store's own "redeem a code" screen; false when the platform has none to open.</summary>
    Task<bool> RedeemCodeAsync();
}
