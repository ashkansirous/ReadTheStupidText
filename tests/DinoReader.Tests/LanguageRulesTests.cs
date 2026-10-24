using DinoReader.Core;

namespace DinoReader.Tests;

public class LanguageRulesTests
{
    [Theory]
    [InlineData("fr", "«")]
    [InlineData("pt-BR", "«")]
    [InlineData("RU", "«")]
    [InlineData("zh", "“")]
    [InlineData("ja", "「")]
    public void For_picks_the_rules_by_the_primary_language_subtag(string language, string opening)
    {
        Assert.Equal(opening[0], LanguageRules.For(language).CurlyOpenQuote);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("en")]
    [InlineData("en-GB")]
    [InlineData("de")]
    public void For_falls_back_to_english(string? language)
    {
        Assert.Same(LanguageRules.English, LanguageRules.For(language));
    }

    [Fact]
    public void Guillemets_delimit_quotes_with_the_inner_spaces_trimmed()
    {
        const string text = "Il dit : « Bonjour tout le monde ! » puis il partit.";
        var segments = new StorySegmenter(LanguageRules.For("fr")).Split(text);

        Assert.Equal(new[] { "Il dit :", "Bonjour tout le monde !", "puis il partit." }, segments.Select(s => s.Text));
        Assert.Equal("« Bonjour tout le monde ! »", text[segments[1].SourceStart..segments[1].SourceEnd]);
    }

    [Fact]
    public void Corner_brackets_delimit_quotes_in_Japanese_and_Chinese()
    {
        Assert.Equal("こんにちは", new StorySegmenter(LanguageRules.For("ja")).Split("「こんにちは」と彼は言った。")[0].Text);
        Assert.Equal("你好", new StorySegmenter(LanguageRules.For("zh")).Split("他说：「你好」")[1].Text);
    }

    [Fact]
    public void A_dash_line_is_a_quote_and_a_tag_between_dashes_is_narration()
    {
        const string text = "— Привет, — сказал Пётр. — Как дела?";
        var segments = new StorySegmenter(LanguageRules.For("ru")).Split(text);

        Assert.Equal(new[] { "Привет,", "сказал Пётр.", "Как дела?" }, segments.Select(s => s.Text));
        Assert.Equal(new[] { SegmentKind.Quote, SegmentKind.Narration, SegmentKind.Quote }, segments.Select(s => s.Kind));
        Assert.All(segments.Where(s => s.Kind == SegmentKind.Quote), q => Assert.True(q.IsClosed));
    }

    [Fact]
    public void Two_sentences_in_one_dash_quote_stay_one_quote()
    {
        var segments = new StorySegmenter(LanguageRules.For("ru")).Split("— Привет! Как дела?");

        Assert.Single(segments);
        Assert.Equal("Привет! Как дела?", segments[0].Text);
    }

    [Fact]
    public void A_dash_inside_a_sentence_of_narration_is_not_dialogue()
    {
        var segments = new StorySegmenter(LanguageRules.For("ru")).Split("Москва — столица России.");

        Assert.Single(segments);
        Assert.Equal(SegmentKind.Narration, segments[0].Kind);
    }

    [Fact]
    public void A_dash_after_a_colon_opens_a_quote()
    {
        var segments = new StorySegmenter(LanguageRules.For("ru")).Split("Он сказал: — Привет!");

        Assert.Equal(new[] { "Он сказал:", "Привет!" }, segments.Select(s => s.Text));
    }

    [Fact]
    public void English_does_not_read_dashes_as_dialogue()
    {
        Assert.Single(new StorySegmenter(LanguageRules.English).Split("— Hello, — said Peter."));
    }

    [Fact]
    public void Chinese_sentences_split_at_the_full_width_marks()
    {
        var sentences = StorySentences.Build("他去了花园。天气很好！你好吗？", LanguageRules.For("zh"));

        Assert.Equal(3, sentences.Count);
    }

