namespace DinoReader.Tests;

/// <summary>Slice 375 D: the states of a book's high-res upgrade: none, pending(v), staged(v), applied.</summary>
public sealed class HighResUpgradeTableTests
{
    private const string Book = "7d0e5a4e000040008000000000000001";
    private const string Other = "7d0e5a4e000040008000000000000002";

    [Fact]
    public void ABookWithNoEntry_IsNone_AndEveryLaterStepIsANoOp()
    {
        HighResUpgradeTable table = HighResUpgradeTable.Empty;

        Assert.Null(table.Get(Book));
        Assert.Same(table, table.MarkStaged(Book, 1));
        Assert.Same(table, table.MarkApplied(Book, 1));
        Assert.Same(table, table.MarkFailed(Book, 1));
        Assert.Same(table, table.Remove(Book));
        Assert.Equal(HighResPhase.Pending, table.Install(Book, 2, upgradeLater: true).Get(Book)!.Phase);
        Assert.Null(table.Install(Book, 2, upgradeLater: true).Install(Book, 3, upgradeLater: false).Get(Book));
        Assert.Null(table.NextPending());
    }

    [Fact]
    public void PendingThenStagedThenApplied_IsTheWholeHappyPath()
    {
        HighResUpgradeTable pending = HighResUpgradeTable.Empty.MarkPending(Book, 3);
        Assert.Equal(new HighResUpgrade(3, HighResPhase.Pending), pending.Get(Book));
        Assert.Equal(Book, pending.NextPending()!.Value.Key);

        HighResUpgradeTable staged = pending.MarkStaged(Book, 3);
        Assert.Equal(HighResPhase.Staged, staged.Get(Book)!.Phase);
        Assert.Null(staged.NextPending()); // a staged book is not fetched again

        HighResUpgradeTable applied = staged.MarkApplied(Book, 3);
        Assert.Null(applied.Get(Book));
        Assert.Equal(0, applied.Count);
    }

    [Fact]
    public void AStepForAnotherVersion_ChangesNothing()
    {
        HighResUpgradeTable pending = HighResUpgradeTable.Empty.MarkPending(Book, 3);
        Assert.Same(pending, pending.MarkStaged(Book, 2));

        HighResUpgradeTable staged = pending.MarkStaged(Book, 3);
        Assert.Same(staged, staged.MarkApplied(Book, 4));
        Assert.Same(staged, staged.MarkFailed(Book, 4));
        Assert.Same(pending, pending.MarkApplied(Book, 3)); // only a staged entry is applied
    }

    [Fact]
    public void ALostStagedFile_GoesBackToPendingWithoutCountingAFailure()
    {
        HighResUpgradeTable staged = HighResUpgradeTable.Empty.MarkPending(Book, 3).MarkStaged(Book, 3);

        for (int round = 0; round < HighResUpgradeTable.MaxFailures + 1; round++)
        {
            staged = staged.MarkLost(Book, 3).MarkStaged(Book, 3);
        }

        Assert.Equal(new HighResUpgrade(3, HighResPhase.Staged), staged.Get(Book));
        Assert.Same(staged, staged.MarkLost(Book, 4));
    }

    [Fact]
    public void ANewInstallOfTheBook_ReplacesTheEntryWithItsOwnChoice()
    {
        HighResUpgradeTable waiting = HighResUpgradeTable.Empty.MarkPending(Book, 3).MarkStaged(Book, 3);

        HighResUpgradeTable again = waiting.MarkPending(Book, 4);
        Assert.Equal(new HighResUpgrade(4, HighResPhase.Pending), again.Get(Book));

        Assert.Null(waiting.Remove(Book).Get(Book)); // the new install was not StandardThenHigh
    }

    [Fact]
    public void ABadStagedCopy_GoesBackToPending_AndAfterThreeFailuresTheUpgradeIsDropped()
    {
        HighResUpgradeTable table = HighResUpgradeTable.Empty.MarkPending(Book, 3);
        for (int strike = 1; strike < HighResUpgradeTable.MaxFailures; strike++)
        {
            table = table.MarkStaged(Book, 3).MarkFailed(Book, 3);
            Assert.Equal(new HighResUpgrade(3, HighResPhase.Pending, strike), table.Get(Book));
        }

        Assert.Null(table.MarkStaged(Book, 3).MarkFailed(Book, 3).Get(Book));
    }

