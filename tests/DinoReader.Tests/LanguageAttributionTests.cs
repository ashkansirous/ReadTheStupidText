using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>A three-line dialogue in each story language attributes every quote and casts distinct voices (Slice 66).</summary>
public class LanguageAttributionTests
{
    // code, text, first speaker, second speaker (speakers alternate first, second, first).
    public static TheoryData<string, string, string, string> Dialogues => new()
    {
        { "fr", "Pierre dit : « Où vas-tu ? »\n« Au jardin », répondit Marie.\n« Reviens vite ! » cria Pierre.", "Pierre", "Marie" },
        { "es", "—¿Adónde vas? —preguntó Pedro.\n—Al jardín —dijo María.\n—¡Vuelve pronto! —gritó Pedro.", "Pedro", "María" },
        { "pt", "— Onde você vai? — perguntou Pedro.\n— Ao jardim — disse Maria.\n— Volte logo! — gritou Pedro.", "Pedro", "Maria" },
        { "ru", "— Куда ты идёшь? — спросил Пётр.\n— В сад, — ответила Мария.\n— Скорее возвращайся! — крикнул Пётр.", "Пётр", "Мария" },
        { "fa", "پیتر پرسید: «کجا می‌روی؟»\n«به باغ»، مریم گفت.\n«زود برگرد!» پیتر فریاد زد.", "پیتر", "مریم" },
        { "ar", "قال بيتر: «إلى أين تذهب؟»\n«إلى الحديقة»، قالت مريم.\n«عد قريبا!» صاح بيتر.", "بيتر", "مريم" },
        { "zh", "彼得问：“你去哪里？”\n“去花园，”小红说。\n“早点回来！”彼得喊道。", "彼得", "小红" },
        { "ja", "「どこへ行くの？」とピーターは尋ねた。\n「庭へ」とメアリーは答えた。\n「早く帰ってきて！」とピーターは叫んだ。", "ピーター", "メアリー" },
        { "da", "Peter sagde: »Hvor skal du hen?«\n»Ud i haven,« svarede Marie.\n»Kom snart tilbage!« råbte Peter.", "Peter", "Marie" },
        { "fi", "”Minne menet?” kysyi Pekka.\n”Puutarhaan”, vastasi Liisa.\n”Tule pian takaisin!” huusi Pekka.", "Pekka", "Liisa" },
        { "sv", "Peter sa: ”Vart ska du?”\n”Ut i trädgården”, svarade Maria.\n”Kom snart tillbaka!” ropade Peter.", "Peter", "Maria" },
        { "nb", "Peter sa: «Hvor skal du?»\n«Ut i hagen,» svarte Maria.\n«Kom snart tilbake!» ropte Peter.", "Peter", "Maria" },
        { "nl", "Peter zei: “Waar ga je heen?”\n“Naar de tuin,” antwoordde Maria.\n“Kom snel terug!” riep Peter.", "Peter", "Maria" },
    };

    private static AttributionResult Run(string language, string text)
    {
        var rules = LanguageRules.For(language);
        return new SpeakerAttributor(rules).Attribute(text, new StorySegmenter(rules).Split(text));
    }

    [Theory]
    [MemberData(nameof(Dialogues))]
    public void Every_quote_of_a_three_line_dialogue_is_attributed(string language, string text, string first, string second)
    {
        AttributionResult result = Run(language, text);

        var speakers = result.Segments.Where(s => s.Kind == SegmentKind.Quote).Select(s => s.CharacterId).ToList();
        Assert.Equal(new string?[] { first, second, first }, speakers);
    }

    [Theory]
    [MemberData(nameof(Dialogues))]
    public void Two_characters_get_two_different_voices(string language, string text, string first, string second)
    {
        AttributionResult result = Run(language, text);
        var voices = Enumerable.Range(0, 10).Select(i => new VoiceOption("v" + i, i < 5 ? Gender.Female : Gender.Male)).ToList();

        VoiceCast cast = new VoiceCaster().Cast(result.Characters, voices, "v9");

        Assert.NotEqual(cast.VoiceFor(first), cast.VoiceFor(second));
        Assert.All(new[] { first, second }, name => Assert.NotNull(cast.VoiceFor(name)));
    }

