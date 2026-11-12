using System.Diagnostics;
using Foundation;
using StoreKit;
using UIKit;

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
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(20);

    private readonly Observer _observer = new();
    private bool _observing;

    public string StoreName => DinoReader.Core.PurchaseProofs.IosStore;

    public async Task<PurchaseSnapshot> RefreshAsync(IReadOnlyCollection<string> productIds, bool interactive, CancellationToken cancel)
    {
        Observe(); // never replays here: a replay can ask for the Apple ID password, so only the Restore purchases button does it; this reads the kept list of what was seen
        if (!SKPaymentQueue.CanMakePayments)
        {
            return _observer.Snapshot([]); // purchases are restricted (Screen Time): no prices, so no Buy; what the account already owns still shows and updates
        }

        Dictionary<string, string> prices = await QueryPricesAsync(productIds);
        return _observer.Snapshot(prices);
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
    public async Task<bool> RedeemCodeAsync()
    {
        if (OperatingSystem.IsIOSVersionAtLeast(14))
        {
            SKPaymentQueue.DefaultQueue.PresentCodeRedemptionSheet();
            return await Task.FromResult(true);
        }

        return await Launcher.Default.OpenAsync(new Uri(RedeemAddress));
    }

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

    private async Task<Dictionary<string, string>> QueryPricesAsync(IReadOnlyCollection<string> productIds)
    {
        var prices = new Dictionary<string, string>();
        if (productIds.Count == 0)
        {
            return prices;
        }

        var done = new TaskCompletionSource<SKProduct[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = new SKProductsRequest(new NSSet(productIds.Select(id => (NSObject)new NSString(id)).ToArray()));
        request.Delegate = new ProductsDelegate(done);
        request.Start();
        Task finished = await Task.WhenAny(done.Task, Task.Delay(QueryTimeout));
        if (finished != done.Task)
        {
            return prices;
        }

        foreach (SKProduct product in done.Task.Result)
        {
            _observer.Products[product.ProductIdentifier] = product;
            prices[product.ProductIdentifier] = LocalizedPrice(product);
        }

        return prices;
    }

    // The App Store's own price text in the storefront's currency.
    private static string LocalizedPrice(SKProduct product)
    {
        using var formatter = new NSNumberFormatter { NumberStyle = NSNumberFormatterStyle.Currency, Locale = product.PriceLocale };
        return formatter.StringFromNumber(product.Price) ?? string.Empty;
    }

    private sealed class ProductsDelegate(TaskCompletionSource<SKProduct[]> done) : SKProductsRequestDelegate
    {
        public override void ReceivedResponse(SKProductsRequest request, SKProductsResponse response) => done.TrySetResult(response.Products);

        public override void RequestFailed(SKRequest request, NSError error)
        {
            Debug.WriteLine($"Dino Reader StoreKit products request failed: {error.LocalizedDescription}");
            done.TrySetResult([]);
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
            new(prices, new HashSet<string>(_owned.Keys), new HashSet<string>(_pending), new Dictionary<string, string>(_owned), true);

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
