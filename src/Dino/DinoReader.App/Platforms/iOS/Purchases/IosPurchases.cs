using System.Diagnostics;
using System.Runtime.Versioning;
using DinoReader.Core;
using Foundation;
using StoreKit;
using UIKit;

// StoreKit 1 is obsoleted from iOS 18; this exception holds until the purchases move to StoreKit 2.
#pragma warning disable CA1422

namespace DinoReader;

/// <summary>
/// StoreKit behind <see cref="IBookPurchases"/> (Slice 346). StoreKit 2 is Swift-only and .NET for iOS binds the original StoreKit API (still supported by Apple), so that is what this uses: an
/// <see cref="SKProductsRequest"/> for prices, <see cref="SKPaymentQueue"/> for buying and restoring. The observer is added at first use and finishes every transaction it sees. The proof for a product is
/// its transaction identifier (the original one for a restored purchase); the Worker asks the App Store Server API whether it is a purchase of that product.
/// Nothing is kept on disk: ownership is the set of purchased or restored transactions the queue reports in this run, read again from Apple each time (a restore replays them).
/// </summary>
public sealed class IosPurchases : IBookPurchases
{
    private const string RedeemAddress = "https://apps.apple.com/redeem";
    private const string ManageAddress = "https://apps.apple.com/account/subscriptions";
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(20);

    private readonly Observer _observer = new();
    private bool _observing;

    public string StoreName => DinoReader.Core.PurchaseProofs.IosStore;

    public async Task<PurchaseSnapshot> RefreshAsync(IReadOnlyCollection<string> productIds, bool interactive, CancellationToken cancel)
    {
        Observe(); // never replays here: a replay can ask for the Apple ID password, so only the Restore purchases button does it; this reads the kept list of what was seen
        if (!SKPaymentQueue.CanMakePayments)
        {
            PurchaseTrace.Line("purchases restricted on this device");
            return _observer.Snapshot([]) with { Restricted = true }; // purchases are restricted (Screen Time): no prices, so no Buy and no fallback event (BookPurchases); what the account already owns still shows and updates
        }

        (Dictionary<string, string> prices, bool pricesFailed) = await QueryPricesAsync(productIds); // a failed or timed-out request is a failed query; whether the store was reached is BookPurchases' call (the device's internet)
        return _observer.Snapshot(prices) with { PricesFailed = pricesFailed };
    }

    // The Restore purchases button: Apple replays every purchase the account made for this app (and finishes them).
    public async Task<PurchaseSnapshot> RestoreAsync(IReadOnlyCollection<string> productIds, CancellationToken cancel)
    {
        Observe();
        await ReplayAsync(cancel);
        return await RefreshAsync(productIds, interactive: false, cancel);
    }

    // A cancelled or failed replay leaves what was known; what the replay reports is added to the kept list by the observer.
    private async Task ReplayAsync(CancellationToken cancel)
    {
        Task restored = _observer.NextRestoreAsync();
        SKPaymentQueue.DefaultQueue.RestoreCompletedTransactions();
        await Task.WhenAny(restored, Task.Delay(QueryTimeout, cancel));
    }

    public async Task<BuyOutcome> BuyAsync(string productId, CancellationToken cancel)
    {
        Observe();
        SKProduct? product = await ProductAsync(productId);
        if (product is null)
        {
            return BuyOutcome.Failed;
        }

        Task<BuyOutcome> result = _observer.NextPurchaseAsync(productId);
        SKPaymentQueue.DefaultQueue.AddPayment(SKPayment.CreateFrom(product));
        return await result;
    }

    // Apple's own "Redeem Code" sheet exists from iOS 14; the web address is the fallback and also what a TestFlight tester reaches.
    [UnsupportedOSPlatform("maccatalyst")] // the redemption sheet is iOS only; this app has no Mac Catalyst head
    public async Task<bool> RedeemCodeAsync()
    {
        if (OperatingSystem.IsIOSVersionAtLeast(14))
        {
            SKPaymentQueue.DefaultQueue.PresentCodeRedemptionSheet();
            return await Task.FromResult(true);
        }

        return await Launcher.Default.OpenAsync(new Uri(RedeemAddress));
    }

    // Slice 384: Apple's own subscriptions page, where a subscription is cancelled or its plan changed.
    public async Task<bool> ManageSubscriptionAsync() => await Launcher.Default.OpenAsync(new Uri(ManageAddress));

