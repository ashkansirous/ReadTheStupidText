using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 285: which languages a language change starts a shelf audio run for.
public sealed class ShelfAudioLanguagesTests
{
    [Fact]
    public void A_new_main_language_is_added_and_the_old_one_that_became_a_side_is_not()
    {
        StoryLanguageSet before = StoryLanguageSet.Create("en", []);
        StoryLanguageSet after = before.ChangeMain("fa").Set!;

        Assert.Equal(["fa"], ShelfAudioLanguages.Added(before, after));
    }

    [Fact]
    public void A_side_that_swaps_to_main_adds_nothing()
    {
        StoryLanguageSet before = StoryLanguageSet.Create("en", ["fa"]);
        StoryLanguageSet after = before.ChangeMain("fa").Set!;

        Assert.Empty(ShelfAudioLanguages.Added(before, after));
    }

    [Fact]
    public void A_side_added_beside_a_new_main_counts_too_and_main_comes_first()
    {
        StoryLanguageSet before = StoryLanguageSet.Create("en", []);
        StoryLanguageSet after = StoryLanguageSet.Create("fa", ["en", "sv"]);

        Assert.Equal(["fa", "sv"], ShelfAudioLanguages.Added(before, after));
    }

    [Fact]
    public void An_unchanged_set_adds_nothing()
    {
        StoryLanguageSet set = StoryLanguageSet.Create("en", ["sv"]);

        Assert.Empty(ShelfAudioLanguages.Added(set, set));
    }

    [Fact]
    public void Leaving_the_page_starts_the_added_languages_the_page_has_not_started()
    {
        StoryLanguageSet before = StoryLanguageSet.Create("en", []);
        StoryLanguageSet after = StoryLanguageSet.Create("fa", ["en", "sv"]);

        Assert.Equal(["fa", "sv"], ShelfAudioLanguages.ToStartOnLeaving(before, after, new HashSet<string>()));
        Assert.Equal(["sv"], ShelfAudioLanguages.ToStartOnLeaving(before, after, new HashSet<string> { "fa" }));
        Assert.Empty(ShelfAudioLanguages.ToStartOnLeaving(before, after, new HashSet<string> { "fa", "sv" }));
    }

    [Theory]
    [InlineData(true, 3, null, true)] // the store lists a pack and none is installed
    [InlineData(true, 3, 1, true)] // an older pack is installed
    [InlineData(true, 3, 3, false)] // the newest is installed
    [InlineData(true, 3, 5, false)] // a newer one than listed is left alone
    [InlineData(true, null, null, false)] // the store lists no pack for the book in this language
    [InlineData(false, 3, null, false)] // the language is not one of the story languages
    public void Opening_a_book_starts_a_quiet_run_only_when_a_listed_pack_is_missing_or_older(bool inSet, int? listed, int? installed, bool expected) =>
        Assert.Equal(expected, ShelfAudioLanguages.BookNeedsQuietStart(inSet, listed, installed));
}
