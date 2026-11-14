using DinoReader.Core;

namespace DinoReader.Tests;

public class SpeakerAttributorTests
{
    private static readonly LanguageRules Rules = LanguageRules.English;

    private static AttributionResult Run(string text)
    {
        var segments = new StorySegmenter(Rules).Split(text);
        return new SpeakerAttributor(Rules).Attribute(text, segments);
    }

    private static List<string?> Speakers(AttributionResult r) =>
        r.Segments.Where(s => s.Kind == SegmentKind.Quote).Select(s => s.CharacterId).ToList();

    [Fact]
    public void Peter_and_Cotton_Tail_attribute_three_of_three()
    {
        const string text =
            "Peter said, \"Where are you going?\" " +
            "\"To the garden,\" said Cotton Tail. " +
            "\"Come back at once!\" cried Peter.";

        var result = Run(text);

        Assert.Equal(new string?[] { "Peter", "Cotton Tail", "Peter" }, Speakers(result));
        Assert.Equal(Gender.Male, result.Characters.Single(c => c.Id == "Peter").Gender);
        Assert.Equal(Gender.Female, result.Characters.Single(c => c.Id == "Cotton Tail").Gender);
    }

    [Fact]
    public void Cotton_Tail_is_Female_when_the_story_uses_she()
    {
        const string text =
            "Cotton Tail hopped over the log. She was in a hurry. " +
            "\"Hurry up,\" said Cotton Tail. \"Why?\" asked Peter.";

        var result = Run(text);

        Assert.Equal(Gender.Female, result.Characters.Single(c => c.Id == "Cotton Tail").Gender);
    }

    [Fact]
    public void Pronoun_tag_resolves_to_the_last_character_of_that_gender()
    {
        const string text =
            "\"Hello,\" said Peter. \"Hi,\" said Lucy. \"Nice day,\" she said. \"Very,\" he said.";

        Assert.Equal(new string?[] { "Peter", "Lucy", "Lucy", "Peter" }, Speakers(Run(text)));
    }

    [Fact]
    public void Tag_before_the_quote_is_read() =>
        Assert.Equal(new string?[] { "Lucy" }, Speakers(Run("Lucy whispered, \"Be quiet.\"")));

    [Fact]
    public void Name_then_verb_with_an_adverb_is_read() =>
        Assert.Equal(new string?[] { "Tom" }, Speakers(Run("\"Go!\" Tom shouted loudly.")));

    [Fact]
    public void Title_names_keep_their_full_name() =>
        Assert.Equal(new string?[] { "Mr. Brown" }, Speakers(Run("\"Stop!\" said Mr. Brown.")));

    [Fact]
    public void Leading_filler_words_are_not_part_of_the_name() =>
        Assert.Equal(new string?[] { "Peter" }, Speakers(Run("Then Peter said, \"Run.\"")));

    [Fact]
    public void Untagged_back_and_forth_alternates_speakers()
    {
        const string text =
            "\"Hello,\" said Peter. \"Hi,\" said Lucy.\n\n\"How are you?\"\n\n\"Fine.\"\n\n\"Good.\"";

        Assert.Equal(
            new string?[] { "Peter", "Lucy", "Peter", "Lucy", "Peter" },
            Speakers(Run(text)));
    }

    [Fact]
    public void Unattributable_quote_keeps_a_null_speaker()
    {
        var result = Run("\"Who goes there?\"");

        Assert.Equal(new string?[] { null }, Speakers(result));
        Assert.Empty(result.Characters);
    }

    [Fact]
    public void Continued_paragraph_keeps_the_same_speaker()
    {
        const string text = "Peter said, \"First part.\n\n\"Second part,\" he finished.";

        Assert.Equal(new string?[] { "Peter", "Peter" }, Speakers(Run(text)));
    }

