using System.Diagnostics;
using Android.BillingClient.Api;
using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// Google Play Billing behind <see cref="IBookPurchases"/> (Slice 346). One <see cref="BillingClient"/> is opened on first use and reconnects by itself; every call is bridged to a task. A purchase is
/// acknowledged as soon as it is seen (Play refunds one that is not acknowledged within three days). The proof for a product is its purchase token; the Worker asks Google whether it is a purchase.
/// Nothing is kept on disk: prices, ownership and tokens are read from Play each time and held by <see cref="BookPurchases"/> in memory.
/// </summary>
public sealed class AndroidPurchases : IBookPurchases
{
    private const string RedeemAddress = "https://play.google.com/redeem";
    private const string ManageAddress = "https://play.google.com/store/account/subscriptions?sku=" + DinoReader.Core.SubscriptionProducts.Subscription + "&package=uk.sirous.dinoreader";
    private static readonly TimeSpan SetupTimeout = TimeSpan.FromSeconds(15);

    private readonly PurchasesUpdates _updates = new();
    private readonly Dictionary<string, ProductDetails> _details = [];
    // Slice 397 review: one immutable map per read, swapped in whole, so a purchase that starts during a refresh reads either the old map or the new one, never a half-cleared one.
    // OfferTokens: the app's plan product (monthly or yearly) to the Play offer token to buy (the trial offer when Play lists it, else the base plan). BaseTokens: the base plan's token, bought when the card does not promise a trial (an Ended card).
    // TrialPlans: the plans for which Play listed the trial offer (it lists it only for an account that may still use it).
    private sealed record PlanTokens(IReadOnlyDictionary<string, string> OfferTokens, IReadOnlyDictionary<string, string> BaseTokens, IReadOnlySet<string> TrialPlans);

    private volatile PlanTokens _plans = new(new Dictionary<string, string>(), new Dictionary<string, string>(), new HashSet<string>());
    private BillingClient? _client;

    public string StoreName => DinoReader.Core.PurchaseProofs.AndroidStore;

    public async Task<PurchaseSnapshot> RefreshAsync(IReadOnlyCollection<string> productIds, bool interactive, CancellationToken cancel)
    {
        BillingClient? client = await ConnectAsync();
        if (client is null)
        {
            return PurchaseSnapshot.Empty;
        }

        string[] bookProducts = [.. productIds.Where(id => !SubscriptionProducts.Is(id))];
        (Dictionary<string, string> prices, bool pricesFailed) = await QueryPricesAsync(client, bookProducts); // any non-OK code is a failed query; whether the store was reached is BookPurchases' call (the device's internet), never a code table
        bool? trialAvailable = null;
        if (productIds.Any(SubscriptionProducts.Is))
        {
            (Dictionary<string, string> planPrices, _) = await QueryPricesAsync(client, [SubscriptionProducts.Subscription], BillingClient.ProductType.Subs); // a failed subscription query only means no subscription card, never a red strip for the books
            foreach ((string plan, string price) in planPrices)
            {
                prices[plan] = price;
            }

            trialAvailable = planPrices.Count > 0 ? _plans.TrialPlans.Count > 0 : null; // no plan listed: Play did not answer, so nothing is concluded about the trial
            PurchaseTrace.Line($"subscription trial available={(trialAvailable?.ToString() ?? "unknown")}"); // Slice 397: which card branch the next read shows (offer with the trial, or the plain price)
        }

        (HashSet<string> owned, HashSet<string> pending, Dictionary<string, string> proofs, bool ok, SubscriptionStatus? subscription, Dictionary<string, DateTimeOffset> purchasedAt) = await QueryOwnedAsync(client);
        return new PurchaseSnapshot(prices, owned, pending, proofs, ok, pricesFailed, Subscription: subscription, PurchasedAt: purchasedAt, TrialAvailable: trialAvailable);
    }

    // Play has no separate restore: what the account owns is read again.
    public Task<PurchaseSnapshot> RestoreAsync(IReadOnlyCollection<string> productIds, CancellationToken cancel) => RefreshAsync(productIds, interactive: true, cancel);

