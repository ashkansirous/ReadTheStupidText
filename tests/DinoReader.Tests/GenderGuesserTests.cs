using DinoReader.Core;

namespace DinoReader.Tests;

public class GenderGuesserTests
{
    private static readonly GenderGuesser Guesser = new(LanguageRules.English);

    [Theory]
    [InlineData("Mr. Brown", Gender.Male)]
    [InlineData("Mrs. Rabbit", Gender.Female)]
    [InlineData("Queen Anna", Gender.Female)]
    [InlineData("King Zog", Gender.Male)]
    [InlineData("Miss Moppet", Gender.Female)]
    public void Titles_decide_gender(string name, Gender expected) =>
        Assert.Equal(expected, Guesser.Guess(name, ""));

    [Theory]
    [InlineData("Peter", Gender.Male)]
    [InlineData("Lucy", Gender.Female)]
    public void Known_first_names_decide_gender(string name, Gender expected) =>
        Assert.Equal(expected, Guesser.Guess(name, ""));

    [Fact]
    public void Pronouns_after_the_name_decide_gender()
    {
        const string text = "Cotton Tail hopped over the log. She was in a hurry.";

        Assert.Equal(Gender.Female, Guesser.Guess("Cotton Tail", text));
    }

    [Fact]
    public void Pronouns_after_a_different_name_do_not_count()
    {
        const string text = "Zorp saw Mary. She smiled.";

        Assert.Equal(Gender.Unknown, Guesser.Guess("Zorp", text));
    }

    [Fact]
    public void Unknown_stays_unknown() =>
        Assert.Equal(Gender.Unknown, Guesser.Guess("Gruffalo", "The Gruffalo roared."));

    [Fact]
    public void Sentence_initial_adverb_does_not_cut_the_pronoun_window() =>
        Assert.Equal(Gender.Female, Guesser.Guess("Zorp", "Zorp hopped. Quickly she ran."));

    [Fact]
    public void Multi_word_names_look_up_the_compacted_name() =>
        Assert.Equal(Gender.Female, Guesser.Guess("Cotton Tail", "Cotton Tail hopped. Quickly she ran."));
}
