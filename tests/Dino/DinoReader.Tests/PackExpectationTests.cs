using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 360, "Expectation rule after review": the one pure rule for "is a pack coming".
public sealed class PackExpectationTests
{
    private const string Main = "en";

    private static StoreItem Listed(params string[] packLanguages)
    {
        var audio = packLanguages.ToDictionary(l => l, _ => new AudioPackInfo(1, 1000, "https://x/p.zip"));
        return new StoreItem("pigs", "Pigs", "en", 1, 1, 5, 3, null, "https://x/pigs", Audio: audio.Count == 0 ? null : audio);
    }

    [Fact]
    public void A_known_listing_decides_alone_expected_iff_it_lists_a_pack_for_the_language()
    {
        Assert.Equal(PackExpectationKind.Expected, PackExpectation.For(Listed("fa"), false, true, "fa", "en", Main));
        Assert.Equal(PackExpectationKind.NotExpected, PackExpectation.For(Listed("fa"), false, true, "fr", "en", Main));
        Assert.Equal(PackExpectationKind.NotExpected, PackExpectation.For(Listed(), false, true, "fa", "en", Main));
    }

    [Fact]
    public void A_known_listing_overrides_the_fallback_even_for_a_language_that_is_not_the_books_own()
    {
        Assert.Equal(PackExpectationKind.NotExpected, PackExpectation.For(Listed("fa"), false, true, "sv", "en", Main));
    }

    [Fact]
    public void With_no_known_listing_a_store_book_expects_a_pack_in_every_language_but_its_own()
    {
        Assert.Equal(PackExpectationKind.Expected, PackExpectation.For(null, false, true, "fa", "en", Main));
        Assert.Equal(PackExpectationKind.NotExpected, PackExpectation.For(null, false, true, "en", "en", "fa"));
    }

    [Fact]
    public void A_live_listing_read_this_launch_that_leaves_the_store_book_out_expects_no_pack()
    {
        Assert.Equal(PackExpectationKind.NotExpected, PackExpectation.For(null, true, true, "fa", "en", Main));
        Assert.Equal(PackExpectationKind.Expected, PackExpectation.For(Listed("fa"), true, true, "fa", "en", Main));
    }

    [Fact]
    public void With_no_known_listing_an_own_book_expects_none()
    {
        Assert.Equal(PackExpectationKind.NotExpected, PackExpectation.For(null, false, false, "fa", "en", Main));
    }

    [Fact]
    public void With_no_listing_and_an_unknown_own_language_the_main_family_language_is_not_expected()
    {
        Assert.Equal(PackExpectationKind.NotExpected, PackExpectation.For(null, false, true, "fa", null, "fa"));
        Assert.Equal(PackExpectationKind.Expected, PackExpectation.For(null, false, true, "sv", null, "fa"));
    }

    [Fact]
    public void The_books_own_language_is_never_expected_even_when_a_listing_lists_a_pack_for_it()
    {
        Assert.Equal(PackExpectationKind.NotExpected, PackExpectation.For(Listed("en"), false, true, "en", "en", "fa"));
    }

    [Theory]
    [InlineData("fa-IR", "FA")]
    [InlineData("EN", "en-GB")]
    public void Languages_are_compared_by_their_normalised_key(string language, string own)
    {
        Assert.Equal(PackExpectationKind.NotExpected, PackExpectation.For(null, false, true, language, own, Main));
    }
}
