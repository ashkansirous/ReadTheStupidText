using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 359 item 6: the credits card is 300 / 340 / 360 wide and never wider than the picture less 32.</summary>
public class BackCoverCardTests
{
    [Theory]
    [InlineData(BackCoverCardKind.Phone, 300, 20, 16)]
    [InlineData(BackCoverCardKind.PhoneLandscape, 340, 19, 15)]
    [InlineData(BackCoverCardKind.Wide, 360, 18, 18)]
    public void A_wide_picture_gets_the_mocks_card_and_sizes(BackCoverCardKind kind, double width, double title, double line)
    {
        BackCoverCardSpec spec = BackCoverCard.For(kind, 1000);

        Assert.Equal(width, spec.Width);
        Assert.Equal(title, spec.TitleSize);
        Assert.Equal(line, spec.LineSize);
    }

    [Theory]
    [InlineData(BackCoverCardKind.Phone, 320, 288)]
    [InlineData(BackCoverCardKind.PhoneLandscape, 360, 328)]
    [InlineData(BackCoverCardKind.Wide, 380, 348)]
    [InlineData(BackCoverCardKind.Phone, 332, 300)]
    [InlineData(BackCoverCardKind.Phone, 10, 0)]
    public void A_narrow_picture_shrinks_the_card_to_the_picture_less_32(BackCoverCardKind kind, double picture, double expected)
    {
        Assert.Equal(expected, BackCoverCard.For(kind, picture).Width);
    }

    private static readonly string[] ShortLines = ["Written by A", "Drawn by B", "Read by C", "2026"];

    [Fact]
    public void A_card_that_fits_keeps_the_fixed_sizes()
    {
        BackCoverCardSpec spec = BackCoverCard.Fit(BackCoverCardKind.Phone, 400, 800, "The End", ShortLines);

        Assert.Equal(20, spec.TitleSize);
        Assert.Equal(16, spec.LineSize);
    }

    [Fact]
    public void A_card_too_tall_for_the_stage_steps_title_and_lines_down_together_until_it_fits()
    {
        string[] long4 = Enumerable.Repeat(new string('x', 60), 4).ToArray();
        const double stageHeight = 280;

        BackCoverCardSpec spec = BackCoverCard.Fit(BackCoverCardKind.PhoneLandscape, 852, stageHeight, new string('t', 40), long4);

        Assert.True(spec.LineSize < 15);
        Assert.Equal(19 - spec.TitleSize, 15 - spec.LineSize);
        Assert.True(BackCoverCard.EstimatedHeight(spec, new string('t', 40), long4) <= stageHeight - 32);
    }

    [Fact]
    public void A_card_that_can_never_fit_stops_at_the_floor()
    {
        BackCoverCardSpec spec = BackCoverCard.Fit(BackCoverCardKind.Phone, 400, 100, "The End", ShortLines);

        Assert.Equal(12, spec.TitleSize);
        Assert.Equal(10, spec.LineSize);
    }

    [Theory]
    [InlineData(700, 393)]
    [InlineData(360, 700)]
    [InlineData(300, 400)]
    public void The_picture_fills_the_stage_and_loses_40_percent_of_the_cut_from_the_top(double stageW, double stageH)
    {
        BackCoverPictureFrame frame = BackCoverPicture.Frame(stageW, stageH);

        Assert.True(frame.Width >= stageW - 1e-9);
        Assert.True(frame.Height >= stageH - 1e-9);
        Assert.Equal(0.75, frame.Width / frame.Height, 9);
        double cut = frame.Height - stageH;
        Assert.Equal(-cut * 0.4, frame.OffsetY, 9);
    }

    [Fact]
    public void Full_width_text_is_estimated_taller_than_the_same_count_of_latin_letters()
    {
        BackCoverCardSpec spec = BackCoverCard.For(BackCoverCardKind.Phone, 1000);
        string cjk = new('日', 20); // 20 ideographs, about 20 em: wraps in a 256 wide card at 16
        string latin = new('x', 20);

        double cjkHeight = BackCoverCard.EstimatedHeight(spec, "T", [cjk]);
        double latinHeight = BackCoverCard.EstimatedHeight(spec, "T", [latin]);

        Assert.True(cjkHeight > latinHeight);
    }

    [Fact]
    public void A_picture_of_another_aspect_gets_its_own_frame()
    {
        BackCoverPictureFrame frame = BackCoverPicture.Frame(300, 400, 1.5);

        Assert.Equal(1.5, frame.Width / frame.Height, 9);
        Assert.True(frame.Height >= 400 - 1e-9);
    }

    [Theory]
    [InlineData(false, false, BackCoverCardKind.Phone)]
    [InlineData(false, true, BackCoverCardKind.PhoneLandscape)]
    [InlineData(true, false, BackCoverCardKind.Wide)]
    [InlineData(true, true, BackCoverCardKind.Wide)]
    public void The_kind_follows_the_screen(bool wide, bool landscape, BackCoverCardKind expected)
    {
        Assert.Equal(expected, BackCoverCard.KindFor(wide, landscape));
    }
}
