using DinoReader.Core;

namespace DinoReader.Tests;

public class BookMatchTests
{
    private static Story Book(string id, string title, string? sourceId, params string[] pages) =>
        new(id, title, "en", DateTimeOffset.UnixEpoch, pages.Select(p => new StoryPage(p)).ToList(), [], CoverStyle.Colour, 0, "F1", SourceId: sourceId);

    [Fact]
    public void Find_ByStoryIdOrSourceId_IgnoringCase()
    {
        var a = Book("id-a", "Alpha", "pkg-a", "One.");
        var b = Book("id-b", "Beta", "pkg-b", "Two.");

        Assert.Same(b, BookMatch.Find([a, b], "ID-B"));
        Assert.Same(a, BookMatch.Find([a, b], " pkg-a "));
    }

    [Fact]
    public void Find_FallsBackToTheTitleSlug()
    {
        var a = Book("id-a", "The Red Hen", null, "One.");
        var b = Book("id-b", "Little Fox", null, "Two.");

        Assert.Same(a, BookMatch.Find([b, a], "red-hen"));
        Assert.Same(b, BookMatch.Find([b, a], "little fox"));
    }

    [Fact]
    public void Find_PrefersAnIdMatchOverATitleMatch()
    {
        var byTitle = Book("x", "Id B", null, "One.");
        var byId = Book("id-b", "Other", null, "Two.");

        Assert.Same(byId, BookMatch.Find([byTitle, byId], "id-b"));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("")]
    [InlineData("   ")]
    public void Find_ReturnsNullWhenNothingMatches(string id)
    {
        Assert.Null(BookMatch.Find([Book("id-a", "Alpha", null, "One.")], id));
    }

    [Fact]
    public void SentencesOfPage_FirstAndLastPage_CoverTheirOwnSentences()
    {
        var story = Book("id", "T", null, "One. Two.", "Three. Four. Five.");

        Assert.Equal((0, 1), BookMatch.SentencesOfPage(story, 0));
        Assert.Equal((2, 4), BookMatch.SentencesOfPage(story, 1));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(99)]
    public void SentencesOfPage_OutOfRange_IsNull(int page)
    {
        var story = Book("id", "T", null, "One.", "Two.");

        Assert.Null(BookMatch.SentencesOfPage(story, page));
    }
}
