using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class EditorTextLimitTests
{
    [Fact]
    public void A_text_within_the_limit_is_untouched()
    {
        (string text, bool cut) = EditorTextLimit.Cap("hello world", 11);

        Assert.Equal(("hello world", false), (text, cut));
    }

    [Fact]
    public void A_longer_text_is_cut_at_the_last_word_break()
    {
        (string text, bool cut) = EditorTextLimit.Cap("alpha beta gamma delta", 14);

        Assert.True(cut);
        Assert.Equal("alpha beta", text);
    }

    [Fact]
    public void A_text_with_no_break_is_cut_at_the_limit_without_splitting_a_surrogate_pair()
    {
        string text = new string('a', 9) + "😀" + new string('b', 20);

        (string capped, bool cut) = EditorTextLimit.Cap(text, 10);

        Assert.True(cut);
        Assert.Equal(new string('a', 9), capped);
    }

    [Fact]
    public void A_huge_text_is_cut_to_the_editors_maximum()
    {
        string huge = string.Concat(Enumerable.Repeat("word ", 100_000));

        (string capped, bool cut) = EditorTextLimit.Cap(huge);

        Assert.True(cut);
        Assert.InRange(capped.Length, EditorTextLimit.MaxCharacters - 10, EditorTextLimit.MaxCharacters);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_limit_of_nothing_cuts_everything_instead_of_throwing(int max)
    {
        (string capped, bool cut) = EditorTextLimit.Cap("some text", max);

        Assert.True(cut);
        Assert.Equal(string.Empty, capped);
    }
}
