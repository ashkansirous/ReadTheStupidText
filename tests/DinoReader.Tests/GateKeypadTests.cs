using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 164: the gate's number pad and the store's author labels, moved out of their pages.
public class GateKeypadTests
{
    [Fact]
    public void Digits_append_up_to_the_limit()
    {
        string answer = string.Empty;
        answer = GateKeypad.Press(answer, "4");
        answer = GateKeypad.Press(answer, "7");
        answer = GateKeypad.Press(answer, "1");

        Assert.Equal("47", answer);
    }

    [Fact]
    public void Backspace_removes_the_last_digit_and_is_safe_when_empty()
    {
        Assert.Equal("4", GateKeypad.Press("47", GateKeypad.BackspaceKey));
        Assert.Equal(string.Empty, GateKeypad.Press(string.Empty, GateKeypad.BackspaceKey));
    }

    [Fact]
    public void Ok_submits_and_leaves_the_answer()
    {
        Assert.True(GateKeypad.IsSubmit(GateKeypad.OkKey));
        Assert.False(GateKeypad.IsSubmit("5"));
        Assert.Equal("12", GateKeypad.Press("12", GateKeypad.OkKey));
    }

    [Fact]
    public void Label_is_the_name_when_free()
    {
        Assert.Equal("Ann", ChoiceLabels.Unique("Ann", ["Anyone"], "Cancel"));
    }

    [Fact]
    public void Label_gets_a_number_when_taken_or_reserved()
    {
        Assert.Equal("Anyone (2)", ChoiceLabels.Unique("Anyone", ["Anyone"], "Cancel"));
        Assert.Equal("Cancel (2)", ChoiceLabels.Unique("Cancel", ["Anyone"], "Cancel"));
        Assert.Equal("Ann (3)", ChoiceLabels.Unique("Ann", ["Ann", "Ann (2)"], "Cancel"));
    }
}