    [Fact]
    public void Characters_come_in_first_appearance_order_with_word_counts()
    {
        const string text = "\"One two three,\" said Tom. \"Four five,\" said Lucy. \"Six,\" said Tom.";

        var characters = Run(text).Characters;

        Assert.Equal(new[] { "Tom", "Lucy" }, characters.Select(c => c.Id));
        Assert.Equal(new[] { 4, 2 }, characters.Select(c => c.WordCount));
    }

    [Fact]
    public void Narration_segments_carry_no_speaker()
    {
        var result = Run("Peter said, \"Hi.\"");

        Assert.All(
            result.Segments.Where(s => s.Kind == SegmentKind.Narration),
            s => Assert.Null(s.CharacterId));
    }

    [Fact]
    public void Tag_before_wins_over_a_following_sentence_with_another_name() =>
        Assert.Equal(
            new string?[] { "Peter" },
            Speakers(Run("Peter asked, \"Where is it?\" Lucy said nothing.")));

    [Fact]
    public void Tag_before_wins_over_a_following_unrelated_name() =>
        Assert.Equal(new string?[] { "Lucy" }, Speakers(Run("Lucy said, \"Hi.\" Peter laughed.")));

    [Fact]
    public void Untagged_quote_in_the_same_paragraph_keeps_the_previous_speaker() =>
        Assert.Equal(
            new string?[] { "Lucy", "Peter", "Peter" },
            Speakers(Run("\"Hi,\" said Lucy. \"Hello,\" said Peter. \"How are you today?\"")));

    [Fact]
    public void Uncorroborated_sentence_initial_word_is_not_a_character()
    {
        var result = Run("\"Hello,\" said Lucy. Peter fell. Running away, he laughed. \"Ouch,\" he said.");

        Assert.Equal(new string?[] { "Lucy", "Peter" }, Speakers(result));
        Assert.Equal(Gender.Male, result.Characters.Single(c => c.Id == "Peter").Gender);
        Assert.DoesNotContain(result.Characters, c => c.Id == "Running");
    }

    [Fact]
    public void Missing_close_quote_does_not_swallow_the_next_paragraph()
    {
        var result = Run("Peter said, \"Where are you going\n\nLucy ran off. \"Home,\" said Lucy.");

        var quotes = result.Segments.Where(s => s.Kind == SegmentKind.Quote).ToList();
        Assert.Equal(new[] { "Where are you going", "Home," }, quotes.Select(q => q.Text));
        Assert.Equal(new string?[] { "Peter", "Lucy" }, quotes.Select(q => q.CharacterId));
        Assert.False(quotes[0].IsClosed);
        Assert.Contains(result.Segments, s => s.Kind == SegmentKind.Narration && s.Text == "Lucy ran off.");
    }

    [Fact]
    public void Pronoun_tag_ignores_a_stray_capitalised_word()
    {
        const string text = "\"Hi,\" said Lucy. The Gruffalo roared. He was hungry. \"Help,\" she said.";

        Assert.Equal(new string?[] { "Lucy", "Lucy" }, Speakers(Run(text)));
    }

    [Fact]
    public void Sneezed_tag_after_a_quote_gives_the_quote_to_the_sneezer()
    {
        const string text = "\"Hi,\" said Pop. Bubbles went up his nose. \"Achoo! Achoo! Achoo!\" sneezed Wolf.";

        Assert.Equal(new string?[] { "Pop", "Wolf" }, Speakers(Run(text)));
    }

    [Fact]
    public void Barked_tag_before_a_quote_gives_the_quote_to_the_barker()
    {
        const string text = "\"Hi,\" said Pop. Rex barked, \"Woof! Woof!\"";

        Assert.Equal(new string?[] { "Pop", "Rex" }, Speakers(Run(text)));
    }

    [Fact]
    public void Narration_after_a_page_break_does_not_tag_the_earlier_pages_quote()
    {
        const string text =
            "\"Please, please let me go, Mister Lion,\" said Mouse. \"I didn't mean to wake you up. If you're kind to me today, I'll be kind to you one day, too!\"\n\n" +
            "The Lion laughed so hard that his tummy wobbled. \"Ha, ha, ha!\" said Lion.";

        Assert.Equal(new string?[] { "Mouse", "Mouse", "Lion" }, Speakers(Run(text)));
    }