    // Dash dialogue with the tag after the dash line, no second dash (Slice 105); the comma before the tag and a ? before it both end the quote.
    [Theory]
    [InlineData("fi", "– Hei, sanoi Pekka.\n– Minne menet? kysyi Liisa.\n– Puutarhaan, vastasi Pekka.")]
    [InlineData("sv", "– Hej, sa Peter.\n– Vart ska du? frågade Maria.\n– Till trädgården, svarade Peter.")]
    [InlineData("da", "– Hej, sagde Peter.\n– Hvor skal du hen? spurgte Marie.\n– Ud i haven, svarede Peter.")]
    [InlineData("nb", "– Hei, sa Peter.\n– Hvor skal du? spurte Maria.\n– Ut i hagen, svarte Peter.")]
    [InlineData("nl", "– Hoi, zei Peter.\n– Waar ga je heen? vroeg Maria.\n– Naar de tuin, antwoordde Peter.")]
    public void Dash_lines_with_a_trailing_tag_are_attributed(string language, string text)
    {
        AttributionResult result = Run(language, text);

        var quotes = result.Segments.Where(s => s.Kind == SegmentKind.Quote).ToList();
        Assert.Equal(3, quotes.Count);
        Assert.All(quotes, q => Assert.DoesNotContain(",", q.Text.Split(' ').Last()));
        Assert.Equal(quotes[0].CharacterId, quotes[2].CharacterId);
        Assert.NotNull(quotes[0].CharacterId);
        Assert.NotNull(quotes[1].CharacterId);
        Assert.NotEqual(quotes[0].CharacterId, quotes[1].CharacterId);
    }

    [Theory]
    [InlineData("fr", "Pierre dit : « Où vas-tu ? »\n« Au jardin », répondit Marie.", "Pierre", Gender.Male, "Marie", Gender.Female)]
    [InlineData("es", "—Hola —dijo Pedro.\n—Adiós —dijo María.", "Pedro", Gender.Male, "María", Gender.Female)]
    [InlineData("ru", "— Привет, — сказала Мария.\n— Пока, — сказал Пётр.", "Пётр", Gender.Male, "Мария", Gender.Female)]
    [InlineData("fa", "«سلام»، علی گفت.\n«خداحافظ»، مریم گفت.", "علی", Gender.Male, "مریم", Gender.Female)]
    [InlineData("zh", "“你好，”彼得说。\n“再见，”小红说。", "彼得", Gender.Male, "小红", Gender.Female)]
    [InlineData("ja", "「こんにちは」とピーターは言った。\n「さようなら」とメアリーは言った。", "ピーター", Gender.Male, "メアリー", Gender.Female)]
    [InlineData("da", "»Hej,« sagde Peter.\n»Farvel,« sagde Marie.", "Peter", Gender.Male, "Marie", Gender.Female)]
    [InlineData("fi", "”Hei”, sanoi Pekka.\n”Moi”, sanoi Liisa.", "Pekka", Gender.Male, "Liisa", Gender.Female)]
    [InlineData("sv", "”Hej”, sa Peter.\n”Hejdå”, sa Maria.", "Peter", Gender.Male, "Maria", Gender.Female)]
    [InlineData("nb", "«Hei,» sa Peter.\n«Ha det,» sa Maria.", "Peter", Gender.Male, "Maria", Gender.Female)]
    [InlineData("nl", "“Hoi,” zei Peter.\n“Dag,” zei Maria.", "Peter", Gender.Male, "Maria", Gender.Female)]
    public void Names_are_guessed_male_or_female(string language, string text, string boy, Gender boyGender, string girl, Gender girlGender)
    {
        AttributionResult result = Run(language, text);

        Assert.Equal(boyGender, result.Characters.Single(c => c.Id == boy).Gender);
        Assert.Equal(girlGender, result.Characters.Single(c => c.Id == girl).Gender);
    }