    [Fact]
    public void Reconcile_DropsABookThatLeftTheShelfWasUpdatedOrIsNoLongerOfferedInHighRes()
    {
        HighResUpgradeTable table = HighResUpgradeTable.Empty.MarkPending(Book, 3).MarkPending(Other, 5);

        HighResUpgradeTable gone = table.Reconcile(id => id == Book ? null : 5, _ => null, _ => true);
        Assert.Null(gone.Get(Book));
        Assert.NotNull(gone.Get(Other));

        HighResUpgradeTable newer = table.Reconcile(id => id == Book ? 4 : 5, _ => null, _ => true);
        Assert.Null(newer.Get(Book));

        HighResUpgradeTable notOffered = table.Reconcile(_ => 3, _ => null, id => id == Book);
        Assert.NotNull(notOffered.Get(Book));
        Assert.Null(notOffered.Get(Other)); // Other is at v3 here, not v5, and not offered
    }

    [Fact]
    public void Reconcile_KeepsABookWhoseUpdateIsStaged_AndAnUnknownListingDropsNothing()
    {
        HighResUpgradeTable table = HighResUpgradeTable.Empty.MarkPending(Book, 3);

        Assert.NotNull(table.Reconcile(_ => 2, _ => 3, _ => true).Get(Book)); // installed v2, its v3 update is staged
        Assert.Null(table.Reconcile(_ => 2, _ => null, _ => true).Get(Book)); // the update was dropped
        Assert.Null(table.Reconcile(_ => 2, _ => 2, _ => true).Get(Book)); // a staged v2 is not the v3 it waits for
        Assert.NotNull(table.Reconcile(_ => 3, _ => null, _ => null).Get(Book)); // listing unknown: only the shelf decides
        Assert.Null(table.Reconcile(_ => 3, _ => null, _ => false).Get(Book));
    }

    [Fact]
    public void NextPending_TakesOneBookAtATime_InIdOrder()
    {
        HighResUpgradeTable table = HighResUpgradeTable.Empty.MarkPending(Other, 1).MarkPending(Book, 1);
        Assert.Equal(Book, table.NextPending()!.Value.Key);
        Assert.Equal(Other, table.MarkStaged(Book, 1).NextPending()!.Value.Key);
    }

    [Fact]
    public void TheJson_RoundTripsAndSurvivesDamage()
    {
        HighResUpgradeTable table = HighResUpgradeTable.Empty.MarkPending(Book, 3).MarkPending(Other, 2).MarkStaged(Other, 2);

        HighResUpgradeTable back = HighResUpgradeTable.FromJson(table.ToJson());
        Assert.Equal(table.Get(Book), back.Get(Book));
        Assert.Equal(table.Get(Other), back.Get(Other));

        Assert.Equal(0, HighResUpgradeTable.FromJson(null).Count);
        Assert.Equal(0, HighResUpgradeTable.FromJson("  ").Count);
        Assert.Equal(0, HighResUpgradeTable.FromJson("{not json").Count);
        Assert.Equal(0, HighResUpgradeTable.FromJson("{\"a\":{\"Version\":0,\"Phase\":0}}").Count);
        Assert.Equal(0, HighResUpgradeTable.FromJson("{\"a\":{\"Version\":2,\"Phase\":9}}").Count);
    }

    [Theory]
    [InlineData(HighResPhase.Pending)]
    [InlineData(HighResPhase.Staged)]
    public void Done_AndGone_RemoveTheEntry(HighResPhase phase)
    {
        HighResUpgradeTable table = InPhase(phase);

        Assert.Null(table.Apply(Book, 3, HighResOutcome.Done).Get(Book));
        Assert.Null(table.Apply(Book, 3, HighResOutcome.Gone).Get(Book));
    }

    [Theory]
    [InlineData(HighResOutcome.Refused)]
    [InlineData(HighResOutcome.Transient)]
    public void RefusedAndTransient_KeepTheEntryAndCountNoFailure(HighResOutcome outcome)
    {
        HighResUpgradeTable table = InPhase(HighResPhase.Pending);

        Assert.Same(table, table.Apply(Book, 3, outcome));
        Assert.Same(table, table.Apply(Book, 3, outcome).Apply(Book, 3, outcome));
    }

    [Fact]
    public void BadPackage_CountsAFailureAndDropsTheEntryAfterMaxFailures()
    {
        HighResUpgradeTable table = InPhase(HighResPhase.Staged);

        for (int failed = 1; failed < HighResUpgradeTable.MaxFailures; failed++)
        {
            table = table.Apply(Book, 3, HighResOutcome.BadPackage);
            Assert.Equal(new HighResUpgrade(3, HighResPhase.Pending, failed), table.Get(Book));
        }

        Assert.Null(table.Apply(Book, 3, HighResOutcome.BadPackage).Get(Book));
    }

