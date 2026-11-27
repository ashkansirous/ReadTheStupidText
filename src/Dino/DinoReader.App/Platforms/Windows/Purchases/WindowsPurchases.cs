using System.Diagnostics;
using System.Net.Http.Json;
using DinoReader.Core;
using Sirous.Reading.Application.Usage;
using Windows.Services.Store;

namespace DinoReader;

/// <summary>
/// The Microsoft Store behind <see cref="IBookPurchases"/> (Slice 346): <c>Windows.Services.Store</c> durable add-ons. Each book's product id is the add-on's developer-chosen product id in Partner Center
/// (<c>StoreProduct.InAppOfferToken</c>). The proof for every owned product is one Store ID key, made by the store from a short-lived ticket the Worker issues (Microsoft's documented flow); the Worker reads
/// the key with the Store collections API. Nothing is kept on disk. An unpackaged run, or an app not associated with the Store, cannot ask (0x803F6107), which is an unreachable store here.
/// </summary>
public sealed class WindowsPurchases : IBookPurchases
{
    private const string DurableKind = "Durable";
    private const string TicketPath = "/api/stories/windows-ticket";
    private const string RedeemAddress = "https://account.microsoft.com/billing/redeem";
    private const string ManageAddress = "https://account.microsoft.com/services"; // the Microsoft account's own subscriptions page
    private const string PublisherUserId = "dino"; // names no person: the Store ID key needs some value, and this one is the same for everybody
    private static readonly HttpClient Http = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };

    private StoreContext? _context;
    private readonly Dictionary<string, StoreProduct> _products = [];

    public string StoreName => DinoReader.Core.PurchaseProofs.WindowsStore;

    public async Task<PurchaseSnapshot> RefreshAsync(IReadOnlyCollection<string> productIds, bool interactive, CancellationToken cancel)
    {
        // Slice 460: the launch read waits for the window in PrepareAsync, before it takes the read gate; here (and for a press: Restore, the Store tab) the window is asked for once and the answer is final.
        (StoreContext? context, StoreCause? failure) = OpenContext();
        if (context is null)
        {
            return PurchaseSnapshot.Empty with { Failure = failure }; // the cause travels with this read, never in a field another read could overwrite (Slice 460)
        }

        (Dictionary<string, string> prices, bool pricesFailed) = await QueryPricesAsync(context);
        (HashSet<string> owned, bool ok, SubscriptionStatus? subscription) = await QueryOwnedAsync(context);
        Dictionary<string, string> proofs = await ProofsAsync(context, owned);
        // Slice 397: TrialAvailable stays null. StoreSubscriptionInfo.HasTrialPeriod says the SKU has a trial configured, not that this account may still use it, so Windows keeps "Try free" (Decision 4) until the Store answers eligibility.
        return new PurchaseSnapshot(prices, owned, new HashSet<string>(), proofs, ok, pricesFailed, Subscription: subscription);
    }

    // The Store has no separate restore: the account's add-ons are read again.
    public Task<PurchaseSnapshot> RestoreAsync(IReadOnlyCollection<string> productIds, CancellationToken cancel) => RefreshAsync(productIds, interactive: true, cancel);

    /// <summary>Slice 455: the paid offer and whether the Store said the account already owns it, for the purchase usage count.</summary>
    public BuyNote LastBuyNote { get; private set; } = BuyNote.None;

    public async Task<BuyOutcome> BuyAsync(string productId, CancellationToken cancel)
    {
        LastBuyNote = BuyNote.None;
        StoreContext? context = OpenContext().Context; // a purchase is a press: no waiting for the window
        if (context is null)
        {
            return BuyOutcome.Failed;
        }

        if (!_products.ContainsKey(productId))
        {
            await QueryPricesAsync(context);
        }

        if (!_products.TryGetValue(productId, out StoreProduct? product))
        {
            return BuyOutcome.Failed;
        }

        StorePurchaseResult result = await product.RequestPurchaseAsync();
        LastBuyNote = new BuyNote(Offer: null, AlreadyOwned: result.Status == StorePurchaseStatus.AlreadyPurchased); // Slice 455: no offer from the buy itself; BookPurchases reads the subscription's trial state (collection data) after the buy
        return result.Status switch
        {
            StorePurchaseStatus.Succeeded or StorePurchaseStatus.AlreadyPurchased => BuyOutcome.Bought,
            StorePurchaseStatus.NotPurchased => BuyOutcome.Cancelled,
            _ => BuyOutcome.Failed,
        };
    }

    public async Task<bool> RedeemCodeAsync() => await Launcher.Default.OpenAsync(new Uri(RedeemAddress));

    // Slice 460: the first read after launch may come before the app's window exists. The context is asked for again after 1 s, 3 s and 7 s (the one retry schedule) only while the window is missing; any other failure
    // (not associated with the Store, an unpackaged run) is final for that read and not waited through. The cause is returned with the result, never kept in a field. The read's Cancel ends the wait.
    // Review 2 #6: concurrent launch reads share one window wait (one cached Task), and each caller's Cancel ends only its own await. The wait is cleared when it ends, so a later read can wait again.
    public Task PrepareAsync(CancellationToken cancel)
    {
        if (_context is not null)
        {
            return Task.CompletedTask;
        }

        Task wait;
        lock (_windowWaitLock)
        {
            wait = _windowWait ??= Task.Run(WaitForWindowAsync);
        }

        return wait.WaitAsync(cancel);
    }

    private readonly object _windowWaitLock = new();
    private Task? _windowWait;

    private async Task WaitForWindowAsync()
    {
        try
        {
            await ContextWithRetryAsync(); // sets _context when the window shows up; a miss is answered by the read's own OpenContext
        }
        finally
        {
            lock (_windowWaitLock)
            {
                _windowWait = null;
            }
        }
    }

    // The wait is cut at WindowWaitBudget, so it plus the price, owned and proof queries stays well inside the 25 s the downloads wait for this read (review 2 #2). The window is polled evenly (review 2 #5), so one that
    // appears at any moment inside the cap is found within WindowPoll of showing up.
    private static readonly TimeSpan WindowWaitBudget = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan WindowPoll = TimeSpan.FromMilliseconds(500);

    private async Task ContextWithRetryAsync()
    {
        using CancellationTokenSource limit = new(WindowWaitBudget);
        (StoreContext? Context, StoreCause? Failure) opened = OpenContext();
        try
        {
            for (int polls = 1; opened.Context is null && opened.Failure == StoreCause.NoWindow; polls++)
            {
                await Task.Delay(WindowPoll, limit.Token);
                PurchaseTrace.Line($"store context retry: poll {polls} after {StoreCause.NoWindow}");
                opened = OpenContext();
            }
        }
        catch (OperationCanceledException)
        {
            // no-fallback: the window wait ran out; the read's own OpenContext then answers NoWindow and BookPurchases reports it
            PurchaseTrace.Line("store context: the window wait ran out");
        }

        PurchaseTrace.Line($"store context: {(opened.Context is null ? $"none ({opened.Failure?.ToString() ?? "other"})" : "ready")}");
    }

    // The window is looked for first: the Store context is not made before the window handler exists, and it needs that window to show its purchase sheet over (Slice 460).
    private (StoreContext? Context, StoreCause? Failure) OpenContext()
    {
        if (_context is not null)
        {
            return (_context, null);
        }

        try
        {
            if (Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window window)
            {
                return (null, StoreCause.NoWindow);
            }

            StoreContext context = StoreContext.GetDefault();
            WinRT.Interop.InitializeWithWindow.Initialize(context, WinRT.Interop.WindowNative.GetWindowHandle(window));
            return (_context = context, null);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            // no-fallback: the caller returns the empty (unreachable) snapshot, which BookPurchases reports with the cause carried in it (not_associated, else store_failed); an unpackaged run has no Store to ask
            Debug.WriteLine($"Dino Reader could not open the Microsoft Store context: {ex.Message}");
            PurchaseTrace.Line($"store context failed: hresult=0x{ex.HResult:X8} {ex.GetType().Name}"); // the HRESULT only, never the message; the retry is unchanged (Slice 460 review)
            return (null, StoreCauses.OfStoreHResult(ex.HResult) is StoreCause.NotAssociated ? StoreCause.NotAssociated : null);
        }
    }

    private async Task<(Dictionary<string, string> Prices, bool Failed)> QueryPricesAsync(StoreContext context)
    {
        var prices = new Dictionary<string, string>();
        StoreProductQueryResult result = await context.GetAssociatedStoreProductsAsync([DurableKind]);
        foreach (StoreProduct product in result.Products.Values)
        {
            if (!string.IsNullOrEmpty(product.InAppOfferToken))
            {
                _products[product.InAppOfferToken] = product;
                prices[product.InAppOfferToken] = product.Price.FormattedPrice;
            }
        }

        int hresult = result.ExtendedError?.HResult ?? 0;
        bool failed = hresult != 0; // a Store error is billing_error; an answer with no priced add-on is prices_missing
        PurchaseTrace.Line($"product details: hresult=0x{hresult:X8} got={result.Products.Count} priced={prices.Count}");
        return (prices, failed);
    }

    private static async Task<(HashSet<string> Owned, bool Ok, SubscriptionStatus? Subscription)> QueryOwnedAsync(StoreContext context)
    {
        StoreProductQueryResult result = await context.GetUserCollectionAsync([DurableKind]);
        bool ok = result.ExtendedError is null || result.ExtendedError.HResult == 0;
        HashSet<string> owned = [.. result.Products.Values.Select(p => p.InAppOfferToken).Where(token => !string.IsNullOrEmpty(token))];
        return (owned, ok, ok ? SubscriptionOf(result.Products.Values) : null);
    }

    // Slice 384: a subscription add-on is a durable add-on whose collection data carries its end date and whether it is a trial (Windows.Services.Store StoreCollectionData); the newest end among the plans decides, and an end in
    // the past is no subscription. A failed read is no answer (null), never "no subscription".
    private static SubscriptionStatus SubscriptionOf(IEnumerable<StoreProduct> products)
    {
        SubscriptionStatus? best = null;
        foreach (StoreProduct product in products.Where(p => SubscriptionProducts.Is(p.InAppOfferToken)))
        {
            StoreCollectionData? data = product.Skus.Select(sku => sku.CollectionData).FirstOrDefault(d => d is not null);
            DateTimeOffset? ends = data is null ? null : data.EndDate;
            if (ends is { } end && end <= DateTimeOffset.UtcNow)
            {
                continue;
            }

            if (best?.EndsAt is null || (ends is { } later && later > best.EndsAt))
            {
                best = new SubscriptionStatus(data?.IsTrial == true ? SubscriptionState.Trial : SubscriptionState.Active, ends, product.InAppOfferToken);
            }
        }

        return best ?? SubscriptionStatus.None;
    }

    public async Task<bool> ManageSubscriptionAsync() => await Launcher.Default.OpenAsync(new Uri(ManageAddress));

    // One Store ID key covers every product of the account; the Worker reads the whole list with it.
    private static async Task<Dictionary<string, string>> ProofsAsync(StoreContext context, HashSet<string> owned)
    {
        var proofs = new Dictionary<string, string>();
        if (owned.Count == 0)
        {
            return proofs;
        }

        try
        {
            string? ticket = await TicketAsync();
            string key = ticket is null ? string.Empty : await context.GetCustomerCollectionsIdAsync(ticket, PublisherUserId);
            if (key.Length > 0)
            {
                foreach (string product in owned)
                {
                    proofs[product] = key;
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Runtime.InteropServices.COMException)
        {
            // no-fallback: the snapshot then holds an owned book without a proof, which BookPurchases reports as a store fallback; the book is not asked for and the next open tries again
            Debug.WriteLine($"Dino Reader could not make a Store ID key: {ex.Message}");
        }

        return proofs;
    }

    private static async Task<string?> TicketAsync()
    {
        using HttpResponseMessage response = await Http.GetAsync(StoreClient.Resolve(TicketPath));
        if (!response.IsSuccessStatusCode)
        {
            // Slice 357: background proof work, so the person is told nothing here; the kind is only logged. A Worker answer is a status with no body.
            Debug.WriteLine($"Dino Reader: the Worker gave no Store ticket ({(int)response.StatusCode}, {Sirous.Reading.Application.Net.HttpErrorClassifier.Classify((int)response.StatusCode)})");
            return null; // the Worker has no ticket to give (books are not sold yet, or its Microsoft keys are missing)
        }

        TicketBody? body = await response.Content.ReadFromJsonAsync<TicketBody>();
        return string.IsNullOrWhiteSpace(body?.Ticket) ? null : body.Ticket;
    }

    private sealed record TicketBody(string? Ticket);
}

public static partial class PlatformPurchases
{
    public static partial IBookPurchases Create() => new WindowsPurchases();
}
