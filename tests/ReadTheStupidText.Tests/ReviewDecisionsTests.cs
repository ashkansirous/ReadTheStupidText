using ReadTheStupidText.Application.Translation;

namespace ReadTheStupidText.Tests;

public class ReviewDecisionsTests
{
    [Theory]
    [InlineData("fr", "en", true, true)]
    [InlineData("en", "en", true, false)]
    [InlineData(null, "en", true, false)]
    [InlineData("fr", "en", false, false)]
    public void ShowTranslateOption_needs_a_different_language_and_a_translator(string? detected, string target, bool canTranslate, bool expected)
    {
        Assert.Equal(expected, ReviewDecisions.ShowTranslateOption(detected, target, canTranslate));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void TranslatesOnRead_only_when_the_checkbox_is_shown_and_ticked(bool shown, bool ticked, bool expected)
    {
        Assert.Equal(expected, ReviewDecisions.TranslatesOnRead(shown, ticked));
    }

    [Theory]
    [InlineData(true, false, "Hola a todos", true)] // pending at Play: detect now
    [InlineData(true, true, "Hola a todos", false)] // done (or failed): decide as before
    [InlineData(false, false, "Hola a todos", false)] // no detector
    [InlineData(true, false, "  ", false)] // nothing to read
    [InlineData(true, false, null, false)]
    public void NeedsDetectionAtRead_only_while_a_pass_is_still_pending(bool hasDetector, bool settled, string? text, bool expected)
    {
        Assert.Equal(expected, ReviewDecisions.NeedsDetectionAtRead(hasDetector, settled, text));
    }

    [Fact]
    public void ReadLanguage_is_the_target_when_translated_else_the_detected_one()
    {
        Assert.Equal("fa", ReviewDecisions.ReadLanguage(true, "fr", "fa"));
        Assert.Equal("fr", ReviewDecisions.ReadLanguage(false, "fr", "fa"));
        Assert.Null(ReviewDecisions.ReadLanguage(false, null, "fa"));
    }

    [Fact]
    public void ShowOriginal_restores_the_text_exactly_and_clears_the_original()
    {
        string original = "Bonjour le monde  fin ";
        var state = new ReviewTextState(original);

        ReviewTextState translated = state.Translated("Hello world");
        ReviewTextState restored = translated.ShowOriginal();

        Assert.True(translated.HasOriginal);
        Assert.Equal("Hello world", translated.Text);
        Assert.Equal(original, restored.Text);
        Assert.False(restored.HasOriginal);
    }

    [Fact]
    public void Translated_keeps_the_first_original_and_Edited_keeps_it_too()
    {
        ReviewTextState state = new ReviewTextState("a").Translated("b").Edited("b!").Translated("c");

        Assert.Equal("a", state.Original);
        Assert.Equal("a", state.ShowOriginal().Text);
    }

    [Fact]
    public void ReviewHandOff_hands_each_value_over_once()
    {
        var handOff = new ReviewHandOff();
        handOff.Open(new ReviewRequest("text", null, [2], [1, 3]));
        handOff.Finish(new ReviewResult("done", "orig", "fa"));

        Assert.Equal("text", handOff.TakeRequest()?.Text);
        Assert.Null(handOff.TakeRequest());
        Assert.Equal("orig", handOff.TakeResult()?.Original);
        Assert.Null(handOff.TakeResult());
    }
}
