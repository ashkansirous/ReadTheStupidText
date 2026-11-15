using System.Diagnostics;
using DinoReader.Core;
using Sirous.Reading.Application.Usage;

namespace DinoReader;

/// <summary>
/// The app's one view of what the platform store says about paid books (Slice 346). It wraps the platform <see cref="IBookPurchases"/>, keeps the last snapshot in memory only and tells
/// listeners when it changed. Nothing here is saved: on launch and whenever the store page opens the app asks the store again, so a book bought or redeemed outside the app shows as owned
/// without pressing Restore. A store that cannot be reached leaves the last snapshot in place.
/// </summary>
public sealed class BookPurchases(IBookPurchases platform, IFallbackSink fallbacks, SubscriptionStore? subscriptionStore = null, ISubscriptionEnd? subscriptionEnd = null) : IDisposable
{
    // Slice 384: the Worker that asks Apple or Google for the subscription's real end date (iOS and Android; Windows keeps its own end date). The host registers none, so the app uses the real client.
    private readonly ISubscriptionEnd _end = subscriptionEnd ?? new SubscriptionEndClient();
    private readonly SemaphoreSlim _endGate = new(1, 1);

    // Slice 384: the device's kept subscription record and per-book unlock records; the one shared store when the host registers none (the app has one process-wide instance).
    private readonly SubscriptionStore subscription = subscriptionStore ?? SubscriptionStore.Shared;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private PurchaseSnapshot _snapshot = PurchaseSnapshot.Empty;

    // Slice 390: the listing of the last read, re-read when the platform store reports an update (an approved Ask to Buy, a renewal, a refund).
    private readonly object _listedLock = new(); // _lastListed is written by the page's thread and read on the platform's update thread
    private StoreItem[]? _lastListed;
    private int _listening;

    /// <summary>Raised after a read that changed the snapshot, on whatever thread ran it; a subscriber that touches UI marshals itself.</summary>
    public event EventHandler? Changed;

    /// <summary>The last read as the books must see it: the subscription's products are owned only while the kept record still grants access (<see cref="SubscriptionAccess.OwnedWhile"/>).</summary>
    public PurchaseSnapshot Snapshot
    {
        get
        {
            PurchaseSnapshot held = _snapshot;
            bool grants = subscription.Grants;
            return grants ? held : held with { Owned = SubscriptionAccess.OwnedWhile(held.Owned, grants), Proofs = SubscriptionAccess.ProofsWhile(held.Proofs, grants) };
        }
    }

    private volatile bool _hasInternet;
    private volatile bool _lastPricesFailed;
    private string _reportedProblem = string.Empty;
    private bool _reportedIncomplete;

    /// <summary>How the latest read went (Slice 358); <see cref="BookAccess.Plan"/> turns it into the card's strip. One rule on every platform: a device with no internet has not reached the store (the sand strip); with internet any failed price query is a failure (the red strip).</summary>
    public StoreAnswer Answer => BookAccess.AnswerOf(_hasInternet, _lastPricesFailed, _snapshot.Restricted);

    public string StoreName => platform.StoreName;

    /// <summary>Slice 384: what the device keeps about the subscription and about what opened each book.</summary>
    public SubscriptionStore Subscription => subscription;

    /// <summary>True while the subscription opens paid books: the store's last answer, followed with no internet until the offline rule (<see cref="SubscriptionAccess"/>) ends it.</summary>
    public bool SubscriptionActive => subscription.Grants;

    /// <summary>The Manage subscription link: the store's own page. Never throws; false when it could not be opened.</summary>
    public async Task<bool> ManageSubscriptionAsync()
    {
        try
        {
            return await platform.ManageSubscriptionAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Debug.WriteLine($"Dino Reader could not open the subscription page: {ex.Message}");
            fallbacks.Report(FallbackArea.Store, FallbackReason.StoreFailed);
            return false;
        }
    }

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
        BookAccess.ProofHeader(paidBooksOn, item, Snapshot.Proofs, platform.StoreName);

    private void ListenForStoreUpdates()
    {
        if (Interlocked.Exchange(ref _listening, 1) == 0)
        {
            PurchaseUpdates.StoreChanged += OnStoreChanged;
            if (PurchaseUpdates.TakeMissed())
            {
                MarkStale(); // an update came before anyone listened: the read that follows must not be skipped as fresh
            }
        }
    }

