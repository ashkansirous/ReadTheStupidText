using System.Diagnostics;
using DinoReader.Core;
using ReadTheStupidText.Application.Usage;

namespace DinoReader;

/// <summary>
/// The app's one view of what the platform store says about paid books (Slice 346). It wraps the platform <see cref="IBookPurchases"/>, keeps the last snapshot in memory only and tells
/// listeners when it changed. Nothing here is saved: on launch and whenever the store page opens the app asks the store again, so a book bought or redeemed outside the app shows as owned
/// without pressing Restore. A store that cannot be reached leaves the last snapshot in place.
/// </summary>
public sealed class BookPurchases(IBookPurchases platform, IFallbackSink fallbacks)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private PurchaseSnapshot _snapshot = PurchaseSnapshot.Empty;

    /// <summary>Raised after a read that changed the snapshot, on whatever thread ran it; a subscriber that touches UI marshals itself.</summary>
    public event EventHandler? Changed;

    public PurchaseSnapshot Snapshot => _snapshot;

    public string StoreName => platform.StoreName;

    private static readonly TimeSpan FreshFor = TimeSpan.FromSeconds(60);
    private long _readAt;

    /// <summary>Forgets that the store was read, so the next listing asks again (the Debug fake store swaps the store under the app).</summary>
    public void MarkStale() => Interlocked.Exchange(ref _readAt, 0);

    /// <summary>True when the store answered less than a minute ago, so a background listing need not ask again (a store page open always does).</summary>
    public bool IsFresh => _readAt != 0 && Stopwatch.GetElapsedTime(Interlocked.Read(ref _readAt)) < FreshFor;

    /// <summary>Asks the store again for the products of the listing. Never throws; a failure keeps the last snapshot and reports one store fallback.</summary>
    public Task<PurchaseSnapshot> RefreshAsync(IEnumerable<StoreItem> items, CancellationToken cancel, bool interactive = false) => ReadAsync(items, restore: false, interactive, cancel);

    /// <summary>The Restore purchases button: as <see cref="RefreshAsync"/>, with the platform's own restore where it has one.</summary>
    public Task<PurchaseSnapshot> RestoreAsync(IEnumerable<StoreItem> items, CancellationToken cancel) => ReadAsync(items, restore: true, interactive: true, cancel);

    /// <summary>Buys the product, then re-reads the store; returns how it ended. Never throws.</summary>
    public async Task<BuyOutcome> BuyAsync(string productId, IEnumerable<StoreItem> items, CancellationToken cancel)
    {
        BuyOutcome outcome;
        try
        {
            outcome = await platform.BuyAsync(productId, cancel);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Debug.WriteLine($"Dino Reader purchase failed: {ex}");
            fallbacks.Report(FallbackArea.Store, FallbackReason.StoreFailed); // the grown-up sees a plain "didn't work" message; nothing changes
            return BuyOutcome.Failed;
        }

        Debug.WriteLine($"Dino Reader purchase of {productId} ended: {outcome}");
        await ReadAsync(items, restore: false, interactive: false, cancel);
        return outcome;
    }

    /// <summary>Opens the store's redeem screen; false when there is none.</summary>
    public async Task<bool> RedeemCodeAsync()
    {
        try
        {
            return await platform.RedeemCodeAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Debug.WriteLine($"Dino Reader could not open the redeem screen: {ex.Message}");
            fallbacks.Report(FallbackArea.Store, FallbackReason.StoreFailed);
            return false;
        }
    }

    /// <summary>The Worker header for a book's files, or null when the book is not sold or no proof is held.</summary>
    public string? ProofHeader(bool paidBooksOn, StoreItem item) =>
        BookAccess.ProofHeader(paidBooksOn, item, _snapshot.Proofs, platform.StoreName);

    private async Task<PurchaseSnapshot> ReadAsync(IEnumerable<StoreItem> items, bool restore, bool interactive, CancellationToken cancel)
    {
        string[] products = [.. items.Where(i => i.Sale == BookSale.Paid).SelectMany(i => i.Products).Distinct()];
        await _gate.WaitAsync(cancel);
        try
        {
            PurchaseSnapshot read = restore ? await platform.RestoreAsync(products, cancel) : await platform.RefreshAsync(products, interactive, cancel);
            if (!read.Reachable)
            {
                fallbacks.Report(FallbackArea.Store, FallbackReason.StoreUnreachable); // the platform store could not be asked (no Play, offline, a timeout): the last snapshot stays
            }

            else if (read.Owned.Any(product => !read.Proofs.ContainsKey(product)))
            {
                fallbacks.Report(FallbackArea.Store, FallbackReason.StoreFailed); // an owned book has no proof to send (Windows: no Store ID key), so its files are not asked for until the next read
            }

            Keep(read);
            return _snapshot;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Debug.WriteLine($"Dino Reader could not read the store's purchases: {ex.Message}");
            fallbacks.Report(FallbackArea.Store, FallbackReason.StoreFailed);
            return _snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    // An unreachable store keeps what was known (a phone that went offline still reads and offers what it knew); a reachable one replaces what is owned and pending, and its prices
    // overlay the last known ones (a price query that failed or timed out while the purchases query worked must not blank every card).
    private void Keep(PurchaseSnapshot read)
    {
        if (!read.Reachable)
        {
            return;
        }

        PurchaseSnapshot before = _snapshot;
        Interlocked.Exchange(ref _readAt, Stopwatch.GetTimestamp());
        _snapshot = read with { Prices = BookAccess.KeepPrices(before.Prices, read.Prices) };
#if DEBUG
        ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Purchases] snapshot owned={read.Owned.Count} pending={read.Pending.Count} prices={read.Prices.Count}");
#endif
        if (!before.Owned.SetEquals(read.Owned) || !before.Pending.SetEquals(read.Pending) || !before.Prices.OrderBy(p => p.Key).SequenceEqual(_snapshot.Prices.OrderBy(p => p.Key)))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}

/// <summary>What the app sells right now: the remote switch and the store's proofs, the one place StoreClient, the updater and the pages ask (Slice 346).</summary>
public sealed class BookSales(AppConfigClient config, BookPurchases purchases)
{
#if DINO_TEST_BOOKS
    /// <summary>Only the Debug <c>fake-purchases</c> hook sets this (and the Windows VM test package); a Store build never has it.</summary>
    public bool? ForcedEnabled { get; set; }

    /// <summary>True only while the remote config says <c>paidBooks.enabled</c> is true; off means every book is free and no price, Buy or Restore shows.</summary>
    public bool Enabled => ForcedEnabled ?? config.Current.PaidBooksEnabled;
#else
    /// <summary>True only while the remote config says <c>paidBooks.enabled</c> is true; off means every book is free and no price, Buy or Restore shows.</summary>
    public bool Enabled => config.Current.PaidBooksEnabled;
#endif

    public BookPurchases Purchases => purchases;

#if DINO_TEST_BOOKS
    /// <summary>True once the switch is known: a real config is held (fetched, or a kept copy), or the Debug hook forced it.</summary>
    public bool ConfigKnown => ForcedEnabled is not null || config.HasCopy;
#else
    /// <summary>True once the switch is known: a real config is held (fetched, or a kept copy).</summary>
    public bool ConfigKnown => config.HasCopy;
#endif

    /// <summary>The value the first-run picker and a restore decide with: the switch once it is known, else "sold", so only books whose listing says free are offered or fetched (Slice 346).</summary>
    public bool EnabledForOffer => BookAccess.AssumeSold(ConfigKnown, Enabled);

    private static readonly TimeSpan TabRefreshGap = TimeSpan.FromMinutes(10);
    private int _launchRefreshStarted;
    private long _lastTabRefreshTicks;

    /// <summary>Starts one quiet background read of the remote config per app launch, and only while no copy is held yet; returns at once and never throws. The launch fetch already runs in the app start, so this only covers a launch whose start-up fetch found no network. No user-facing path waits for it: they use the last known value (default off), and the app's Changed event tells the pages when a fetch brings a different one.</summary>
    public void RefreshConfigInBackground()
    {
        if (!ConfigKnown && Interlocked.Exchange(ref _launchRefreshStarted, 1) == 0)
        {
            StartQuietRefresh();
        }
    }

    /// <summary>The grown-ups Store tab opened: one quiet background read, at most once per 10 minutes.</summary>
    public void RefreshConfigOnStoreTab()
    {
        long now = Environment.TickCount64;
        long last = Interlocked.Read(ref _lastTabRefreshTicks);
        bool due = last == 0 || now - last >= (long)TabRefreshGap.TotalMilliseconds;
        if (due && Interlocked.CompareExchange(ref _lastTabRefreshTicks, Math.Max(now, 1), last) == last)
        {
            StartQuietRefresh();
        }
    }

    // Offline or failing: the last known value stays and no fallback event is reported for this read (the launch fetch reports as before).
    private void StartQuietRefresh()
    {
        // no-fallback: an offline or failed background read keeps the last known switch silently; the launch fetch reports its own failure
        _ = config.RefreshAsync(CancellationToken.None, reportFallback: false);
    }

    /// <summary>The purchase proof header for a book's files, or null when none is needed or held.</summary>
    public string? ProofHeader(StoreItem item) => purchases.ProofHeader(Enabled, item);

    /// <summary>The card plan for a listed book from the current snapshot.</summary>
    public StoreCardPlan PlanFor(StoreItem item, bool onShelf)
    {
        PurchaseSnapshot snapshot = purchases.Snapshot;
        string? price = item.Products.Select(p => snapshot.Prices.GetValueOrDefault(p)).FirstOrDefault(p => p is not null);
        return BookAccess.Plan(Enabled, item, onShelf, snapshot.Owned, snapshot.Pending, price);
    }

    public bool MayUpdate(StoreItem item) => BookAccess.MayUpdate(Enabled, item, purchases.Snapshot.Owned);
}
