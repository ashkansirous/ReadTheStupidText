using DinoReader.Core;

namespace DinoReader.Tests;

public sealed class BookUpdatePlanTests
{
    private const string Id = "0123456789abcdef0123456789abcdef";

    // A shelf copy as the app reads it now: its text, and its text plus pictures (the "c2:" form the baseline is recorded in).
    private static ShelfBook Shelf(string id, string text) => new(id, text, StarterUpdate.ContentHashPrefix + text);

    private static BookUpdateDecision Decide(int storeVersion, int? installed, string? recorded, string current = "A", bool onShelf = true, bool inStore = true, int? seen = null)
    {
        StoreBookVersion[] store = inStore ? [new StoreBookVersion(Id, storeVersion)] : [];
        ShelfBook[] shelf = onShelf ? [Shelf(Id, current)] : [];
        return BookUpdatePlan.Decide(store, [new InstalledBookRecord(Id, installed, recorded is null ? null : "c2:" + recorded, seen)], shelf).Single();
    }

    [Theory]
    [InlineData(3, 2)]
    [InlineData(2, null)]
    public void AVersionBelowTheStores_IsUpdated(int storeVersion, int? installed)
    {
        BookUpdateDecision decision = Decide(storeVersion, installed, "A");

        Assert.Equal(BookUpdateAction.Update, decision.Action);
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(2, 3)]
    [InlineData(1, null)]
    public void AVersionAtOrAboveTheStores_IsUpToDate(int storeVersion, int? installed)
    {
        BookUpdateDecision decision = Decide(storeVersion, installed, "A");

        Assert.Equal(BookUpdateAction.Skip, decision.Action);
        Assert.Equal(BookUpdateSkip.UpToDate, decision.Skip);
    }

    [Fact]
    public void ABookWhoseTextChangedSinceItArrived_IsEditedAndLeftAlone()
    {
        BookUpdateDecision decision = Decide(3, 2, recorded: "A", current: "B");

        Assert.Equal(BookUpdateSkip.Edited, decision.Skip);
    }

    [Theory]
    [InlineData(3, 3, true)]
    [InlineData(3, 4, true)]
    [InlineData(4, 3, false)]
    public void AnEditedBookSeenAtTheStoresVersion_IsNotFetchedAgainUntilTheStoreHasANewerOne(int storeVersion, int seen, bool skipped)
    {
        BookUpdateDecision decision = Decide(storeVersion, 2, "A", seen: seen);

        Assert.Equal(skipped, decision.Skip == BookUpdateSkip.Edited);
    }

    [Fact]
    public void AStagedPackageIsJudgedAgainstTheLibraryAsItIsNow()
    {
        StoreBookVersion[] staged = [new StoreBookVersion(Id, 3)];
        InstalledBookRecord[] installed = [new InstalledBookRecord(Id, 2, "c2:A")];

        Assert.Equal(BookUpdateAction.Update, BookUpdatePlan.Decide(staged, installed, [Shelf(Id, "A")]).Single().Action);
        Assert.Equal(BookUpdateSkip.Edited, BookUpdatePlan.Decide(staged, installed, [Shelf(Id, "B")]).Single().Skip);
        Assert.Equal(BookUpdateSkip.Deleted, BookUpdatePlan.Decide(staged, installed, []).Single().Skip);
        Assert.Equal(BookUpdateSkip.UpToDate, BookUpdatePlan.Decide(staged, [new InstalledBookRecord(Id, 3, "c2:A")], [Shelf(Id, "A")]).Single().Skip);
    }

    [Theory]
    [InlineData(13)]
    [InlineData(1)]
    public void AForcedEntry_PlansAsUpdateAtApply_WhenTheRecordedVersionIsLoweredThen(int storeVersion)
    {
        StoreBookVersion[] staged = [new StoreBookVersion(Id, storeVersion)];
        ShelfBook[] shelf = [Shelf(Id, "A")];
        int lowered = BookUpdatePlan.ForcedRecordedVersion(storeVersion);

        Assert.Equal(BookUpdateSkip.UpToDate, BookUpdatePlan.Decide(staged, [new InstalledBookRecord(Id, storeVersion, "c2:A")], shelf).Single().Skip);
        Assert.Equal(BookUpdateAction.Update, BookUpdatePlan.Decide(staged, [new InstalledBookRecord(Id, lowered, "c2:A")], shelf).Single().Action);
    }

    [Fact]
    public void ALoweredRecordedVersionLeftInPlace_WouldLetTheLaunchRefreshSwapTheBundledCopyFirst()
    {
        // The bundled starter is at the store's version: a recorded version one lower is "older than bundled" at launch (before the apply), which is why the hook lowers it only at apply.
        Assert.True(StarterUpdate.NeedsUpdate(BookUpdatePlan.ForcedRecordedVersion(13), 13));
    }

    [Fact]
    public void ABookOffTheShelf_IsDeletedAndNotBroughtBack()
    {
        BookUpdateDecision decision = Decide(3, 2, "A", onShelf: false);

        Assert.Equal(BookUpdateSkip.Deleted, decision.Skip);
    }

    [Fact]
    public void ABookTheStoreDoesNotList_IsNotInStore()
    {
        BookUpdateDecision decision = Decide(3, 2, "A", inStore: false);

        Assert.Equal(BookUpdateSkip.NotInStore, decision.Skip);
    }

