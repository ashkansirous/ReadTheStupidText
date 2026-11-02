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

    [Fact]
    public void The_reading_screen_names_the_book_and_every_other_page_names_the_app()
    {
        Assert.Equal("The Tortoise and the Hare - Dino Reader", TitleBarTones.WindowTitle(TitleBarPage.Reading, "The Tortoise and the Hare", "Dino Reader"));
        Assert.Equal("Dino Reader", TitleBarTones.WindowTitle(TitleBarPage.Kids, "A book", "Dino Reader"));
        Assert.Equal("Dino Reader", TitleBarTones.WindowTitle(TitleBarPage.Reading, null, "Dino Reader"));
        Assert.Equal("Dino Reader", TitleBarTones.WindowTitle(TitleBarPage.Reading, "  ", "Dino Reader"));
    }
}
