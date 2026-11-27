using DinoReader.Core;
using Xunit;

namespace DinoReader.Tests;

/// <summary>Slice 463: the first book's slow-link switch, the quick listing fields and the quick-to-standard upgrade rules (all pure).</summary>
public sealed class FirstBookSwitchTests
{
    private const long Total = 4_140_000; // The Lion and the Mouse, standard, book + en pack (the slice's measured table)

    [Theory]
    [InlineData(0, 2.9, FirstBookChoice.Keep)] // before the check nothing is judged, however slow
    [InlineData(10, 2.99, FirstBookChoice.Keep)]
    [InlineData(0, 3.0, FirstBookChoice.Switch)] // nothing arrived in 3 s: no speed, an unbounded rest
    [InlineData(100_000, 3.0, FirstBookChoice.Switch)] // 33 kB/s: 121 s left
    [InlineData(1_000_000, 3.0, FirstBookChoice.Keep)] // 333 kB/s: 9.4 s left, within the 10 s
    [InlineData(1_000_000, 2.0, FirstBookChoice.Keep)] // still before the check, whatever the speed
    public void Decide_JudgesTheSpeedMeasuredSoFar(long bytes, double seconds, FirstBookChoice expected)
    {
        Assert.Equal(expected, FirstBookSwitch.Decide(bytes, TimeSpan.FromSeconds(seconds), Total));
    }

    [Fact]
    public void Decide_SwitchesOnlyWhenMoreThanTenSecondsAreLeft()
    {
        // 3 s elapsed: 1 MB/s-ish speeds. Bytes so far b, total T: left = (T - b) / (b / 3).
        Assert.Equal(FirstBookChoice.Keep, FirstBookSwitch.Decide(1_000_000, TimeSpan.FromSeconds(3), Total)); // 9.42 s left
        Assert.Equal(FirstBookChoice.Keep, FirstBookSwitch.Decide(1_380_000, TimeSpan.FromSeconds(3), Total)); // 6.0 s left
        Assert.Equal(FirstBookChoice.Switch, FirstBookSwitch.Decide(900_000, TimeSpan.FromSeconds(3), Total)); // 10.8 s left
        Assert.Equal(FirstBookChoice.Switch, FirstBookSwitch.Decide(32_000, TimeSpan.FromSeconds(3), Total)); // 1 Mbit/s link that stalled: far more than 10 s
    }

    [Fact]
    public void Decide_KeepsWhenThereIsNothingToPredictFrom()
    {
        Assert.Equal(FirstBookChoice.Keep, FirstBookSwitch.Decide(0, TimeSpan.FromSeconds(5), null)); // no listed size
        Assert.Equal(FirstBookChoice.Keep, FirstBookSwitch.Decide(0, TimeSpan.FromSeconds(5), 0));
        Assert.Equal(FirstBookChoice.Keep, FirstBookSwitch.Decide(Total, TimeSpan.FromSeconds(5), Total)); // everything is here
        Assert.Equal(FirstBookChoice.Keep, FirstBookSwitch.Decide(Total + 1, TimeSpan.FromSeconds(5), Total));
    }

    [Fact]
    public async Task SlowLinkWatch_CancelsASlowDownloadAndSaysItSwitched()
    {
        using var caller = new CancellationTokenSource();
        using var watch = new SlowLinkWatch(caller.Token, Total, TimeSpan.FromMilliseconds(40));
        watch.Observe(1_000); // far too slow for the listed size

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Delay(TimeSpan.FromSeconds(5), watch.Token));

