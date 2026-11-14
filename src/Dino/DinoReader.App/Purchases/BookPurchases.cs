using System.Diagnostics;
using DinoReader.Core;
using Sirous.Reading.Application.Usage;

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

    private volatile bool _hasInternet;
    private volatile bool _lastPricesFailed;
    private string _reportedProblem = string.Empty;

    /// <summary>How the latest read went (Slice 358); <see cref="BookAccess.Plan"/> turns it into the card's strip. One rule on every platform: a device with no internet has not reached the store (the sand strip); with internet any failed price query is a failure (the red strip).</summary>
    public StoreAnswer Answer => BookAccess.AnswerOf(_hasInternet, _lastPricesFailed, _snapshot.Restricted);

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
        StoreItem[] listed = [.. items];
        string[] products = [.. listed.Where(i => i.Sale == BookSale.Paid).SelectMany(i => i.Products).Distinct()];
        await _gate.WaitAsync(cancel);
        try
        {
            PurchaseSnapshot read = restore ? await platform.RestoreAsync(products, cancel) : await platform.RefreshAsync(products, interactive, cancel);
            bool hasInternet = HasInternet(); // read once per read: the only thing that says "store not reached"
            ReportReadProblems(listed, read, hasInternet);
            Keep(read, hasInternet);
            return _snapshot;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Debug.WriteLine($"Dino Reader could not read the store's purchases: {ex.Message}");
            fallbacks.Report(FallbackArea.Store, FallbackReason.StoreFailed);
            if (NoteRead(HasInternet(), pricesFailed: true))
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }

            return _snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    // A device with no internet is the calm "connect" case: no event for it (the store was not reached), whatever the platform answered.
    private void ReportReadProblems(IReadOnlyCollection<StoreItem> items, PurchaseSnapshot read, bool hasInternet)
    {
        if (!read.Reachable)
        {
            if (hasInternet)
            {
                fallbacks.Report(FallbackArea.Store, FallbackReason.StoreUnreachable); // the platform store could not be asked though the device is online (no Play, a refused setup): the last snapshot stays
            }

            return;
        }

        if (hasInternet)
        {
            ReportPriceProblem(items, read);
        }

        if (read.Owned.Any(product => !read.Proofs.ContainsKey(product)))
        {
            fallbacks.Report(FallbackArea.Store, FallbackReason.StoreFailed); // an owned book has no proof to send (Windows: no Store ID key), so its files are not asked for until the next read
        }
    }

    // A failed price query is billing_error; sold books left with no priced product are prices_missing (counted per book, not per product, since a bundle alone may lack a price while every card shows Buy).
    // A device that forbids purchases (Restricted) is an answer, not a fault: no event. The event is sent once per change of the problem, not on every read; the Debug marker on every read.
    private void ReportPriceProblem(IReadOnlyCollection<StoreItem> items, PurchaseSnapshot read)
    {
        int sold = items.Count(i => i.Sale == BookSale.Paid);
        int unpriced = read.PricesFailed || read.Restricted ? 0 : BookAccess.UnpricedBooks(items, BookAccess.KeepPrices(_snapshot.Prices, read.Prices)).Length; // the prices the cards use: the kept ones overlaid with this read
        string? reason = read.PricesFailed ? FallbackReason.BillingError : (unpriced > 0 ? FallbackReason.PricesMissing : null);
#if DEBUG
        if (reason is not null)
        {
            Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write(read.PricesFailed ? "[Purchases] billing error prices" : $"[Purchases] prices missing count={unpriced} of={sold}");
        }
#endif
        string key = reason is null ? string.Empty : $"{reason}:{unpriced}";
        if (key == _reportedProblem)
        {
            return;
        }

        _reportedProblem = key;
        if (reason is not null)
        {
            PurchaseTrace.Line($"price problem {reason}: unpriced_books={unpriced} sold_books={sold}");
            fallbacks.Report(FallbackArea.Store, reason); // billing_error: the price query itself failed (its code is in the platform's trace line); prices_missing: the cards say "can't be bought right now"
        }
    }

    // The device's internet is the one thing that decides "store not reached" (MAUI Connectivity: only NetworkAccess.Internet counts), the same on every platform.
    private bool HasInternet()
    {
#if DINO_TEST_BOOKS
        if (platform is SwitchablePurchases { Override: Testing.FakePurchases { Offline: true } })
        {
            return false; // the Debug fake's offline flag stands for a phone with no internet, so the "not reached" rule is the one under test
        }
#endif
        return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
    }

    // Records how the latest read went; true when the answer changed (so the cards must be planned again).
    private bool NoteRead(bool hasInternet, bool pricesFailed)
    {
        StoreAnswer before = Answer;
        _hasInternet = hasInternet;
        _lastPricesFailed = pricesFailed;
        return before != Answer;
    }

    // A read the platform could not make keeps what was known; any other read replaces what is owned and pending even with no internet (a purchase made elsewhere still shows), and its prices
    // overlay the last known ones (a price query that failed or timed out while the purchases query worked must not blank every card).
    private void Keep(PurchaseSnapshot read, bool hasInternet)
    {
        bool flipped = NoteRead(hasInternet, read.PricesFailed || !read.Reachable);
        if (!read.Reachable)
        {
            if (flipped)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }

            return;
        }

        PurchaseSnapshot before = _snapshot;
        Interlocked.Exchange(ref _readAt, Stopwatch.GetTimestamp());
        _snapshot = read with { Prices = BookAccess.KeepPrices(before.Prices, read.Prices) };
#if DEBUG
        Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[Purchases] snapshot owned={read.Owned.Count} pending={read.Pending.Count} prices={read.Prices.Count}");
#endif
        if (flipped || before.PricesFailed != read.PricesFailed || before.Restricted != read.Restricted || !before.Owned.SetEquals(read.Owned) || !before.Pending.SetEquals(read.Pending) || !before.Prices.OrderBy(p => p.Key).SequenceEqual(_snapshot.Prices.OrderBy(p => p.Key)))
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

    /// <summary>The ONE gate for every behaviour added by the paid-books work (settle wait, refusal codes, purchase reads, unlisted-book reports, the backup format): true only when the switch is known AND on, so off or unknown can only take the path of before the switch existed.</summary>
    public bool SwitchKnownOn => ConfigKnown && Enabled;

    /// <summary>Raised on the main thread when a fetch brought a different remote config (so the switch may have changed).</summary>
    public event EventHandler? ConfigChanged
    {
        add => config.Changed += value;
        remove => config.Changed -= value;
    }

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
    public string? ProofHeader(StoreItem item) => purchases.ProofHeader(SwitchKnownOn, item);

    /// <summary>The card plan for a listed book from the current snapshot.</summary>
    public StoreCardPlan PlanFor(StoreItem item, bool onShelf)
    {
        PurchaseSnapshot snapshot = purchases.Snapshot;
        string? price = BookAccess.PriceOf(item, snapshot.Prices);
        return BookAccess.Plan(SwitchKnownOn, item, onShelf, snapshot.Owned, snapshot.Pending, price, purchases.Answer);
    }

    public bool MayUpdate(StoreItem item) => BookAccess.MayUpdate(SwitchKnownOn, item, purchases.Snapshot.Owned);
}
