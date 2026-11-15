using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// StoreKit 2 behind <see cref="IBookPurchases"/> (Slice 390, replacing the StoreKit 1 code of Slice 346). StoreKit 2 is Swift-only, so the calls go to our own wrapper, <c>DinoStoreKit.xcframework</c>
/// (<c>Platforms/iOS/StoreKit2</c>), through <see cref="StoreKit2Bridge"/>. The proof for a product is Apple's signed transaction (JWS) of its newest transaction; the Worker verifies the signature chain to Apple's root and
/// then asks the App Store Server API (transaction ids from a build before this one are still accepted). The Apple ID's entitlements are read silently from Apple's own list on every refresh, so nothing is kept on the device and
/// a new device sees its purchases at once; only the Restore purchases button asks Apple to sync (it can ask for the Apple ID password).
/// </summary>
public sealed class IosPurchases : IBookPurchases
{
    private const string RedeemAddress = "https://apps.apple.com/redeem";
    private const string ManageAddress = "https://apps.apple.com/account/subscriptions";
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan RestoreTimeout = TimeSpan.FromSeconds(60);
    private static readonly string[] PlanIds = [SubscriptionProducts.Monthly, SubscriptionProducts.Yearly];

    private readonly object _pendingLock = new();
    private readonly HashSet<string> _pending = [];

    public IosPurchases() => StoreKit2Bridge.ProductUpdated += OnProductUpdated;

    public string StoreName => PurchaseProofs.IosStore;

    public async Task<PurchaseSnapshot> RefreshAsync(IReadOnlyCollection<string> productIds, bool interactive, CancellationToken cancel)
    {
        StoreKit2Bridge? bridge = StoreKit2Bridge.Load();
        if (bridge is null)
        {
            PurchaseTrace.Line("storekit2 unavailable");
            return PurchaseSnapshot.Empty with { PricesFailed = true };
        }

        bool restricted = !bridge.CanMakePayments;
        if (restricted)
        {
            PurchaseTrace.Line("purchases restricted on this device"); // no prices, so no Buy and no fallback event (BookPurchases); what the account already owns still shows
        }

        (Dictionary<string, string> prices, bool pricesFailed) = restricted ? (new Dictionary<string, string>(), false) : await QueryPricesAsync(bridge, productIds, cancel); // a failed or timed-out request is a failed query; whether the store was reached is BookPurchases' call (the device's internet)
        StoreKitEntitlements? owned = await ReadEntitlementsAsync(bridge, cancel);
        return Snapshot(prices, owned) with { PricesFailed = pricesFailed, Restricted = restricted };
    }

    // The Restore purchases button: Apple syncs the account's purchases (and may ask for the password), then the list is read again.
    public async Task<PurchaseSnapshot> RestoreAsync(IReadOnlyCollection<string> productIds, CancellationToken cancel)
    {
        StoreKit2Bridge? bridge = StoreKit2Bridge.Load();
        if (bridge is not null)
        {
            string? synced = await AskAsync(bridge.SyncAsync(), RestoreTimeout, cancel);
            PurchaseTrace.Line(synced is not null && StoreKit2Answers.IsOk(synced) ? "restore synced" : "restore sync failed or timed out"); // a cancelled sync leaves what was known; the read below still runs
        }

        return await RefreshAsync(productIds, interactive: false, cancel);
    }

    public async Task<BuyOutcome> BuyAsync(string productId, CancellationToken cancel)
    {
        StoreKit2Bridge? bridge = StoreKit2Bridge.Load();
        if (bridge is null)
        {
            return BuyOutcome.Failed;
        }

        Task<string> request = bridge.BuyAsync(productId);
        string answer;
        try
        {
            answer = await request.WaitAsync(cancel); // no timeout: Apple's purchase sheet waits for the person
        }
        catch (OperationCanceledException)
        {
            _ = DeliverLateAnswerAsync(request); // the page closed while Apple's sheet was open: a purchase that still completes unlocks the book through a re-read; the bridge drops the waiting entry when the answer arrives
            throw;
        }

        StoreKitBuyAnswer? bought = StoreKit2Answers.Buy(answer);
        PurchaseTrace.Line($"buy answer outcome={bought?.Outcome ?? "unreadable"}");
        return bought is null ? BuyOutcome.Failed : OutcomeOf(productId, bought.Outcome);
    }

    // Apple's own "Redeem Code" sheet (iOS 16 and newer); the web address is the fallback and also what a TestFlight tester reaches.
    public async Task<bool> RedeemCodeAsync()
    {
        StoreKit2Bridge? bridge = StoreKit2Bridge.Load();
        string? answer = bridge is null ? null : await bridge.RedeemAsync(); // no timeout: the sheet waits for the person, and the web address must never open on top of it
        if (answer is not null && StoreKit2Answers.IsOk(answer))
        {
            return true;
        }

        bool sheetUnavailable = answer is null || StoreKit2Answers.Error(answer) == StoreKit2Answers.Unsupported;
        if (!sheetUnavailable)
        {
            PurchaseTrace.Line($"redeem sheet failed error={StoreKit2Answers.Error(answer!)}"); // a StoreKit error is the sheet failing, not the sheet being unavailable: the page says it did not work, the web page stays closed
            return false;
        }

        return await Launcher.Default.OpenAsync(new Uri(RedeemAddress)); // the sheet could not be shown (no framework, iOS before 16, no scene)
    }

