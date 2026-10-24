using DinoReader.Core;

namespace DinoReader.Tests;

public class ScandinavianGuessTests
{
    [Theory]
    [InlineData("Jeg har ikke noe å si, og hva skjer med henne etter at han kom, sa seg selv.", "nb")]
    [InlineData("Jeg har ikke noget at sige, og hvad sker der med hende efter han kom, sagde hun.", "da")]
    [InlineData("Jag har inte något att säga, och vad händer med hennes vän från byn, sa han.", "sv")]
    public void SamplesAreToldApart(string text, string expected)
    {
        Assert.Equal(expected, ScriptLanguageGuesser.Guess(text));
    }
}
