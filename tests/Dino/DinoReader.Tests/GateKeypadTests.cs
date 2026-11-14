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

    // Slice 363 (mock G2): the card's six answer keys.
    [Theory]
    [InlineData(12)]
    [InlineData(25)]
    [InlineData(38)]
    public void Answer_choices_are_six_different_numbers_holding_the_right_one(int answer)
    {
        for (int seed = 0; seed < 50; seed++)
        {
            IReadOnlyList<int> choices = GateKeypad.AnswerChoices(answer, new Random(seed));
            Assert.Equal(GateKeypad.AnswerKeyCount, choices.Count);
            Assert.Equal(choices.Count, choices.Distinct().Count());
            Assert.Contains(answer, choices);
            Assert.All(choices, c => Assert.InRange(c, answer - 3, answer + 3));
        }
    }

    [Fact]
    public void Answer_choices_do_not_always_put_the_right_answer_in_the_same_place()
    {
        var places = Enumerable.Range(0, 60).Select(seed => GateKeypad.AnswerChoices(20, new Random(seed)).ToList().IndexOf(20)).ToHashSet();
        Assert.True(places.Count > 1);
    }

    [Fact]
    public void Caption_splits_around_the_key_word()
    {
        CaptionParts parts = CaptionParts.Split("With the mouse, or hold the Space bar.", "Space bar");
        Assert.Equal("With the mouse, or hold the ", parts.Before);
        Assert.Equal("Space bar", parts.Key);
        Assert.Equal(".", parts.After);
    }

    [Theory]
    [InlineData("o mantén pulsada la barra espaciadora.", "Barra espaciadora", "o mantén pulsada la ", "barra espaciadora", ".")]
    [InlineData("hold mellemrumstasten nede", "Mellemrumstast", "hold ", "mellemrumstasten", " nede")]
    [InlineData("スペースキーを押し続けます", "スペースキー", "", "スペースキー", "を押し続けます")]
    public void Caption_matches_the_key_ignoring_case_and_takes_the_captions_own_spelling(string caption, string key, string before, string cap, string after)
    {
        CaptionParts parts = CaptionParts.Split(caption, key);
        Assert.Equal(before, parts.Before);
        Assert.Equal(cap, parts.Key);
        Assert.Equal(after, parts.After);
    }

    [Fact]
    public void Caption_cuts_by_the_matched_length_when_the_caption_holds_an_ignorable_character()
    {
        CaptionParts parts = CaptionParts.Split("hold the Spa­ce bar now", "Space bar");
        Assert.Equal("hold the ", parts.Before);
        Assert.Equal("Spa­ce bar", parts.Key);
        Assert.Equal(" now", parts.After);
    }

    [Theory]
    [InlineData("Tippe die Zahl", "Enter")]
    [InlineData("Anything", "")]
    public void Caption_without_the_key_word_comes_back_whole(string caption, string key)
    {
        CaptionParts parts = CaptionParts.Split(caption, key);
        Assert.Equal(caption, parts.Before);
        Assert.Equal(string.Empty, parts.Key);
        Assert.Equal(string.Empty, parts.After);
    }
}
