using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 384: the subscription rules, row by row from Decisions 3-6 and the Slice 346 exceptions.</summary>
public class SubscriptionAccessTests
{
    private const string Id = "0123456789abcdef0123456789abcdef";
    private const string Own = "uk.sirous.dinoreader.book.goldilocks";
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly HashSet<string> None = [];

    private static StoreItem Paid() =>
        new(Id, "Goldilocks", "en", 1, 1, 5, 3, null, "/d", Sale: BookSale.Paid, ProductId: Own, UnlockedBy: [Own, .. SubscriptionProducts.All]);

    private static SubscriptionRecord Record(SubscriptionState state, TimeSpan endsFromNow) => new(state, Now + endsFromNow, Now - TimeSpan.FromDays(1), SubscriptionProducts.Monthly);

    [Theory]
    [InlineData(SubscriptionState.Active)]
    [InlineData(SubscriptionState.Trial)]
    [InlineData(SubscriptionState.InGrace)]
    public void ARunningSubscription_Grants(SubscriptionState state)
    {
        Assert.True(SubscriptionAccess.Grants(Record(state, TimeSpan.FromDays(10)), Now));
    }

    [Theory]
    [InlineData(SubscriptionState.Unknown)]
    [InlineData(SubscriptionState.Expired)]
    public void AnExpiredOrUnknownSubscription_NeverGrants_EvenWithAFutureEndDate(SubscriptionState state)
    {
        Assert.False(SubscriptionAccess.Grants(Record(state, TimeSpan.FromDays(10)), Now)); // online the store's "expired" locks at once (Decision 6)
    }

    [Fact]
    public void NoRecord_NeverGrants()
    {
        Assert.False(SubscriptionAccess.Grants(null, Now));
    }

