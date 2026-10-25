using DinoReader.Core;

namespace DinoReader.Tests;

public class PictureTextBookTests
{
    [Fact]
    public void SortNaturally_PutsTwoBeforeTen_AndIgnoresTheFolder()
    {
        IReadOnlyList<string> sorted = PictureTextBook.SortNaturally(["c/10.jpg", "a/1.jpg", "b/2.jpg", "d/Page3.JPG"]);

        Assert.Equal(["a/1.jpg", "b/2.jpg", "c/10.jpg", "d/Page3.JPG"], sorted);
    }

    [Fact]
    public void SortNaturally_ComparesDigitRunsAsNumbers_NotText()
    {
        IReadOnlyList<string> sorted = PictureTextBook.SortNaturally(["p10.jpg", "p9.jpg", "p01.jpg", "P2.jpg"]);

        Assert.Equal(["p01.jpg", "P2.jpg", "p9.jpg", "p10.jpg"], sorted);
    }

    [Fact]
    public void SplitPages_CutsAtSeparatorLines_WithCrlfAndSpaces()
    {
        IReadOnlyList<string> pages = PictureTextBook.SplitPages("One.\r\n  ---  \r\nTwo.\r\n---\r\nThree.");

        Assert.Equal(["One.", "Two.", "Three."], pages);
    }

    [Fact]
    public void SplitPages_WithoutSeparator_IsOnePage()
    {
        Assert.Equal(["Just one page.\n\nTwo paragraphs."], PictureTextBook.SplitPages("Just one page.\n\nTwo paragraphs."));
    }

    [Fact]
    public void SplitPages_DropsEmptyPages_LeadingTrailingAndDoubled()
    {
        IReadOnlyList<string> pages = PictureTextBook.SplitPages("---\nOne.\n---\n\n---\nTwo.\n---\n");

        Assert.Equal(["One.", "Two."], pages);
    }

    [Fact]
    public void SplitPages_KeepsADashRunInsideALine()
    {
        Assert.Equal(["Wait --- what?\n----\nStill one."], PictureTextBook.SplitPages("Wait --- what?\n----\nStill one."));
    }

    [Fact]
    public void SplitPages_OfBlankText_IsNoPages()
    {
        Assert.Empty(PictureTextBook.SplitPages("  \n "));
    }

    [Fact]
    public void FillEmptySlots_FillsInOrder_AndLeavesFilledOnesAlone()
    {
        IReadOnlyList<string?> filled = PictureTextBook.FillEmptySlots([null, "mine.jpg", null, null], ["10.jpg", "2.jpg", "1.jpg"]);

        Assert.Equal(["1.jpg", "mine.jpg", "2.jpg", "10.jpg"], filled);
    }

    [Fact]
    public void FillEmptySlots_WithTooFewPictures_LeavesTheRestEmpty()
    {
        IReadOnlyList<string?> filled = PictureTextBook.FillEmptySlots([null, null, null], ["1.jpg"]);

        Assert.Equal(["1.jpg", null, null], filled);
    }

    [Fact]
    public void FillEmptySlots_WithTooManyPictures_IgnoresTheExtra()
    {
        Assert.Equal(["1.jpg"], PictureTextBook.FillEmptySlots([null], ["2.jpg", "1.jpg"]));
    }

    [Fact]
    public void WithPicture_MakesAPictureTextDraft_WithoutOcr()
    {
        PageDraft page = PictureTextBook.WithPicture(PageDraft.ForText("Once upon a time."), "pig.jpg");

        Assert.True(PictureTextBook.IsPictureText(page));
        Assert.False(page.NeedsOcr);
        Assert.Equal("Once upon a time.", page.Text);
    }

    [Fact]
    public void ADraftWithAnOcrReading_IsNotPictureText()
    {
        var lines = new PageReading([new PageLine("Hello", new NormalizedRect(0, 0, 1, 0.1))]);

        Assert.False(PictureTextBook.IsPictureText(PageDraft.ForPicture("scan.jpg", lines)));
        Assert.False(PictureTextBook.IsPictureText(PageDraft.ForText("Text only.")));
    }

