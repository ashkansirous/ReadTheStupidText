using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 323 (Decision 173): the kids' shelf names a book in the language picked on its button, only when the book can be read in it.
public class ShelfTitleChoiceTests
{
    private const string Pages = "Hello there.";

    private static Story BookWith(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? translations, bool translate = true, string language = "en") =>
        new("0123456789abcdef0123456789abcdef", "Pigs", language, DateTimeOffset.UnixEpoch, [new StoryPage(Pages)], [], CoverStyle.Colour, 0, "v",
            TranslateToMyLanguage: translate, Translations: translations);

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Farsi =
        new Dictionary<string, IReadOnlyDictionary<string, string>> { ["fa"] = new Dictionary<string, string> { [Pages] = "salam.", ["Pigs"] = "khook" } };

    private static string? NoShipped(Story story, string language) => null;

    private static string? ShippedFarsi(Story story, string language) => language == "fa" ? "shipped-fa" : null;

    [Fact]
    public void A_book_with_a_full_table_is_named_in_the_picked_language()
    {
        Assert.Equal("khook", ShelfTitleChoice.For(BookWith(Farsi), "fa", "en", NoShipped));
    }

    [Fact]
    public void The_picked_language_is_the_app_language_so_the_title_is_as_it_is()
    {
        Assert.Equal("Pigs", ShelfTitleChoice.For(BookWith(Farsi), "en", "en", ShippedFarsi));
    }

    [Fact]
    public void A_book_that_cannot_be_read_in_the_picked_language_keeps_its_app_language_title()
    {
        Assert.Equal("Pigs", ShelfTitleChoice.For(BookWith(null), "fa", "en", ShippedFarsi));
        Assert.Equal("Pigs", ShelfTitleChoice.For(BookWith(Farsi, translate: false), "fa", "en", ShippedFarsi));
    }

    [Fact]
    public void A_table_with_a_hole_does_not_make_the_book_readable_in_that_language()
    {
        var partial = new Dictionary<string, IReadOnlyDictionary<string, string>> { ["fa"] = new Dictionary<string, string> { ["Pigs"] = "khook" } };

        Assert.False(ShelfTitleChoice.CanReadIn(BookWith(partial), "fa"));
        Assert.Equal("Pigs", ShelfTitleChoice.For(BookWith(partial), "fa", "en", NoShipped));
    }

    [Fact]
    public void The_shipped_title_is_used_only_for_an_english_book_without_a_table_title()
    {
        var noTitle = new Dictionary<string, IReadOnlyDictionary<string, string>> { ["fa"] = new Dictionary<string, string> { [Pages] = "salam." } };

        Assert.Equal("shipped-fa", ShelfTitleChoice.TitleIn(BookWith(noTitle), "fa", ShippedFarsi));
        Assert.Equal("Pigs", ShelfTitleChoice.TitleIn(BookWith(noTitle, language: "fr"), "fa", ShippedFarsi));
        Assert.Equal("Pigs", ShelfTitleChoice.TitleIn(BookWith(noTitle, translate: false), "fa", ShippedFarsi));
    }

    [Fact]
    public void An_unknown_language_leaves_the_title_as_it_is()
    {
        Assert.Equal("Pigs", ShelfTitleChoice.For(BookWith(Farsi), "xx", "en", ShippedFarsi));
    }

    [Fact]
    public void The_language_button_is_shown_on_the_kids_shelf_with_two_choices_and_titles_follow_the_same_rule()
    {
        Assert.True(LanguagePick.ShelfButtonShown(false, ["en", "fa"]));
        Assert.False(LanguagePick.ShelfButtonShown(false, ["en"]));
        Assert.False(LanguagePick.ShelfButtonShown(true, ["en", "fa"]));
    }
}
