using DinoReader.Core;

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
/// purchase proof per owned product (the token the Worker checks with the store). The prices of the subscription plans are under <see cref="SubscriptionProducts.Monthly"/> and <see cref="SubscriptionProducts.Yearly"/>; while the subscription runs
/// its product is in <c>Owned</c> with its proof, and <c>Subscription</c> is the store's answer about it (null when this read did not ask, so nothing is concluded from silence; a store that knows none answers <see cref="SubscriptionStatus.None"/>). Held in memory only; <see cref="Reachable"/> is false when the store could not be asked; <see cref="PricesFailed"/> is true when it was asked but its price query ended in an error (Slice 358: a non-OK Play response code, a StoreKit request failure or timeout, a Windows Store error), as opposed to answering with no price; <see cref="Restricted"/> is true when the device forbids purchases (iOS Screen Time), which is an answer and not a fault; <see cref="TrialAvailable"/> (Slice 397) is the store's answer about the free trial for this account (Play lists the trial offer only for an eligible account), null when the store cannot say (Windows, whose HasTrialPeriod is not eligibility; iOS until StoreKit 2 answers; a read that did not ask), which keeps "Try free".
/// </summary>
public sealed record PurchaseSnapshot(
    IReadOnlyDictionary<string, string> Prices,
    IReadOnlySet<string> Owned,
    IReadOnlySet<string> Pending,
    IReadOnlyDictionary<string, string> Proofs,
    bool Reachable,
    bool PricesFailed = false,
    bool Restricted = false,
    SubscriptionStatus? Subscription = null,
    IReadOnlyDictionary<string, DateTimeOffset>? PurchasedAt = null,
    bool Incomplete = false,
    bool? TrialAvailable = null)
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
    /// button. No platform may prompt for a password here: iOS reads Apple's own entitlements silently (StoreKit 2, nothing is kept on the device) and only <see cref="RestoreAsync"/> asks Apple to sync, which may prompt.
    /// </summary>
    Task<PurchaseSnapshot> RefreshAsync(IReadOnlyCollection<string> productIds, bool interactive, CancellationToken cancel);

    /// <summary>Opens the store's own purchase sheet for the product. The caller re-reads with <see cref="RefreshAsync"/> afterwards.</summary>
    Task<BuyOutcome> BuyAsync(string productId, CancellationToken cancel);

    /// <summary>
    /// As above for a subscription plan, saying whether the card promised the free trial. <paramref name="withTrial"/> false must not start one (Android buys the base plan); a store with no such choice ignores it (slice 397 review 4).
    /// </summary>
    Task<BuyOutcome> BuyAsync(string productId, bool withTrial, CancellationToken cancel) => BuyAsync(productId, cancel);

    /// <summary>Brings back what the store account owns (iOS asks Apple to restore; Android and Windows re-read): same answer as <see cref="RefreshAsync"/>.</summary>
    Task<PurchaseSnapshot> RestoreAsync(IReadOnlyCollection<string> productIds, CancellationToken cancel);

    /// <summary>Opens the store's own "redeem a code" screen; false when the platform has none to open.</summary>
    Task<bool> RedeemCodeAsync();

    /// <summary>Slice 384: opens the store's own subscription page (cancel and change plan happen there, never in the app); false when it could not be opened.</summary>
    Task<bool> ManageSubscriptionAsync();
}