    [Fact]
    public void ADraftWithAPicture_BecomesAStoryPageWithImageAndTextButNoLines()
    {
        var draft = new StoryDraft(Original());
        draft.AddPageDraft(PictureTextBook.WithPicture(PageDraft.ForText("Page one."), "pig.jpg"));

        StoryPage page = draft.Pages.Single();

        Assert.NotNull(page.ImageFile);
        Assert.Equal("Page one.", page.Text);
        Assert.Null(page.Lines);
        Assert.True(PictureTextBook.IsPictureText(page));
    }

    [Fact]
    public void IsPictureText_NeedsAPictureAndTextAndNoLines()
    {
        var line = new PageLine("x", new NormalizedRect(0, 0, 1, 1));

        Assert.True(PictureTextBook.IsPictureText(new StoryPage("Text", "a.jpg")));
        Assert.False(PictureTextBook.IsPictureText(new StoryPage("Text")));
        Assert.False(PictureTextBook.IsPictureText(new StoryPage(string.Empty, "a.jpg")));
        Assert.False(PictureTextBook.IsPictureText(new StoryPage("Text", "a.jpg", [line])));
    }

    [Fact]
    public async Task APictureTextStory_RoundTripsThroughAPackage()
    {
        string root = Path.Combine(Path.GetTempPath(), "dino-picture-text-" + Guid.NewGuid().ToString("N"));
        try
        {
            var library = new FileStoryLibrary(root);
            Story story = Original() with
            {
                Pages = [new StoryPage("One.", "page-1.jpg"), new StoryPage("Two.", "page-2.jpg")],
            };
            await library.AddAsync(story);
            foreach (StoryPage page in story.Pages)
            {
                await File.WriteAllBytesAsync(library.TryResolveFile(story.Id, page.ImageFile)!, [1, 2, 3]);
            }

            using var package = new MemoryStream();
            await StoryPackageWriter.WriteAsync(story, library, package);
            package.Position = 0;
            Story imported = await StoryPackageReader.ImportAsync(package, library);

            Assert.All(imported.Pages, p => Assert.True(PictureTextBook.IsPictureText(p)));
            Assert.Equal(["One.", "Two."], imported.Pages.Select(p => p.Text));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void StarterBuilder_WithPictures_NamesOnePicturePerPage()
    {
        Story bare = StarterBuilder.Build("id", "Title", "One.\n---\nTwo.", 0);

        Story withPictures = StarterBuilder.WithPictures(bare, ["1.jpg", "2.jpg"]);

        Assert.Equal([StarterBuilder.PictureName(0), StarterBuilder.PictureName(1)], withPictures.Pages.Select(p => p.ImageFile));
    }

    [Fact]
    public void StarterBuilder_WithoutPictures_ChangesNothing()
    {
        Story bare = StarterBuilder.Build("id", "Title", "One.\n---\nTwo.", 0);

        Assert.Same(bare, StarterBuilder.WithPictures(bare, []));
    }

    [Fact]
    public void StarterBuilder_FailsWhenThePictureCountDiffersFromThePages()
    {
        Story bare = StarterBuilder.Build("id", "Title", "One.\n---\nTwo.\n---\nThree.", 0);

        Assert.Throws<InvalidOperationException>(() => StarterBuilder.WithPictures(bare, ["1.jpg", "2.jpg"]));
    }

    [Fact]
    public void IsPictureTextBook_StaysTrueWhenTheBookMixesPictureOnlyAndTextOnlyPages()
    {
        StoryPage[] pages = [new("One.", "a.jpg"), new(string.Empty, "b.jpg"), new("Three.")];

        Assert.True(PictureTextBook.IsPictureTextBook(pages));
    }

    [Fact]
    public void IsPictureTextBook_IsFalseForAScannedBookWithLineBoxes()
    {
        StoryPage[] pages = [new("One.", "a.jpg", [new PageLine("One.", new NormalizedRect(0, 0, 1, 0.1))]), new(string.Empty, "b.jpg")];

        Assert.False(PictureTextBook.IsPictureTextBook(pages));
    }

    [Fact]
    public void ImportPages_SplitsOnSeparatorsOnly_AndKeepsOtherTextAsOnePage()
    {
        Assert.Equal(["One.", "Two."], PictureTextBook.ImportPages("One.\n---\nTwo."));
        Assert.Equal([" Plain \n"], PictureTextBook.ImportPages(" Plain \n"));
    }

    [Fact]
    public void PanelMaxHeight_TallCardWithLetterbox_CountsBandUnderPictureAndMargin()
    {
        // Area 372 x 794 (card 400 x 800 less 6 on top and 14 each side); a 1:1 picture is 372 high, bands of 211 above and below.
        double max = PictureTextBook.PanelMaxHeight(400, 800, 1.0, 10, areaTop: 6, areaSide: 14);

        Assert.Equal(0.30 * 372 + 211 - 10, max, 3);
    }

    [Fact]
    public void PanelMaxHeight_WidePicture_UsesPictureHeightNotCard()
    {
        // A 2:1 picture in a 400-wide area is 186 high.
        double max = PictureTextBook.PanelMaxHeight(400, 800, 2.0, 10, areaTop: 0, areaSide: 14);

        Assert.Equal(0.30 * 186 + (800 - 186) / 2.0 - 10, max, 3);
    }

    [Fact]
    public void PanelMaxHeight_PictureFillsAreaHeight_IsThirtyPercentLessMargin()
    {
        // A tall 1:4 picture fills the height: no band, so the panel is 30% of the picture less the margin.
        double max = PictureTextBook.PanelMaxHeight(400, 800, 0.25, 10);

        Assert.Equal(0.30 * 800 - 10, max, 3);
    }

    [Fact]
    public void PanelMaxHeight_NeverNegative_AndSurvivesNoAspect()
    {
        Assert.Equal(0, PictureTextBook.PanelMaxHeight(10, 20, 0.5, 100));
        Assert.True(PictureTextBook.PanelMaxHeight(400, 800, 0, 10) > 0);
    }

    [Fact]
    public void ShowsWordsOverPictures_DefaultsOnForPictureTextBooks()
    {
        Story story = Original() with { Pages = [new StoryPage("Once.", "1.jpg")] };

        Assert.True(PictureTextBook.ShowsWordsOverPictures(story));
    }

    [Fact]
    public void WordsOverPictures_IsOmittedFromJsonWhenUnset()
    {
        Assert.DoesNotContain("wordsOverPictures", StoryJson.Serialize(Original()), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShowsWordsOverPictures_StoredChoiceBeatsTheDefault()
    {
        Story off = Original() with { Pages = [new StoryPage("Once.", "1.jpg")], WordsOverPictures = false };
        Story on = Original() with { WordsOverPictures = true };

        Assert.False(PictureTextBook.ShowsWordsOverPictures(off));
        Assert.True(PictureTextBook.ShowsWordsOverPictures(on));
    }

    [Fact]
    public void MarkPicturesAsPages_TurnsWordsOff_ButKeepsAnExplicitChoice()
    {
        var fresh = new StoryDraft(Original());
        fresh.MarkPicturesAsPages();
        var chosen = new StoryDraft(Original() with { WordsOverPictures = true });
        chosen.MarkPicturesAsPages();

        Assert.False(fresh.WordsOverPictures);
        Assert.True(chosen.WordsOverPictures);
    }

    [Fact]
    public async Task WordsOverPictures_SurvivesPackageExportAndImport()
    {
        string root = Path.Combine(Path.GetTempPath(), "dino-wop-" + Guid.NewGuid().ToString("N"));
        try
        {
            var source = new FileStoryLibrary(Path.Combine(root, "source"));
            var target = new FileStoryLibrary(Path.Combine(root, "target"));
            using var package = new MemoryStream();
            await StoryPackageWriter.WriteAsync(Original() with { WordsOverPictures = false }, source, package);
            package.Position = 0;

            Story imported = await StoryPackageReader.ImportAsync(package, target);

            Assert.False(imported.WordsOverPictures);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void WordsOverPictures_RoundTripsThroughJson_AndStaysNullWhenUnset()
    {
        Story set = Original() with { WordsOverPictures = false };

        Assert.False(StoryJson.Deserialize(StoryJson.Serialize(set)).WordsOverPictures);
        Assert.Null(StoryJson.Deserialize(StoryJson.Serialize(Original())).WordsOverPictures);
    }

    private static Story Original() => new("0123456789abcdef0123456789abcdef", "Title", "en", DateTimeOffset.UnixEpoch, [], [], CoverStyle.Colour, 0, "supertonic:M1");
}
