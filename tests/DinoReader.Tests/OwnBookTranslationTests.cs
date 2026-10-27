using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>A parent's own book keeps its translation inside the book (Slice 159, Decision 164).</summary>
public class OwnBookTranslationTests
{
    private static Story Book(string text = "The cat sat. The dog ran.", string title = "My Book") =>
        Story.NewOwnBook("0123456789abcdef0123456789abcdef", "en", DateTimeOffset.UtcNow, 0) with
        {
            Title = title,
            Pages = [new StoryPage(text)],
        };

    [Fact]
    public void Packs_are_both_languages_and_english_when_neither_is_english()
    {
        Assert.Equal(["en", "fa"], OwnBookTranslation.PackLanguages("en", "fa"));
        Assert.Equal(["fa", "de", "en"], OwnBookTranslation.PackLanguages("fa", "de"));
    }

    [Fact]
    public void A_book_with_no_table_misses_its_pieces_and_its_title()
    {
        IReadOnlyList<string> missing = OwnBookTranslation.Missing(Book(), "fa");

        Assert.Contains("My Book", missing);
        Assert.Contains("The cat sat.", missing);
        Assert.Contains("The dog ran.", missing);
    }

    [Fact]
    public void A_saved_table_makes_the_book_complete_and_gives_the_shelf_its_title()
    {
        Story book = Book();
        var table = OwnBookTranslation.WantedTexts(book).ToDictionary(t => t, t => "fa:" + t);

        Story saved = OwnBookTranslation.WithTable(book, "fa", table);

        Assert.Empty(OwnBookTranslation.Missing(saved, "fa"));
        Assert.True(BundledTranslations.IsComplete(saved, "fa"));
        Assert.Equal("fa:My Book", BundledTranslations.Lookup(saved, "fa", saved.Title));
    }

    [Fact]
    public void Changed_text_misses_the_new_pieces_and_drops_the_old_translation()
    {
        Story book = Book();
        Story saved = OwnBookTranslation.WithTable(book, "fa", OwnBookTranslation.WantedTexts(book).ToDictionary(t => t, t => "fa:" + t));
        Story edited = saved with { Pages = [new StoryPage("The cat sat. The bird flew.")] };

        Assert.Equal(["The bird flew."], OwnBookTranslation.Missing(edited, "fa"));

        Story again = OwnBookTranslation.WithTable(edited, "fa", BundledTranslations.TableFor(edited, "fa")!);
        Assert.DoesNotContain("The dog ran.", BundledTranslations.TableFor(again, "fa")!.Keys);
    }

    [Fact]
    public void Another_language_needs_its_own_table_and_keeps_the_first()
    {
        Story book = Book();
        Story fa = OwnBookTranslation.WithTable(book, "fa", OwnBookTranslation.WantedTexts(book).ToDictionary(t => t, t => "fa:" + t));

        Assert.NotEmpty(OwnBookTranslation.Missing(fa, "de"));

        Story both = OwnBookTranslation.WithTable(fa, "de", OwnBookTranslation.WantedTexts(fa).ToDictionary(t => t, t => "de:" + t));
        Assert.Equal("fa:My Book", BundledTranslations.Lookup(both, "fa", "My Book"));
        Assert.Equal("de:My Book", BundledTranslations.Lookup(both, "de", "My Book"));
    }
}
