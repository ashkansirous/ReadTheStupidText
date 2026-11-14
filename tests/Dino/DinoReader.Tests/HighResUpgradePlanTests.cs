using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 375 D: the high-res upgrade is derived at each pass from the shelf, the catalogue, the installed size and a failure ledger; nothing is journaled at install time.</summary>
public sealed class HighResUpgradePlanTests
{
    private const string Book = "7d0e5a4e000040008000000000000001";
    private const string Other = "7d0e5a4e000040008000000000000002";

    private static StoreItem Item(string id, int version, bool highRes = true) =>
        new(id, id, "en", 1, version, 5, 3, null, "https://x/" + id, HdDownloadUrl: highRes ? $"https://x/{id}.hd" : null);

    private static IReadOnlyList<StoreItem> Derive(
        IReadOnlyList<StoreItem> listed,
        Dictionary<string, int?> shelf,
        IEnumerable<string>? fetchable = null,
        Dictionary<string, int>? installedHigh = null,
        Dictionary<string, int>? stagedHigh = null,
        HighResLedger? ledger = null,
        bool wantsHigh = true) =>
        HighResUpgradePlan.Derive(
            wantsHigh,
            new Dictionary<string, int?>(shelf, StringComparer.OrdinalIgnoreCase),
            listed,
            (fetchable ?? listed.Select(i => i.Id)).ToHashSet(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(installedHigh ?? [], StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(stagedHigh ?? [], StringComparer.OrdinalIgnoreCase),
            ledger ?? HighResLedger.Empty);

    [Fact]
    public void AStandardCopyOfAHighResBook_IsDue_AfterAKillMidInstallToo()
    {
        // Nothing was journaled at install time: a copy on the shelf at the catalogue's version with no high-res record is simply due.
        IReadOnlyList<StoreItem> due = Derive([Item(Book, 3)], new() { [Book] = 3 });

        Assert.Equal(Book, Assert.Single(due).Id);
    }

    [Fact]
    public void ABookNotOnTheShelf_OrAnOlderCopy_IsNotDue()
    {
        Assert.Empty(Derive([Item(Book, 3)], []));
        Assert.Empty(Derive([Item(Book, 3)], new() { [Book] = 2 })); // the update's own install comes first, then a pass derives it again
        Assert.Empty(Derive([Item(Book, 3)], new() { [Book] = null }));
    }

    [Fact]
    public void ADeviceThatDoesNotWantHighRes_DerivesNothing()
    {
        Assert.Empty(Derive([Item(Book, 3)], new() { [Book] = 3 }, wantsHigh: false));
    }

    [Fact]
    public void ABookWithNoHighResPack_IsNotDue()
    {
        Assert.Empty(Derive([Item(Book, 3, highRes: false)], new() { [Book] = 3 }));
    }

    [Fact]
    public void ACopyAlreadyHighRes_OrWithItsPackAlreadyStaged_IsNotDue()
    {
        Assert.Empty(Derive([Item(Book, 3)], new() { [Book] = 3 }, installedHigh: new() { [Book] = 3 }));
        Assert.Empty(Derive([Item(Book, 3)], new() { [Book] = 3 }, stagedHigh: new() { [Book] = 3 }));
        Assert.Single(Derive([Item(Book, 3)], new() { [Book] = 3 }, installedHigh: new() { [Book] = 2 })); // a record of an older version says nothing
    }

    [Fact]
    public void ABookNotFetchableRightNow_IsSkipped_AndNeverCountedOrDroppedAcrossManyPasses()
    {
        // An unsettled purchase: owned but not fetchable. Fifty passes later it is still not counted, and the pass that can fetch it derives it.
        HighResLedger ledger = HighResLedger.Empty;
        for (int pass = 0; pass < 50; pass++)
        {
            Assert.Empty(Derive([Item(Book, 3)], new() { [Book] = 3 }, fetchable: [], ledger: ledger));
            ledger = ledger.Apply(Book, 3, HighResOutcome.Transient); // what a pass records for a book it could not fetch
        }

        Assert.Equal(0, ledger.Count);
        Assert.Single(Derive([Item(Book, 3)], new() { [Book] = 3 }, ledger: ledger));
    }

    [Fact]
    public void ABookTheCatalogueDoesNotList_IsSkipped()
    {
        Assert.Empty(Derive([Item(Other, 1)], new() { [Book] = 3 }));
    }

    [Fact]
    public void AParseFailure_IsCounted_AndTheBookIsDroppedAfterMaxFailures()
    {
        HighResLedger ledger = HighResLedger.Empty;
        for (int failure = 1; failure < HighResLedger.MaxFailures; failure++)
        {
            ledger = ledger.Apply(Book, 3, HighResOutcome.BadPackage);
            Assert.Single(Derive([Item(Book, 3)], new() { [Book] = 3 }, ledger: ledger)); // still tried again
        }

        ledger = ledger.Apply(Book, 3, HighResOutcome.BadPackage);

        Assert.Empty(Derive([Item(Book, 3)], new() { [Book] = 3 }, ledger: ledger));
        Assert.Equal(HighResLedger.MaxFailures, ledger.Get(Book)!.Failures);
        Assert.Same(ledger, ledger.Apply(Book, 3, HighResOutcome.Transient));
    }

    [Fact]
    public void ARestagedNewerVersion_IsRetried_WhateverHappenedToTheOlderOne()
    {
        HighResLedger ledger = HighResLedger.Empty.Apply(Book, 3, HighResOutcome.Gone);
        Assert.Empty(Derive([Item(Book, 3)], new() { [Book] = 3 }, ledger: ledger));

        Assert.Single(Derive([Item(Book, 4)], new() { [Book] = 4 }, ledger: ledger)); // the ledger is about version 3 only
        Assert.Equal(1, ledger.Apply(Book, 4, HighResOutcome.BadPackage).Get(Book)!.Failures); // and a failure of v4 starts its own count
    }

    [Fact]
    public void Done_ClearsTheBook_AndGoneEndsAttemptsForThatVersionOnly()
    {
        HighResLedger failed = HighResLedger.Empty.Apply(Book, 3, HighResOutcome.BadPackage);

        Assert.Equal(0, failed.Apply(Book, 3, HighResOutcome.Done).Count);
        Assert.False(failed.Apply(Book, 3, HighResOutcome.Gone).Allows(Book, 3));
        Assert.True(failed.Apply(Book, 3, HighResOutcome.Gone).Allows(Book, 4));
        Assert.Same(failed, failed.Remove(Other));
    }

    [Fact]
    public void DerivedBooks_AreInIdOrder_AndBookIdsCompareWithoutCase()
    {
        IReadOnlyList<StoreItem> due = Derive([Item(Other, 1), Item(Book, 1)], new() { [Other.ToUpperInvariant()] = 1, [Book] = 1 });

        Assert.Equal([Book, Other], due.Select(i => i.Id));
        Assert.False(HighResLedger.Empty.Apply(Book.ToUpperInvariant(), 3, HighResOutcome.Gone).Allows(Book, 3));
    }

    [Fact]
    public void TheLedgerJson_RoundTripsAndSurvivesDamage()
    {
        HighResLedger ledger = HighResLedger.Empty.Apply(Book, 3, HighResOutcome.BadPackage).Apply(Other, 2, HighResOutcome.Gone);

        HighResLedger again = HighResLedger.FromJson(ledger.ToJson());

        Assert.Equal(ledger.Get(Book), again.Get(Book));
        Assert.Equal(ledger.Get(Other), again.Get(Other));
        Assert.Equal(0, HighResLedger.FromJson("{ not json").Count);
        Assert.Equal(0, HighResLedger.FromJson(null).Count);
        Assert.Equal(0, HighResLedger.FromJson("{\"" + Book + "\":{\"Version\":0,\"Failures\":1}}").Count);
    }

    [Theory]
    [InlineData(2, 2, null, true, false, null)]
    [InlineData(2, 2, null, true, true, HighResOutcome.Done)]
    [InlineData(1, 2, 2, true, false, HighResOutcome.Transient)] // the matching standard update is still staged: wait for it
    [InlineData(1, 2, null, true, false, HighResOutcome.Gone)]
    [InlineData(3, 2, null, true, false, HighResOutcome.Gone)]
    [InlineData(2, 2, null, false, false, HighResOutcome.Gone)] // the book left the shelf
    [InlineData(null, 2, null, false, false, HighResOutcome.Gone)]
    public void OutcomeBeforeSwap_CoversEveryCase(int? installed, int package, int? stagedStandard, bool onShelf, bool installedIsHigh, HighResOutcome? expected) =>
        Assert.Equal(expected, HighResUpgradePlan.OutcomeBeforeSwap(installed, package, stagedStandard, onShelf, installedIsHigh));

    [Fact]
    public void AMissingPackage_CountsOneFailureAndGivesUpAfterTheLimit()
    {
        HighResLedger ledger = HighResLedger.Empty;
        for (int i = 1; i < HighResLedger.MaxFailures; i++)
        {
            ledger = ledger.Apply(Book, 2, HighResUpgradePlan.OutcomeForMissingPackage());
            Assert.True(ledger.Allows(Book, 2)); // a first 404 does not end the attempts
        }

        ledger = ledger.Apply(Book, 2, HighResUpgradePlan.OutcomeForMissingPackage());
        Assert.False(ledger.Allows(Book, 2));
        Assert.True(ledger.Allows(Book, 3)); // a newer version starts again
    }

    [Fact]
    public void AMissingPackage_IsStillDerivedUntilTheLimit()
    {
        HighResLedger ledger = HighResLedger.Empty.Apply(Book, 2, HighResUpgradePlan.OutcomeForMissingPackage());
        Assert.Single(Derive([Item(Book, 2)], new() { [Book] = 2 }, ledger: ledger));
    }

    [Fact]
    public void AStageFault_OfOneBook_SkipsThatBookOnly()
    {
        Assert.True(HighResUpgradePlan.PassContinuesAfterStageFault(new ArgumentException("bad id")));
        Assert.True(HighResUpgradePlan.PassContinuesAfterStageFault(new InvalidOperationException()));
        Assert.False(HighResUpgradePlan.PassContinuesAfterStageFault(new IOException()));
        Assert.False(HighResUpgradePlan.PassContinuesAfterStageFault(new UnauthorizedAccessException()));
    }

    [Fact]
    public void OnlyAPackageThatNeverParses_IsAContentFault()
    {
        Assert.True(HighResUpgradePlan.IsContentFault(new InvalidDataException()));
        Assert.True(HighResUpgradePlan.IsContentFault(new System.Text.Json.JsonException()));
        Assert.False(HighResUpgradePlan.IsContentFault(new IOException()));
        Assert.False(HighResUpgradePlan.IsContentFault(new OperationCanceledException()));
    }
}
