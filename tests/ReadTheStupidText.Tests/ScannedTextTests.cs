using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Tests;

public class ScannedTextTests
{
    [Fact]
    public void AppendPages_EmptyExisting_JoinsPagesWithBlankLine()
    {
        Assert.Equal("a\n\nb", ScannedText.AppendPages("", ["a", "b"]));
        Assert.Equal("a", ScannedText.AppendPages(null, ["a"]));
    }

    [Fact]
    public void AppendPages_ExistingText_AppendsBelowWithBlankLine()
    {
        Assert.Equal("old\n\nnew", ScannedText.AppendPages("old  \n", ["new"]));
    }

    [Fact]
    public void AppendPages_SkipsWhitespaceAndNullPages_AndTrims()
    {
        Assert.Equal("a\n\nc", ScannedText.AppendPages("", [" a \n", "   ", null, "\tc"]));
    }

    [Fact]
    public void AppendPages_AllEmpty_ReturnsExistingUnchanged()
    {
        Assert.Equal("keep  ", ScannedText.AppendPages("keep  ", [" ", null, ""]));
        Assert.Equal("", ScannedText.AppendPages(null, [null]));
    }

    [Fact]
    public void AppendPages_PreservesPageOrder()
    {
        Assert.Equal("1\n\n2\n\n3", ScannedText.AppendPages("", ["1", "2", "3"]));
    }

    [Fact]
    public void FailedPageNumbers_ReportsOneBasedNullEntriesOnly()
    {
        Assert.Equal([3, 9], ScannedText.FailedPageNumbers(["a", "", null, "b", "c", "d", "e", "f", null]));
        Assert.Empty(ScannedText.FailedPageNumbers(["a", ""]));
    }

    [Fact]
    public void FormatPageNumbers_JoinsWithCommaSpace()
    {
        Assert.Equal("3, 9", ScannedText.FormatPageNumbers([3, 9]));
    }

    [Fact]
    public void ToLocalPath_PlainPath_PassesThrough()
    {
        Assert.Equal("/data/cache/p1.jpg", ScannedText.ToLocalPath("/data/cache/p1.jpg"));
    }

    [Fact]
    public void ToLocalPath_FileUri_DecodesPercentEncoding()
    {
        string? path = ScannedText.ToLocalPath("file:///data/cache/my%20scan%231.jpg");
        Assert.NotNull(path);
        Assert.EndsWith("my scan#1.jpg", path);
    }

    [Fact]
    public void ToLocalPath_ContentUri_IsNull()
    {
        Assert.Null(ScannedText.ToLocalPath("content://media/external/images/1"));
    }
}
