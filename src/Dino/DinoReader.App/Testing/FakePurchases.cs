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

    public string StoreName => PurchaseProofs.AndroidStore;

    public static string ProductOf(string bookId) => ProductPrefix + bookId;

    /// <summary>Replaces the set of sold test books (stems or book ids; no asset is read, so it is instant).</summary>
    public static void SetPaid(IEnumerable<string> stemsOrIds) => _paidNames = new HashSet<string>(stemsOrIds, StringComparer.OrdinalIgnoreCase);

    /// <summary>The test store's line for a book, sold with its fake product when the hook said so (by its stem or its id).</summary>
    public static StoreItem Marked(StoreItem item, string stem) =>
        _paidNames.Contains(item.Id) || _paidNames.Contains(stem) ? item with { Sale = BookSale.Paid, ProductId = ProductOf(item.Id), UnlockedBy = [ProductOf(item.Id)] } : item;

    public Task<PurchaseSnapshot> RefreshAsync(IReadOnlyCollection<string> productIds, bool interactive, CancellationToken cancel) => Task.FromResult(Snapshot(productIds));

    public Task<PurchaseSnapshot> RestoreAsync(IReadOnlyCollection<string> productIds, CancellationToken cancel) => Task.FromResult(Snapshot(productIds));

    public async Task<BuyOutcome> BuyAsync(string productId, CancellationToken cancel)
    {
        await Task.Delay(800, cancel); // long enough for the wait overlay (it appears after 500 ms)
        if (NextBuy == BuyOutcome.Bought)
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

        Dictionary<string, string> prices = productIds.ToDictionary(p => p, _ => Price);
        Dictionary<string, string> proofs = OwnedProducts.ToDictionary(p => p, p => "fake-token-" + p);
        return new PurchaseSnapshot(prices, new HashSet<string>(OwnedProducts), new HashSet<string>(PendingProducts), proofs, true);
    }
}
#endif