    [Fact]
    public void Narration_after_a_page_break_does_not_tag_the_earlier_pages_quote_in_Farsi()
    {
        var rules = LanguageRules.For("fa");
        const string text =
            "«خواهش می‌کنم، آقا شیره،» موش گفت. «نمی‌خواستم بیدارت کنم.»\n\n" +
            "شیر خندید. «ها ها ها!» شیر گفت.";
        var result = new SpeakerAttributor(rules).Attribute(text, new StorySegmenter(rules).Split(text));

        var speakers = Speakers(result);
        Assert.Equal(3, speakers.Count);
        Assert.Equal(speakers[0], speakers[1]);
        Assert.NotEqual(speakers[1], speakers[2]);
        Assert.Contains("موش", speakers[1]);
        Assert.Contains("شیر", speakers[2]);
    }

    [Fact]
    public void A_reply_with_its_own_tag_then_more_narration_still_alternates() =>
        Assert.Equal(new string?[] { "Peter", "Lucy", "Lucy" },
            Speakers(Run("\"Hi,\" said Peter. \"Hello!\" Lucy replied. She smiled. \"Fine.\"")));

    [Fact]
    public void A_tag_in_the_next_paragraph_does_not_tag_the_quote_before_the_blank_line()
    {
        const string text = "\"Hi,\" said Peter. \"Wait for me.\"\n\nLucy laughed. \"Ha!\" said Lucy.";

        Assert.Equal(new string?[] { "Peter", "Peter", "Lucy" }, Speakers(Run(text)));
    }

    [Fact]
    public void A_tag_after_a_single_newline_still_tags_the_quote() =>
        Assert.Equal("Lucy", Speakers(Run("\"Hi,\" said Peter. \"Wait.\"\nLucy replied.")).Last());

    [Fact]
    public void A_tag_before_a_blank_line_inside_one_narration_still_counts() =>
        Assert.Equal("Lucy", Speakers(Run("\"Hi,\" said Peter. \"Wait.\" said Lucy.\n\nThe Lion laughed.")).Last());

    [Fact]
    public void A_CRLF_blank_line_is_a_paragraph_break()
    {
        const string text = "\"Hi,\" said Peter. \"Wait for me.\"\r\n\r\nLucy laughed. \"Ha!\" said Lucy.";

        Assert.Equal(new string?[] { "Peter", "Peter", "Lucy" }, Speakers(Run(text)));
    }

    [Fact]
    public void A_whitespace_only_line_is_a_paragraph_break()
    {
        const string text = "\"Hi,\" said Peter. \"Wait for me.\"\n  \t\nLucy laughed. \"Ha!\" said Lucy.";

        Assert.Equal(new string?[] { "Peter", "Peter", "Lucy" }, Speakers(Run(text)));
    }

    [Fact]
    public void A_quote_ending_a_page_with_its_tag_on_the_next_page_gets_no_tag_after()
    {
        const string text = "\"Hi,\" said Peter. \"Wait for me.\"\n\nsaid Lucy.";

        Assert.Equal(new string?[] { "Peter", "Peter" }, Speakers(Run(text)));
    }

    [Fact]
    public void A_bare_next_tag_in_the_same_paragraph_still_tags_its_quote() =>
        Assert.Equal(new string?[] { "Peter", "Lucy" },
            Speakers(Run("\"Hi!\" Peter said. \"Hello!\" Lucy replied. \"Fine.\"")).Take(2));

    [Fact]
    public void Name_followed_by_a_non_speech_verb_is_not_the_tag() =>
        Assert.Equal(new string?[] { "Lucy" }, Speakers(Run("Lucy whispered, \"Be quiet.\" Peter nodded.")));
}
