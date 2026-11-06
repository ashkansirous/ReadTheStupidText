using DinoReader.Core;
using Xunit;

namespace DinoReader.Tests;

/// <summary>Slice 278 part B: one rule for which languages can be read, shared by the shelf and the reading page.</summary>
public class InstalledShelfAudioTests
{
    private static readonly string[] Languages = ["en", "fa", "de"];

    [Fact]
    public void The_shelf_offers_the_main_language_and_each_language_installed_for_any_book()
    {
        var audio = new InstalledShelfAudio([("fa", "book-1")]);

        Assert.Equal(["en", "fa"], audio.ForShelf(Languages, "en"));
    }

    [Fact]
    public void An_empty_snapshot_offers_only_the_main_language()
    {
        Assert.Equal(["en"], InstalledShelfAudio.Empty.ForShelf(Languages, "en"));
    }

    [Fact]
    public void A_book_is_offered_in_a_language_only_when_that_book_has_it()
    {
        var audio = new InstalledShelfAudio([("fa", "book-1")]);

        Assert.True(audio.OffersBook("fa", "en", "book-1"));
        Assert.False(audio.OffersBook("fa", "en", "book-2"));
        Assert.True(audio.OffersBook("en", "en", "book-2"));
        Assert.True(audio.OffersBook("de", "en", null));
    }

    [Fact]
    public void The_saved_text_round_trips_and_a_damaged_one_is_empty()
    {
        var audio = new InstalledShelfAudio([("fa", "book-1"), ("de", "book-2")]);

        Assert.True(audio.SameAs(InstalledShelfAudio.Parse(audio.Serialize())));
        Assert.True(InstalledShelfAudio.Parse("nonsense\n\t\nfa\t").SameAs(InstalledShelfAudio.Empty));
        Assert.True(InstalledShelfAudio.Parse(null).SameAs(InstalledShelfAudio.Empty));
    }
}
