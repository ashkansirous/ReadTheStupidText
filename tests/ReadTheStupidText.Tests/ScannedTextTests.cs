using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Tests;

public class ScannedTextTests
{
    [Fact]
    public void JoinPages_JoinsPagesWithBlankLine()
    {
        Assert.Equal("a\n\nb", ScannedText.JoinPages(["a", "b"]));
        Assert.Equal("a", ScannedText.JoinPages(["a"]));
    }

    [Fact]
    public void JoinPages_SkipsWhitespaceAndNullPages_AndTrims()
    {
        Assert.Equal("a\n\nc", ScannedText.JoinPages([" a \n", "   ", null, "\tc"]));
    }

    [Fact]
    public void JoinPages_AllEmpty_ReturnsEmpty()
    {
        Assert.Equal("", ScannedText.JoinPages([" ", null, ""]));
    }

    [Fact]
    public void JoinPages_PreservesPageOrder()
    {
        Assert.Equal("1\n\n2\n\n3", ScannedText.JoinPages(["1", "2", "3"]));
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