    private static async Task DeliverLateAnswerAsync(Task<string> request)
    {
        try
        {
            string answer = await request;
            if (StoreKit2Answers.Buy(answer)?.Outcome == StoreKit2Answers.Bought)
            {
                PurchaseTrace.Line("buy completed after the page closed");
                PurchaseUpdates.Raise();
            }
        }
        catch (Exception ex)
        {
            // no-fallback: a late answer that cannot be read only means the next refresh reads Apple's list instead
            PurchaseTrace.Line($"late buy answer failed: {ex.GetType().Name}");
        }
    }

    // Slice 384: Apple's own subscriptions page, where a subscription is cancelled or its plan changed.
    public async Task<bool> ManageSubscriptionAsync() => await Launcher.Default.OpenAsync(new Uri(ManageAddress));

    private BuyOutcome OutcomeOf(string productId, string outcome)
    {
        lock (_pendingLock)
        {
            if (outcome == StoreKit2Answers.Pending)
            {
                _pending.Add(productId); // Ask to Buy: shown as waiting until Apple's approval arrives (the transaction listener clears it)
            }
            else
            {
                _pending.Remove(productId);
            }
        }

        return outcome switch
        {
            StoreKit2Answers.Bought => BuyOutcome.Bought,
            StoreKit2Answers.Cancelled => BuyOutcome.Cancelled,
            StoreKit2Answers.Pending => BuyOutcome.Pending,
            _ => BuyOutcome.Failed,
        };
    }

    private void OnProductUpdated(string productId)
    {
        lock (_pendingLock)
        {
            _pending.Remove(productId);
        }
    }

    private PurchaseSnapshot Snapshot(Dictionary<string, string> prices, StoreKitEntitlements? owned)
    {
        HashSet<string> pending;
        lock (_pendingLock)
        {
            pending = [.. _pending.Where(p => owned is null || !owned.Owned.Contains(p))];
        }

        if (owned is null)
        {
            return new PurchaseSnapshot(prices, new HashSet<string>(), pending, new Dictionary<string, string>(), Reachable: false); // Apple's list could not be read: BookPurchases keeps the last snapshot
        }

        return new PurchaseSnapshot(prices, owned.Owned, pending, owned.Proofs, Reachable: true, Subscription: owned.Subscription(DateTimeOffset.UtcNow), PurchasedAt: owned.PurchasedAt.ToDictionary(e => e.Key, e => e.Value));
    }

    // Failed is true when the request failed or timed out (billing_error when the device has internet); products Apple does not know come back as no price (prices_missing).
    private static async Task<(Dictionary<string, string> Prices, bool Failed)> QueryPricesAsync(StoreKit2Bridge bridge, IReadOnlyCollection<string> productIds, CancellationToken cancel)
    {
        if (productIds.Count == 0)
        {
            return ([], false);
        }

        string? answer = await AskAsync(bridge.ProductsAsync(productIds), QueryTimeout, cancel);
        (Dictionary<string, string> Prices, IReadOnlyList<string> Invalid)? read = answer is null ? null : StoreKit2Answers.Prices(answer);
        if (read is null)
        {
            PurchaseTrace.Line($"products request failed or timed out error={(answer is null ? "timeout" : StoreKit2Answers.Error(answer))}"); // the domain and code only, never the localized text
            return ([], true);
        }

        PurchaseTrace.Line($"products response: got={read.Value.Prices.Count} invalid={string.Join(",", read.Value.Invalid)}"); // product ids Apple does not know (not yet approved or wrong id)
        return (read.Value.Prices, false);
    }

    private static async Task<StoreKitEntitlements?> ReadEntitlementsAsync(StoreKit2Bridge bridge, CancellationToken cancel)
    {
        string? answer = await AskAsync(bridge.EntitlementsAsync(PlanIds), QueryTimeout, cancel);
        StoreKitEntitlements? owned = answer is null ? null : StoreKit2Answers.Entitlements(answer);
        PurchaseTrace.Line(owned is null ? "entitlements read failed or timed out" : $"entitlements: owned={owned.Current.Count} latestPlans={owned.Latest.Count}");
        return owned;
    }

    // The answer text, or null when it did not come within the time (or the caller cancelled).
    // The timer is cancelled on every path, and a request the caller gave up on is dropped from the bridge's waiting list.
    private static async Task<string?> AskAsync(Task<string> request, TimeSpan timeout, CancellationToken cancel)
    {
        using var timer = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        Task finished = await Task.WhenAny(request, Task.Delay(timeout, timer.Token));
        timer.Cancel();
        if (finished == request)
        {
            return await request;
        }

        StoreKit2Bridge.Abandon(request);
        return null;
    }
}

public static partial class PlatformPurchases
{
    public static partial IBookPurchases Create() => new IosPurchases();
}