    /// <summary>Slice 455: the offer the last Buy asked for and whether Play said the account already owns it, for the purchase usage count.</summary>
    public BuyNote LastBuyNote { get; private set; } = BuyNote.None;

    public Task<BuyOutcome> BuyAsync(string productId, CancellationToken cancel) => BuyAsync(productId, withTrial: true, cancel);

    // withTrial false buys the base plan even when Play lists the trial offer: the card (an Ended one) showed the plain price with no trial, so the sheet must not start one.
    public async Task<BuyOutcome> BuyAsync(string productId, bool withTrial, CancellationToken cancel)
    {
        LastBuyNote = BuyNote.None; // first: an early Failed below must not leave the previous buy's note (an already-owned one) to be read
        BillingClient? client = await ConnectAsync();
        if (client is null || Platform.CurrentActivity is not { } activity)
        {
            return BuyOutcome.Failed;
        }

        bool isPlan = SubscriptionProducts.Is(productId);
        string detailsId = isPlan ? SubscriptionProducts.Subscription : productId; // both plans are base plans of Play's one subscription
        if (!_details.ContainsKey(detailsId) || (isPlan && !_plans.OfferTokens.ContainsKey(productId)))
        {
            await QueryPricesAsync(client, [detailsId], isPlan ? BillingClient.ProductType.Subs : BillingClient.ProductType.Inapp);
        }

        PlanTokens plans = _plans; // one read: a refresh that swaps the map meanwhile cannot change what this purchase sees
        if (!_details.TryGetValue(detailsId, out ProductDetails? details) || (isPlan && !plans.OfferTokens.ContainsKey(productId)))
        {
            return BuyOutcome.Failed;
        }

        Task<PurchasesUpdates.Result> result = _updates.NextAsync();
        BillingFlowParams.ProductDetailsParams.Builder builder = BillingFlowParams.ProductDetailsParams.NewBuilder().SetProductDetails(details);
        if (isPlan)
        {
            // the trial offer when asked for and Play showed it (new subscribers only), else the base plan; a missing base token falls back to the offer token
            builder.SetOfferToken(!withTrial && plans.BaseTokens.TryGetValue(productId, out string? baseToken) ? baseToken : plans.OfferTokens[productId]);
        }

        BillingFlowParams.ProductDetailsParams item = builder.Build();
        BillingFlowParams flow = BillingFlowParams.NewBuilder().SetProductDetailsParamsList([item]).Build();
        BillingResult launched = client.LaunchBillingFlow(activity, flow);
        if (launched.ResponseCode != BillingResponseCode.Ok)
        {
            _updates.Abandon();
            string? launchOffer = isPlan ? PurchaseDetail.OfferOf(withTrial && plans.TrialPlans.Contains(productId)) : null;
            LastBuyNote = new BuyNote(launchOffer, AlreadyOwned: launched.ResponseCode == BillingResponseCode.ItemAlreadyOwned); // Slice 455: Play refuses to launch the sheet for an owned item, and says so here
            return launched.ResponseCode == BillingResponseCode.UserCancelled ? BuyOutcome.Cancelled : BuyOutcome.Failed;
        }

        (BuyOutcome ended, bool alreadyOwned) = await result; // alreadyOwned is this buy's own completion, never an earlier or unrelated Play callback's
        // Slice 455: the offer asked for (the trial only when Play listed it for this account); Play's client cannot confirm a trial was granted, so Slice 458's server notifications are the source of truth for trials.
        string? offer = isPlan ? PurchaseDetail.OfferOf(withTrial && plans.TrialPlans.Contains(productId)) : null;
        LastBuyNote = new BuyNote(offer, alreadyOwned);
        return ended; // the purchase is acknowledged in the updates listener, and any other one wherever the account's purchases are read
    }

    public async Task<bool> RedeemCodeAsync() => await Launcher.Default.OpenAsync(new Uri(RedeemAddress));