        Assert.True(watch.Switched);
        Assert.False(caller.IsCancellationRequested); // the caller's own token is untouched: that is how a switch is told from the grown-up's Cancel
        Assert.StartsWith("Switch", watch.Judgement);
    }

    [Fact]
    public async Task SlowLinkWatch_ATimerThatFiresBeforeTheClockReadsTheCheckTime_StillJudges()
    {
        using var caller = new CancellationTokenSource();
        using var watch = new SlowLinkWatch(caller.Token, Total, TimeSpan.FromMilliseconds(40), () => TimeSpan.FromMilliseconds(38)); // the clock reads 2 ms early when the timer fires
        watch.Observe(1_000);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Delay(TimeSpan.FromSeconds(5), watch.Token));

        Assert.True(watch.Switched);
    }

    [Fact]
    public void SlowLinkWatch_JudgesOnEveryProgressTickFromTheCheckTimeOn()
    {
        using var caller = new CancellationTokenSource();
        TimeSpan now = TimeSpan.Zero;
        using var watch = new SlowLinkWatch(caller.Token, Total, TimeSpan.FromSeconds(3), () => now);

        now = TimeSpan.FromSeconds(1);
        watch.Observe(10); // too early: nothing is judged
        Assert.False(watch.Switched);

        now = TimeSpan.FromSeconds(3.2);
        watch.Observe(20); // the first tick past the check time is judged, with no timer involved

        Assert.True(watch.Switched);
        Assert.True(watch.Token.IsCancellationRequested);
    }

    [Fact]
    public void SlowLinkWatch_ATimerCallbackOfAnEarlierFetch_IsIgnoredAfterARestart()
    {
        using var caller = new CancellationTokenSource();
        TimeSpan now = TimeSpan.Zero;
        using var watch = new SlowLinkWatch(caller.Token, Total, TimeSpan.FromSeconds(3), () => now);
        int first = watch.Generation;
        now = TimeSpan.FromSeconds(2);
        watch.Restart(); // a new fetch: a new generation

        watch.TimerFired(first); // the callback of the first fetch, already queued: 0 bytes would read as a slow link

        Assert.NotEqual(first, watch.Generation);
        Assert.False(watch.Switched);
        now = TimeSpan.FromSeconds(5); // the new fetch's own check time, still no bytes
        watch.TimerFired(watch.Generation);
        Assert.True(watch.Switched);
    }

    [Fact]
    public void SlowLinkWatch_Restart_StartsTheCountAndTheClockOver()
    {
        using var caller = new CancellationTokenSource();
        TimeSpan now = TimeSpan.Zero;
        using var watch = new SlowLinkWatch(caller.Token, Total, TimeSpan.FromSeconds(3), () => now);
        now = TimeSpan.FromSeconds(2);
        watch.Observe(Total / 2); // a high-res fetch that was going well, then fell back to standard

        watch.Restart();
        now = TimeSpan.FromSeconds(4); // 2 s into the standard fetch: not judged yet, though 4 s went by since the start
        watch.Observe(100);

        Assert.False(watch.Switched);
    }

    [Fact]
    public async Task SlowLinkWatch_LeavesAFastDownloadAlone()
    {
        using var caller = new CancellationTokenSource();
        using var watch = new SlowLinkWatch(caller.Token, Total, TimeSpan.FromMilliseconds(40));
        watch.Observe(Total - 1_000); // nearly done

        await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);

        Assert.False(watch.Switched);
        Assert.False(watch.Token.IsCancellationRequested);
        Assert.StartsWith("Keep", watch.Judgement);
    }

    [Fact]
    public async Task SlowLinkWatch_TheGrownUpsCancelIsNotASwitch()
    {
        using var caller = new CancellationTokenSource();
        using var watch = new SlowLinkWatch(caller.Token, Total, TimeSpan.FromSeconds(30));

        await caller.CancelAsync();

        Assert.True(watch.Token.IsCancellationRequested);
        Assert.False(watch.Switched);
    }

    [Fact]
    public async Task SlowLinkWatch_ADownloadThatEndsBeforeTheCheckIsNeverJudged()
    {
        using var caller = new CancellationTokenSource();
        var watch = new SlowLinkWatch(caller.Token, Total, TimeSpan.FromMilliseconds(80));
        watch.Dispose(); // the download ended first

        await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);

        Assert.False(watch.Switched);
    }

    [Fact]
    public void Variants_HaveTheirOwnWireNames()
    {
        Assert.Equal("quick", FirstBookSwitch.VariantName(BookVariant.Quick));
        Assert.Equal("standard", FirstBookSwitch.VariantName(BookVariant.Standard));
        Assert.Equal("hd", FirstBookSwitch.VariantName(BookVariant.High));
    }

    private static StoreItem Item(string id, int version) => new(id, id, "en", 1, version, 5, 3, null, "https://x/" + id);

    private static readonly IReadOnlySet<string> Everything = new HashSet<string>(["a", "b", "c"], StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Derive_OffersAQuickBookOnTheShelfAtTheCataloguesVersion()
    {
        IReadOnlyList<StoreItem> due = QuickUpgradePlan.Derive(
            new Dictionary<string, int?> { ["a"] = 2, ["b"] = 2, ["c"] = 1 },
            [Item("a", 2), Item("b", 2), Item("c", 2)],
            Everything,
            new Dictionary<string, int> { ["a"] = 2, ["c"] = 1 }, // b is standard; c is quick but the catalogue moved on (an ordinary update fetches standard)
            new Dictionary<string, int>(),
            HighResLedger.Empty);

        Assert.Equal(["a"], due.Select(i => i.Id));
    }

    [Fact]
    public void Derive_SkipsABookWithAStagedStandardPackageAnUnfetchableOneAndOneTheLedgerGaveUp()
    {
        HighResLedger given = HighResLedger.Empty;
        for (int i = 0; i < HighResLedger.MaxFailures; i++)
        {
            given = given.Apply(QuickUpgradePlan.LedgerKey("c"), 2, HighResOutcome.BadPackage);
        }

        IReadOnlyList<StoreItem> due = QuickUpgradePlan.Derive(
            new Dictionary<string, int?> { ["a"] = 2, ["b"] = 2, ["c"] = 2 },
            [Item("a", 2), Item("b", 2), Item("c", 2)],
            new HashSet<string>(["a", "c"], StringComparer.OrdinalIgnoreCase), // b is not fetchable right now (an unsettled purchase): not counted, derived again later
            new Dictionary<string, int> { ["a"] = 2, ["b"] = 2, ["c"] = 2 },
            new Dictionary<string, int> { ["a"] = 2 },
            given);

        Assert.Empty(due);
    }

    [Fact]
    public void Derive_CountsTheQuickLedgerApartFromTheHighResOne()
    {
        HighResLedger highResFailures = HighResLedger.Empty;
        for (int i = 0; i < HighResLedger.MaxFailures; i++)
        {
            highResFailures = highResFailures.Apply("a", 2, HighResOutcome.BadPackage); // the book's high-res package never verified
        }

        IReadOnlyList<StoreItem> due = QuickUpgradePlan.Derive(
            new Dictionary<string, int?> { ["a"] = 2 }, [Item("a", 2)], Everything, new Dictionary<string, int> { ["a"] = 2 }, new Dictionary<string, int>(), highResFailures);

        Assert.Single(due);
    }

    [Theory]
    [InlineData(2, 2, null, true, true, null)] // a quick copy of the package's version: swap it
    [InlineData(2, 2, null, true, false, HighResOutcome.Done)] // already standard: nothing to do
    [InlineData(2, 2, null, false, true, HighResOutcome.Gone)] // the book left the shelf
    [InlineData(3, 2, null, true, true, HighResOutcome.Gone)] // a newer copy is installed: the staged one is stale
    [InlineData(1, 2, 2, true, true, HighResOutcome.Transient)] // the matching ordinary update is still staged: wait for it
    public void OutcomeBeforeSwap_FollowsTheHighResRuleWithQuickAsTheCopyToReplace(int? installed, int package, int? stagedUpdate, bool onShelf, bool stillQuick, HighResOutcome? expected)
    {
        Assert.Equal(expected, QuickUpgradePlan.OutcomeBeforeSwap(installed, package, stagedUpdate, onShelf, stillQuick));
    }

    [Fact]
    public void ABadStandardPackageIsCountedAndTheQuickCopyStaysUntilTheLedgerGivesUp()
    {
        string key = QuickUpgradePlan.LedgerKey("a");
        HighResLedger ledger = HighResLedger.Empty;
        for (int i = 1; i < HighResLedger.MaxFailures; i++)
        {
            ledger = ledger.Apply(key, 2, HighResOutcome.BadPackage);
            Assert.True(ledger.Allows(key, 2)); // still tried again: the book keeps its quick copy meanwhile
        }

        ledger = ledger.Apply(key, 2, HighResOutcome.BadPackage);
        Assert.False(ledger.Allows(key, 2));
        Assert.True(ledger.Allows("a", 2)); // the high-res ledger of the same book is untouched
        Assert.Equal(ledger, ledger.Apply(key, 2, HighResOutcome.Transient)); // a lost connection is never counted
    }

    [Fact]
    public void TheListingCarriesTheQuickPackageAndEachPacksQuickCopy()
    {
        const string json = """
            {"stories":[
              {"id":"7d0e5a4e000040008000000000000001","title":"A","language":"en","schemaVersion":1,"version":2,"downloadUrl":"/d?v=2","bytes":3000000,"hdDownloadUrl":"/d?v=2&size=hd","hdBytes":10000000,
               "quickDownloadUrl":"/d?v=2&size=quick","quickBytes":330000,
               "audio":{"en":{"version":3,"bytes":1660000,"url":"/a/en?v=3","quickUrl":"/a/en?v=3&size=quick","quickBytes":850000},"fa":{"version":1,"bytes":900000,"url":"/a/fa?v=1"}}},
              {"id":"7d0e5a4e000040008000000000000002","title":"B","language":"en","schemaVersion":1,"version":1,"downloadUrl":"/d","quickDownloadUrl":"  ","quickBytes":-4}
            ]}
            """;

        IReadOnlyList<StoreItem> items = StoreCatalog.Parse(json);

        StoreItem full = items[0];
        Assert.True(full.HasQuick);
        Assert.Equal("/d?v=2&size=quick", full.QuickDownloadUrl);
        Assert.Equal(330_000, full.BytesFor(PictureSize.Quick));
        Assert.Equal(3_000_000, full.BytesFor(PictureSize.Standard));
        Assert.Equal(10_000_000, full.BytesFor(PictureSize.High));
        AudioPackInfo en = full.AudioIn("en")!;
        Assert.Equal(new AudioPackInfo(3, 850_000, "/a/en?v=3&size=quick", "/a/en?v=3&size=quick", 850_000), en.AsQuick());
        Assert.Equal("/a/en?v=3", en.Url);
        AudioPackInfo fa = full.AudioIn("fa")!;
        Assert.Equal(fa, fa.AsQuick()); // a pack with no quick copy is fetched as it is

        StoreItem none = items[1];
        Assert.False(none.HasQuick);
        Assert.Null(none.QuickBytes);
        Assert.Equal(none.Bytes, none.BytesFor(PictureSize.Quick));
    }

    [Fact]
    public void QuickPack_AFailingStandardCopy_IsGivenUpAfterMaxFailures_PerPackAndVersion()
    {
        HighResLedger ledger = HighResLedger.Empty;
        for (int i = 0; i < HighResLedger.MaxFailures; i++)
        {
            Assert.True(QuickUpgradePlan.PackMayRetry(ledger, "b", "fa", 3));
            ledger = ledger.Apply(QuickUpgradePlan.PackLedgerKey("b", "fa"), 3, HighResOutcome.BadPackage);
        }

        Assert.False(QuickUpgradePlan.PackMayRetry(ledger, "b", "fa", 3)); // not fetched again on every pass
        Assert.True(QuickUpgradePlan.PackMayRetry(ledger, "b", "nb", 3)); // another language of the book is its own entry
        Assert.True(QuickUpgradePlan.PackMayRetry(ledger, "b", "fa", 4)); // a new pack version starts again
        Assert.True(ledger.Allows(QuickUpgradePlan.LedgerKey("b"), 3)); // the book's own upgrade is not affected
        Assert.True(QuickUpgradePlan.PackMayRetry(ledger.Remove(QuickUpgradePlan.PackLedgerKey("b", "fa")), "b", "fa", 3)); // forgetting the pack clears it
    }

    [Fact]
    public void QuickRecords_OfBooksNotOnTheShelf_AreOrphaned()
    {
        var quick = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["onshelf"] = 1, ["gone"] = 2 };

        IReadOnlyList<string> orphaned = QuickUpgradePlan.OrphanedQuickRecords(quick, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ONSHELF", "other" }, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal(["gone"], orphaned);
    }

    [Fact]
    public void QuickRecord_OfThisLaunch_SurvivesTheOrphanDrop_AnEarlierLaunchOneIsDropped()
    {
        var quick = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["inflight"] = 1, ["earlier"] = 2 };
        var thisLaunch = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "INFLIGHT" }; // its import has not put the book on the shelf yet

        IReadOnlyList<string> orphaned = QuickUpgradePlan.OrphanedQuickRecords(quick, new HashSet<string>(StringComparer.OrdinalIgnoreCase), thisLaunch);

        Assert.Equal(["earlier"], orphaned);
    }

    [Theory]
    [InlineData(true, true, false, false)] // book there, listing the same: nothing to forget
    [InlineData(false, true, false, true)] // book absent from a complete read: forgotten
    [InlineData(false, true, true, false)] // book absent but the record is this launch's (install in flight): kept
    [InlineData(true, false, false, true)] // listing moved on: forgotten
    public void QuickPackRecord_IsForgottenOnlyWhenGoneOrSupersededAndNotWrittenThisLaunch(bool onShelf, bool listingMatches, bool thisLaunch, bool forgotten) =>
        Assert.Equal(forgotten, QuickUpgradePlan.ShouldForgetQuickPack(onShelf, listingMatches, thisLaunch));

    [Fact]
    public void OnlyAConfirmedBadPackage_CountsForAStagedStandardSwap()
    {
        Assert.True(QuickUpgradePlan.IsBadStagedPackage(new InvalidDataException("zip")));
        Assert.True(QuickUpgradePlan.IsBadStagedPackage(new StoryPackageException("manifest")));
        Assert.True(QuickUpgradePlan.IsBadStagedPackage(new System.Text.Json.JsonException("json")));
        Assert.False(QuickUpgradePlan.IsBadStagedPackage(new InvalidOperationException("sqlite busy")));
        Assert.False(QuickUpgradePlan.IsBadStagedPackage(new IOException("disk")));
        Assert.False(QuickUpgradePlan.IsBadStagedPackage(new TimeoutException()));
    }

    [Theory]
    [InlineData(false, true, false)] // cancelled after the book landed: the record stays
    [InlineData(false, false, true)] // the import failed and left no book: the record goes
    [InlineData(true, true, false)]
    public void QuickRecord_GoesOnlyWhenTheBookIsNotOnTheShelf(bool importOk, bool onShelf, bool forgotten) =>
        Assert.Equal(forgotten, QuickUpgradePlan.ShouldForgetQuickRecord(importOk, onShelf));

    [Theory]
    [InlineData(PackFailureKind.Size, true)]
    [InlineData(PackFailureKind.Unzip, true)]
    [InlineData(PackFailureKind.Clip, true)]
    [InlineData(PackFailureKind.Missing, true)]
    [InlineData(PackFailureKind.Network, false)]
    [InlineData(PackFailureKind.Storage, false)]
    public void OnlyABadFile_CountsTowardTheLedger(PackFailureKind kind, bool counts) =>
        Assert.Equal(counts, QuickUpgradePlan.CountsAsBadPack(kind));

    [Fact]
    public void QuickIsNeverChosenByTheDeviceRule()
    {
        foreach (PictureDeviceClass device in Enum.GetValues<PictureDeviceClass>())
        {
            foreach (PictureNetwork network in Enum.GetValues<PictureNetwork>())
            {
                Assert.NotEqual(PictureSize.Quick, PictureSizeChoice.Choose(device, network, hasHighRes: true));
            }
        }
    }
}