    [Fact]
    public void A_dash_tag_stays_in_the_sentence_of_its_quote()
    {
        const string text = "— Привет! — сказал он. — Пока.";
        var sentences = StorySentences.Build(text, LanguageRules.For("ru"));

        Assert.Equal(2, sentences.Count);
        Assert.Equal("— Привет! — сказал он.", text[sentences[0].Start..sentences[0].End]);
    }

    [Fact]
    public void Every_language_has_distinct_rules_with_speech_verbs()
    {
        foreach (string code in UiLanguage.Supported)
        {
            LanguageRules rules = LanguageRules.For(code);

            Assert.NotEmpty(rules.SpeechVerbs);
            Assert.NotEmpty(rules.SentenceTerminators);
            Assert.True(rules.MaleNames.Count > 5 && rules.FemaleNames.Count > 5, code);
        }
    }

    [Fact]
    public void Chinese_and_Japanese_words_are_counted_two_characters_to_a_word()
    {
        var chinese = new SpeakerAttributor(LanguageRules.For("zh"));
        const string text = "“你好吗朋友们，”彼得说。";
        var result = chinese.Attribute(text, new StorySegmenter(LanguageRules.For("zh")).Split(text));

        Assert.Equal(3, result.Characters.Single().WordCount);
    }

    [Theory]
    [InlineData("pt-BR", "pt")]
    [InlineData("fa_IR", "fa")]
    [InlineData("de", "en")]
    [InlineData(null, "en")]
    public void Ui_language_resolves_to_one_of_the_nine(string? language, string expected)
    {
        Assert.Equal(expected, UiLanguage.Resolve(language));
    }

    [Theory]
    [InlineData("fa", true)]
    [InlineData("ar-EG", true)]
    [InlineData("en", false)]
    [InlineData("zh", false)]
    [InlineData(null, false)]
    public void Only_farsi_and_arabic_run_right_to_left(string? language, bool expected)
    {
        Assert.Equal(expected, UiLanguage.IsRightToLeft(language));
    }

    [Theory]
    [InlineData("The boy said that he was tired and they went home with his dog.", "en")]
    [InlineData("Il est très fatigué et elle dit que le chat est dans la maison pour toujours.", "fr")]
    [InlineData("El niño dijo que los perros estaban en el jardín con su madre.", "es")]
    [InlineData("O menino disse que os cães estavam no jardim com a sua mãe, não é.", "pt")]    [InlineData("De man en de vrouw gingen naar de markt en het kind was niet thuis.", "nl")]
    [InlineData("Mannen och kvinnan gick till marknaden och det var inte jag som var hemma.", "sv")]
    [InlineData("Manden og kvinden gik til markedet og det var ikke mig som var hjemme.", "da")]
    [InlineData("Mannen og kvinnen gikk til markedet og det var ikke meg som var hjemme.", "nb")]
    [InlineData("Mies ja nainen menivät torille ja lapsi ei ollut kotona, mutta hän oli.", "fi")]

