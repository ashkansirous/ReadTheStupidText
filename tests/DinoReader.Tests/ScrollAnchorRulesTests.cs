using DinoReader.Core;

namespace DinoReader.Tests;

public class ScrollAnchorRulesTests
{
    [Fact]
    public void A_normal_scroll_moves_the_anchor() =>
        Assert.True(ScrollAnchorRules.ShouldUpdateAnchor(ScrollSwapPhase.Idle));

    [Theory]
    [InlineData(ScrollSwapPhase.SwapStarted)]
    [InlineData(ScrollSwapPhase.ScrollToIssued)]
    public void Events_during_a_swap_never_move_the_anchor(ScrollSwapPhase phase) =>
        Assert.False(ScrollAnchorRules.ShouldUpdateAnchor(phase));

    [Fact]
    public void Events_before_the_scroll_to_leave_the_swap_pending() =>
        Assert.Equal(ScrollSwapPhase.SwapStarted, ScrollAnchorRules.AfterScrolled(ScrollSwapPhase.SwapStarted));

    [Fact]
    public void The_first_event_after_the_scroll_to_is_ignored_and_ends_the_swap()
    {
        ScrollSwapPhase phase = ScrollAnchorRules.AfterScrollToIssued(ScrollSwapPhase.SwapStarted);
        Assert.Equal(ScrollSwapPhase.ScrollToIssued, phase);
        Assert.False(ScrollAnchorRules.ShouldUpdateAnchor(phase));
        Assert.Equal(ScrollSwapPhase.Idle, ScrollAnchorRules.AfterScrolled(phase));
        Assert.True(ScrollAnchorRules.ShouldUpdateAnchor(ScrollAnchorRules.AfterScrolled(phase)));
    }

    [Fact]
    public void An_idle_list_ignores_a_scroll_to_that_was_never_part_of_a_swap() =>
        Assert.Equal(ScrollSwapPhase.Idle, ScrollAnchorRules.AfterScrollToIssued(ScrollSwapPhase.Idle));

    [Fact]
    public void The_anchor_is_found_by_book_id_even_when_the_list_changed_shape() =>
        Assert.Equal(2, ScrollAnchorRules.IndexOfAnchor(["a", "b", "c", "d"], "c"));

    [Fact]
    public void A_mode_toggle_clears_the_anchor_so_the_first_book_is_shown() =>
        Assert.Equal(0, ScrollAnchorRules.IndexOfAnchor(["x", "y"], null));

    [Fact]
    public void A_book_that_is_gone_falls_back_to_the_first_book() =>
        Assert.Equal(0, ScrollAnchorRules.IndexOfAnchor(["x", "y"], "gone"));
}
