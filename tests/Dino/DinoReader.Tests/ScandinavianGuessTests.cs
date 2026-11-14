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

    [Theory]
    [InlineData("Ik denk dat die man het niet weet.")]
    [InlineData("Die jongen was ook met ons naar het huis.")]
    [InlineData("Der er en mand, der ikke ved det.")]
    [InlineData("Jag tror att den där mannen inte vet det.")]
    [InlineData("Jeg tror at den mannen ikke vet det.")]
    public void Short_dutch_danish_and_swedish_samples_are_not_german(string text)
    {
        Assert.NotEqual("de", ScriptLanguageGuesser.Guess(text));
    }

    [Theory]
    [InlineData("Der Hund läuft im Garten und ist nicht müde.")]
    [InlineData("Die Kinder spielen auch mit dem Ball im Park.")]
    [InlineData("Den Fuchs sah ich nicht, aber er ist nur klein.")]
    public void Short_german_sentences_with_der_die_den_are_german(string text)
    {
        Assert.Equal("de", ScriptLanguageGuesser.Guess(text));
    }

    [Fact]
    public void A_clear_german_sentence_is_german()
    {
        Assert.Equal("de", ScriptLanguageGuesser.Guess("Ich habe nicht gesehen, dass der Hund auch mit dem Ball spielt, aber er ist nur müde."));
    }
}
