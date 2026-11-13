using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Tests;

public class TextRegionsTests
{
    private static OcrLine L(string text, float left, float top, float width, float height) =>
        new(text, new OcrBox(left, top, left + width, top + height));

    private static OcrReading Page(params OcrLine[] lines) => new(lines, 1000, 1400);

    private static OcrLine[] Story(float top) =>
    [
        L("Carrot and Hazelnut sat on the hill", 100, top, 800, 30),
        L("and watched the big match begin. Now", 100, top + 40, 800, 30),
        L("they have a good view, but Carrot", 100, top + 80, 800, 30),
        L("wants to get even closer.", 100, top + 120, 500, 30),
    ];

    [Fact]
    public void Sign_is_dropped_and_the_story_block_kept()
    {
        var sign = L("BRIGHT STADIUM", 250, 100, 500, 90);
        IReadOnlyList<TextRegion> regions = TextRegions.Classify(Page([sign, .. Story(900)]));

        Assert.Equal(2, regions.Count);
        Assert.Equal(RegionClass.Background, regions.Single(r => r.Lines[0].Text == "BRIGHT STADIUM").Class);
        TextRegion story = regions.Single(r => r.Lines.Count == 4);
        Assert.Equal(RegionClass.Story, story.Class);
    }

    [Fact]
    public void Two_paragraph_blocks_are_both_story()
    {
        OcrLine[] second =
        [
            L("The next morning the sun came up", 100, 1100, 800, 30),
            L("and they went home for breakfast.", 100, 1140, 800, 30),
            L("It was a very good day indeed.", 100, 1180, 600, 30),
        ];
        IReadOnlyList<TextRegion> regions = TextRegions.Classify(Page([.. Story(100), .. second]));

        Assert.All(regions, r => Assert.Equal(RegionClass.Story, r.Class));
    }

    [Fact]
    public void Page_number_at_the_foot_is_background()
    {
        IReadOnlyList<TextRegion> regions = TextRegions.Classify(Page([.. Story(300), L("12", 480, 1340, 40, 30)]));

        Assert.Equal(RegionClass.Background, regions.Single(r => r.Lines[0].Text == "12").Class);
        Assert.Equal(RegionClass.Story, regions.Single(r => r.Lines.Count == 4).Class);
    }

    [Fact]
    public void Single_region_page_is_kept()
    {
        IReadOnlyList<TextRegion> regions = TextRegions.Classify(Page(Story(300)));

        TextRegion only = Assert.Single(regions);
        Assert.Equal(RegionClass.Story, only.Class);
    }

    [Fact]
    public void Title_page_with_one_big_line_is_kept()
    {
        IReadOnlyList<TextRegion> regions = TextRegions.Classify(Page(L("CARROT AND HAZELNUT", 100, 500, 800, 120)));

        Assert.Equal(RegionClass.Story, Assert.Single(regions).Class);
    }

    [Fact]
    public void Empty_reading_has_no_regions()
    {
        Assert.Empty(TextRegions.Classify(Page()));
    }
}
