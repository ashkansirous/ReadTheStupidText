using DinoReader.Core;

namespace DinoReader.Tests;

public class TabletCardLayoutTests
{
    [Fact]
    public void The_drawn_card_widths_are_520_and_560()
    {
        Assert.Equal(520, TabletCardLayout.MaxWidthOf(TabletCardKind.Gate));
        Assert.Equal(560, TabletCardLayout.MaxWidthOf(TabletCardKind.BookSheet));
    }

    [Theory]
    [InlineData(1164, 1084)]
    [InlineData(600, 520)]
    [InlineData(60, 0)]
    public void The_book_sheet_card_leaves_40_dp_above_and_below(double h, double expected) =>
        Assert.Equal(expected, TabletCardLayout.BookSheetMaxHeight(h));

    [Theory]
    [InlineData(600, 600, 520)]
    [InlineData(640, 640, 560)]
    [InlineData(804, 1164, 1084)]
    public void The_tablet_card_never_outgrows_the_window_less_margins(double w, double h, double expected)
    {
        Assert.Equal(expected, TabletCardLayout.CardMaxHeight(h)); // the book sheet's card; the gate's (Slice 363) is OnboardingLayout.Gate
    }

    [Fact]
    public void The_gate_card_has_80_dp_answers_and_a_160_dp_ring()
    {
        Assert.Equal(80, TabletCardLayout.GateKeyHeight);
        Assert.Equal(160, TabletCardLayout.GateHoldRing);
    }

    [Theory]
    [InlineData(520, 80, 280, 160)]
    [InlineData(100, 80, 280, 0)]
    public void The_scrolling_part_gets_the_cap_less_padding_and_what_sits_outside_it(double cap, double chrome, double below, double expected) =>
        Assert.Equal(expected, TabletCardLayout.ScrollAreaHeight(cap, chrome, below));

    [Fact]
    public void The_gate_keeps_the_ring_its_caption_and_back_outside_the_scroll_on_the_hold_step()
    {
        double withRing = TabletCardLayout.GateFixedBelow(true, 160, 4, 18, 52, 12);
        Assert.Equal(160 + 4 + 18 + 52 + 24, withRing);
        Assert.Equal(52 + 12, TabletCardLayout.GateFixedBelow(false, 160, 4, 18, 52, 12));
    }

    [Fact]
    public void A_card_is_never_wider_than_the_window_less_its_margins()
    {
        for (double width = 600; width <= 1400; width += 37)
        {
            foreach (TabletCardKind kind in Enum.GetValues<TabletCardKind>())
            {
                Assert.True(TabletCardLayout.CardWidth(width, kind) <= width - (2 * TabletCardLayout.SideMargin));
            }
        }
    }
}
