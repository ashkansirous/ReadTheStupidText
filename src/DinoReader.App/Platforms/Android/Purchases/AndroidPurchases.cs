using System.Diagnostics;
using Android.BillingClient.Api;

namespace DinoReader;

/// <summary>
/// Google Play Billing behind <see cref="IBookPurchases"/> (Slice 346). One <see cref="BillingClient"/> is opened on first use and reconnects by itself; every call is bridged to a task. A purchase is
/// acknowledged as soon as it is seen (Play refunds one that is not acknowledged within three days). The proof for a product is its purchase token; the Worker asks Google whether it is a purchase.
/// Nothing is kept on disk: prices, ownership and tokens are read from Play each time and held by <see cref="BookPurchases"/> in memory.
/// </summary>
public sealed class AndroidPurchases : IBookPurchases
{
    private const string RedeemAddress = "https://play.google.com/redeem";
    private static readonly TimeSpan SetupTimeout = TimeSpan.FromSeconds(15);

    private readonly PurchasesUpdates _updates = new();
    private readonly Dictionary<string, ProductDetails> _details = [];
    private BillingClient? _client;

    public string StoreName => DinoReader.Core.PurchaseProofs.AndroidStore;

    public async Task<PurchaseSnapshot> RefreshAsync(IReadOnlyCollection<string> productIds, bool interactive, CancellationToken cancel)
    {
        BillingClient? client = await ConnectAsync();
        if (client is null)
        {
            return PurchaseSnapshot.Empty;
        }

        (Dictionary<string, string> prices, bool pricesFailed) = await QueryPricesAsync(client, productIds); // any non-OK code is a failed query; whether the store was reached is BookPurchases' call (the device's internet), never a code table
        (HashSet<string> owned, HashSet<string> pending, Dictionary<string, string> proofs, bool ok) = await QueryOwnedAsync(client);
        return new PurchaseSnapshot(prices, owned, pending, proofs, ok, pricesFailed);
    }

    // Play has no separate restore: what the account owns is read again.
    public Task<PurchaseSnapshot> RestoreAsync(IReadOnlyCollection<string> productIds, CancellationToken cancel) => RefreshAsync(productIds, interactive: true, cancel);

    public async Task<BuyOutcome> BuyAsync(string productId, CancellationToken cancel)
    {
        BillingClient? client = await ConnectAsync();
        if (client is null || Platform.CurrentActivity is not { } activity)
        {
            return BuyOutcome.Failed;
        }

        if (!_details.ContainsKey(productId))
        {
            await QueryPricesAsync(client, [productId]);
        }

        if (!_details.TryGetValue(productId, out ProductDetails? details))
        {
            return BuyOutcome.Failed;
        }

        Task<BuyOutcome> result = _updates.NextAsync();
        BillingFlowParams.ProductDetailsParams item = BillingFlowParams.ProductDetailsParams.NewBuilder().SetProductDetails(details).Build();
        BillingFlowParams flow = BillingFlowParams.NewBuilder().SetProductDetailsParamsList([item]).Build();
        BillingResult launched = client.LaunchBillingFlow(activity, flow);
        if (launched.ResponseCode != BillingResponseCode.Ok)
        {
            _updates.Abandon();
            return launched.ResponseCode == BillingResponseCode.UserCancelled ? BuyOutcome.Cancelled : BuyOutcome.Failed;
        }

        return await result; // the purchase is acknowledged in the updates listener, and any other one wherever the account's purchases are read
    }

    public async Task<bool> RedeemCodeAsync() => await Launcher.Default.OpenAsync(new Uri(RedeemAddress));

    private async Task<BillingClient?> ConnectAsync()
    {
        if (_client is { IsReady: true })
        {
            return _client;
        }

        EndClient(); // one client at a time: a client that is not ready is ended, not left reconnecting beside the new one
        try
        {
            PendingPurchasesParams pending = PendingPurchasesParams.NewBuilder().EnableOneTimeProducts().Build();
            BillingClient client = BillingClient.NewBuilder(Platform.AppContext).SetListener(_updates).EnablePendingPurchases(pending).EnableAutoServiceReconnection().Build();
            _client = client;
            _updates.Client = client;
            var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.StartConnection(new SetupListener(ready));
            Task finished = await Task.WhenAny(ready.Task, Task.Delay(SetupTimeout));
            if (finished == ready.Task && ready.Task.Result)
            {
                return client;
            }

            EndClient(); // setup timed out or Play refused: the next call starts again from nothing
            return null;
        }
        catch (Exception ex) when (ex is Java.Lang.Exception or InvalidOperationException)
        {
            // no-fallback: BookPurchases reports the empty snapshot (store_unreachable) that follows; the Play store simply is not there (no Play, or a robot)
            Debug.WriteLine($"Dino Reader could not connect to Google Play Billing: {ex.Message}");
            EndClient();
            return null;
        }
    }

    private void EndClient()
    {
        BillingClient? old = _client;
        _client = null;
        _updates.Client = null;
        old?.EndConnection();
    }

