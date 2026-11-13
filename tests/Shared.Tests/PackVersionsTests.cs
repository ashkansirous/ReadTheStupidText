using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class PackVersionsTests
{
    [Theory]
    [InlineData("2", "1", true)]
    [InlineData("1.10", "1.9", true)]
    [InlineData("1.0.1", "1", true)]
    [InlineData("1", "1.0", false)]
    [InlineData("1", "1", false)]
    [InlineData("1.9", "1.10", false)]
    [InlineData("1", "1.0.1", false)]
    [InlineData("1.0", "1.0beta", true)]
    public void IsNewer_compares_dotted_segments_numerically(string candidate, string installed, bool expected) =>
        Assert.Equal(expected, PackVersions.IsNewer(candidate, installed));
}
