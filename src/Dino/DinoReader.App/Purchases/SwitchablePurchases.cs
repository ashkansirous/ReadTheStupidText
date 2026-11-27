namespace DinoReader;

/// <summary>
/// The platform store, with one seam for the Debug test hook (Slice 346): <see cref="Override"/> is null in every Release build, and a scripted device flow sets a fake store there
/// so a purchase can be tested without money.
/// </summary>
public sealed class SwitchablePurchases(IBookPurchases real) : IBookPurchases, IDisposable
{
    /// <summary>Lets the platform store (iOS: the static StoreKit update subscription) release what it holds when the container disposes this.</summary>
    public void Dispose() => (real as IDisposable)?.Dispose();

    /// <summary>Set only by the Debug <c>fake-purchases</c> hook.</summary>
    public IBookPurchases? Override { get; set; }

    private IBookPurchases Current => Override ?? real;

    public string StoreName => real.StoreName;

    public Task<PurchaseSnapshot> RefreshAsync(IReadOnlyCollection<string> productIds, bool interactive, CancellationToken cancel) => Current.RefreshAsync(productIds, interactive, cancel);

    public Task<BuyOutcome> BuyAsync(string productId, CancellationToken cancel) => Current.BuyAsync(productId, cancel);

    public Task<BuyOutcome> BuyAsync(string productId, bool withTrial, CancellationToken cancel) => Current.BuyAsync(productId, withTrial, cancel);

    public BuyNote LastBuyNote => Current.LastBuyNote;

    public IReadOnlyList<string> CodeRedemptions => Current.CodeRedemptions;

    public Task<PurchaseSnapshot> RestoreAsync(IReadOnlyCollection<string> productIds, CancellationToken cancel) => Current.RestoreAsync(productIds, cancel);

    public Task<bool> RedeemCodeAsync() => Current.RedeemCodeAsync();

    public Task<bool> ManageSubscriptionAsync() => Current.ManageSubscriptionAsync();
}

/// <summary>Makes the platform's store (implemented once per head under <c>Platforms/&lt;OS&gt;/Purchases</c>).</summary>
public static partial class PlatformPurchases
{
    public static partial IBookPurchases Create();
}
