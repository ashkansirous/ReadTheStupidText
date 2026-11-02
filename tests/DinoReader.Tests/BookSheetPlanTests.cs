using DinoReader.Core;

namespace DinoReader.Tests;

public class BookSheetPlanTests
{
    [Fact]
    public void OwnBookGetsEditPagesAndNoStoreNote()
    {
        BookSheetPlan plan = BookSheetPlan.For(BookOrigin.Own, translated: false, hasRecording: false, developerExport: false);

        Assert.True(plan.ShowEditPages);
        Assert.False(plan.CanReAddFromStore);
    }

    [Theory]
    [InlineData(BookOrigin.Starter)]
    [InlineData(BookOrigin.Store)]
    public void CatalogueBooksHaveNoEditPages(BookOrigin origin)
    {
        Assert.False(BookSheetPlan.For(origin, false, false, false).ShowEditPages);
    }

    [Fact]
    public void OnlyStoreBooksCanBeAddedAgain()
    {
        Assert.True(BookSheetPlan.For(BookOrigin.Store, false, false, false).CanReAddFromStore);
        Assert.False(BookSheetPlan.For(BookOrigin.Starter, false, false, false).CanReAddFromStore);
    }

    [Fact]
    public void OriginalAndVoiceRowsFollowTheBooksState()
    {
        BookSheetPlan plan = BookSheetPlan.For(BookOrigin.Store, translated: true, hasRecording: true, developerExport: true);

        Assert.True(plan.ShowOriginalText);
        Assert.True(plan.ShowVoice);
        Assert.True(plan.ShowExport);
        Assert.False(BookSheetPlan.For(BookOrigin.Store, false, false, false).ShowOriginalText);
    }

    [Theory]
    [InlineData(false, false, BookOrigin.Own)]
    [InlineData(false, true, BookOrigin.Own)]
    [InlineData(true, true, BookOrigin.Starter)]
    [InlineData(true, false, BookOrigin.Store)]
    public void OriginComesFromTheCatalogueAndStarterMarks(bool fromCatalogue, bool starter, BookOrigin expected)
    {
        Assert.Equal(expected, BookSheetPlan.OriginOf(fromCatalogue, starter));
    }

    [Theory]
    [InlineData(BookOrigin.Own)]
    [InlineData(BookOrigin.Starter)]
    [InlineData(BookOrigin.Store)]
    public void ReadNowIsFirstForEveryBookKind(BookOrigin origin)
    {
        Assert.Equal(BookSheetRow.ReadNow, BookSheetPlan.For(origin, false, false, false).Rows[0]);
        Assert.Equal(BookSheetRow.ReadNow, BookSheetPlan.For(origin, true, true, true).Rows[0]);
    }

    [Fact]
    public void RowsFollowTheMockOrderAndOnlyShowWhatApplies()
    {
        BookSheetPlan all = BookSheetPlan.For(BookOrigin.Own, translated: true, hasRecording: true, developerExport: true);
        BookSheetPlan bare = BookSheetPlan.For(BookOrigin.Store, translated: false, hasRecording: false, developerExport: false);

        Assert.Equal(
            [BookSheetRow.ReadNow, BookSheetRow.ShowOriginalText, BookSheetRow.ReadWithMyVoice, BookSheetRow.RecordAndCast, BookSheetRow.EditPages, BookSheetRow.Export, BookSheetRow.Remove],
            all.Rows);
        Assert.Equal([BookSheetRow.ReadNow, BookSheetRow.RecordAndCast, BookSheetRow.Remove], bare.Rows);
    }

    [Theory]
    [InlineData(360, 760)]
    [InlineData(804, 1164)]
    public void HeldUprightTheSheetIsABottomSheet(double w, double h) =>
        Assert.False(BookSheetPlan.ShapeFor(w, h).Card);

    [Theory]
    [InlineData(760, 360, 580, 328)]
    [InlineData(1164, 804, 580, 772)]
    public void HeldSidewaysTheSheetIsOneCentredCardOn580dp(double w, double h, double cardWidth, double maxHeight)
    {
        BookSheetShape shape = BookSheetPlan.ShapeFor(w, h);
        Assert.True(shape.Card);
        Assert.Equal(cardWidth, shape.CardWidth);
        Assert.Equal(maxHeight, shape.CardMaxHeight);
    }

    [Fact]
    public void ANarrowWindowShrinksTheCardToFit() =>
        Assert.Equal(468, BookSheetPlan.ShapeFor(500, 300).CardWidth);
}
