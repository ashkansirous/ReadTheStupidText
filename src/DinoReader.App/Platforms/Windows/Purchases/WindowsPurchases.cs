using System.Diagnostics;
using System.Net.Http.Json;
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
    private const string PublisherUserId = "dino"; // names no person: the Store ID key needs some value, and this one is the same for everybody
    private static readonly HttpClient Http = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };

    private StoreContext? _context;
    private readonly Dictionary<string, StoreProduct> _products = [];

    public string StoreName => DinoReader.Core.PurchaseProofs.WindowsStore;

    public async Task<PurchaseSnapshot> RefreshAsync(IReadOnlyCollection<string> productIds, bool interactive, CancellationToken cancel)
    {
        StoreContext? context = Context();
        if (context is null)
        {
            return PurchaseSnapshot.Empty;
        }

        (Dictionary<string, string> prices, bool pricesFailed) = await QueryPricesAsync(context);
        (HashSet<string> owned, bool ok) = await QueryOwnedAsync(context);
        Dictionary<string, string> proofs = await ProofsAsync(context, owned);
        return new PurchaseSnapshot(prices, owned, new HashSet<string>(), proofs, ok, pricesFailed);
    }

    // The Store has no separate restore: the account's add-ons are read again.
    public Task<PurchaseSnapshot> RestoreAsync(IReadOnlyCollection<string> productIds, CancellationToken cancel) => RefreshAsync(productIds, interactive: true, cancel);

    public async Task<BuyOutcome> BuyAsync(string productId, CancellationToken cancel)
    {
        StoreContext? context = Context();
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
        return result.Status switch
        {
            StorePurchaseStatus.Succeeded or StorePurchaseStatus.AlreadyPurchased => BuyOutcome.Bought,
            StorePurchaseStatus.NotPurchased => BuyOutcome.Cancelled,
            _ => BuyOutcome.Failed,
        };
    }

    public async Task<bool> RedeemCodeAsync() => await Launcher.Default.OpenAsync(new Uri(RedeemAddress));

    // The Store context needs the window it will show its purchase sheet over.
    private StoreContext? Context()
    {
        if (_context is not null)
        {
            return _context;
        }

        try
        {
            StoreContext context = StoreContext.GetDefault();
            if (Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window window)
            {
                return null;
            }

            WinRT.Interop.InitializeWithWindow.Initialize(context, WinRT.Interop.WindowNative.GetWindowHandle(window));
            return _context = context;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            // no-fallback: the caller returns the empty (unreachable) snapshot, which BookPurchases reports as store_unreachable; an unpackaged run has no Store to ask
            Debug.WriteLine($"Dino Reader could not open the Microsoft Store context: {ex.Message}");
            return null;
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

    private static async Task<(HashSet<string> Owned, bool Ok)> QueryOwnedAsync(StoreContext context)
    {
        StoreProductQueryResult result = await context.GetUserCollectionAsync([DurableKind]);
        bool ok = result.ExtendedError is null || result.ExtendedError.HResult == 0;
        HashSet<string> owned = [.. result.Products.Values.Select(p => p.InAppOfferToken).Where(token => !string.IsNullOrEmpty(token))];
        return (owned, ok);
    }

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
            Debug.WriteLine($"Dino Reader: the Worker gave no Store ticket ({(int)response.StatusCode}, {ReadTheStupidText.Application.Net.HttpErrorClassifier.Classify((int)response.StatusCode)})");
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