    public async Task<bool> ManageSubscriptionAsync() => await Launcher.Default.OpenAsync(new Uri(ManageAddress));

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

    private async Task<(Dictionary<string, string> Prices, bool Failed)> QueryPricesAsync(BillingClient client, IReadOnlyCollection<string> productIds, string productType = BillingClient.ProductType.Inapp)
    {
        var prices = new Dictionary<string, string>();
        if (productIds.Count == 0)
        {
            return (prices, false);
        }

        var products = productIds.Select(id => QueryProductDetailsParams.Product.NewBuilder().SetProductId(id).SetProductType(productType).Build()).ToList();
        QueryProductDetailsParams query = QueryProductDetailsParams.NewBuilder().SetProductList(products).Build();
        // Never client.QueryProductDetailsAsync: the binding's async wrapper returns Ok with an empty ProductDetailsList while the listener overload returns the products
        // (dotnet/android-libraries #1396, open; seen on the S21 in Slice 358). Check that issue before a binding upgrade (Slice 379) puts the wrapper back.
        DetailsAnswer? answer = await QueryViaListenerAsync(client, query, productIds.Count);
        if (answer is null)
        {
            return (prices, true); // no answer or a throw: a failed query (BookPurchases reports billing_error); the trace line says which
        }

        TraceDetails(productIds.Count, answer);
        foreach (ProductDetails details in answer.Details.ProductDetailsList ?? [])
        {
            _details[details.ProductId] = details;
            if (productType == BillingClient.ProductType.Subs)
            {
                ReadPlans(details, prices);
            }
            else if (details.GetOneTimePurchaseOfferDetails()?.FormattedPrice is { } price)
            {
                prices[details.ProductId] = price;
            }
        }

        return (prices, !IsOk(answer.Result)); // any non-OK code is a failed query (BookPurchases reports billing_error when the device has internet; the code is in the trace line above); OK with no price is prices_missing
    }

    // Slice 384: Play's one subscription has two base plans (monthly, yearly), each with an optional 7-day trial offer that Play lists only for an account that may still use it. The price is the recurring one (the last pricing phase of the
    // offer); the offer token to buy is the trial offer's when listed, else the base plan's. Product ids are the app's plan products, never Play's base plan ids.
    private void ReadPlans(ProductDetails details, Dictionary<string, string> prices)
    {
        var trialPlans = new HashSet<string>(); // built on the side and swapped in at the end: a trial offer Play no longer lists (the account used it) must not be bought from an old token, and a purchase must never see a half-built map
        var baseTokens = new Dictionary<string, string>();
        var offerTokens = new Dictionary<string, string>();
        foreach (ProductDetails.SubscriptionOfferDetails offer in details.GetSubscriptionOfferDetails() ?? [])
        {
            string? plan = PlanProductOf(offer.BasePlanId);
            if (plan is null)
            {
                continue;
            }

            bool isTrial = offer.OfferId == SubscriptionProducts.TrialOffer;
            if (offer.OfferId is null || isTrial)
            {
                string? price = offer.PricingPhases.PricingPhaseList.LastOrDefault()?.FormattedPrice;
                if (price is not null && (offer.OfferId is null || !prices.ContainsKey(plan)))
                {
                    prices[plan] = price;
                }

                if (isTrial)
                {
                    trialPlans.Add(plan);
                }
                else
                {
                    baseTokens[plan] = offer.OfferToken;
                }

                if (isTrial || !offerTokens.ContainsKey(plan))
                {
                    offerTokens[plan] = offer.OfferToken;
                }
            }
        }

        _plans = new PlanTokens(offerTokens, baseTokens, trialPlans);
    }

    private static string? PlanProductOf(string? basePlanId) => basePlanId switch
    {
        SubscriptionProducts.MonthlyBasePlan => SubscriptionProducts.Monthly,
        SubscriptionProducts.YearlyBasePlan => SubscriptionProducts.Yearly,
        _ => null,
    };