    [Fact]
    public void ABookWithNoRecordedText_NeedsItsBaselineFirst()
    {
        BookUpdateDecision decision = Decide(3, 2, recorded: null);

        Assert.Equal(BookUpdateAction.NeedsBaseline, decision.Action);
    }

    [Fact]
    public void ABookWithNoRecordedTextThatIsUpToDate_NeedsNoBaseline()
    {
        BookUpdateDecision decision = Decide(2, 2, recorded: null);

        Assert.Equal(BookUpdateSkip.UpToDate, decision.Skip);
    }

    [Fact]
    public void IdsMatchWithoutRegardToCase()
    {
        var decisions = BookUpdatePlan.Decide(
            [new StoreBookVersion(Id.ToUpperInvariant(), 3)], [new InstalledBookRecord(Id, 2, "c2:A")], [Shelf(Id, "A")]);

        Assert.Equal(BookUpdateAction.Update, decisions.Single().Action);
    }

    [Fact]
    public void EveryRecordedBookGetsItsOwnDecision()
    {
        const string other = "fedcba9876543210fedcba9876543210";
        var decisions = BookUpdatePlan.Decide(
            [new StoreBookVersion(Id, 3), new StoreBookVersion(other, 5)],
            [new InstalledBookRecord(Id, 3, "c2:A"), new InstalledBookRecord(other, 4, "c2:B")],
            [Shelf(Id, "A"), Shelf(other, "B")]);

        Assert.Equal([BookUpdateAction.Skip, BookUpdateAction.Update], decisions.Select(d => d.Action));
    }

    [Fact]
    public void DuplicateCopies_AreJudgedTogether()
    {
        StoreBookVersion[] store = [new StoreBookVersion(Id, 3)];
        InstalledBookRecord[] installed = [new InstalledBookRecord(Id, 2, "c2:A")];

        var oneStillOriginal = BookUpdatePlan.Decide(store, installed, [Shelf(Id, "B"), Shelf(Id, "A")]).Single();
        var allChanged = BookUpdatePlan.Decide(store, installed, [Shelf(Id, "B"), Shelf(Id, "C")]).Single();

        Assert.Equal(BookUpdateAction.Update, oneStillOriginal.Action);
        Assert.Equal(BookUpdateSkip.Edited, allChanged.Skip);
    }

    [Fact]
    public void ABookWhosePictureChanged_IsEditedEvenWithTheSameText()
    {
        var shelf = new ShelfBook(Id, "A", "c2:A-with-my-picture");

        var decision = BookUpdatePlan.Decide([new StoreBookVersion(Id, 3)], [new InstalledBookRecord(Id, 2, "c2:A")], [shelf]).Single();

        Assert.Equal(BookUpdateSkip.Edited, decision.Skip);
    }

    [Fact]
    public void ATextOnlyRecordedHash_NeedsItsBaselineAgainWhileTheTextMatches_AndIsEditedOtherwise()
    {
        StoreBookVersion[] store = [new StoreBookVersion(Id, 3)];
        InstalledBookRecord[] installed = [new InstalledBookRecord(Id, 2, "A")];

        Assert.Equal(BookUpdateAction.NeedsBaseline, BookUpdatePlan.Decide(store, installed, [new ShelfBook(Id, "A")]).Single().Action);
        Assert.Equal(BookUpdateSkip.Edited, BookUpdatePlan.Decide(store, installed, [new ShelfBook(Id, "B")]).Single().Skip);
    }

    [Fact]
    public void TheContentHash_ChangesWithAPictureAndKeepsTheTextOnlyFormApart()
    {
        Story story = Story.NewOwnBook("s", "en", DateTimeOffset.UnixEpoch, 0) with { Pages = [new StoryPage("One")] };

        string none = StarterUpdate.ContentHash(story, [null]);

        Assert.NotEqual(none, StarterUpdate.ContentHash(story, ["abc"]));
        Assert.Equal(none, StarterUpdate.ContentHash(story, [null]));
        Assert.True(StarterUpdate.IsContentHash(none));
        Assert.False(StarterUpdate.IsContentHash(StarterUpdate.PageTextHash(story)));
    }

    [Fact]
    public void ASwapMarker_RoundTripsAndRejectsGarbage()
    {
        var started = new SwapMarker(Id, 4, "old");
        var named = started with { FreshId = "fresh" };

        Assert.Equal(started, SwapMarker.TryParse(started.Format()));
        Assert.Equal(named, SwapMarker.TryParse(named.Format()));
        Assert.Null(SwapMarker.TryParse(null));
        Assert.Null(SwapMarker.TryParse("nonsense"));
        Assert.Null(SwapMarker.TryParse($"{Id}|x|old|"));
    }

    private static readonly DateTimeOffset Now =new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TheScheduleIsDueWhenNoCheckEverFinished()
    {
        Assert.True(BookUpdateSchedule.IsDue(null, Now));
    }

    [Theory]
    [InlineData(6, false)]
    [InlineData(7, true)]
    [InlineData(30, true)]
    public void TheScheduleIsDueOnlyOnceSevenDaysHavePassed(int daysAgo, bool expected)
    {
        Assert.Equal(expected, BookUpdateSchedule.IsDue(Now.AddDays(-daysAgo), Now));
    }

    [Fact]
    public void TheScheduleIsDueWhenTheClockMovedBackBeforeTheLastCheck()
    {
        Assert.True(BookUpdateSchedule.IsDue(Now.AddDays(1), Now));
    }
}
