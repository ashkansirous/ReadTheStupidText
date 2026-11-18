#if DINO_TEST_BOOKS
using DinoReader.Core;

namespace DinoReader.Testing;

/// <summary>
/// A pretend store for scripted device flows (Slice 346): which test books are sold, which the account owns, what a Buy does, so Buy, Restore, the wait overlay and the picker can be checked with no money
/// and no real store. Switched on by the Debug <c>fake-purchases</c> hook (<see cref="SwitchablePurchases.Override"/> and <see cref="BookSales.ForcedEnabled"/>); it lives only in a Debug build (or the
/// Windows VM test package, which writes no [Purchases] markers) and in this process. The Worker is never asked: the test store's books come from the app's own assets.
/// </summary>
internal sealed class FakePurchases : IBookPurchases
{
    internal const string ProductPrefix = "uk.sirous.dinoreader.book.test-";
    private const string Marker = "[Purchases]";

    // Stems or book ids exactly as the hook was given them, swapped whole (set before any await in the hook, so the next hook always sees them).
    private static volatile HashSet<string> _paidNames = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> OwnedProducts { get; } = new(StringComparer.Ordinal);

    public HashSet<string> PendingProducts { get; } = new(StringComparer.Ordinal);

    public string Price { get; set; } = "$0.99";

    public BuyOutcome NextBuy { get; set; } = BuyOutcome.Bought;

    public bool Offline { get; set; }

    /// <summary>Slice 397: whether the pretend store allows the free trial (false = the account used it: the plain-price card). The <c>trial=used</c> hook switches it off.</summary>
    public bool TrialAvailable { get; set; } = true;

    public string StoreName => PurchaseProofs.AndroidStore;

    public static string ProductOf(string bookId) => ProductPrefix + bookId;

    /// <summary>Replaces the set of sold test books (stems or book ids; no asset is read, so it is instant).</summary>
    public static void SetPaid(IEnumerable<string> stemsOrIds) => _paidNames = new HashSet<string>(stemsOrIds, StringComparer.OrdinalIgnoreCase);

    /// <summary>The test store's line for a book, sold with its fake product when the hook said so (by its stem or its id).</summary>
    public static StoreItem Marked(StoreItem item, string stem) =>
        _paidNames.Contains(item.Id) || _paidNames.Contains(stem) ? item with { Sale = BookSale.Paid, ProductId = ProductOf(item.Id), UnlockedBy = [ProductOf(item.Id), .. SubscriptionProducts.All] } : item; // like the Worker's list: the subscription products unlock every paid book (Slice 384)

    public Task<PurchaseSnapshot> RefreshAsync(IReadOnlyCollection<string> productIds, bool interactive, CancellationToken cancel) => Task.FromResult(Snapshot(productIds));

    public Task<PurchaseSnapshot> RestoreAsync(IReadOnlyCollection<string> productIds, CancellationToken cancel) => Task.FromResult(Snapshot(productIds));

    /// <summary>Slice 384: the pretend subscription (set by a Buy of a plan, or the <c>fake-subscription</c> hook); null = the store knows none. The plan products' prices are fixed.</summary>
    public SubscriptionStatus? Subscription { get; set; }

    private const string MonthlyPrice = "£1.99";
    private const string YearlyPrice = "£14.99";

    public Task<bool> ManageSubscriptionAsync()
    {
        WriteMarker($"{Marker} manage subscription opened");
        return Task.FromResult(true);
    }

    public Task<BuyOutcome> BuyAsync(string productId, CancellationToken cancel) => BuyAsync(productId, withTrial: true, cancel);

    // Slice 397 item 7: like Android, withTrial false buys the plain plan (no trial), and the marker says which one ran, so a flow catches an Ended card that starts a trial.
    public async Task<BuyOutcome> BuyAsync(string productId, bool withTrial, CancellationToken cancel)
    {
        await Task.Delay(800, cancel); // long enough for the wait overlay (it appears after 500 ms)
        if (NextBuy == BuyOutcome.Bought && SubscriptionProducts.Is(productId))
        {
            Subscription = withTrial
                ? new SubscriptionStatus(SubscriptionState.Trial, DateTimeOffset.UtcNow.AddDays(7), productId)
                : new SubscriptionStatus(SubscriptionState.Active, DateTimeOffset.UtcNow.AddDays(productId == SubscriptionProducts.Yearly ? 365 : 30), productId);
            if (withTrial)
            {
                // Side effect: the account has used its trial now, so the pretend store reports TrialAvailable=false and
                // the card shows TrialUsed (not Offer) until fake-subscription trial= resets it; a flow that expects an Offer card with trial=True after a trial buy must re-run that hook first.
                TrialAvailable = false;
            }

            WriteMarker($"{Marker} subscribe product={productId} withTrial={withTrial}");
        }
        else if (NextBuy == BuyOutcome.Bought)
        {
            OwnedProducts.Add(productId);
        }
        else if (NextBuy == BuyOutcome.Pending)
        {
            PendingProducts.Add(productId);
        }

        WriteMarker($"{Marker} buy product={productId} outcome={NextBuy}");
        return NextBuy;
    }

    public Task<bool> RedeemCodeAsync()
    {
        WriteMarker($"{Marker} redeem opened");
        return Task.FromResult(true);
    }

    // Mobile.Shared's TestHookLog is compiled in Debug only (Slice 162), so a Release DINO_TEST_BOOKS package (the Windows VM build, Slice 353) writes no [Purchases] markers; the calls are compiled out there. (Windows has no TestHookLog sink even in Debug.) VM flows must not assert on these lines.
    private static void WriteMarker(string line)
    {
#if DEBUG
        Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write(line);
#endif
    }

    private PurchaseSnapshot Snapshot(IReadOnlyCollection<string> productIds)
    {
        if (Offline)
        {
            return PurchaseSnapshot.Empty;
        }

        Dictionary<string, string> prices = productIds.Where(p => !SubscriptionProducts.Is(p)).ToDictionary(p => p, _ => Price);
        if (productIds.Any(SubscriptionProducts.Is))
        {
            prices[SubscriptionProducts.Monthly] = MonthlyPrice;
            prices[SubscriptionProducts.Yearly] = YearlyPrice;
        }

        var owned = new HashSet<string>(OwnedProducts);
        Dictionary<string, string> proofs = OwnedProducts.ToDictionary(p => p, p => "fake-token-" + p);
        SubscriptionStatus status = Subscription ?? SubscriptionStatus.None;
        if (SubscriptionAccess.IsEntitled(status.State) && status.ProductId is { } plan)
        {
            owned.Add(plan);
            proofs[plan] = "fake-sub-token-" + plan;
        }

        return new PurchaseSnapshot(prices, owned, new HashSet<string>(PendingProducts), proofs, true, Subscription: status, TrialAvailable: productIds.Any(SubscriptionProducts.Is) ? TrialAvailable : null);
    }
}
#endif
