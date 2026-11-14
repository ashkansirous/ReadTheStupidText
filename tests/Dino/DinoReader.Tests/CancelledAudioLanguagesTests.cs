namespace DinoReader.Tests;

// Slice 269: the weekly book check must not fetch audio packs for a language the grown-up cancelled (Slice 262's Cancel).
public sealed class CancelledAudioLanguagesTests
{
    [Fact]
    public void A_cancelled_side_language_is_left_out_of_the_weekly_pack_sync()
    {
        IReadOnlyList<string> languages = CancelledAudioLanguages.Without(["en", "fa", "fr"], "fa");

        Assert.Equal(["en", "fr"], languages);
    }

    [Fact]
    public void Every_language_is_kept_when_nothing_is_marked()
    {
        Assert.Equal(["en", "fa"], CancelledAudioLanguages.Without(["en", "fa"], null));
        Assert.Equal(["en", "fa"], CancelledAudioLanguages.Without(["en", "fa"], string.Empty));
    }

    [Fact]
    public void Two_marked_languages_are_both_left_out_and_a_regional_code_matches_its_language()
    {
        Assert.Equal(["en"], CancelledAudioLanguages.Without(["en", "fa", "fr-FR"], "fa,fr"));
    }
}