    private void Observe()
    {
        if (_observing)
        {
            return;
        }

        SKPaymentQueue.DefaultQueue.AddTransactionObserver(_observer);
        _observing = true;
    }

    private async Task<SKProduct?> ProductAsync(string productId)
    {
        if (_observer.Products.TryGetValue(productId, out SKProduct? known))
        {
            return known;
        }

        await QueryPricesAsync([productId]);
        return _observer.Products.GetValueOrDefault(productId);
    }

    // Failed is true when the request failed or timed out (billing_error when the device has internet); products Apple does not know come back as no price (prices_missing).
    private async Task<(Dictionary<string, string> Prices, bool Failed)> QueryPricesAsync(IReadOnlyCollection<string> productIds)
    {
        var prices = new Dictionary<string, string>();
        if (productIds.Count == 0)
        {
            return (prices, false);
        }

        var done = new TaskCompletionSource<SKProduct[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var products = new ProductsDelegate(done);
        var request = new SKProductsRequest(new NSSet(productIds.Select(id => (NSObject)new NSString(id)).ToArray()));
        request.Delegate = products;
        request.Start();
        Task finished = await Task.WhenAny(done.Task, Task.Delay(QueryTimeout));
        if (finished != done.Task || done.Task.Result is not { } found)
        {
            PurchaseTrace.Line("products request failed or timed out");
            return (prices, true);
        }

        foreach (SKProduct product in found)
        {
            _observer.Products[product.ProductIdentifier] = product;
            prices[product.ProductIdentifier] = LocalizedPrice(product);
        }

        return (prices, false);
    }

    // The App Store's own price text in the storefront's currency.
    private static string LocalizedPrice(SKProduct product)
    {
        using var formatter = new NSNumberFormatter { NumberStyle = NSNumberFormatterStyle.Currency, Locale = product.PriceLocale };
        return formatter.StringFromNumber(product.Price) ?? string.Empty;
    }

    private sealed class ProductsDelegate(TaskCompletionSource<SKProduct[]?> done) : SKProductsRequestDelegate
    {
        public override void ReceivedResponse(SKProductsRequest request, SKProductsResponse response)
        {
            string invalid = string.Join(",", response.InvalidProducts ?? []); // product ids Apple does not know (not yet approved or wrong id)
            PurchaseTrace.Line($"products response: got={response.Products.Length} invalid={invalid}");
            done.TrySetResult(response.Products);
        }

        public override void RequestFailed(SKRequest request, NSError error)
        {
            PurchaseTrace.Line($"products request failed: domain={error.Domain} code={error.Code}"); // the domain and code only, never the localized text
            done.TrySetResult(null);
        }
    }

    // Every purchased or restored transaction of this run is kept (product to transaction id) and finished; failed and cancelled ones finish with no change.
    private sealed class Observer : SKPaymentTransactionObserver
    {
        // The one place the app keeps purchase state (an exception to "the store owns it": StoreKit 1 has no silent owned list): product id to transaction id, in Preferences, written when a purchase or a
        // Restore reports a transaction. Opening the Store tab reads this list only; Restore purchases replays and refreshes it.
        private const string KeptKey = "dino.ios.purchases";
        private readonly Dictionary<string, string> _owned = LoadKept();
        private readonly HashSet<string> _pending = [];
        private TaskCompletionSource<bool>? _restore;
        private TaskCompletionSource<BuyOutcome>? _purchase;
        private string? _purchaseProduct;
        private bool _readOnce;

        public Dictionary<string, SKProduct> Products { get; } = [];

        private static Dictionary<string, string> LoadKept()
        {
            try
            {
                string json = Preferences.Default.Get(KeptKey, string.Empty);
                return json.Length == 0 ? [] : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidCastException)
            {
                // no-fallback: an unreadable kept list is an empty one; Restore purchases fills it again
                return [];
            }
        }

        private void Keep() => Preferences.Default.Set(KeptKey, System.Text.Json.JsonSerializer.Serialize(_owned));

        public PurchaseSnapshot Snapshot(Dictionary<string, string> prices) =>
            new(prices, new HashSet<string>(_owned.Keys), new HashSet<string>(_pending), new Dictionary<string, string>(_owned), true, Subscription: SubscriptionNow(), PurchasedAt: PurchasedAtNow());

        private Dictionary<string, DateTimeOffset> PurchasedAtNow()
        {
            lock (_subLock)
            {
                return _subLast.ToDictionary(e => e.Key, e => DateTimeOffset.FromUnixTimeSeconds(e.Value));
            }
        }

        // Slice 384: StoreKit 1 gives no expiry date on the device, so the end is worked out from the kept transaction: its date plus the period it paid for (the 7 days of a free trial when it was the account's first
        // purchase of a plan with a free trial, else a month or a year), pushed on by each renewal the observer is told of (the queue delivers renewals when the app runs). This estimate is used only until the first
        // Worker answer: BookPurchases asks the Worker (Apple's all-statuses answer) after each read and keeps the real end date, so a reinstall's old transaction or a renewal delivered late no longer decides. The newest end among the kept plans decides the estimate.
        private const string SubEndsKey = "dino.ios.subscription.ends";
        private readonly Dictionary<string, long> _subEnds = LoadSubEnds();
        private readonly HashSet<string> _subTrial = [];
        private readonly object _subLock = new(); // the observer's callbacks and a background read both touch _subEnds, _subTrial and _subNewest
        private const string SubNewestKey = "dino.ios.subscription.newest";
        private long _subNewest = Preferences.Default.Get(SubNewestKey, 0L); // unix seconds of the newest subscription transaction kept (0: none)
        private const string SubLastKey = "dino.ios.subscription.last";
        private readonly Dictionary<string, long> _subLast = LoadSubLast(); // product to the unix seconds of its newest transaction (restored ones count by their original date): the proof sent to the Worker is the newest one's

        private static Dictionary<string, long> LoadSubLast()
        {
            try
            {
                string json = Preferences.Default.Get(SubLastKey, string.Empty);
                return json.Length == 0 ? [] : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, long>>(json) ?? [];
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidCastException)
            {
                // no-fallback: an unreadable list only loses the tie-break between plans; the next transaction writes it again
                return [];
            }
        }

        private static Dictionary<string, long> LoadSubEnds()
        {
            try
            {
                string json = Preferences.Default.Get(SubEndsKey, string.Empty);
                return json.Length == 0 ? [] : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, long>>(json) ?? [];
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidCastException)
            {
                // no-fallback: an unreadable kept end is no end; the next renewal or Restore purchases writes it again
                return [];
            }
        }

        private SubscriptionStatus SubscriptionNow()
        {
            KeyValuePair<string, long>[] kept;
            HashSet<string> trials;
            long newest;
            lock (_subLock)
            {
                kept = [.. _subEnds];
                trials = [.. _subTrial];
                newest = _subNewest;
            }

            if (kept.Length == 0)
            {
                return new SubscriptionStatus(SubscriptionState.Unknown, null, null, DeviceReadKind.Estimated); // nothing kept is no information, not a lapse: SubscriptionAccess.Merge leaves the record unchanged
            }

            (string product, long ends) = kept.MaxBy(e => e.Value);
            DateTimeOffset end = DateTimeOffset.FromUnixTimeSeconds(ends);
            // No grace here: the 3 days after an end date are applied once, by SubscriptionAccess.Grants, on the end this status carries. A kept end that has passed stays Active (a renewal the queue has not delivered yet
            // is not a lapse), so the books lock exactly 3 days after it, not 6.
            SubscriptionState state = trials.Contains(product) && DateTimeOffset.UtcNow <= end ? SubscriptionState.Trial : SubscriptionState.Active;
            return new SubscriptionStatus(state, end, product, DeviceReadKind.Estimated, newest > 0 ? DateTimeOffset.FromUnixTimeSeconds(newest) : null);
        }

        private void NoteSubscription(string product, SKPaymentTransaction transaction)
        {
            // Apple (SKPaymentTransaction.transactionDate / .original): a Restored transaction is a new transaction whose transactionDate is when it entered the queue (the restore time), and `original` is the
            // transaction that was restored; so a Restore reads the original's date and never counts as a new purchase or renewal. Only a Purchased transaction (a purchase or a renewal the queue delivers) can be newest.
            bool restored = transaction.TransactionState == SKPaymentTransactionState.Restored;
            NSDate? stamp = restored ? (transaction.OriginalTransaction ?? transaction).TransactionDate : transaction.TransactionDate;
            DateTimeOffset start = stamp is { } date ? new DateTimeOffset((DateTime)date) : DateTimeOffset.UtcNow; // NSDate converts to a UTC DateTime
            // Only a first purchase gets the trial estimate: a restored transaction (a reinstall, another device) may be long past its trial, and its true end comes from the Worker.
            bool trial;
            DateTimeOffset end;
            lock (_subLock)
            {
                bool firstPurchase = transaction.TransactionState == SKPaymentTransactionState.Purchased && transaction.OriginalTransaction is null && _subEnds.Count == 0;
                trial = firstPurchase && Products.TryGetValue(product, out SKProduct? known) && known.IntroductoryPrice?.PaymentMode == SKProductDiscountPaymentMode.FreeTrial;
                end = trial ? start + TrialLength : (product == SubscriptionProducts.Yearly ? start.AddYears(1) : start.AddMonths(1));
                if (!_subEnds.TryGetValue(product, out long held) || end.ToUnixTimeSeconds() > held)
                {
                    _subEnds[product] = end.ToUnixTimeSeconds();
                    Preferences.Default.Set(SubEndsKey, System.Text.Json.JsonSerializer.Serialize(_subEnds));
                }

                if (trial)
                {
                    _subTrial.Add(product);
                }

                if (!_subLast.TryGetValue(product, out long lastSeen) || start.ToUnixTimeSeconds() > lastSeen)
                {
                    _subLast[product] = start.ToUnixTimeSeconds();
                    Preferences.Default.Set(SubLastKey, System.Text.Json.JsonSerializer.Serialize(_subLast));
                }

                if (!restored && start.ToUnixTimeSeconds() > _subNewest)
                {
                    _subNewest = start.ToUnixTimeSeconds();
                    Preferences.Default.Set(SubNewestKey, _subNewest);
                }
            }

            PurchaseTrace.Line($"subscription transaction product={product} trial={trial} ends={end:yyyy-MM-dd}");
        }

        private static readonly TimeSpan TrialLength = TimeSpan.FromDays(7);

        public Task NextRestoreAsync()
        {
            _restore = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            return _restore.Task;
        }

        public Task<BuyOutcome> NextPurchaseAsync(string productId)
        {
            _purchaseProduct = productId;
            _purchase = new TaskCompletionSource<BuyOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            return _purchase.Task;
        }

        public override void UpdatedTransactions(SKPaymentQueue queue, SKPaymentTransaction[] transactions)
        {
            _readOnce = true;
            foreach (SKPaymentTransaction transaction in transactions)
            {
                Handle(queue, transaction);
            }
        }

        public override void RestoreCompletedTransactionsFinished(SKPaymentQueue queue) => _restore?.TrySetResult(true);

        public override void RestoreCompletedTransactionsFailedWithError(SKPaymentQueue queue, NSError error)
        {
            Debug.WriteLine($"Dino Reader StoreKit restore failed: {error.LocalizedDescription}");
            _restore?.TrySetResult(false);
        }

        private void Handle(SKPaymentQueue queue, SKPaymentTransaction transaction)
        {
            string product = transaction.Payment.ProductIdentifier;
            switch (transaction.TransactionState)
            {
                case SKPaymentTransactionState.Purchased:
                case SKPaymentTransactionState.Restored:
                    _pending.Remove(product);
                    _owned[product] = (transaction.OriginalTransaction ?? transaction).TransactionIdentifier ?? string.Empty;
                    if (SubscriptionProducts.Is(product))
                    {
                        NoteSubscription(product, transaction); // the end is saved before the owned product, so a kill between them cannot leave owned-without-end
                    }

                    Keep();

                    queue.FinishTransaction(transaction);
                    Complete(product, BuyOutcome.Bought);
                    break;
                case SKPaymentTransactionState.Deferred:
                    _pending.Add(product);
                    Complete(product, BuyOutcome.Pending);
                    break;
                case SKPaymentTransactionState.Failed:
                    _pending.Remove(product); // a declined Ask to Buy (or any failed payment) is no longer waiting: the card offers Buy again
                    queue.FinishTransaction(transaction);
                    Complete(product, transaction.Error?.Code == (long)SKError.PaymentCancelled ? BuyOutcome.Cancelled : BuyOutcome.Failed);
                    break;
                default:
                    break;
            }
        }

        private void Complete(string product, BuyOutcome outcome)
        {
            if (_purchase is not null && product == _purchaseProduct)
            {
                _purchase.TrySetResult(outcome);
            }
        }
    }
}

public static partial class PlatformPurchases
{
    public static partial IBookPurchases Create() => new IosPurchases();
}