    [InlineData("Мальчик пошёл в сад и увидел там большую собаку.", "ru")]
    [InlineData("پسرک به باغ رفت و گربه‌ای چاق دید که می‌خندید.", "fa")]
    [InlineData("ذهب الولد إلى الحديقة وشاهد قطة كبيرة هناك.", "ar")]
    [InlineData("小明去了花园，看到一只大猫。", "zh")]
    [InlineData("太郎は庭へ行って、大きな猫を見ました。", "ja")]
    public void The_script_and_common_words_tell_the_language(string text, string expected)
    {
        Assert.Equal(expected, ScriptLanguageGuesser.Guess(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234 !!!")]
    [InlineData("Zxqv plorb")]
    public void An_unclear_text_has_no_guess(string text)
    {
        Assert.Null(ScriptLanguageGuesser.Guess(text));
    }

    [Fact]
    public void A_translate_flag_and_language_survive_the_story_file()
    {
        var story = new Story(StoryIds.New(), "Titre", "fr", DateTimeOffset.UnixEpoch, [new StoryPage("Bonjour")], [],
            CoverStyle.Colour, 0, "supertonic:M1", TranslateToMyLanguage: true);

        Story back = StoryJson.Deserialize(StoryJson.Serialize(story));

        Assert.True(back.TranslateToMyLanguage);
        Assert.Equal("fr", back.Language);
    }

    [Fact]
    public void A_draft_carries_its_language_and_translate_choice_into_the_story()
    {
        var story = new Story(StoryIds.New(), "T", "en", DateTimeOffset.UnixEpoch, [new StoryPage("Hi")], [], CoverStyle.Colour, 0, "v");
        var draft = new StoryDraft(story) { Language = "ru", TranslateToMyLanguage = true };

        Assert.Equal("ru", draft.Current.Language);
        Assert.True(draft.Current.TranslateToMyLanguage);
    }

    [Fact]
    public void A_hyphen_inside_a_word_is_not_a_dialogue_dash()
    {
        var segments = new StorySegmenter(LanguageRules.For("fr")).Split("Sa grand-mère, dit-elle, était partie.");

        Assert.Single(segments);
        Assert.Equal(SegmentKind.Narration, segments[0].Kind);
    }

    [Fact]
    public void A_hyphen_list_item_is_not_a_quote_but_an_em_dash_line_is()
    {
        var rules = LanguageRules.For("fr");
        Assert.All(new StorySegmenter(rules).Split("Il y avait:\n-pain\n-lait"), s => Assert.Equal(SegmentKind.Narration, s.Kind));

        // An ASCII hyphen opens a quote only at the start of a line and before a space.
        Assert.Equal(SegmentKind.Quote, new StorySegmenter(rules).Split("- Bonjour !")[0].Kind);
        Assert.Equal(SegmentKind.Quote, new StorySegmenter(rules).Split("— Bonjour !")[0].Kind);
    }

    [Fact]
    public void Only_dash_characters_are_listed_for_dash_languages()
    {
        foreach (string code in new[] { "fr", "es", "pt", "ru" })
            Assert.False(LanguageRules.For(code).IsDialogueDash('-'));
    }

    [Fact]
    public void Rules_are_shared_per_language()
    {
        Assert.Same(LanguageRules.For("ru"), LanguageRules.For("ru-RU"));
        Assert.Same(LanguageRules.English, LanguageRules.For(null));
    }

    [Fact]
    public void Language_codes_come_from_the_app_language_enum()
    {
        Assert.Equal(Enum.GetValues<ReadTheStupidText.Application.Reading.AppLanguage>().Length, UiLanguage.Supported.Count);
        Assert.True(UiLanguage.IsRightToLeft("fa") && UiLanguage.IsRightToLeft("ar-SA"));
        Assert.False(UiLanguage.IsRightToLeft("fr"));
    }

    [Fact]
    public void A_gendered_verb_before_a_terminator_is_not_a_cue_for_the_next_name()
    {
        var guesser = new GenderGuesser(LanguageRules.For("ru"));

        Assert.Equal(Gender.Female, guesser.Guess("Зорби", "Мария сказала Зорби уйти."));
        Assert.Equal(Gender.Unknown, guesser.Guess("Зорби", "Мария сказала. Зорби тут."));
        Assert.Equal(Gender.Unknown, guesser.Guess("Зорби", "Мария сказала: «Зорби тут»."));
    }

    [Fact]
    public void An_arabic_narration_word_ending_in_ta_marbuta_is_not_a_name()
    {
        StoryNames names = new NameExtractor(LanguageRules.For("ar")).ForStory("ذهبت الفتاة إلى الحديقة. قالت مريم: «مرحبا».");

        Assert.False(names.Accepts("الفتاة"));
        Assert.False(names.Accepts("ذهبت"));
        Assert.True(names.Accepts("مريم"));
    }

    [Fact]
    public void A_farsi_sentence_ending_in_said_does_not_name_its_first_word_unless_known_or_tagged()
    {
        StoryNames names = new NameExtractor(LanguageRules.For("fa")).ForStory("خورشید در آسمان بالا آمد و علی گفت.");

        Assert.False(names.Accepts("خورشید"));
        Assert.False(names.Accepts("آسمان"));
        Assert.True(names.Accepts("علی"));
    }
}