    // Slice 358: Play's answer, so a run can tell "not ok", "ok but empty" and "no price in the details" apart (BookPurchases reports prices_missing for the products left without a price). Codes, counts and product ids only. The Release log, not Debug only. One line per refresh (Slice 380).
    private static void TraceDetails(int asked, DetailsAnswer answer)
    {
        QueryProductDetailsResult details = answer.Details;
        string unfetched = string.Join(",", (details.UnfetchedProductList ?? []).Select(u => $"{u.ProductId}:{u.StatusCodeValue}"));
        string code = answer.Result?.ResponseCode.ToString() ?? NoResultCode;
        PurchaseTrace.Line($"product details: code={code} debug='{OneLine(answer.Result?.DebugMessage)}' asked={asked} got={details.ProductDetailsList?.Count ?? 0} unfetched={details.UnfetchedProductList?.Count ?? 0} [{unfetched}]");
    }

    private const string NoResultCode = "none";

    // Slice 358: the listener overload hands its BillingResult to the callback, and the details object's own Result is null there
    // (reading it threw and silently ended the refresh on 1.128.1-1.128.2). So every answer carries the BillingResult it really came with.
    private sealed record DetailsAnswer(QueryProductDetailsResult Details, BillingResult? Result);

    // A missing result is not ok: only a real Ok code is.
    private static bool IsOk(BillingResult? result) => result?.ResponseCode == BillingResponseCode.Ok;

    private const int TraceMessageMax = 200;

    // One line, capped, so a multi-line Play message cannot split the [Purchases] log line.
    private static string OneLine(string? text)
    {
        string flat = string.Join(' ', (text ?? string.Empty).Split(['\r', '\n', '\'', '\t'], StringSplitOptions.RemoveEmptyEntries));
        return flat.Length > TraceMessageMax ? flat[..TraceMessageMax] : flat;
    }

    // The product-details query, through the listener overload; null (a failed query) when Play does not answer in time, answers with no details, or the call throws.
    private static async Task<DetailsAnswer?> QueryViaListenerAsync(BillingClient client, QueryProductDetailsParams query, int asked)
    {
        var done = new TaskCompletionSource<DetailsAnswer?>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            client.QueryProductDetails(query, new DetailsListener(done));
        }
        catch (Exception ex) when (ex is Java.Lang.Exception or InvalidOperationException)
        {
            // no-fallback: RefreshAsync returns Failed=true and BookPurchases reports billing_error; the trace line names the exception type
            PurchaseTrace.Line($"product details: query threw {ex.GetType().Name} asked={asked}");
            return null;
        }

        if (await Task.WhenAny(done.Task, Task.Delay(SetupTimeout)) != done.Task)
        {
            PurchaseTrace.Line($"product details: no answer within the timeout asked={asked}");
            return null;
        }

        DetailsAnswer? answer = await done.Task;
        if (answer is null)
        {
            PurchaseTrace.Line($"product details: the answer had no details asked={asked}");
        }

