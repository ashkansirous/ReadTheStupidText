using DinoReader.Core;

namespace DinoReader.Tests;

public class ReviewFixTests
{
    private static readonly LanguageRules Rules = LanguageRules.English;

    private static AttributionResult Run(string text)
    {
        var segments = new StorySegmenter(Rules).Split(text);
        return new SpeakerAttributor(Rules).Attribute(text, segments);
    }

    private static List<string?> Speakers(AttributionResult r) =>
        r.Segments.Where(s => s.Kind == SegmentKind.Quote).Select(s => s.CharacterId).ToList();

    private static List<string> Names(string text) =>
        new NameExtractor(Rules).Find(text).Select(n => n.Name).ToList();

    [Theory]
    [InlineData("forest. Peter ran.")]
    [InlineData("last. Peter ran.")]
    [InlineData("items. Peter ran.")]
    [InlineData("against. Peter ran.")]
    public void Name_is_never_prefixed_with_the_tail_of_the_previous_word(string text) =>
        Assert.Equal(new[] { "Peter" }, Names(text));

    [Fact]
    public void A_real_title_abbreviation_still_joins_the_name() =>
        Assert.Equal(new[] { "St. Peter" }, Names("He saw St. Peter run."));

    [Fact]
    public void Unfinished_lead_in_names_the_next_quote_not_the_previous()
    {
        var speakers = Speakers(Run("\"Hello.\" Cotton Tail said, \"Hi there.\""));

        Assert.NotEqual("Cotton Tail", speakers[0]);
        Assert.Equal("Cotton Tail", speakers[1]);
    }

    [Fact]
    public void Tag_after_a_comma_ended_quote_that_continues_still_names_both()
    {
        var speakers = Speakers(Run("\"Hello,\" said Peter, \"how are you?\""));

        Assert.Equal(new string?[] { "Peter", "Peter" }, speakers);
    }

    [Fact]
    public void Lowercase_tag_after_an_exclamation_keeps_both_quotes_with_the_speaker()
    {
        var speakers = Speakers(Run("\"Hello!\" cried Peter, \"come here.\""));

        Assert.Equal(new string?[] { "Peter", "Peter" }, speakers);
    }

    [Theory]
    [InlineData("\"Hurray!\" Everyone cheered.")]
    [InlineData("\"No.\" Nobody answered.")]
    [InlineData("\"Oh!\" No one answered.")]
    [InlineData("\"Good.\" They cheered.")]
    public void Indefinite_pronoun_tag_creates_no_character(string text)
    {
        var result = Run(text);

        Assert.Equal(new string?[] { null }, Speakers(result));
        Assert.Empty(result.Characters);
    }

    [Fact]
    public void Indefinite_pronoun_is_not_remembered_as_a_known_name()
    {
        var result = Run("\"Hurray!\" Everyone cheered. Everyone ran. \"Wait,\" said Peter.");

        Assert.DoesNotContain(result.Characters, c => c.Id.Contains("Everyone"));
    }

    [Fact]
    public void Other_character_as_subject_of_narration_takes_the_next_quote()
    {
        var speakers = Speakers(Run("\"Come here,\" said Peter. Cotton Tail hopped over. \"Why?\""));

        Assert.Equal(new string?[] { "Peter", "Cotton Tail" }, speakers);
    }

    [Fact]
    public void Same_character_as_subject_keeps_the_inherited_speaker()
    {
        var speakers = Speakers(Run("\"Come here,\" said Peter. Peter waved. \"Why not?\""));

        Assert.Equal(new string?[] { "Peter", "Peter" }, speakers);
    }

    [Fact]
    public void Narration_without_another_subject_keeps_the_inherited_speaker()
    {
        var speakers = Speakers(Run("\"Come here,\" said Peter. The sun was warm. \"Why?\""));

        Assert.Equal(new string?[] { "Peter", "Peter" }, speakers);
    }

    [Fact]
    public void Single_word_tag_merges_into_the_one_longer_name()
    {
        var result = Run("\"Hi,\" said Peter. \"Hello,\" said Peter Rabbit. \"Bye,\" said Lucy.");

        Assert.Equal(new string?[] { "Peter Rabbit", "Peter Rabbit", "Lucy" }, Speakers(result));
        Assert.Equal(new[] { "Peter Rabbit", "Lucy" }, result.Characters.Select(c => c.Id));
    }

    [Fact]
    public void Single_word_matching_the_last_word_of_the_one_longer_name_merges()
    {
        var result = Run("\"Hi,\" said Rabbit. \"Hello,\" said Peter Rabbit.");

        Assert.Equal(new string?[] { "Peter Rabbit", "Peter Rabbit" }, Speakers(result));
    }

    [Fact]
    public void Ambiguous_single_word_stays_separate()
    {
        var result = Run("\"A,\" said Peter. \"B,\" said Peter Rabbit. \"C,\" said Peter Pan.");

        Assert.Equal(new string?[] { "Peter", "Peter Rabbit", "Peter Pan" }, Speakers(result));
    }

    [Fact]
    public void A_wrapped_line_starting_with_a_name_is_mid_sentence()
    {
        var story = new NameExtractor(Rules).ForStory("She told\nZorp to wait.");

        Assert.True(story.Accepts("Zorp"));
    }

    [Fact]
    public void A_blank_line_starts_a_sentence()
    {
        var story = new NameExtractor(Rules).ForStory("She sat.\n\nZorp ran.");

        Assert.False(story.Accepts("Zorp"));
    }

    [Fact]
    public void Terminal_punctuation_before_a_line_feed_starts_a_sentence()
    {
        var story = new NameExtractor(Rules).ForStory("It ended.\nZorp sat.");

        Assert.False(story.Accepts("Zorp"));
    }

    [Fact]
    public void Name_positions_are_found_by_range_and_by_first_from()
    {
        var story = new NameExtractor(Rules).ForStory("Cotton Tail ran. Then Big Bear sat. Cotton Tail left.");

        Assert.Equal(new[] { "Cotton Tail" }, story.InRange(0, 16).Select(n => n.Name));
        Assert.Equal(2, story.InRange(0, 60).Count(n => n.Name == "Cotton Tail"));
        Assert.Equal("Big Bear", story.FirstFrom(16, 40)?.Name);
        Assert.Null(story.FirstFrom(30, 34));
    }
}