    // The verb after a ? or comma opens a question or clause inside the quote, not a tag (first person), or speech goes on after the tag.
    [Theory]
    [InlineData("sv", "– Vad? Sa du något?", "Vad? Sa du något?")]
    [InlineData("nb", "– Hva? Sa du noe?", "Hva? Sa du noe?")]
    [InlineData("da", "– Hvad? Sagde du noget?", "Hvad? Sagde du noget?")]
    [InlineData("nl", "– Wat? Zei je iets?", "Wat? Zei je iets?")]
    [InlineData("sv", "– När jag kom hem, sa jag inget.", "När jag kom hem, sa jag inget.")]
    [InlineData("sv", "– Jag kommer, sa Peter, men jag är sen.", "Jag kommer, sa Peter, men jag är sen.")]
    [InlineData("nl", "– Ik kom, zei Peter, maar ik was laat.", "Ik kom, zei Peter, maar ik was laat.")]
    public void A_speech_verb_that_is_not_a_tag_leaves_the_dash_line_one_quote(string language, string text, string expectedQuote)
    {
        var quotes = Run(language, text).Segments.Where(s => s.Kind == SegmentKind.Quote).ToList();

        Assert.Equal(expectedQuote, Assert.Single(quotes).Text);
    }

    // A first-person tag still ends the quote; the tag stays narration and the line gets no named speaker.
    [Theory]
    [InlineData("sv", "– Nej, sa jag.", "Nej")]
    [InlineData("nl", "– Hoi, zei ik.", "Hoi")]
    [InlineData("da", "– Ja, sagde jeg.", "Ja")]
    [InlineData("nb", "– Nei, sa jeg.", "Nei")]
    [InlineData("fi", "– Ei, sanoi minä.", "Ei")]
    public void A_first_person_tag_ends_the_quote_and_names_no_speaker(string language, string text, string expectedQuote)
    {
        var segments = Run(language, text).Segments;

        Segment quote = Assert.Single(segments, s => s.Kind == SegmentKind.Quote);
        Assert.Equal(expectedQuote, quote.Text);
        Assert.Null(quote.CharacterId);
        Assert.Single(segments, s => s.Kind == SegmentKind.Narration);
    }

    [Theory]
    [InlineData("fi", "– Tulen, sanoi Pekka, mutta olen myöhässä.")]
    [InlineData("da", "– Jeg kommer, sagde Peter, men jeg er sen.")]
    [InlineData("nb", "– Jeg kommer, sa Peter, men jeg er sen.")]
    public void Other_languages_keep_a_tag_with_more_speech_after_it_as_one_quote(string language, string text)
    {
        Assert.Single(Run(language, text).Segments, s => s.Kind == SegmentKind.Quote);
        Assert.DoesNotContain(Run(language, text).Segments, s => s.Kind == SegmentKind.Narration);
    }

    [Fact]
    public void A_tag_before_a_closing_dash_consumes_the_dash_and_leaves_no_stray_narration()
    {
        var segments = Run("sv", "– Hej, sa Peter – och gick.").Segments;

        Assert.DoesNotContain(segments, s => s.Kind == SegmentKind.Narration && s.Text.Contains('–'));
    }

    [Theory]
    [InlineData("sv", "–, sa Peter")]
    [InlineData("sv", "–? sa Peter")]
    public void A_punctuation_mark_right_after_the_dash_makes_no_empty_quote(string language, string text)
    {
        Assert.DoesNotContain(Run(language, text).Segments, s => s.Kind == SegmentKind.Quote && s.Text.Length == 0);
    }

    [Fact]
    public void A_tag_followed_by_a_second_dash_ends_the_first_quote_and_the_next_dash_opens_another()
    {
        var quotes = Run("sv", "– Hej, sa Peter. – Hej själv.").Segments.Where(s => s.Kind == SegmentKind.Quote).ToList();

        Assert.Equal(["Hej", "Hej själv."], quotes.Select(q => q.Text));
    }

    [Fact]
    public void A_tag_quote_keeps_its_text_and_dash_lines_in_other_languages_are_unchanged()
    {
        Assert.Equal("Hej", Run("sv", "– Hej, sa Peter.").Segments.Single(s => s.Kind == SegmentKind.Quote).Text);
        Assert.Equal("Hola, dijo Pedro.", Run("es", "– Hola, dijo Pedro.").Segments.Single(s => s.Kind == SegmentKind.Quote).Text);
    }

