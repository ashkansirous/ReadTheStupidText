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
    public void Esc_asks_to_close_on_the_shelf_and_closes_a_sheet()
    {
        Assert.Equal(DinoKeyAction.AskToClose, Down(DinoKey.Escape, DinoScreen.Shelf)); // Slice 325 part C4: like Android's Back
        Assert.Equal(DinoKeyAction.CloseSheet, Down(DinoKey.Escape, DinoScreen.Sheet));
    }

    // Slice 325 part A (frame W1): Esc leaves the corner from any tab; Ctrl+1/2/3 pick its tabs and nothing else has a Ctrl shortcut.
    [Fact]
    public void Esc_leaves_the_corner_and_ctrl_digits_pick_its_tabs()
    {
        Assert.Equal(DinoKeyAction.LeaveCorner, Down(DinoKey.Escape, DinoScreen.Corner));
        Assert.Equal(DinoKeyAction.LeaveCorner, Down(DinoKey.Escape, DinoScreen.Corner, textField: true));
        Assert.Equal(DinoKeyAction.CornerBooks, DinoKeyMap.ResolveShortcut(DinoKey.Digit1, DinoScreen.Corner));
        Assert.Equal(DinoKeyAction.CornerStore, DinoKeyMap.ResolveShortcut(DinoKey.Digit2, DinoScreen.Corner));
        Assert.Equal(DinoKeyAction.CornerSettings, DinoKeyMap.ResolveShortcut(DinoKey.Digit3, DinoScreen.Corner));
        Assert.Equal(DinoKeyAction.None, DinoKeyMap.ResolveShortcut(DinoKey.Digit4, DinoScreen.Corner));
        Assert.Equal(DinoKeyAction.None, DinoKeyMap.ResolveShortcut(DinoKey.Digit1, DinoScreen.Shelf));
        Assert.Equal(DinoKeyAction.None, DinoKeyMap.ResolveShortcut(DinoKey.Digit1, DinoScreen.Gate));
        Assert.Equal(DinoKeyAction.None, Down(DinoKey.Digit1, DinoScreen.Corner));
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

    // Slice 235 part C (frame 157): Right opens the focused Settings row; nothing else changes there, and no other screen reacts to it.
    [Fact]
    public void Right_opens_the_focused_settings_row()
    {
        Assert.Equal(DinoKeyAction.OpenRow, Down(DinoKey.Right, DinoScreen.Settings));
        Assert.Equal(DinoKeyAction.None, Down(DinoKey.Right, DinoScreen.Settings, textField: true));
        foreach (DinoKey key in new[] { DinoKey.Space, DinoKey.Left, DinoKey.Up, DinoKey.Down, DinoKey.Enter, DinoKey.Digit3 })
        {
            Assert.Equal(DinoKeyAction.None, Down(key, DinoScreen.Settings));
        }
    }

    // Slice 325 part B: the Settings list is the corner's Settings tab now, so the corner answers the Right arrow the same way (the page passes it on to the focused row, or lets the arrow move the focus).
    [Fact]
    public void Right_in_the_corner_opens_the_focused_settings_row()
    {
        Assert.Equal(DinoKeyAction.OpenRow, Down(DinoKey.Right, DinoScreen.Corner));
        Assert.Equal(DinoKeyAction.None, Down(DinoKey.Right, DinoScreen.Corner, textField: true));
        foreach (DinoKey key in new[] { DinoKey.Space, DinoKey.Left, DinoKey.Up, DinoKey.Down, DinoKey.Enter })
        {
            Assert.Equal(DinoKeyAction.None, Down(key, DinoScreen.Corner));
        }
    }

    [Fact]
    public void Up_and_down_do_nothing_on_any_screen_but_the_reorder_mode()
    {
        foreach (DinoScreen screen in Enum.GetValues<DinoScreen>().Where(s => s != DinoScreen.Reorder))
        {
            Assert.Equal(DinoKeyAction.None, Down(DinoKey.Up, screen));
            Assert.Equal(DinoKeyAction.None, Down(DinoKey.Down, screen));
        }
    }

    [Fact]
    public void Esc_goes_back_from_settings()
    {
        Assert.Equal(DinoKeyAction.Back, Down(DinoKey.Escape, DinoScreen.Settings));
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

    // Slice 325 part D (frame OW2): the first-run picker.
    [Fact]
    public void The_first_books_picker_ticks_with_space_and_continues_with_enter()
    {
        Assert.Equal(DinoKeyAction.PickToggle, Down(DinoKey.Space, DinoScreen.FirstBooks));
        Assert.Equal(DinoKeyAction.PickContinue, Down(DinoKey.Enter, DinoScreen.FirstBooks));
        Assert.Equal(DinoKeyAction.Back, Down(DinoKey.Escape, DinoScreen.FirstBooks));
        Assert.Equal(DinoKeyAction.None, Down(DinoKey.Left, DinoScreen.FirstBooks));
    }

    // Slice 325 part E (frame OW6): the intro.
    [Fact]
    public void The_intro_takes_enter_as_next_esc_as_skip_all_and_the_arrows_as_steps()
    {
        Assert.Equal(DinoKeyAction.IntroNext, Down(DinoKey.Enter, DinoScreen.Intro));
        Assert.Equal(DinoKeyAction.IntroSkipAll, Down(DinoKey.Escape, DinoScreen.Intro));
        Assert.Equal(DinoKeyAction.IntroPrevious, Down(DinoKey.Left, DinoScreen.Intro));
        Assert.Equal(DinoKeyAction.IntroForward, Down(DinoKey.Right, DinoScreen.Intro));
        Assert.Equal(DinoKeyAction.None, Down(DinoKey.Space, DinoScreen.Intro));
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
