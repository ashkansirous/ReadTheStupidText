using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 267: audio packs follow only the family's languages, never the language the book was written in.
public sealed class AudioPackDueTests
{
    private static StoreItem Book(string language, params string[] packs) =>
        new("a", "A", language, 1, 1, 5, 3, null, "https://x/a", Audio: packs.ToDictionary(p => p, p => new AudioPackInfo(1, 1000, $"https://x/a-{p}.zip")));

    [Fact]
    public void A_french_and_farsi_family_asks_for_exactly_french_and_farsi_for_an_english_book()
    {
        IReadOnlyList<(string Language, AudioPackInfo Pack)> due = AudioPackDue.For(Book("en", "en", "fr", "fa", "sv"), ["fr", "fa"]);

        Assert.Equal(["fr", "fa"], due.Select(d => d.Language));
    }

    [Fact]
    public void A_language_the_book_has_no_pack_for_is_left_out_and_repeats_are_asked_once()
    {
        IReadOnlyList<(string Language, AudioPackInfo Pack)> due = AudioPackDue.For(Book("en", "fa"), ["fa", "fr", "fa"]);

        Assert.Equal(["fa"], due.Select(d => d.Language));
    }

    [Fact]
    public void A_book_written_in_a_family_language_gets_that_pack_once_as_any_other()
    {
        Assert.Equal(["en", "fa"], AudioPackDue.For(Book("en", "en", "fa"), ["en", "fa"]).Select(d => d.Language));
    }
}
