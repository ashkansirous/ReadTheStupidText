using DinoReader.Core;

namespace DinoReader.Tests;

public class WizardDraftTests
{
    private static StoryDraft DraftOf(params PageDraft[] pages)
    {
        var story = new Story(StoryIds.New(), "T", "en", DateTimeOffset.UnixEpoch, [], [], CoverStyle.FirstPage, 0, "F1");
        var draft = new StoryDraft(story);
        foreach (PageDraft page in pages)
        {
            draft.AddPageDraft(page);
        }

        return draft;
    }

    private static PageDraft Read(string file, string text) =>
        PageDraft.ForPicture(file, new PageReading([new PageLine(text, new NormalizedRect(0.1, 0.1, 0.5, 0.1))]));

    [Fact]
    public void Pages_FollowTheDraftsInOrder_WithTheirPicturesAndLines()
    {
        PageDraft a = Read("a.jpg", "One");
        PageDraft b = Read("b.jpg", "Two");
        StoryDraft draft = DraftOf(a, b);

        draft.MovePageDraft(1, 0);

        Assert.Equal(["Two", "One"], draft.Pages.Select(p => p.Text));
        Assert.Equal([StoryDraft.DraftImageName(b.Id), StoryDraft.DraftImageName(a.Id)], draft.Pages.Select(p => p.ImageFile));
        Assert.All(draft.Pages, p => Assert.Single(p.Lines!));
    }

    [Fact]
    public void ReorderTo_KeepsThePageCoverOnThePicture_AndIgnoresAForeignList()
    {
        PageDraft a = Read("a.jpg", "A");
        PageDraft b = Read("b.jpg", "B");
        PageDraft c = Read("c.jpg", "C");
        StoryDraft draft = DraftOf(a, b, c);
        draft.CoverStyle = CoverStyle.Picture;
        draft.CoverPageIndex = 1;

        draft.ReorderTo([c.Id, a.Id, b.Id]);

        Assert.Equal(["C", "A", "B"], draft.Pages.Select(p => p.Text));
        Assert.Equal(2, draft.CoverPageIndex);

        draft.ReorderTo([a.Id, "unknown", b.Id]);
        draft.ReorderTo([a.Id]);
        Assert.Equal(["C", "A", "B"], draft.Pages.Select(p => p.Text));
    }

    [Fact]
    public void RemovePageDraft_ShiftsTheCover_OrFallsBackToTheFirstPage()
    {
        StoryDraft draft = DraftOf(Read("a.jpg", "A"), Read("b.jpg", "B"), Read("c.jpg", "C"));
        draft.CoverStyle = CoverStyle.Picture;
        draft.CoverPageIndex = 2;

        draft.RemovePageDraft(0);
        Assert.Equal(1, draft.CoverPageIndex);
        Assert.Equal("C", draft.Pages[draft.CoverPageIndex!.Value].Text);

        draft.RemovePageDraft(1);
        Assert.Null(draft.CoverPageIndex);
        Assert.Equal(CoverStyle.FirstPage, draft.CoverStyle);
        Assert.Single(draft.Pages);
    }

    [Fact]
    public void InsertPageDraft_ShiftsAPageCoverDownTheList()
    {
        StoryDraft draft = DraftOf(Read("a.jpg", "A"), Read("b.jpg", "B"));
        draft.CoverStyle = CoverStyle.Picture;
        draft.CoverPageIndex = 1;

        draft.InsertPageDraft(0, Read("n.jpg", "N"));

        Assert.Equal(2, draft.CoverPageIndex);
        Assert.Equal("B", draft.Pages[2].Text);
    }

    [Fact]
    public void LoadPageDrafts_KeepsOrderTextLinesAndCover_FromAnExistingStory()
    {
        var lines = new List<PageLine> { new("Line", new NormalizedRect(0, 0, 1, 0.1)) };
        var story = new Story(StoryIds.New(), "T", "en", DateTimeOffset.UnixEpoch,
            [new StoryPage("Line", "p0.jpg", lines), new StoryPage("Fixed line", "p1.jpg", lines), new StoryPage("Typed")],
            [], CoverStyle.Picture, 0, "F1", CoverPageIndex: 1);
        var draft = new StoryDraft(story);

        draft.LoadPageDrafts(name => name is null ? null : "/story/" + name);

        Assert.Equal(["Line", "Fixed line", "Typed"], draft.PageDrafts.Select(p => p.Text));
        Assert.False(draft.PageDrafts[0].NeedsOcr);
        Assert.Null(draft.PageDrafts[0].EditedText);
        Assert.Equal("Fixed line", draft.PageDrafts[1].EditedText);
        Assert.False(draft.PageDrafts[2].HasPicture);
        Assert.Equal(1, draft.CoverPageIndex);
        Assert.False(draft.TextChanged);
    }

