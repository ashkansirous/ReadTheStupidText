using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>The pure rules of the child's story languages (Slice 173, Decision 168): the app language plus up to two sides.</summary>
public class StoryLanguageSetTests
{
    [Fact]
    public void A_set_starts_with_only_the_app_language_and_gives_the_child_no_choice()
    {
        StoryLanguageSet set = StoryLanguageSet.Create("en", null);

        Assert.Equal(["en"], set.All);
        Assert.False(set.HasChoice);
    }

    [Fact]
    public void The_app_language_is_always_in_and_never_a_side_and_junk_sides_are_dropped()
    {
        StoryLanguageSet set = StoryLanguageSet.Create("pt-BR", ["pt", "xx", "fa", "fa", "sv", "de"]);

        Assert.Equal("pt", set.Main);
        Assert.Equal(["fa", "sv"], set.Sides);
        Assert.True(set.IsFull);
    }

    [Fact]
    public void Three_languages_is_the_most_and_a_full_set_takes_no_more()
    {
        StoryLanguageSet full = StoryLanguageSet.Create("en", ["fa", "sv"]);

        Assert.False(full.CanAdd("de"));
        Assert.Equal(full.Sides, full.WithSide("de").Sides);
        Assert.Equal(["fa"], full.WithoutSide("sv").Sides);
        Assert.True(full.WithoutSide("sv").CanAdd("de"));
    }

    [Fact]
    public void The_app_language_cannot_be_ticked_again_or_removed_as_a_side()
    {
        StoryLanguageSet set = StoryLanguageSet.Create("en", ["fa"]);

        Assert.False(set.CanAdd("en"));
        Assert.Equal("en", set.WithoutSide("en").Main);
        Assert.Equal(["fa"], set.WithoutSide("en").Sides);
    }

    [Theory]
    [InlineData("fa", "fa")]
    [InlineData("sv", "en")]
    [InlineData(null, "en")]
    public void The_child_reads_in_the_pick_when_it_is_in_the_set_else_in_the_app_language(string? pick, string expected)
    {
        Assert.Equal(expected, StoryLanguageSet.Create("en", ["fa"]).Reading(pick));
    }

    [Fact]
    public void A_side_language_becoming_the_app_language_swaps_places_with_the_old_one()
    {
        MainLanguageChange change = StoryLanguageSet.Create("en", ["fa", "sv"]).ChangeMain("sv");

        Assert.Empty(change.DropChoices);
        Assert.Equal("sv", change.Set!.Main);
        Assert.Equal(["fa", "en"], change.Set.Sides);
    }

    [Fact]
    public void A_new_app_language_keeps_the_old_one_as_a_side_when_there_is_room()
    {
        MainLanguageChange change = StoryLanguageSet.Create("en", ["fa"]).ChangeMain("de");

        Assert.Equal("de", change.Set!.Main);
        Assert.Equal(["en", "fa"], change.Set.Sides);
    }

    [Fact]
    public void A_new_app_language_with_both_sides_taken_asks_which_of_the_three_goes()
    {
        StoryLanguageSet set = StoryLanguageSet.Create("en", ["fa", "sv"]);
        MainLanguageChange change = set.ChangeMain("de");

        Assert.Null(change.Set);
        Assert.Equal(["en", "fa", "sv"], change.DropChoices);
        Assert.Equal(["en", "fa"], set.ChangeMainDropping("de", "sv").Sides);
        Assert.Equal(["fa", "sv"], set.ChangeMainDropping("de", "en").Sides);
        Assert.Equal("de", set.ChangeMainDropping("de", "en").Main);
    }

    [Fact]
    public void Choosing_the_current_app_language_changes_nothing()
    {
        StoryLanguageSet set = StoryLanguageSet.Create("en", ["fa", "sv"]);

        Assert.Same(set, set.ChangeMain("en").Set);
    }

    [Fact]
    public void The_sides_survive_being_saved_as_text_and_damaged_text_gives_none()
    {
        string saved = StoryLanguageSet.Join(["fa", "sv"]);

        Assert.Equal(["fa", "sv"], StoryLanguageSet.Split(saved));
        Assert.Empty(StoryLanguageSet.Split(null));
        Assert.Empty(StoryLanguageSet.Split(" , ,"));
        Assert.Equal(["fa"], StoryLanguageSet.Create("en", StoryLanguageSet.Split("fa,klingon")).Sides);
    }
}
