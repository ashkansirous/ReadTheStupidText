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
}
