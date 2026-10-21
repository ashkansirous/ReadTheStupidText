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

    [Theory]
    [InlineData("fr", "Pierre dit : « Où vas-tu ? »\n« Au jardin », répondit Marie.", "Pierre", Gender.Male, "Marie", Gender.Female)]
    [InlineData("es", "—Hola —dijo Pedro.\n—Adiós —dijo María.", "Pedro", Gender.Male, "María", Gender.Female)]
    [InlineData("ru", "— Привет, — сказала Мария.\n— Пока, — сказал Пётр.", "Пётр", Gender.Male, "Мария", Gender.Female)]
    [InlineData("fa", "«سلام»، علی گفت.\n«خداحافظ»، مریم گفت.", "علی", Gender.Male, "مریم", Gender.Female)]
    [InlineData("zh", "“你好，”彼得说。\n“再见，”小红说。", "彼得", Gender.Male, "小红", Gender.Female)]
    [InlineData("ja", "「こんにちは」とピーターは言った。\n「さようなら」とメアリーは言った。", "ピーター", Gender.Male, "メアリー", Gender.Female)]
    public void Names_are_guessed_male_or_female(string language, string text, string boy, Gender boyGender, string girl, Gender girlGender)
    {
        AttributionResult result = Run(language, text);

        Assert.Equal(boyGender, result.Characters.Single(c => c.Id == boy).Gender);
        Assert.Equal(girlGender, result.Characters.Single(c => c.Id == girl).Gender);
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