    [Fact]
    public void Plural_speech_verbs_and_a_Danish_guillemet_pair_attribute_the_quote()
    {
        Assert.NotNull(Run("nl", "“Kom mee,” riepen Peter.").Segments.Single(s => s.Kind == SegmentKind.Quote).CharacterId);
        Assert.NotNull(Run("fi", "”Tule”, huusivat Pekka.").Segments.Single(s => s.Kind == SegmentKind.Quote).CharacterId);
        Assert.Equal("Peter", Run("da", "«Hej,» sagde Peter.").Segments.Single(s => s.Kind == SegmentKind.Quote).CharacterId);
    }

    [Fact]
    public void Dutch_ze_is_not_taken_as_female_and_ordinary_verbs_are_not_speech_tags()
    {
        Assert.DoesNotContain(Run("nl", "“Kom,” zei ze.").Characters, c => c.Gender == Gender.Female);
        Assert.Null(Run("nl", "“Dag.” Maria legde het boek neer.").Segments.Single(s => s.Kind == SegmentKind.Quote).CharacterId);
    }

    [Fact]
    public void Russian_past_tense_verb_shows_the_gender_of_a_name_that_is_not_listed()
    {
        AttributionResult result = Run("ru", "— Привет, — сказала Зубрилка.\n— Пока, — сказал Бобрик.");

        Assert.Equal(Gender.Female, result.Characters.Single(c => c.Id == "Зубрилка").Gender);
        Assert.Equal(Gender.Male, result.Characters.Single(c => c.Id == "Бобрик").Gender);
    }

    [Fact]
    public void Spanish_name_endings_are_a_last_resort_for_gender()
    {
        AttributionResult result = Run("es", "—Hola —dijo Conejo.\n—Adiós —dijo Ardilla.");

        Assert.Equal(Gender.Male, result.Characters.Single(c => c.Id == "Conejo").Gender);
        Assert.Equal(Gender.Female, result.Characters.Single(c => c.Id == "Ardilla").Gender);
    }

    [Fact]
    public void Arabic_verb_form_shows_the_speaker_gender_for_a_name_that_is_not_listed()
    {
        AttributionResult result = Run("ar", "«مرحبا»، قالت سنجابة.\n«وداعا»، قال أرنوب.");

        Assert.Equal(Gender.Female, result.Characters.Single(c => c.Id == "سنجابة").Gender);
        Assert.Equal(Gender.Male, result.Characters.Single(c => c.Id == "أرنوب").Gender);
    }

    [Fact]
    public void Chinese_and_Japanese_pronoun_tags_resolve_to_the_last_character_of_that_gender()
    {
        Assert.Equal(new string?[] { "彼得", "小红", "小红", "彼得" },
            Speakers(Run("zh", "“你好，”彼得说。“你好，”小红说。“天气真好，”她说。“是啊，”他说。")));
        Assert.Equal(new string?[] { "ピーター", "メアリー", "メアリー", "ピーター" },
            Speakers(Run("ja", "「やあ」とピーターは言った。「こんにちは」とメアリーは言った。「いい天気ね」と彼女は言った。「そうだね」と彼は言った。")));
    }

    [Fact]
    public void A_voice_pack_with_two_voices_shares_them_among_three_characters()
    {
        AttributionResult result = Run("ru", "— Привет, — сказал Пётр.\n— Здравствуй, — сказала Мария.\n— Добрый день, — сказал Иван.");
        var voices = new[] { new VoiceOption("m", Gender.Male), new VoiceOption("f", Gender.Female), new VoiceOption("n", Gender.Male) };

        VoiceCast cast = new VoiceCaster().Cast(result.Characters, voices, "n");

        Assert.Equal(3, cast.Assignments.Count);
        Assert.Contains(cast.Assignments, a => a.IsShared);
    }

    private static List<string?> Speakers(AttributionResult r) =>
        r.Segments.Where(s => s.Kind == SegmentKind.Quote).Select(s => s.CharacterId).ToList();
}
