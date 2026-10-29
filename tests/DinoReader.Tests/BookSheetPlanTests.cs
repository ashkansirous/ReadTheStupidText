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
}