    [Theory]
    [InlineData(HighResOutcome.Done)]
    [InlineData(HighResOutcome.Gone)]
    [InlineData(HighResOutcome.BadPackage)]
    public void AnOutcomeForAnotherVersionOrBook_ChangesNothing(HighResOutcome outcome)
    {
        HighResUpgradeTable table = InPhase(HighResPhase.Pending);

        Assert.Same(table, table.Apply(Book, 2, outcome));
        Assert.Same(table, table.Apply(Other, 3, outcome));
    }

    [Fact]
    public void AStandardUpdateStillOnItsWay_MakesTheStagedHighResPackageTransient_NotStale()
    {
        // StandardThenHigh to v2: both packages staged, the standard swap failed retryably, the book is still v1 (check 2 #1).
        HighResOutcome? outcome = HighResUpgradeTable.OutcomeBeforeSwap(stagedForVersion: true, installed: 1, entryVersion: 2, stagedStandardVersion: 2, onShelf: true, installedIsHigh: false);

        Assert.Equal(HighResOutcome.Transient, outcome);
        HighResUpgradeTable table = HighResUpgradeTable.Empty.MarkPending(Book, 2).MarkStaged(Book, 2);
        Assert.Equal(HighResPhase.Staged, table.Apply(Book, 2, outcome!.Value).Get(Book)!.Phase);
    }

    [Fact]
    public void AStandardFailure_HasNoOutcomeAndLeavesTheHighResEntryAlone()
    {
        // A failed standard update never calls the high-res outcome rule; the entry only changes through Apply, so it is exactly as it was.
        HighResUpgradeTable table = HighResUpgradeTable.Empty.MarkPending(Book, 2).MarkStaged(Book, 2);

        HighResOutcome? beforeSwap = HighResUpgradeTable.OutcomeBeforeSwap(true, installed: 1, entryVersion: 2, stagedStandardVersion: 2, onShelf: true, installedIsHigh: false);

        Assert.Same(table, table.Apply(Book, 2, beforeSwap!.Value));
    }

    [Theory]
    [InlineData(true, 2, 2, null, true, false, null)]
    [InlineData(true, 2, 2, null, true, true, HighResOutcome.Done)]
    [InlineData(false, 2, 2, null, true, false, HighResOutcome.Gone)]
    [InlineData(true, 2, 2, null, false, false, HighResOutcome.Gone)]
    [InlineData(true, 3, 2, null, true, false, HighResOutcome.Gone)]
    [InlineData(true, 1, 2, null, true, false, HighResOutcome.Gone)]
    [InlineData(true, 1, 2, 2, true, false, HighResOutcome.Transient)]
    [InlineData(true, null, 2, 2, true, false, HighResOutcome.Gone)]
    public void OutcomeBeforeSwap_CoversEveryCase(bool staged, int? installed, int entryVersion, int? stagedStandard, bool onShelf, bool installedIsHigh, HighResOutcome? expected) =>
        Assert.Equal(expected, HighResUpgradeTable.OutcomeBeforeSwap(staged, installed, entryVersion, stagedStandard, onShelf, installedIsHigh));

    [Fact]
    public void NextPending_SkipsABookRefusedInThisRun_AndGoesOnToTheNext()
    {
        HighResUpgradeTable table = HighResUpgradeTable.Empty.MarkPending(Book, 3).MarkPending(Other, 3);

        Assert.Equal(Other, table.NextPending(new HashSet<string> { Book })!.Value.Key);
        Assert.Null(table.NextPending(new HashSet<string> { Book, Other }));
    }

    private static HighResUpgradeTable InPhase(HighResPhase phase)
    {
        HighResUpgradeTable pending = HighResUpgradeTable.Empty.MarkPending(Book, 3);
        return phase == HighResPhase.Staged ? pending.MarkStaged(Book, 3) : pending;
    }

    [Fact]
    public void BookIds_AreComparedWithoutCase()
    {
        HighResUpgradeTable table = HighResUpgradeTable.Empty.MarkPending(Book.ToUpperInvariant(), 3);

        Assert.NotNull(table.Get(Book));
        Assert.Null(table.Remove(Book).Get(Book.ToUpperInvariant()));
    }
}