    /// <summary>Stops listening to the static store updates, so a discarded instance is not kept alive and does not read the store twice.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _listening, 0) == 1)
        {
            PurchaseUpdates.StoreChanged -= OnStoreChanged;
        }
    }

    private void OnStoreChanged()
    {
        StoreItem[]? listed;
        lock (_listedLock)
        {
            listed = _lastListed;
        }

        if (listed is not null)
        {
            _ = ReadAfterStoreUpdateAsync(listed);
        }
    }

    private async Task ReadAfterStoreUpdateAsync(StoreItem[] listed)
    {
        try
        {
            await ReadAsync(listed, restore: false, interactive: false, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // no-fallback: a failed background re-read keeps the last snapshot; the next refresh reports the store's problem
            PurchaseTrace.Line($"re-read after a store update failed: {ex.GetType().Name}");
        }
    }

    private async Task<PurchaseSnapshot> ReadAsync(IEnumerable<StoreItem> items, bool restore, bool interactive, CancellationToken cancel)
    {
        StoreItem[] listed = [.. items];
        lock (_listedLock)
        {
            _lastListed = listed;
        }

        ListenForStoreUpdates();
        string[] products = [.. listed.Where(i => i.Sale == BookSale.Paid).SelectMany(i => i.Products).Distinct()];
        await _gate.WaitAsync(cancel);
        try
        {
            PurchaseSnapshot read = NormaliseSubscription(restore ? await platform.RestoreAsync(products, cancel) : await platform.RefreshAsync(products, interactive, cancel));
            bool hasInternet = HasInternet(); // read once per read: the only thing that says "store not reached"
            ReportReadProblems(listed, read, hasInternet);
            bool subscriptionChanged = KeepSubscription(read, hasInternet);
            bool snapshotChanged = Keep(read, hasInternet);
            RecordBought(listed); // before Changed: the shelf the event rebuilds must see the book as bought, not as opened by the subscription
            if (subscriptionChanged || snapshotChanged)
            {
                Changed?.Invoke(this, EventArgs.Empty); // the shelf locks or opens its subscription books
            }

            StartEndCheck(read, hasInternet);
            return _snapshot;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PurchaseTrace.Line($"could not read the store's purchases: {ex.GetType().Name} at {FirstFrame(ex)}"); // the Release log too: a throw here once hid a crash in the price query (Slice 358)
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

    // Slice 384: the store's answer about the subscription is the one thing that says whether its products are owned: an answer that is not paid access takes them out of Owned and Proofs (so an old proof is never sent and the
    // state is never "owned"), whatever a platform left in; a read that gave no answer about it (Subscription null) changes nothing.
    private static PurchaseSnapshot NormaliseSubscription(PurchaseSnapshot read)
    {
        if (read.Subscription is not { } status || SubscriptionAccess.IsEntitled(status.State))
        {
            return read;
        }

        return read with
        {
            Owned = new HashSet<string>(read.Owned.Where(p => !SubscriptionProducts.Is(p))),
            Proofs = read.Proofs.Where(p => !SubscriptionProducts.Is(p.Key)).ToDictionary(p => p.Key, p => p.Value),
        };
    }

    // Replaces the kept record with the store's answer when this read reached the store (the platform answered AND the device has internet: Play Billing answers from its on-device cache offline) and answered about
    // the subscription; true when what the books depend on changed. Any other read keeps the record, its CheckedAt and its end date as they were, so the offline rule (Decision 6) can end the access.
    private bool KeepSubscription(PurchaseSnapshot read, bool hasInternet)
    {
        if (!read.Reachable || !hasInternet || read.Subscription is null)
        {
#if DEBUG
            Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[Purchases] subscription kept (store not asked) state={subscription.Record?.State} grants={subscription.Grants}"); // the offline rule's branch: the kept record decides
#endif
            return false;
        }

        bool grantedBefore = subscription.Grants;
        (SubscriptionRecord? before, SubscriptionRecord? after) = subscription.Merge(new SubscriptionAnswer.DeviceRead(read.Subscription));
        bool changed = grantedBefore != subscription.Grants || before?.State != after?.State;
        PurchaseTrace.Line($"subscription {after?.State} ends={(after?.EndsAt is { } end ? end.ToString("yyyy-MM-dd") : "none")} grants={subscription.Grants} changed={changed}");
#if DEBUG
        Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[Purchases] subscription state={after?.State} grants={subscription.Grants}");
#endif
        return changed;
    }

    // Slice 384 (review finding 2): after a read of the device's own store on iOS and Android, the Worker is asked for the subscription's real end date and state (Apple's all-statuses or Play's subscriptionsv2 answer) and
    // its answer replaces the device's estimate. It runs in the background and never holds the read, the shelf or any page. Not asked: Windows (its own end date), the Debug fake store, a read that did not reach the store,
    // no internet, or a store that says there is no subscription (no proof to send).
    private void StartEndCheck(PurchaseSnapshot read, bool hasInternet)
    {
        string? header = EndCheckHeader(read, hasInternet);
        if (header is not null)
        {
            long readNumber = subscription.Record?.ReadNumber ?? 0; // the device read this check belongs to: a later read makes its answer late
            _ = Task.Run(() => ApplyEndAsync(_end, header, readNumber));
        }
    }

    private string? EndCheckHeader(PurchaseSnapshot read, bool hasInternet)
    {
        bool realStore = platform.StoreName != PurchaseProofs.WindowsStore && platform is not SwitchablePurchases { Override: not null };
        if (!realStore || !read.Reachable || !hasInternet || read.Subscription is not { } status || !SubscriptionAccess.IsEntitled(status.State))
        {
            return null;
        }

        string? product = SubscriptionAccess.NewestProofProduct(read.Proofs.Keys, read.PurchasedAt); // the newest purchase's proof, never the older plan's
        return product is null ? null : $"{platform.StoreName}:{PurchaseProofs.ProductPart(platform.StoreName, product)}:{read.Proofs[product]}";
    }

    private async Task ApplyEndAsync(ISubscriptionEnd source, string header, long readNumber)
    {
        await _endGate.WaitAsync();
        try
        {
            WorkerSubscription? answer = await source.AskAsync(header, CancellationToken.None);
            if (answer is null)
            {
                PurchaseTrace.Line("subscription end: no Worker answer, the kept record stands");
                if (HasInternet())
                {
                    fallbacks.Report(FallbackArea.Store, FallbackReason.StoreUnreachable); // the Worker could not be asked though the device is online: the kept record decides
                }

                return;
            }

            if (KeepWorkerAnswer(answer, readNumber))
            {
                Changed?.Invoke(this, EventArgs.Empty); // the shelf locks or opens its subscription books
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PurchaseTrace.Line($"subscription end failed: {ex.GetType().Name}");
            fallbacks.Report(FallbackArea.Store, FallbackReason.StoreFailed);
        }
        finally
        {
            _endGate.Release();
        }
    }

    // Writes the Worker's answer into the record (an unknown state, or an answer to an older device read, changes nothing); true when what the books depend on changed.
    private bool KeepWorkerAnswer(WorkerSubscription answer, long readNumber)
    {
        bool grantedBefore = subscription.Grants;
        (SubscriptionRecord? before, SubscriptionRecord? after) = subscription.Merge(new SubscriptionAnswer.Worker(readNumber, answer.State, answer.ExpiresAt));
        PurchaseTrace.Line($"subscription from the Worker {after?.State} ends={(after?.EndsAt is { } end ? end.ToString("yyyy-MM-dd") : "none")} grants={subscription.Grants}");
        return grantedBefore != subscription.Grants || before?.State != after?.State || before?.EndsAt != after?.EndsAt;
    }

    // A listed book the account now owns by purchase (its own product, not the subscription) is bought for good: its record never goes back to "the subscription opened it".
    private void RecordBought(IReadOnlyCollection<StoreItem> items)
    {
        IReadOnlySet<string> owned = _snapshot.Owned; // the raw read: only books bought with their own product are upgraded, the subscription's products never count here
        subscription.UpgradeBought(items.Where(i => i.Sale == BookSale.Paid && BookAccess.BoughtProduct(i, owned) is not null).Select(i => i.Id));
    }

    // One line: the first stack frame (code location, no user data), else the exception message flattened.
    private static string FirstFrame(Exception ex)
    {
        string? frame = ex.StackTrace?.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
        string text = string.IsNullOrEmpty(frame) ? ex.Message : frame;
        string flat = text.ReplaceLineEndings(" ");
        return flat.Length > 200 ? flat[..200] : flat;
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

        ReportIncompleteOnce(read);
        if (hasInternet)
        {
            ReportPriceProblem(items, read);
        }

        if (read.Owned.Any(product => !read.Proofs.ContainsKey(product)))
        {
            fallbacks.Report(FallbackArea.Store, FallbackReason.StoreFailed); // an owned book has no proof to send (Windows: no Store ID key), so its files are not asked for until the next read
        }
    }

    // Slice 390: a read in which the store could not verify one entitlement (the others are kept, plus the books known before) is one store fallback per change, not one per read.
    private void ReportIncompleteOnce(PurchaseSnapshot read)
    {
        bool before = _reportedIncomplete;
        _reportedIncomplete = read.Incomplete;
        if (read.Incomplete && !before)
        {
            PurchaseTrace.Line("store read incomplete: an entitlement could not be verified, the books known before are kept");
            fallbacks.Report(FallbackArea.Store, FallbackReason.StoreFailed);
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
    // True when the snapshot changed (the caller raises Changed once, after the unlock records are brought up to date).
    private bool Keep(PurchaseSnapshot read, bool hasInternet)
    {
        bool flipped = NoteRead(hasInternet, read.PricesFailed || !read.Reachable);
        if (!read.Reachable)
        {
            return flipped;
        }

        PurchaseSnapshot before = _snapshot;
        Interlocked.Exchange(ref _readAt, Stopwatch.GetTimestamp());
        _snapshot = read with { Prices = BookAccess.KeepPrices(before.Prices, read.Prices) };
#if DEBUG
        Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[Purchases] snapshot owned={read.Owned.Count} pending={read.Pending.Count} prices={read.Prices.Count(p => !SubscriptionProducts.Is(p.Key))}"); // the books' prices only: the paid-books flow counts them (Slice 346), the plans are the subscription flow's (Slice 384)
#endif
        return flipped || before.PricesFailed != read.PricesFailed || before.Restricted != read.Restricted || !before.Owned.SetEquals(read.Owned) || !before.Pending.SetEquals(read.Pending) || !before.Prices.OrderBy(p => p.Key).SequenceEqual(_snapshot.Prices.OrderBy(p => p.Key));
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
        return BookAccess.Plan(SwitchKnownOn, item, onShelf, snapshot.Owned, snapshot.Pending, price, purchases.Answer, lapsed: IsLapsed(item.Id));
    }

    /// <summary>Slice 384: true while the subscription opens paid books.</summary>
    public bool SubscriptionActive => purchases.SubscriptionActive;

    /// <summary>Slice 384: whether the book on the shelf may be opened; only a book the subscription alone opened can be locked, and only once the subscription has ended (greyed for parents, hidden from the child).</summary>
    public ShelfAccess ShelfAccessOf(string bookId) =>
        BookAccess.ShelfAccessOf(SwitchKnownOn, purchases.Subscription.UnlockOf(bookId), purchases.SubscriptionActive);

    public bool IsLapsed(string bookId) => ShelfAccessOf(bookId) == ShelfAccess.LockedByLapse;

    /// <summary>Whether a story on the shelf is locked: the unlock records are kept under the store item's id, which a store book carries as its <see cref="Story.SourceId"/> (its own <see cref="Story.Id"/> is new at every import); a story with no source is never locked.</summary>
    public bool IsLapsed(Story story) => story.SourceId is { } sourceId && IsLapsed(sourceId);

    /// <summary>Forgets what opened a removed book, so a later download of it is recorded afresh.</summary>
    public void ForgetBook(Story story)
    {
        if (story.SourceId is { } sourceId)
        {
            purchases.Subscription.ForgetUnlock(sourceId);
        }
    }

    /// <summary>Slice 384: written once when a book finishes downloading: what opened it (free, bought, the subscription, or already on the device).</summary>
    public void RecordDownload(StoreItem item) =>
        purchases.Subscription.RecordUnlock(item.Id, BookAccess.UnlockAtDownload(SwitchKnownOn, item, purchases.Snapshot.Owned));

    public bool MayUpdate(StoreItem item) => BookAccess.MayUpdate(SwitchKnownOn, item, purchases.Snapshot.Owned);
}
