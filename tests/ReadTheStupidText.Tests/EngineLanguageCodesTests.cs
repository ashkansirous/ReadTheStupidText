using ReadTheStupidText.Application.Translation;

namespace ReadTheStupidText.Tests;

public class EngineLanguageCodesTests
{
    [Theory]
    [InlineData("nb", "no")]
    [InlineData("NB", "no")]
    [InlineData("sv", "sv")]
    [InlineData("nl", "nl")]
    [InlineData("en", "en")]
    public void ToEngine_maps_norwegian_and_leaves_the_rest(string app, string engine)
    {
        Assert.Equal(engine, EngineLanguageCodes.ToEngine(app));
    }

    [Theory]
    [InlineData("no", "nb")]
    [InlineData("da", "da")]
    [InlineData("fi", "fi")]
    public void FromEngine_is_the_inverse(string engine, string app)
    {
        Assert.Equal(app, EngineLanguageCodes.FromEngine(engine));
    }
}
