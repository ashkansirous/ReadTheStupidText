using DinoReader.BookBuilder;
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
    public void PlacePanel_TallCardWithLetterbox_RaisesThePanelOverTheBandAndTakesItOffTheCap()
    {
        // Card 400x800, square picture: area 372 wide, 794 tall; picture 372 tall, band (794-372)/2 = 211 under it.
        PictureTextBook.PanelPlacement place = PictureTextBook.PlacePanel(400, 800, 1.0, 10, areaTop: 6, areaSide: 14);

        Assert.Equal(211, PictureTextBook.LetterboxBelow(400, 800, 1.0, 6, 14), 3);
        Assert.Equal(221, place.BottomMargin, 3);
        Assert.Equal(0.3 * 372 - 10, place.MaxHeight, 3);
    }

    [Fact]
    public void PlacePanel_UnknownPictureShape_KeepsTheMarginAndCardShare()
    {
        PictureTextBook.PanelPlacement place = PictureTextBook.PlacePanel(400, 800, 0, 10);

        Assert.Equal(10, place.BottomMargin);
        Assert.Equal(0.3 * 800 - 10, place.MaxHeight, 3);
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

    // Slice 171: the card holds whole lines and scrolls with the reading. Line height 37 and padding 10 are close to the 21 pt, 1.5 line-height card.
    [Theory]
    [InlineData(100, 84)]  // two lines fit (10 above, none below)
    [InlineData(84, 84)]
    [InlineData(60, 47)]   // less than a line still gets one
    [InlineData(0, 0)]
    public void SnapPanelHeight_IsWholeLinesPlusPadding(double max, double expected) =>
        Assert.Equal(expected, PictureTextBook.SnapPanelHeight(max, 37, 10), 6);

    [Theory]
    [InlineData(1080, 2400)]
    [InlineData(1600, 2560)]
    public void SnapPanelHeight_OnPhoneAndTablet_NeverCutsALine(double width, double height)
    {
        double dpWidth = width / 2.75;
        double dpHeight = height / 2.75;
        double max = PictureTextBook.PlacePanel(dpWidth, dpHeight, 1.0, 8, 6, 14).MaxHeight;
        double snapped = PictureTextBook.SnapPanelHeight(max, 31.5, 10);

        int lines = PictureTextBook.VisibleLines(snapped, 31.5, 10);
        Assert.True(lines >= 1);
        Assert.Equal(10 + lines * 31.5, snapped, 6);
        Assert.True(snapped <= max || lines == 1);
    }

    [Fact]
    public void CoverFromTop_WideAreaFillsTheWidthAndCropsTheBottom()
    {
        var frame = PictureTextBook.CoverFromTop(400, 300, 1.0);

        Assert.Equal((0.0, 0.0, 400.0, 400.0), frame);
    }

    [Fact]
    public void CoverFromTop_TallAreaFillsTheHeightAndCentresSideways()
    {
        var frame = PictureTextBook.CoverFromTop(200, 400, 1.0);

        Assert.Equal((-100.0, 0.0, 400.0, 400.0), frame);
    }

    [Fact]
    public void CoverFromTop_UnknownShapeFillsTheArea()
    {
        Assert.Equal((0.0, 0.0, 300.0, 200.0), PictureTextBook.CoverFromTop(300, 200, 0));
    }

    [Fact]
    public void CardHeightFor_MeasuredLinesGiveFiveWholeLines()
    {
        TextLines lines = Uniform(12, 30);

        Assert.Equal(10 + 150, PictureTextBook.CardHeightFor(5, lines, 99, 10), 3);
    }

    [Fact]
    public void CardHeightFor_ShortTextIsAsTallAsItsLines()
    {
        TextLines lines = Uniform(2, 30);

        Assert.Equal(10 + 60, PictureTextBook.CardHeightFor(5, lines, 99, 10), 3);
    }

    [Fact]
    public void CardHeightFor_UnmeasuredUsesTheEstimatedPitch()
    {
        Assert.Equal(10 + 5 * 31.5, PictureTextBook.CardHeightFor(5, null, 31.5, 10), 3);
    }

    // Lines as a platform lays them out: a 21 pt font at 1.5 gives 31.5 dp lines; a line with a fallback-font glyph (CJK, Farsi) is taller.
    private static TextLines Uniform(int count, double pitch = 31.5, int width = 20) =>
        new(Enumerable.Range(0, count).Select(i => i * pitch).ToList(), Enumerable.Range(1, count).Select(i => i * pitch).ToList(),
            Enumerable.Range(0, count).Select(i => i * width).ToList());

    private static TextLines Mixed() => new([0, 30, 70, 100, 130], [30, 70, 100, 130, 160], [0, 20, 40, 60, 80]);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(19, 0)]
    [InlineData(20, 1)]
    [InlineData(79, 3)]
    [InlineData(80, 4)]
    [InlineData(500, 4)]
    public void LineOf_FindsTheLineHoldingTheCharacter(int offset, int expected) =>
        Assert.Equal(expected, Uniform(5).LineOf(offset));

    [Fact]
    public void TextLines_ReportHeightPitchAndTallestRun()
    {
        TextLines lines = Mixed();
        Assert.Equal(160, lines.Height);
        Assert.Equal(32, lines.Pitch);
        Assert.Equal(40, lines.TallestRun(1));
        Assert.Equal(70, lines.TallestRun(2));
        Assert.Equal(160, lines.TallestRun(99));
    }

    [Theory]
    [InlineData(10, 31.5, 1)]    // less than a line still shows one
    [InlineData(41.5, 10, 1)]
    [InlineData(73.0, 10, 2)]
    [InlineData(104.5, 10, 3)]
    [InlineData(1000, 10, 8)]
    public void FitLines_CountsWholeLines(double viewport, double padding, int expected) =>
        Assert.Equal(expected, PictureTextBook.FitLines(Uniform(8), viewport, padding));

    [Fact]
    public void FitLines_UsesTheTallestRunSoEveryWindowFits()
    {
        // Two lines starting at line 2 are 60 dp tall, so 10 + 60 must fit before the card is called a two-line card.
        Assert.Equal(1, PictureTextBook.FitLines(Mixed(), 65, 10));
        Assert.Equal(2, PictureTextBook.FitLines(Mixed(), 80, 10));
    }

    [Theory]
    [InlineData(100, 73.0)]   // 8 lines: 10 + two lines
    [InlineData(73, 73.0)]
    [InlineData(60, 41.5)]    // less than two lines: one
    [InlineData(1000, 1000)]  // every line fits: the cap is kept
    [InlineData(0, 0)]
    public void SnapPanelHeight_OnMeasuredLines_IsWholeLinesPlusPadding(double max, double expected) =>
        Assert.Equal(expected, PictureTextBook.SnapPanelHeight(max, Uniform(8), 10), 6);

    [Theory]
    [InlineData(1080, 2400)]
    [InlineData(1600, 2560)]
    public void SnapPanelHeight_OnMeasuredLines_NeverCutsALine(double width, double height)
    {
        double max = PictureTextBook.PlacePanel(width / 2.75, height / 2.75, 1.0, 8, 6, 14).MaxHeight;
        TextLines lines = Uniform(12, 37);
        double snapped = PictureTextBook.SnapPanelHeight(max, lines, 10);

        int fit = PictureTextBook.FitLines(lines, snapped, 10);
        Assert.True(snapped <= max);
        Assert.Equal(10 + fit * 37, snapped, 6);
        Assert.True(fit < PictureTextBook.FitLines(lines, snapped + 37, 10));
    }

    [Theory]
    [InlineData(0, 5, 2, 0)]
    [InlineData(1, 5, 2, 1)]
    [InlineData(4, 5, 2, 3)]     // the last line: the card is filled, not left half empty
    [InlineData(4, 2, 2, 0)]     // two-line page: always the top
    [InlineData(3, 8, 0, 3)]     // unknown visible lines is treated as one
    [InlineData(-1, 8, 2, 0)]
    public void TopLineFor_PutsTheSentenceStartAtTop_AndFillsTheCard(int start, int total, int visible, int expected) =>
        Assert.Equal(expected, PictureTextBook.TopLineFor(start, total, visible));

    [Theory]
    [InlineData("Once upon a time. Then", 0, 0)]
    [InlineData("Once upon a time. Then", 18, 18)]
    [InlineData("Once.  \n  Then", 5, 10)]       // leading blanks and a line break are skipped
    [InlineData("Once.   ", 5, 5)]                // only blanks left: stay where it was
    [InlineData("", 3, 0)]
    public void FirstInk_SkipsBlanksToTheFirstWord(string text, int from, int expected) =>
        Assert.Equal(expected, PictureTextBook.FirstInk(text, from));

    [Fact]
    public void ScrollTargetFor_IsTheTopOfTheSentencesFirstLine_NotAnEstimate()
    {
        // Mixed lines: tops 0, 30, 70, 100, 130; starts 0, 20, 40, 60, 80. A 3-line card (10 + 100 fits 110, a run of 3 is 100 at most).
        string text = new('x', 100);
        Assert.Equal(70, PictureTextBook.ScrollTargetFor(Mixed(), text, 45, 110, 10), 6);
        Assert.Equal(0, PictureTextBook.ScrollTargetFor(Mixed(), text, 5, 110, 10), 6);
        Assert.Equal(70, PictureTextBook.ScrollTargetFor(Mixed(), text, 95, 110, 10), 6);   // last window: the card stays full
        Assert.Equal(0, PictureTextBook.ScrollTargetFor(new TextLines([], [], []), text, 45, 110, 10));
    }

    [Fact]
    public void ScrollTargetFor_AfterTheCardIsResized_UsesTheNewViewport()
    {
        // The card is snapped to whole lines after the first read, so the same sentence needs a new target for the new height.
        string text = new('x', 100);
        Assert.Equal(70, PictureTextBook.ScrollTargetFor(Mixed(), text, 95, 110, 10), 6);
        Assert.Equal(0, PictureTextBook.ScrollTargetFor(Mixed(), text, 95, 190, 10), 6);
    }

    [Fact]
    public void ScrollTargetFor_ASentenceThatStartsOnAWrappedWord_IsOnTheLineItReallyStartsOn()
    {
        // The sentence "begins" at offset 19 (a space at the end of line 0); its first word is at 20, on line 1.
        string text = new string('x', 19) + " " + new string('y', 80);
        Assert.Equal(31.5, PictureTextBook.ScrollTargetFor(Uniform(5), text, 19, 41.5, 10), 6);
    }

    private static Story Original() => new("0123456789abcdef0123456789abcdef", "Title", "en", DateTimeOffset.UnixEpoch, [], [], CoverStyle.Colour, 0, "supertonic:M1");
}
