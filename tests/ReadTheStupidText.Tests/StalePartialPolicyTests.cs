using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile.Shared;

namespace ReadTheStupidText.Tests;

public class StalePartialPolicyTests
{
    private static readonly IReadOnlySet<string> Wanted = new HashSet<string> { "ocr-arabic", "supertonic-3" };

    private static readonly IReadOnlyDictionary<string, string> Versions = new Dictionary<string, string>
    {
        ["ocr-arabic"] = "1", ["piper-fa"] = "2", ["supertonic-3"] = "1.2", ["translation-en-ja"] = "1",
    };

    private static FoundPartial Partial(string name, long length = 1000) => new(name, length);

    [Fact]
    public void A_partial_of_a_wanted_pack_resumes()
    {
        var result = StalePartialPolicy.Decide([Partial("ocr-arabic-1.zip.partial")], Wanted, Versions);

        StalePartialDecision only = Assert.Single(result);
        Assert.Equal("ocr-arabic", only.PackId);
        Assert.Equal(StalePartialAction.Resume, only.Action);
    }

    [Fact]
    public void A_partial_of_a_pack_nothing_wants_is_deleted()
    {
        var result = StalePartialPolicy.Decide([Partial("piper-fa-2.zip.partial")], Wanted, Versions);

        Assert.Equal(StalePartialAction.Delete, Assert.Single(result).Action);
    }

    [Fact]
    public void A_partial_older_than_the_catalogs_version_is_deleted_because_it_can_never_be_resumed()
    {
        var versions = new Dictionary<string, string> { ["ocr-arabic"] = "2" };

        var result = StalePartialPolicy.Decide([Partial("ocr-arabic-1.zip.partial")], Wanted, versions);

        Assert.Equal(StalePartialAction.Delete, Assert.Single(result).Action);
    }

    [Fact]
    public void A_partial_of_the_catalogs_current_version_resumes()
    {
        var versions = new Dictionary<string, string> { ["ocr-arabic"] = "2" };

        var result = StalePartialPolicy.Decide([Partial("ocr-arabic-2.zip.partial")], Wanted, versions);

        Assert.Equal(StalePartialAction.Resume, Assert.Single(result).Action);
    }

    [Fact]
    public void A_partial_newer_than_the_cached_catalog_is_kept_not_resumed_and_not_deleted()
    {
        var versions = new Dictionary<string, string> { ["ocr-arabic"] = "2" };

        var result = StalePartialPolicy.Decide([Partial("ocr-arabic-3.zip.partial")], Wanted, versions);

        Assert.Equal(StalePartialAction.Keep, Assert.Single(result).Action);
    }

    [Fact]
    public void With_no_catalog_every_partial_is_kept()
    {
        var result = StalePartialPolicy.Decide(
            [Partial("ocr-arabic-1.zip.partial"), Partial("piper-fa-2.zip.partial"), Partial("ocr-arabic-1.zip.partial", length: 0)], Wanted, null);

        Assert.All(result, d => Assert.Equal(StalePartialAction.Keep, d.Action));
    }

    [Fact]
    public void A_translation_pack_whose_use_is_unknown_is_kept_even_when_nothing_wants_it()
    {
        IReadOnlySet<string> undetermined = new HashSet<string> { "translation-en-ja" };

        var result = StalePartialPolicy.Decide([Partial("translation-en-ja-1.zip.partial")], Wanted, Versions, undetermined);

        Assert.Equal(StalePartialAction.Keep, Assert.Single(result).Action);
    }

    [Fact]
    public void A_wanted_pack_missing_from_the_catalog_is_kept()
    {
        var result = StalePartialPolicy.Decide([Partial("supertonic-3-1.zip.partial")], Wanted, new Dictionary<string, string>());

        Assert.Equal(StalePartialAction.Keep, Assert.Single(result).Action);
    }

    [Fact]
    public void An_empty_partial_is_deleted_even_for_a_wanted_pack()
    {
        var result = StalePartialPolicy.Decide([Partial("ocr-arabic-1.zip.partial", length: 0)], Wanted, Versions);

        Assert.Equal(StalePartialAction.Delete, Assert.Single(result).Action);
    }

    [Fact]
    public void An_older_partial_next_to_the_current_one_is_a_leftover_and_is_deleted()
    {
        var versions = new Dictionary<string, string> { ["ocr-arabic"] = "2" };

        var result = StalePartialPolicy.Decide([Partial("ocr-arabic-1.zip.partial"), Partial("ocr-arabic-2.zip.partial")], Wanted, versions);

        Assert.Equal([StalePartialAction.Delete, StalePartialAction.Resume], result.Select(d => d.Action));
    }

    [Theory]
    [InlineData("ocr-arabic-1.zip")]            // a finished zip is not a partial
    [InlineData("notes.txt.partial")]           // not a pack file
    [InlineData("-1.zip.partial")]              // no id
    public void Files_that_are_not_pack_partials_are_left_alone(string fileName)
    {
        Assert.Empty(StalePartialPolicy.Decide([Partial(fileName)], Wanted, Versions));
    }

    [Fact]
    public void The_pack_id_keeps_its_own_dashes()
    {
        Assert.True(StalePartialPolicy.TryParse("supertonic-3-1.2.zip.partial", out string id, out string version));
        Assert.Equal("supertonic-3", id);
        Assert.Equal("1.2", version);
    }
}