    private async Task<(Dictionary<string, string> Prices, bool Failed)> QueryPricesAsync(BillingClient client, IReadOnlyCollection<string> productIds)
    {
        var prices = new Dictionary<string, string>();
        if (productIds.Count == 0)
        {
            return (prices, false);
        }

        var products = productIds.Select(id => QueryProductDetailsParams.Product.NewBuilder().SetProductId(id).SetProductType(BillingClient.ProductType.Inapp).Build()).ToList();
        QueryProductDetailsParams query = QueryProductDetailsParams.NewBuilder().SetProductList(products).Build();
        QueryProductDetailsResult answer = await client.QueryProductDetailsAsync(query);
        // Slice 358: Play's answer, so a run can tell "not ok", "ok but empty" and "no price in the details" apart (BookPurchases reports prices_missing for the products left without a price).
        PurchaseTrace.Line($"product details: code={answer.Result.ResponseCode} asked={productIds.Count} got={answer.ProductDetailsList?.Count ?? 0} unfetched={answer.UnfetchedProductList?.Count ?? 0}"); // the Release log, not Debug only
        foreach (ProductDetails details in answer.ProductDetailsList ?? [])
        {
            _details[details.ProductId] = details;
            if (details.GetOneTimePurchaseOfferDetails()?.FormattedPrice is { } price)
            {
                prices[details.ProductId] = price;
            }
        }

        return (prices, answer.Result.ResponseCode != BillingResponseCode.Ok); // any non-OK code is a failed query (BookPurchases reports billing_error when the device has internet; the code is in the trace line above); OK with no price is prices_missing
    }

    private static async Task<(HashSet<string> Owned, HashSet<string> Pending, Dictionary<string, string> Proofs, bool Ok)> QueryOwnedAsync(BillingClient client)
    {
        var owned = new HashSet<string>();
        var pending = new HashSet<string>();
        var proofs = new Dictionary<string, string>();
        QueryPurchasesParams query = QueryPurchasesParams.NewBuilder().SetProductType(BillingClient.ProductType.Inapp).Build();
        IList<Purchase>? purchases = await OwnedPurchasesAsync(client, query);
        foreach (Purchase purchase in purchases ?? [])
        {
            AcknowledgeIfNeeded(client, purchase); // seen here (start-up, store page, Restore), so a purchase approved later or bought on another phone is acknowledged too
            foreach (string product in purchase.Products)
            {
                (purchase.PurchaseState == PurchaseState.Purchased ? owned : pending).Add(product);
                if (purchase.PurchaseState == PurchaseState.Purchased)
                {
                    proofs[product] = purchase.PurchaseToken;
                }
            }
        }

        return (owned, pending, proofs, purchases is not null);
    }

    // Play refunds a purchase that is not acknowledged within three days; only a Purchased one can be (a pending one is acknowledged once it is approved and seen here).
    private static void AcknowledgeIfNeeded(BillingClient client, Purchase purchase)
    {
        if (purchase.PurchaseState == PurchaseState.Purchased && !purchase.IsAcknowledged)
        {
            AcknowledgePurchaseParams ack = AcknowledgePurchaseParams.NewBuilder().SetPurchaseToken(purchase.PurchaseToken).Build();
            client.AcknowledgePurchase(ack, new AcknowledgeListener());
        }
    }

    private sealed class SetupListener(TaskCompletionSource<bool> ready) : Java.Lang.Object, IBillingClientStateListener
    {
        public void OnBillingSetupFinished(BillingResult result) => ready.TrySetResult(result.ResponseCode == BillingResponseCode.Ok);

        public void OnBillingServiceDisconnected()
        {
            // The client reconnects by itself (auto service reconnection); the next call checks IsReady again.
        }
    }

    // Null when Play could not answer (so the snapshot says the store was not reachable).
    private static async Task<IList<Purchase>?> OwnedPurchasesAsync(BillingClient client, QueryPurchasesParams query)
    {
        var answer = await client.QueryPurchasesAsync(query);
        return answer.Result.ResponseCode == BillingResponseCode.Ok ? answer.Purchases : null;
    }

    private sealed class AcknowledgeListener : Java.Lang.Object, IAcknowledgePurchaseResponseListener
    {
        public void OnAcknowledgePurchaseResponse(BillingResult result) => Debug.WriteLine($"Dino Reader acknowledged a purchase: {result.ResponseCode}");
    }

    // Play reports the result of the purchase sheet here; BuyAsync waits for the next one.
    private sealed class PurchasesUpdates : Java.Lang.Object, IPurchasesUpdatedListener
    {
        private TaskCompletionSource<BuyOutcome>? _next;

        /// <summary>The client the updates arrive from, so a purchase reported here (also one nobody is waiting for) is acknowledged.</summary>
        public BillingClient? Client { get; set; }
        public Task<BuyOutcome> NextAsync()
        {
            _next = new TaskCompletionSource<BuyOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            return _next.Task;
        }

        public void Abandon() => _next = null;

        public void OnPurchasesUpdated(BillingResult result, IList<Purchase>? purchases)
        {
            if (result.ResponseCode == BillingResponseCode.Ok && Client is { } client)
            {
                foreach (Purchase purchase in purchases ?? [])
                {
                    AcknowledgeIfNeeded(client, purchase);
                }
            }

            _next?.TrySetResult(OutcomeOf(result, purchases));
        }

        private static BuyOutcome OutcomeOf(BillingResult result, IList<Purchase>? purchases)
        {
            if (result.ResponseCode == BillingResponseCode.UserCancelled)
            {
                return BuyOutcome.Cancelled;
            }

            if (result.ResponseCode == BillingResponseCode.ItemAlreadyOwned)
            {
                return BuyOutcome.Bought;
            }

            if (result.ResponseCode != BillingResponseCode.Ok)
            {
                return BuyOutcome.Failed;
            }

            return (purchases ?? []).Any(p => p.PurchaseState == PurchaseState.Pending) ? BuyOutcome.Pending : BuyOutcome.Bought;
        }
    }
}

public static partial class PlatformPurchases
{
    public static partial IBookPurchases Create() => new AndroidPurchases();
}
