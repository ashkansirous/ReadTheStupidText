using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Infrastructure.Translation;

namespace ReadTheStupidText.Tests;

public class WindowsTranslationPartsTests
{
    [Theory]
    [InlineData("Это длинное русское предложение о погоде.", "ru")]
    [InlineData("这是一个关于天气的中文句子。", "zh")]
    [InlineData("これは天気についての日本語の文です。", "ja")]
    [InlineData("هذه جملة عربية عن الطقس في المدينة.", "ar")]
    [InlineData("این یک جمله فارسی درباره هوای شهر است که می‌گوید چگونه است.", "fa")]
    [InlineData("The boy said that they were here with his friends and that he had a plan.", "en")]
    [InlineData("Il est dans la maison et elle est avec les enfants pour que tout soit prêt.", "fr")]
    [InlineData("El niño dijo que los amigos estaban en la casa por la tarde y con su madre.", "es")]
    [InlineData("O menino disse que os amigos estavam em casa com a sua mãe e não queria sair.", "pt")]
    public void Detector_recognizes_each_of_the_nine_languages(string text, string expected)
    {
        DetectedLanguage? detected = ScriptLanguageDetector.Detect(text);

        Assert.Equal(expected, TranslationFlow.SourceFromDetection(detected));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234 5678 !!!")]
    [InlineData("zzz qqq xxx")]
    public void Detector_is_unsure_without_letters_or_known_words(string text)
    {
        Assert.Null(ScriptLanguageDetector.Detect(text));
    }

    [Theory]
    [InlineData("Це довге українське речення про погоду, їжу та ґанок.")]
    [InlineData("Ово је дуга српска реченица о времену у ћошку.")]
    [InlineData("یہ موسم کے بارے میں ایک لمبا اردو جملہ ہے جو ٹھیک ہے۔")]
    public void Detector_is_unsure_for_cyrillic_and_arabic_script_languages_it_cannot_name(string text)
    {
        Assert.Null(ScriptLanguageDetector.Detect(text));
    }

    [Fact]
    public void Arabic_script_with_farsi_only_letters_is_farsi()
    {
        DetectedLanguage? detected = ScriptLanguageDetector.Detect("پدر و مادر گفتند که چای را بیاور");

        Assert.Equal("fa", TranslationFlow.SourceFromDetection(detected));
    }

    [Theory]
    [InlineData("fr", "en", new[] { "translation-fr-en" })]
    [InlineData("en", "fa", new[] { "translation-en-fa" })]
    [InlineData("fr", "fa", new[] { "translation-fr-en", "translation-en-fa" })]
    [InlineData("FR", "FA", new[] { "translation-fr-en", "translation-en-fa" })]
    public void A_pair_pivots_through_english_unless_english_is_an_end(string source, string target, string[] packs)
    {
        string[] ids = TranslationPacks.Legs(source, target).Select(l => TranslationPacks.PackId(l.Source, l.Target)).ToArray();

        Assert.Equal(packs, ids);
    }

    [Fact]
    public async Task Pack_translator_is_ready_only_when_every_leg_is_installed()
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var translator = new PackTranslator(() => root);
            InstallSpec(root, "translation-fr-en");

            Assert.False(await translator.IsModelReadyAsync("fr", "ru"));
            Assert.True(await translator.IsModelReadyAsync("fr", "en"));

            InstallSpec(root, "translation-en-ru");
            Assert.True(await translator.IsModelReadyAsync("fr", "ru"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void InstallSpec(string root, string packId)
    {
        Directory.CreateDirectory(Path.Combine(root, packId));
        File.WriteAllText(Path.Combine(root, packId, TranslationPackFiles.Spec), "{}");
    }

    [Fact]
    public async Task Pack_translator_without_its_packs_raises_the_model_unavailable_error()
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var translator = new PackTranslator(() => root);

            await Assert.ThrowsAsync<TranslationModelUnavailableException>(
                () => translator.TranslateAsync("bonjour", "fr", "en", CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Pack_translator_supports_eight_languages_and_no_persian()
    {
        var translator = new PackTranslator(() => string.Empty);

        Assert.Equal(8, translator.SupportedLanguages.Count);
        Assert.DoesNotContain("fa", translator.SupportedLanguages);
    }
}