        return answer;
    }

    private static async Task<(HashSet<string> Owned, HashSet<string> Pending, Dictionary<string, string> Proofs, bool Ok, SubscriptionStatus? Subscription, Dictionary<string, DateTimeOffset> PurchasedAt)> QueryOwnedAsync(BillingClient client)
    {
        var owned = new HashSet<string>();
        var pending = new HashSet<string>();
        var proofs = new Dictionary<string, string>();
        var times = new Dictionary<string, DateTimeOffset>();
        QueryPurchasesParams query = QueryPurchasesParams.NewBuilder().SetProductType(BillingClient.ProductType.Inapp).Build();
        IList<Purchase>? purchases = await OwnedPurchasesAsync(client, query);
        Collect(client, purchases, owned, pending, proofs, times);
        SubscriptionStatus? subscription = await QueryOwnedSubscriptionsAsync(client, owned, pending, proofs, times);
        return (owned, pending, proofs, purchases is not null, subscription, times);
    }

    // Slice 384: Play lists a subscription purchase while it is active, in its trial, in grace or cancelled but not yet ended, and no longer once it has ended. The on-device purchase carries no end date, so the answer has none; BookPurchases then asks the Worker
    // (Play subscriptionsv2) for the real end date after each read and keeps that (until its first answer the record counts from this read). A query that fails is no answer (null), never "no subscription".
    private static async Task<SubscriptionStatus?> QueryOwnedSubscriptionsAsync(BillingClient client, HashSet<string> owned, HashSet<string> pending, Dictionary<string, string> proofs, Dictionary<string, DateTimeOffset> times)
    {
        QueryPurchasesParams query = QueryPurchasesParams.NewBuilder().SetProductType(BillingClient.ProductType.Subs).Build();
        IList<Purchase>? purchases = await OwnedPurchasesAsync(client, query);
        if (purchases is null)
        {
            PurchaseTrace.Line("subscription purchases: the query did not answer");
            return null;
        }

        Collect(client, purchases, owned, pending, proofs, times);
        bool running = purchases.Any(p => p.PurchaseState == PurchaseState.Purchased && p.Products.Any(SubscriptionProducts.Is));
        return running ? new SubscriptionStatus(SubscriptionState.Active, null, SubscriptionProducts.Subscription) : SubscriptionStatus.None;
    }

    private static void Collect(BillingClient client, IList<Purchase>? purchases, HashSet<string> owned, HashSet<string> pending, Dictionary<string, string> proofs, Dictionary<string, DateTimeOffset> times)
    {
        foreach (Purchase purchase in purchases ?? [])
        {
            AcknowledgeIfNeeded(client, purchase); // seen here (start-up, store page, Restore), so a purchase approved later or bought on another phone is acknowledged too
            foreach (string product in purchase.Products)
            {
                (purchase.PurchaseState == PurchaseState.Purchased ? owned : pending).Add(product);
                DateTimeOffset at = DateTimeOffset.FromUnixTimeMilliseconds(purchase.PurchaseTime);
                if (purchase.PurchaseState == PurchaseState.Purchased && (!times.TryGetValue(product, out DateTimeOffset held) || at >= held))
                {
                    proofs[product] = purchase.PurchaseToken; // the newest purchase of a product gives its proof
                    times[product] = at;
                }
            }
        }
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

    private sealed class DetailsListener(TaskCompletionSource<DetailsAnswer?> done) : Java.Lang.Object, IProductDetailsResponseListener
    {
        public void OnProductDetailsResponse(BillingResult result, QueryProductDetailsResult details)
        {
            try
            {
                if (details is null)
                {
                    PurchaseTrace.Line($"product details: null details code={result?.ResponseCode.ToString() ?? NoResultCode} debug='{OneLine(result?.DebugMessage)}'"); // the code that says why Play sent no details
                }
            }
            catch (Exception)
            {
                // no-fallback: the trace is diagnostics only; the finally below still completes the query, so a throwing trace never reaches Play's callback thread
            }
            finally
            {
                done.TrySetResult(details is null ? null : new DetailsAnswer(details, result));
            }
        }
    }

    private sealed class AcknowledgeListener : Java.Lang.Object, IAcknowledgePurchaseResponseListener
    {
        public void OnAcknowledgePurchaseResponse(BillingResult result) => Debug.WriteLine($"Dino Reader acknowledged a purchase: {result.ResponseCode}");
    }

    // Play reports the result of the purchase sheet here; BuyAsync waits for the next one.
    private sealed class PurchasesUpdates : Java.Lang.Object, IPurchasesUpdatedListener
    {
        /// <summary>Slice 455: what one buy's completion says: its outcome and whether Play said the account already owns the item.</summary>
        public readonly record struct Result(BuyOutcome Outcome, bool AlreadyOwned);

        private TaskCompletionSource<Result>? _next;

        /// <summary>The client the updates arrive from, so a purchase reported here (also one nobody is waiting for) is acknowledged.</summary>
        public BillingClient? Client { get; set; }
        public Task<Result> NextAsync()
        {
            _next = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
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

            _next?.TrySetResult(new Result(OutcomeOf(result, purchases), result.ResponseCode == BillingResponseCode.ItemAlreadyOwned));
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