    [Fact]
    public void Rotating_OrCropping_ThrowsAwayTheReading_OnlyForThatPage()
    {
        PageDraft read = Read("a.jpg", "A").WithText("Fixed");

        PageDraft turned = read.Rotated(1);
        Assert.True(turned.NeedsOcr);
        Assert.Equal("Fixed", turned.EditedText);
        Assert.Equal("Fixed", turned.Text);
        Assert.Equal(1, turned.RotationQuarterTurns);
        Assert.Null(turned.PictureFile);

        PageDraft cropped = read.WithTransform(0, new NormalizedRect(0, 0, 0.5, 0.5)).WithTransformedFile("t.jpg");
        Assert.True(cropped.NeedsOcr);
        Assert.Equal("t.jpg", cropped.PictureFile);

        Assert.False(read.NeedsOcr);
        Assert.Equal("a.jpg", read.PictureFile);
        Assert.Equal(read, read.Rotated(4));
    }

    [Fact]
    public void Rotating_TurnsTheCropWithThePicture()
    {
        var crop = new NormalizedRect(0.1, 0.2, 0.3, 0.4);

        NormalizedRect once = PageDraft.CropAfterTurn(crop, 1)!.Value;
        Assert.Equal(0.4, once.X, 6);
        Assert.Equal(0.1, once.Y, 6);
        Assert.Equal(0.4, once.Width, 6);
        Assert.Equal(0.3, once.Height, 6);

        NormalizedRect full = PageDraft.CropAfterTurn(crop, 4)!.Value;
        Assert.Equal(crop.X, full.X, 6);
        Assert.Equal(crop.Height, full.Height, 6);

        NormalizedRect back = PageDraft.CropAfterTurn(PageDraft.CropAfterTurn(crop, 1), -1)!.Value;
        Assert.Equal(crop.X, back.X, 6);
        Assert.Equal(crop.Y, back.Y, 6);
        Assert.Null(PageDraft.CropAfterTurn(null, 1));
    }

    [Fact]
    public void ATextOnlyPage_CannotBeRotated_AndNeverNeedsReading()
    {
        PageDraft typed = PageDraft.ForText("Hello");

        Assert.Equal(typed, typed.Rotated(1));
        Assert.False(typed.NeedsOcr);
        Assert.Equal("Hello", typed.Text);
    }

    [Fact]
    public void TextPages_AreStoredWithLfLineEndings()
    {
        Assert.Equal("a\nb\nc", PageDraft.ForText("a\r\nb\rc").Text);
        Assert.Equal("x\ny", PageDraft.ForText("").WithText("x\r\ny").Text);
    }

    [Fact]
    public void ReplaceIfSamePicture_DropsAReadingForAPageChangedOrDeletedMeanwhile()
    {
        PageDraft page = PageDraft.ForPicture("a.jpg");
        StoryDraft draft = DraftOf(page);
        PageDraft read = page.WithOcr(new PageReading([new PageLine("Hi", new NormalizedRect(0, 0, 1, 0.1))]));

        PageDraft turned = page.Rotated(1);
        draft.ReplacePageDraft(turned);
        Assert.False(draft.ReplaceIfSamePicture(read));
        Assert.Equal(turned, draft.PageDrafts[0]);

        draft.ReplacePageDraft(page);
        Assert.True(draft.ReplaceIfSamePicture(read));
        Assert.Equal("Hi", draft.Pages[0].Text);

        draft.RemovePageDraft(0);
        Assert.False(draft.ReplaceIfSamePicture(read));
        Assert.Empty(draft.PageDrafts);
    }

    [Fact]
    public void AddPageDrafts_SyncsOnce()
    {
        var draft = new StoryDraft(new Story(StoryIds.New(), "T", "en", DateTimeOffset.UnixEpoch, [], [], CoverStyle.FirstPage, 0, "F1"));
        int before = draft.Version;

        draft.AddPageDrafts([PageDraft.ForText("a"), PageDraft.ForText("b"), PageDraft.ForText("c")]);

        Assert.Equal(before + 1, draft.Version);
        Assert.Equal(["a", "b", "c"], draft.Pages.Select(p => p.Text));
    }
}