    [Fact]
    public void Offline_TheBooksLock_ThreeDaysAfterTheStoredEndDate_NotBefore()
    {
        SubscriptionRecord record = Record(SubscriptionState.Active, TimeSpan.Zero);
        Assert.True(SubscriptionAccess.Grants(record, Now + TimeSpan.FromDays(3) - TimeSpan.FromMinutes(1)));
        Assert.True(SubscriptionAccess.Grants(record, Now + TimeSpan.FromDays(3)));
        Assert.False(SubscriptionAccess.Grants(record, Now + TimeSpan.FromDays(3) + TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void WithoutAnEndDate_TheRecordCountsFromTheMomentTheStoreWasRead()
    {
        var record = new SubscriptionRecord(SubscriptionState.Active, null, Now, null);
        Assert.True(SubscriptionAccess.Grants(record, Now + TimeSpan.FromDays(2)));
        Assert.False(SubscriptionAccess.Grants(record, Now + TimeSpan.FromDays(4)));
    }

    [Fact]
    public void ARecord_RoundTripsThroughItsJson_AndGarbageIsNoRecord()
    {
        SubscriptionRecord record = Record(SubscriptionState.Trial, TimeSpan.FromDays(7));
        SubscriptionRecord? back = SubscriptionAccess.FromJson(SubscriptionAccess.ToJson(record));
        Assert.Equal(record.State, back!.State);
        Assert.Equal(record.EndsAt, back.EndsAt);
        Assert.Equal(record.CheckedAt, back.CheckedAt);
        Assert.Equal(record.ProductId, back.ProductId);
        Assert.Equal(SubscriptionState.Active, SubscriptionAccess.FromJson(SubscriptionAccess.ToJson(new SubscriptionRecord(SubscriptionState.Active, null, Now, null)))!.State);
        foreach (string? bad in new string?[] { null, "", "  ", "nonsense", "{\"State\":\"Bogus\",\"CheckedAt\":1}", "{\"State\":\"Active\",\"CheckedAt\":99999999999999999}" })
        {
            Assert.Null(SubscriptionAccess.FromJson(bad));
        }
    }

    [Fact]
    public void AtDownload_TheRecordSaysWhatOpenedTheBook()
    {
        Assert.Equal(BookUnlock.Free, BookAccess.UnlockAtDownload(false, Paid(), None)); // switch off: everything is free
        Assert.Equal(BookUnlock.Free, BookAccess.UnlockAtDownload(true, Paid() with { Sale = BookSale.Free }, None));
        Assert.Equal(BookUnlock.Bought, BookAccess.UnlockAtDownload(true, Paid(), new HashSet<string> { Own }));
        Assert.Equal(BookUnlock.Subscription, BookAccess.UnlockAtDownload(true, Paid(), new HashSet<string> { SubscriptionProducts.Yearly }));
        Assert.Equal(BookUnlock.Bought, BookAccess.UnlockAtDownload(true, Paid(), new HashSet<string> { SubscriptionProducts.Yearly, Own })); // owning wins over the subscription
        Assert.Equal(BookUnlock.FromBefore, BookAccess.UnlockAtDownload(true, Paid(), None));
    }

    [Fact]
    public void OnlyASubscriptionBook_WithNoRunningSubscription_IsLocked()
    {
        Assert.Equal(ShelfAccess.LockedByLapse, BookAccess.ShelfAccessOf(true, BookUnlock.Subscription, subscriptionGrants: false));
        Assert.Equal(ShelfAccess.Readable, BookAccess.ShelfAccessOf(true, BookUnlock.Subscription, subscriptionGrants: true));
        foreach (BookUnlock never in new[] { BookUnlock.Free, BookUnlock.Bought, BookUnlock.FromBefore })
        {
            Assert.Equal(ShelfAccess.Readable, BookAccess.ShelfAccessOf(true, never, subscriptionGrants: false)); // Decision 5 and the Slice 346 rule
        }

        Assert.Equal(ShelfAccess.Readable, BookAccess.ShelfAccessOf(true, null, subscriptionGrants: false)); // a book with no record is never locked
        Assert.Equal(ShelfAccess.Readable, BookAccess.ShelfAccessOf(false, BookUnlock.Subscription, subscriptionGrants: false)); // switch off: origin/main behaviour
    }

    [Fact]
    public void BuyingASubscriptionBook_KeepsItForever_AndNothingIsDowngraded()
    {
        Assert.Equal(BookUnlock.Bought, BookAccess.UpgradeUnlock(BookUnlock.Subscription, boughtNow: true));
        Assert.Equal(BookUnlock.Subscription, BookAccess.UpgradeUnlock(BookUnlock.Subscription, boughtNow: false));
        Assert.Equal(BookUnlock.Bought, BookAccess.UpgradeUnlock(BookUnlock.Bought, boughtNow: false));
        Assert.Null(BookAccess.UpgradeUnlock(null, boughtNow: true));
        Assert.Equal(BookUnlock.Bought, BookUnlockRecords.Merge(BookUnlock.Subscription, BookUnlock.Bought));
        Assert.Equal(BookUnlock.Subscription, BookUnlockRecords.Merge(BookUnlock.Subscription, BookUnlock.Subscription));
        Assert.Equal(BookUnlock.Free, BookUnlockRecords.Merge(BookUnlock.Free, BookUnlock.Subscription));
        Assert.Equal(BookUnlock.FromBefore, BookUnlockRecords.Merge(BookUnlock.FromBefore, BookUnlock.Bought));
        Assert.Equal(BookUnlock.Subscription, BookUnlockRecords.Merge(null, BookUnlock.Subscription));
    }

    [Fact]
    public void TheUnlockRecords_RoundTrip_AndDropWhatTheyCannotRead()
    {
        var records = new Dictionary<string, BookUnlock> { ["a"] = BookUnlock.Subscription, ["b"] = BookUnlock.Bought };
        Dictionary<string, BookUnlock> back = BookUnlockRecords.FromJson(BookUnlockRecords.ToJson(records));
        Assert.Equal(records, back);
        Assert.Equal(BookUnlock.Bought, BookUnlockRecords.FromJson("{\"a\":\"Bogus\",\"b\":\"Bought\"}")["b"]);
        Assert.DoesNotContain("a", BookUnlockRecords.FromJson("{\"a\":\"Bogus\"}").Keys);
        Assert.Empty(BookUnlockRecords.FromJson("garbage"));
        Assert.Empty(BookUnlockRecords.FromJson(null));
    }

    [Fact]
    public void ALapsedBook_OffersBuy_ButABookFromBeforeNeverDoes()
    {
        StoreCardPlan lapsed = BookAccess.Plan(true, Paid(), onShelf: true, None, None, "£0.89", StoreAnswer.Answered, lapsed: true);
        Assert.Equal((BookState.LockedByLapse, StoreAction.Buy, "£0.89"), (lapsed.State, lapsed.Action, lapsed.Price));
        Assert.Equal(StoreAction.OnShelf, BookAccess.Plan(true, Paid(), onShelf: true, None, None, "£0.89", StoreAnswer.Answered).Action);
        Assert.Equal(StoreAction.OnShelf, BookAccess.Plan(true, Paid(), true, new HashSet<string> { SubscriptionProducts.Monthly }, None, "£0.89", lapsed: true).Action); // a running subscription wins over a stale lapse flag
    }

    [Fact]
    public void ABooksPrice_IsNeverTheSubscriptions_AndASubscriptionProofOpensEveryPaidBook()
    {
        var prices = new Dictionary<string, string> { [SubscriptionProducts.Subscription] = "£1.99" };
        Assert.Null(BookAccess.PriceOf(Paid(), prices));
        prices[Own] = "£0.89";
        Assert.Equal("£0.89", BookAccess.PriceOf(Paid(), prices));
        var proofs = new Dictionary<string, string> { [SubscriptionProducts.Yearly] = "tok" };
        Assert.Equal($"ios:{SubscriptionProducts.Yearly}:tok", BookAccess.ProofHeader(true, Paid(), proofs, PurchaseProofs.IosStore));
        proofs[Own] = "own";
        Assert.Equal($"ios:{Own}:own", BookAccess.ProofHeader(true, Paid(), proofs, PurchaseProofs.IosStore)); // the book's own purchase is the proof first
        Assert.True(BookAccess.MayUpdate(true, Paid(), new HashSet<string> { SubscriptionProducts.Monthly }));
        Assert.False(BookAccess.MayUpdate(true, Paid(), None));
    }

    // Review #2: the 3 days are added once, on the end date the record holds.
    [Fact]
    public void TheGraceIsAddedOnce_NotOnTopOfAnEndDateThatAlreadyIncludesIt()
    {
        var record = new SubscriptionRecord(SubscriptionState.Active, Now - TimeSpan.FromDays(4), Now, SubscriptionProducts.Monthly);
        Assert.False(SubscriptionAccess.Grants(record, Now)); // 4 days past the end: no longer open (6 days would mean the grace counted twice)
        Assert.True(SubscriptionAccess.Grants(record with { EndsAt = Now - TimeSpan.FromDays(2) }, Now));
    }

    // Review #5: a subscription book the account then bought is Bought, and a Bought record never locks.
    [Fact]
    public void ABookBoughtAfterTheSubscription_IsUpgraded_AndNeverLocks()
    {
        BookUnlock? upgraded = BookAccess.UpgradeUnlock(BookUnlock.Subscription, boughtNow: true);
        Assert.Equal(BookUnlock.Bought, upgraded);
        Assert.Equal(ShelfAccess.Readable, BookAccess.ShelfAccessOf(true, upgraded, subscriptionGrants: false));
    }

    // Review #6: a pending subscription purchase does not turn every unowned book into "waiting".
    [Fact]
    public void APendingSubscription_StillLetsABookBeBought()
    {
        var pending = new HashSet<string> { SubscriptionProducts.Monthly };
        Assert.Equal(BookState.Buyable, BookAccess.StateOf(true, Paid(), onShelf: false, None, pending));
        Assert.Equal(BookState.Pending, BookAccess.StateOf(true, Paid(), onShelf: false, None, new HashSet<string> { Own })); // the book's own pending purchase still says waiting
    }

    // Review #7: the subscription's products count as owned only while the kept record grants access, whatever the last read held.
    [Fact]
    public void WhenTheKeptRecordNoLongerGrants_TheSubscriptionIsNotOwned_AndItsProofIsNotSent()
    {
        var owned = new HashSet<string> { SubscriptionProducts.Monthly, Own };
        var proofs = new Dictionary<string, string> { [SubscriptionProducts.Monthly] = "sub", [Own] = "own" };
        Assert.Equal([Own], SubscriptionAccess.OwnedWhile(owned, grants: false));
        Assert.Equal([Own], SubscriptionAccess.ProofsWhile(proofs, grants: false).Keys);
        Assert.Same(owned, SubscriptionAccess.OwnedWhile(owned, grants: true));
        Assert.Same(proofs, SubscriptionAccess.ProofsWhile(proofs, grants: true));
        var onlySubscription = new HashSet<string> { SubscriptionProducts.Monthly };
        Assert.Equal(BookState.LockedByLapse, BookAccess.StateOf(true, Paid(), true, SubscriptionAccess.OwnedWhile(onlySubscription, false), None, lapsed: true));
        Assert.Null(BookAccess.ProofHeader(true, Paid(), SubscriptionAccess.ProofsWhile(new Dictionary<string, string> { [SubscriptionProducts.Monthly] = "sub" }, false), PurchaseProofs.IosStore));
    }

    // Review #2 (2) and (3): the Worker's answer (Apple's or Google's real end date) replaces the device's estimate; the merge rules.
    private static SubscriptionRecord Estimate(SubscriptionState state, TimeSpan endsFromNow) => new(state, Now + endsFromNow, Now, SubscriptionProducts.Monthly);

    [Theory]
    [InlineData("active", SubscriptionState.Active)]
    [InlineData("grace", SubscriptionState.InGrace)]
    [InlineData("expired", SubscriptionState.Expired)]
    [InlineData("revoked", SubscriptionState.Expired)]
    public void TheWorkersStates_MapToTheAppsStates(string worker, SubscriptionState expected)
    {
        Assert.Equal(expected, SubscriptionAccess.StatusFromWorker(worker, Now + TimeSpan.FromDays(9), null)!.State);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("paused")]
    public void AnUnknownWorkerState_IsNoAnswer(string? worker)
    {
        Assert.Null(SubscriptionAccess.StatusFromWorker(worker, Now, null));
    }

    [Fact]
    public void AFreeTrialThatEndsWhenTheWorkerSaysItDoes_StaysATrial()
    {
        SubscriptionRecord trial = Estimate(SubscriptionState.Trial, TimeSpan.FromDays(7));
        Assert.Equal(SubscriptionState.Trial, SubscriptionAccess.StatusFromWorker("active", trial.EndsAt, trial)!.State);
        Assert.Equal(SubscriptionState.Active, SubscriptionAccess.StatusFromWorker("active", trial.EndsAt + TimeSpan.FromDays(30), trial)!.State);
    }

    // Review round 2 (findings 1 and 2): SubscriptionAccess.Merge is the only way the record changes; one test per row of the Design note's table.
    private static SubscriptionAnswer.DeviceRead Device(SubscriptionState state, TimeSpan? endsFromNow = null) =>
        new(new SubscriptionStatus(state, endsFromNow is { } e ? Now + e : null, SubscriptionProducts.Monthly));

    // An iOS read: entitled from a kept transaction, with the newest transaction's date.
    private static SubscriptionAnswer.DeviceRead Estimated(TimeSpan newestFromNow, TimeSpan? endsFromNow = null) =>
        new(new SubscriptionStatus(SubscriptionState.Active, endsFromNow is { } e ? Now + e : null, SubscriptionProducts.Monthly, DeviceReadKind.Estimated, Now + newestFromNow));

    private static SubscriptionRecord Read(long number, SubscriptionState state = SubscriptionState.Active, bool confirmed = false) =>
        new(state, Now + TimeSpan.FromDays(5), Now, SubscriptionProducts.Monthly, confirmed, number);

    [Fact]
    public void Merge_DeviceEntitled_IsActiveOrTrial_Unconfirmed_WithTheDevicesEstimate_AndANewReadNumber()
    {
        SubscriptionRecord active = SubscriptionAccess.Merge(Read(4, confirmed: true), Device(SubscriptionState.Active, TimeSpan.FromDays(9)), Now)!;
        Assert.Equal((SubscriptionState.Active, false, 5L, Now + TimeSpan.FromDays(9)), (active.State, active.Confirmed, active.ReadNumber, active.EndsAt));
        Assert.Equal(SubscriptionState.Trial, SubscriptionAccess.Merge(null, Device(SubscriptionState.Trial, TimeSpan.FromDays(7)), Now)!.State);
        Assert.Equal(Now, SubscriptionAccess.Merge(null, Device(SubscriptionState.Active), Now)!.EndsAt); // Android: no end date, so the end is the read time
        Assert.Equal(1, SubscriptionAccess.Merge(null, Device(SubscriptionState.Active), Now)!.ReadNumber);
    }

    [Fact]
    public void Merge_EstimatedReadWithNoKeptTransaction_IsNoInformation_AndLeavesTheRecordUnchanged()
    {
        SubscriptionRecord confirmed = Read(4, confirmed: true);
        SubscriptionAnswer.DeviceRead nothingKept = new(new SubscriptionStatus(SubscriptionState.Unknown, null, null, DeviceReadKind.Estimated));
        Assert.Same(confirmed, SubscriptionAccess.Merge(confirmed, nothingKept, Now)); // a Worker-confirmed Active record is not locked by an empty read
        Assert.Null(SubscriptionAccess.Merge(null, nothingKept, Now));
        Assert.Equal(SubscriptionState.Expired, SubscriptionAccess.Merge(confirmed, new SubscriptionAnswer.DeviceRead(SubscriptionStatus.None), Now)!.State); // a Store read that lists nothing is still Expired
    }

    [Fact]
    public void Merge_DeviceEntitled_NeverShortensAKnownEnd_AndCarriesNoEndFromAnExpiredRecord()
    {
        SubscriptionRecord held = Read(4, confirmed: true); // ends Now + 5 days
        Assert.Equal(Now + TimeSpan.FromDays(5), SubscriptionAccess.Merge(held, Device(SubscriptionState.Active, TimeSpan.FromDays(2)), Now)!.EndsAt); // device earlier: the known end stays
        Assert.Equal(Now + TimeSpan.FromDays(5), SubscriptionAccess.Merge(held, Device(SubscriptionState.Active), Now)!.EndsAt); // Android: no device end, the known end stays
        Assert.Equal(Now + TimeSpan.FromDays(9), SubscriptionAccess.Merge(held, Device(SubscriptionState.Active, TimeSpan.FromDays(9)), Now)!.EndsAt); // device later: it wins
        SubscriptionRecord expired = Read(4, SubscriptionState.Expired, confirmed: true);
        Assert.Equal(Now, SubscriptionAccess.Merge(expired, Device(SubscriptionState.Active), Now)!.EndsAt); // the expired record's own end (Now + 5 days) is not carried
        Assert.Equal(Now + TimeSpan.FromDays(2), SubscriptionAccess.Merge(expired, Device(SubscriptionState.Active, TimeSpan.FromDays(2)), Now)!.EndsAt);
        SubscriptionRecord grace = Read(4, SubscriptionState.InGrace, confirmed: true);
        Assert.Equal(Now + TimeSpan.FromDays(5), SubscriptionAccess.Merge(grace, Device(SubscriptionState.Active, TimeSpan.FromDays(2)), Now)!.EndsAt);
        SubscriptionRecord trial = Read(4, SubscriptionState.Trial, confirmed: true);
        Assert.Equal(Now + TimeSpan.FromDays(5), SubscriptionAccess.Merge(trial, Device(SubscriptionState.Active), Now)!.EndsAt);
    }

    [Fact]
    public void Merge_DeviceEntitled_NeverEndsBeforeTheRead_SoItGrantsThreeDaysFromTheRead()
    {
        SubscriptionRecord renewedToday = Read(4, confirmed: true) with { EndsAt = Now - TimeSpan.FromDays(1) }; // the Worker's end passed; the renewal is not confirmed yet (429, 503, timeout)
        SubscriptionRecord? afterStore = SubscriptionAccess.Merge(renewedToday, Device(SubscriptionState.Active), Now);
        Assert.Equal(Now, afterStore!.EndsAt);
        Assert.True(SubscriptionAccess.Grants(afterStore, Now + TimeSpan.FromDays(2)));
        Assert.False(SubscriptionAccess.Grants(afterStore, Now + SubscriptionAccess.OfflineGrace + TimeSpan.FromSeconds(1)));
        Assert.Equal(Now, SubscriptionAccess.Merge(renewedToday, Device(SubscriptionState.Active, -TimeSpan.FromDays(2)), Now)!.EndsAt); // an old device estimate cannot pull it back
    }

    [Fact]
    public void Merge_EstimatedEntitled_DoesNotReplaceAConfirmedExpired_UnlessItsNewestTransactionIsAfterThatRecordsCheck()
    {
        SubscriptionRecord confirmedExpired = Read(4, SubscriptionState.Expired, confirmed: true);
        Assert.Same(confirmedExpired, SubscriptionAccess.Merge(confirmedExpired, Estimated(-TimeSpan.FromDays(1), TimeSpan.FromDays(20)), Now)); // an old kept transaction: the Worker's Expired stands
        Assert.Same(confirmedExpired, SubscriptionAccess.Merge(confirmedExpired, Estimated(TimeSpan.Zero), Now)); // same moment as CheckedAt is not after it
        Assert.Same(confirmedExpired, SubscriptionAccess.Merge(confirmedExpired, new SubscriptionAnswer.DeviceRead(new SubscriptionStatus(SubscriptionState.Active, Now, null, DeviceReadKind.Estimated)), Now)); // no transaction date: not a renewal
        SubscriptionRecord renewed = SubscriptionAccess.Merge(confirmedExpired, Estimated(TimeSpan.FromSeconds(1), TimeSpan.FromDays(30)), Now)!;
        Assert.Equal((SubscriptionState.Active, false, 5L), (renewed.State, renewed.Confirmed, renewed.ReadNumber)); // a renewal or a new purchase
    }

    [Fact]
    public void Merge_EstimatedEntitled_StillReplacesAnUnconfirmedExpired_AndAnActiveRecord()
    {
        Assert.Equal(SubscriptionState.Active, SubscriptionAccess.Merge(Read(4, SubscriptionState.Expired, confirmed: false), Estimated(-TimeSpan.FromDays(1)), Now)!.State);
        Assert.Equal(5L, SubscriptionAccess.Merge(Read(4, confirmed: true), Estimated(-TimeSpan.FromDays(1)), Now)!.ReadNumber);
        Assert.Equal(1L, SubscriptionAccess.Merge(null, Estimated(-TimeSpan.FromDays(1)), Now)!.ReadNumber);
    }

    [Fact]
    public void Merge_StoreEntitled_AlwaysReplacesAConfirmedExpired()
    {
        SubscriptionRecord confirmedExpired = Read(4, SubscriptionState.Expired, confirmed: true);
        SubscriptionRecord? after = SubscriptionAccess.Merge(confirmedExpired, Device(SubscriptionState.Active), Now);
        Assert.Equal((SubscriptionState.Active, false, 5L), (after!.State, after.Confirmed, after.ReadNumber));
    }

    [Fact]
    public void Merge_EstimatedNotEntitled_IsExpired_LikeAnyDeviceRead()
    {
        SubscriptionRecord? after = SubscriptionAccess.Merge(Read(1, confirmed: true), new SubscriptionAnswer.DeviceRead(new SubscriptionStatus(SubscriptionState.Expired, null, null, DeviceReadKind.Estimated)), Now);
        Assert.Equal(SubscriptionState.Expired, after!.State);
    }

    [Fact]
    public void Merge_DeviceEntitled_NeverKeepsAnEarlierExpired_EvenWhenTheWorkerAnswerNeverComes()
    {
        SubscriptionRecord expired = Read(2, SubscriptionState.Expired, confirmed: true);
        SubscriptionRecord? after = SubscriptionAccess.Merge(expired, Device(SubscriptionState.Active), Now);
        Assert.Equal(SubscriptionState.Active, after!.State);
        Assert.True(SubscriptionAccess.Grants(after, Now));
    }

    [Theory]
    [InlineData(SubscriptionState.Expired)]
    [InlineData(SubscriptionState.Unknown)]
    public void Merge_DeviceNotEntitled_IsExpired(SubscriptionState state)
    {
        SubscriptionRecord? after = SubscriptionAccess.Merge(Read(1, confirmed: true), Device(state, TimeSpan.FromDays(3)), Now);
        Assert.Equal((SubscriptionState.Expired, false, 2L), (after!.State, after.Confirmed, after.ReadNumber));
        Assert.False(SubscriptionAccess.Grants(after, Now));
    }

    [Fact]
    public void Merge_DeviceReadThatListsNoSubscription_IsExpired()
    {
        Assert.Equal(SubscriptionState.Expired, SubscriptionAccess.Merge(Read(1), new SubscriptionAnswer.DeviceRead(null), Now)!.State);
    }

    [Fact]
    public void Merge_DeviceReadThatFailedOrWasOffline_ChangesNothing()
    {
        SubscriptionRecord held = Read(3, confirmed: true);
        Assert.Same(held, SubscriptionAccess.Merge(held, new SubscriptionAnswer.DeviceRead(null, Reached: false), Now));
        Assert.Same(held, SubscriptionAccess.Merge(held, new SubscriptionAnswer.DeviceRead(new SubscriptionStatus(SubscriptionState.Expired, null, null), Reached: false), Now));
        Assert.Null(SubscriptionAccess.Merge(null, new SubscriptionAnswer.DeviceRead(null, Reached: false), Now));
    }

    [Fact]
    public void Merge_RepeatedOfflineReads_NeverRefreshTheRecord_SoTheOfflineRuleEndsAccess()
    {
        SubscriptionRecord held = new(SubscriptionState.Active, Now - TimeSpan.FromDays(1), Now - TimeSpan.FromDays(40), SubscriptionProducts.Monthly);
        SubscriptionRecord? kept = SubscriptionAccess.Merge(held, new SubscriptionAnswer.DeviceRead(new SubscriptionStatus(SubscriptionState.Active, null, SubscriptionProducts.Monthly), Reached: false), Now);
        Assert.Same(held, kept);
        Assert.True(SubscriptionAccess.Grants(kept, Now));
        Assert.False(SubscriptionAccess.Grants(kept, Now + TimeSpan.FromDays(3))); // end date + 3 days has passed: locked, however many offline reads came
    }

    [Fact]
    public void Merge_WorkerAnswerForAnOlderRead_IsLate_AndIgnored()
    {
        SubscriptionRecord current = Read(7, SubscriptionState.Expired); // a newer device read saved Expired while the old check was in flight
        Assert.Same(current, SubscriptionAccess.Merge(current, new SubscriptionAnswer.Worker(6, "active", Now + TimeSpan.FromDays(20)), Now));
        Assert.Same(current, SubscriptionAccess.Merge(current, new SubscriptionAnswer.Worker(8, "active", Now + TimeSpan.FromDays(20)), Now));
        Assert.Null(SubscriptionAccess.Merge(null, new SubscriptionAnswer.Worker(0, "active", Now), Now));
    }

    [Theory]
    [InlineData("active", SubscriptionState.Active)]
    [InlineData("grace", SubscriptionState.InGrace)]
    [InlineData("expired", SubscriptionState.Expired)]
    [InlineData("revoked", SubscriptionState.Expired)]
    public void Merge_WorkerAnswerForTheCurrentRead_SetsStateAndEndDate_Confirmed(string worker, SubscriptionState expected)
    {
        SubscriptionRecord estimate = Read(3, confirmed: false) with { EndsAt = Now - TimeSpan.FromDays(10) }; // a restored old StoreKit 1 transaction: an active subscriber looks expired
        SubscriptionRecord after = SubscriptionAccess.Merge(estimate, new SubscriptionAnswer.Worker(3, worker, Now + TimeSpan.FromDays(20)), Now)!;
        Assert.Equal((expected, true, 3L, Now + TimeSpan.FromDays(20)), (after.State, after.Confirmed, after.ReadNumber, after.EndsAt));
    }

    [Theory]
    [InlineData(SubscriptionState.Active, true)]
    [InlineData(SubscriptionState.Trial, true)]
    [InlineData(SubscriptionState.InGrace, true)]
    [InlineData(SubscriptionState.Expired, false)]
    public void Merge_WorkerAnswerWithNoEndDate_CarriesTheHeldEndOnlyFromAnEntitledRecord(SubscriptionState held, bool carried)
    {
        SubscriptionRecord record = Read(4) with { State = held, EndsAt = Now - TimeSpan.FromDays(10) };
        DateTimeOffset? end = SubscriptionAccess.Merge(record, new SubscriptionAnswer.Worker(4, "active", null), Now)!.EndsAt;
        Assert.Equal(carried ? record.EndsAt : null, end);
    }

    [Fact]
    public void NewestProofProduct_IsTheSubscriptionPurchasedLast_NotTheFirstListed()
    {
        var times = new Dictionary<string, DateTimeOffset> { [SubscriptionProducts.Monthly] = Now - TimeSpan.FromDays(40), [SubscriptionProducts.Yearly] = Now - TimeSpan.FromDays(2) };
        string[] listed = ["book-one", SubscriptionProducts.Monthly, SubscriptionProducts.Yearly];
        Assert.Equal(SubscriptionProducts.Yearly, SubscriptionAccess.NewestProofProduct(listed, times));
        Assert.Equal(SubscriptionProducts.Monthly, SubscriptionAccess.NewestProofProduct(listed, null));
        Assert.Null(SubscriptionAccess.NewestProofProduct(["book-one"], times));
    }

    [Fact]
    public void Merge_WorkerAnswerWithNoEndDate_KeepsTheKnownEnd_AndAnEarlierOrLaterEndReplacesIt()
    {
        SubscriptionRecord held = Read(4);
        Assert.Equal(held.EndsAt, SubscriptionAccess.Merge(held, new SubscriptionAnswer.Worker(4, "active", null), Now)!.EndsAt);
        DateTimeOffset earlier = held.EndsAt!.Value - TimeSpan.FromDays(5);
        DateTimeOffset later = held.EndsAt!.Value + TimeSpan.FromDays(5);
        Assert.Equal(earlier, SubscriptionAccess.Merge(held, new SubscriptionAnswer.Worker(4, "active", earlier), Now)!.EndsAt);
        Assert.Equal(later, SubscriptionAccess.Merge(held, new SubscriptionAnswer.Worker(4, "active", later), Now)!.EndsAt);
    }

    [Fact]
    public void Merge_WorkerActive_KeepsAFreeTrialATrial_WhenItEndsAtTheSameMoment()
    {
        SubscriptionRecord trial = Read(1, SubscriptionState.Trial);
        Assert.Equal(SubscriptionState.Trial, SubscriptionAccess.Merge(trial, new SubscriptionAnswer.Worker(1, "active", trial.EndsAt), Now)!.State);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("paused")]
    public void Merge_WorkerUnknownErrorOrOffline_ChangesNothing_NeverExpired(string? state)
    {
        SubscriptionRecord held = Read(2);
        Assert.Same(held, SubscriptionAccess.Merge(held, new SubscriptionAnswer.Worker(2, state, null), Now));
    }

    [Fact]
    public void TheReadNumberAndConfirmedFlag_SurviveTheSavedForm_AndAnOldRecordIsNotConfirmed()
    {
        SubscriptionRecord confirmed = new(SubscriptionState.Active, Now + TimeSpan.FromDays(20), Now, SubscriptionProducts.Monthly, Confirmed: true, ReadNumber: 9);
        Assert.Equal(9, SubscriptionAccess.FromJson(SubscriptionAccess.ToJson(confirmed))!.ReadNumber);
        Assert.True(SubscriptionAccess.FromJson(SubscriptionAccess.ToJson(confirmed))!.Confirmed);
        Assert.False(SubscriptionAccess.FromJson("{\"State\":\"Active\",\"EndsAt\":null,\"CheckedAt\":1700000000,\"ProductId\":null}")!.Confirmed);
    }
}
