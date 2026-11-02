using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 235 part B: the keyboard map of the Windows window (Batch 46 mock frame 160).
public class DinoKeyMapTests
{
    private static DinoKeyAction Down(DinoKey key, DinoScreen screen, bool textField = false) =>
        DinoKeyMap.Resolve(key, screen, textField, keyDown: true);

    [Theory]
    [InlineData(DinoKey.Space, DinoKeyAction.PlayPause)]
    [InlineData(DinoKey.Left, DinoKeyAction.PreviousPage)]
    [InlineData(DinoKey.Right, DinoKeyAction.NextPage)]
    [InlineData(DinoKey.Escape, DinoKeyAction.Back)]
    [InlineData(DinoKey.Enter, DinoKeyAction.None)]
    public void Reading_keys(DinoKey key, DinoKeyAction expected)
    {
        Assert.Equal(expected, Down(key, DinoScreen.Reading));
    }

    [Fact]
    public void Space_on_the_back_cover_reads_again_and_the_arrows_still_turn_pages()
    {
        Assert.Equal(DinoKeyAction.ReadAgain, Down(DinoKey.Space, DinoScreen.ReadingBackCover));
        Assert.Equal(DinoKeyAction.PreviousPage, Down(DinoKey.Left, DinoScreen.ReadingBackCover));
        Assert.Equal(DinoKeyAction.Back, Down(DinoKey.Escape, DinoScreen.ReadingBackCover));
    }

    [Fact]
    public void Esc_does_nothing_on_the_shelf_and_closes_a_sheet()
    {
        Assert.Equal(DinoKeyAction.None, Down(DinoKey.Escape, DinoScreen.Shelf));
        Assert.Equal(DinoKeyAction.CloseSheet, Down(DinoKey.Escape, DinoScreen.Sheet));
    }

    [Fact]
    public void Enter_and_Space_activate_the_focused_control_of_a_sheet()
    {
        Assert.Equal(DinoKeyAction.SheetActivate, Down(DinoKey.Enter, DinoScreen.Sheet));
        Assert.Equal(DinoKeyAction.SheetActivate, Down(DinoKey.Space, DinoScreen.Sheet));
        Assert.Equal(DinoKeyAction.None, Down(DinoKey.Left, DinoScreen.Sheet));
        Assert.Equal(DinoKeyAction.None, Down(DinoKey.Enter, DinoScreen.Sheet, textField: true));
    }

    [Fact]
    public void Esc_goes_back_on_any_other_page()
    {
        Assert.Equal(DinoKeyAction.Back, Down(DinoKey.Escape, DinoScreen.Other));
    }

    [Theory]
    [InlineData(DinoScreen.Shelf)]
    [InlineData(DinoScreen.Other)]
    public void Space_and_the_arrows_do_nothing_on_the_shelf_and_other_pages(DinoScreen screen)
    {
        Assert.Equal(DinoKeyAction.None, Down(DinoKey.Space, screen));
        Assert.Equal(DinoKeyAction.None, Down(DinoKey.Left, screen));
        Assert.Equal(DinoKeyAction.None, Down(DinoKey.Right, screen));
    }

    [Theory]
    [InlineData(DinoScreen.Reading)]
    [InlineData(DinoScreen.ReadingBackCover)]
    [InlineData(DinoScreen.Gate)]
    public void A_focused_text_field_keeps_the_space_bar_arrows_and_gate_keys(DinoScreen screen)
    {
        foreach (DinoKey key in new[] { DinoKey.Space, DinoKey.Left, DinoKey.Right, DinoKey.Enter, DinoKey.Backspace, DinoKey.Digit5 })
        {
            Assert.Equal(DinoKeyAction.None, Down(key, screen, textField: true));
        }
    }

    [Fact]
    public void Esc_still_goes_back_from_a_text_field()
    {
        Assert.Equal(DinoKeyAction.Back, Down(DinoKey.Escape, DinoScreen.Reading, textField: true));
        Assert.Equal(DinoKeyAction.CloseSheet, Down(DinoKey.Escape, DinoScreen.Sheet, textField: true));
    }

    [Fact]
    public void Gate_keys()
    {
        Assert.Equal(DinoKeyAction.GateHoldStart, Down(DinoKey.Space, DinoScreen.Gate));
        Assert.Equal(DinoKeyAction.GateDigit, Down(DinoKey.Digit7, DinoScreen.Gate));
        Assert.Equal(DinoKeyAction.GateErase, Down(DinoKey.Backspace, DinoScreen.Gate));
        Assert.Equal(DinoKeyAction.GateSubmit, Down(DinoKey.Enter, DinoScreen.Gate));
        Assert.Equal(DinoKeyAction.Back, Down(DinoKey.Escape, DinoScreen.Gate));
        Assert.Equal(DinoKeyAction.None, Down(DinoKey.Left, DinoScreen.Gate));
    }

    [Fact]
    public void Letting_go_of_Space_on_the_gate_ends_the_hold_and_no_other_key_up_does_anything()
    {
        Assert.Equal(DinoKeyAction.GateHoldEnd, DinoKeyMap.Resolve(DinoKey.Space, DinoScreen.Gate, false, keyDown: false));
        Assert.Equal(DinoKeyAction.None, DinoKeyMap.Resolve(DinoKey.Space, DinoScreen.Reading, false, keyDown: false));
        Assert.Equal(DinoKeyAction.None, DinoKeyMap.Resolve(DinoKey.Enter, DinoScreen.Gate, false, keyDown: false));
        Assert.Equal(DinoKeyAction.None, DinoKeyMap.Resolve(DinoKey.Escape, DinoScreen.Sheet, false, keyDown: false));
    }

    [Fact]
    public void Keys_never_mirror_so_Left_is_previous_whatever_the_language()
    {
        // The map takes no language at all (Slice 96): there is nothing to swap.
        Assert.Equal(DinoKeyAction.PreviousPage, Down(DinoKey.Left, DinoScreen.Reading));
        Assert.Equal(DinoKeyAction.NextPage, Down(DinoKey.Right, DinoScreen.Reading));
    }

    [Fact]
    public void Digits_map_to_their_value()
    {
        Assert.Equal(0, DinoKeyMap.DigitOf(DinoKey.Digit0));
        Assert.Equal(9, DinoKeyMap.DigitOf(DinoKey.Digit9));
        Assert.Null(DinoKeyMap.DigitOf(DinoKey.Space));
        Assert.Null(DinoKeyMap.DigitOf(DinoKey.Other));
    }
}
