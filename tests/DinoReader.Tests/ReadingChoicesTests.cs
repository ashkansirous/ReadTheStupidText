using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 164 follow-up: reading-screen, shelf and wizard decisions moved out of the pages.
public class ReadingChoicesTests
{
    [Theory]
    [InlineData(true, false, 0, true)]
    [InlineData(false, false, 0, false)]
    [InlineData(true, true, 0, false)]
    [InlineData(true, false, 2, false)]
    public void A_book_is_switchable_only_with_a_choice_a_translation_and_no_recordings(bool choice, bool original, int recordings, bool expected)
    {
        Assert.Equal(expected, LanguagePick.BookSwitchable(choice, original, recordings));
    }

    [Fact]
    public void Effective_language_follows_the_pick_then_the_app_language_then_the_first()
    {
        string[] readable = ["fr", "en"];

        Assert.Equal("en", LanguagePick.Effective(["en"], "en", "fr"));
        Assert.Equal("fr", LanguagePick.Effective(readable, "en", "fr"));
        Assert.Equal("en", LanguagePick.Effective(readable, "en", "de"));
        Assert.Equal("fr", LanguagePick.Effective(readable, "de", "es"));
    }

    [Fact]
    public void Shown_language_is_kept_while_it_is_still_on_the_list()
    {
        string[] readable = ["fr", "en"];

        Assert.Equal("fr", LanguagePick.Shown(readable, "fr", "en", "en"));
        Assert.Equal("en", LanguagePick.Shown(readable, "de", "en", "en"));
        Assert.Equal("en", LanguagePick.Shown(["fr"], "fr", "en", "fr"));
    }

    [Fact]
    public void Shelf_shows_the_pick_when_it_is_choosable_else_the_app_language()
    {
        Assert.Equal("fr", LanguagePick.ShelfShown(["en", "fr"], "fr", "en"));
        Assert.Equal("en", LanguagePick.ShelfShown(["en", "fr"], "de", "en"));
    }

    [Theory]
    [InlineData(800, 400, ReadingMode.Text, true, ReadingLayout.TextRow)]
    [InlineData(400, 800, ReadingMode.Text, true, ReadingLayout.Upright)]
    [InlineData(0, 0, ReadingMode.PictureOnly, true, ReadingLayout.Upright)]
    [InlineData(800, 400, ReadingMode.PictureOnly, false, ReadingLayout.Upright)]
    [InlineData(800, 400, ReadingMode.PictureOnly, true, ReadingLayout.PictureSide)]
    [InlineData(800, 400, ReadingMode.PictureAndText, true, ReadingLayout.PictureOverlay)]
    public void Layout_depends_on_orientation_mode_and_the_book(double w, double h, ReadingMode mode, bool pictureText, ReadingLayout expected)
    {
        Assert.Equal(expected, ReadingLayoutRules.For(w, h, mode, pictureText));
    }

    [Fact]
    public void A_mode_the_switch_cannot_offer_folds_to_picture_only()
    {
        Assert.Equal(ReadingMode.PictureOnly, ReadingLayoutRules.Offered(ReadingMode.PictureAndText, false));
        Assert.Equal(ReadingMode.PictureAndText, ReadingLayoutRules.Offered(ReadingMode.PictureAndText, true));
        Assert.Equal(ReadingMode.Text, ReadingLayoutRules.Offered(ReadingMode.Text, false));
    }

    [Fact]
    public void Wizard_buttons_follow_the_step()
    {
        Assert.False(WizardSteps.ShowsBack(WizardSteps.Pages));
        Assert.True(WizardSteps.ShowsBack(WizardSteps.Cover));
        Assert.Equal(2, WizardSteps.NextColumnSpan(WizardSteps.Pages));
        Assert.Equal(1, WizardSteps.NextColumnSpan(WizardSteps.Text));
        Assert.Equal(NextAction.GoToCover, WizardSteps.NextActionFor(WizardSteps.Pages));
        Assert.Equal(NextAction.Next, WizardSteps.NextActionFor(WizardSteps.Cover));
        Assert.Equal(NextAction.Save, WizardSteps.NextActionFor(WizardSteps.Text));
    }

    [Fact]
    public void Next_on_the_first_step_needs_a_page()
    {
        Assert.False(WizardSteps.NextEnabled(WizardSteps.Pages, 0));
        Assert.True(WizardSteps.NextEnabled(WizardSteps.Pages, 1));
        Assert.True(WizardSteps.NextEnabled(WizardSteps.Cover, 0));
    }
}
