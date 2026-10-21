using DinoReader.Core;

namespace DinoReader.Tests;

public class LineBreaksTests
{
    [Theory]
    [InlineData("a\rb", "a\nb")]
    [InlineData("a\r\nb", "a\nb")]
    [InlineData("a\nb\r\rc", "a\nb\n\nc")]
    [InlineData(null, "")]
    public void ToLf_NormalisesEveryLineBreakForm(string? input, string expected) =>
        Assert.Equal(expected, LineBreaks.ToLf(input));
}
