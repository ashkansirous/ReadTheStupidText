using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 235 part B: which title bar colour each page kind gets (Batch 46 mock frame 155).
public class TitleBarToneTests
{
    [Theory]
    [InlineData(TitleBarPage.Kids, TitleBarTone.Cream)]
    [InlineData(TitleBarPage.Reading, TitleBarTone.Cream)]
    [InlineData(TitleBarPage.GrownUp, TitleBarTone.Paper)]
    [InlineData(TitleBarPage.Corner, TitleBarTone.Ink)]
    public void Each_page_kind_has_its_tone(TitleBarPage page, TitleBarTone expected)
    {
        Assert.Equal(expected, TitleBarTones.For(page));
    }

    // Slice 325 decision 8 (frame W1): "Grown-ups' corner · Dino Reader" in the corner, the shelf's plain "Dino Reader".
    [Fact]
    public void The_corner_names_itself_before_the_app()
    {
        Assert.Equal("Grown-ups’ corner · Dino Reader", TitleBarTones.WindowTitle(TitleBarPage.Corner, null, "Dino Reader", "Grown-ups’ corner"));
        Assert.Equal("Dino Reader", TitleBarTones.WindowTitle(TitleBarPage.Corner, null, "Dino Reader"));
        Assert.Equal("Dino Reader", TitleBarTones.WindowTitle(TitleBarPage.Kids, null, "Dino Reader", "Grown-ups’ corner"));
    }

    [Fact]
    public void The_reading_screen_names_the_book_and_every_other_page_names_the_app()
    {
        Assert.Equal("The Tortoise and the Hare - Dino Reader", TitleBarTones.WindowTitle(TitleBarPage.Reading, "The Tortoise and the Hare", "Dino Reader"));
        Assert.Equal("Dino Reader", TitleBarTones.WindowTitle(TitleBarPage.Kids, "A book", "Dino Reader"));
        Assert.Equal("Dino Reader", TitleBarTones.WindowTitle(TitleBarPage.Reading, null, "Dino Reader"));
        Assert.Equal("Dino Reader", TitleBarTones.WindowTitle(TitleBarPage.Reading, "  ", "Dino Reader"));
    }
}
