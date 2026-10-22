using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;
using Xunit;

namespace ReadTheStupidText.Tests;

public class PageLanguagePromptTests
{
    private static PackInfo Ocr(string id, params string[] languages) =>
        new(id, languages, PackKind.Ocr, "1", 1, "x", "MIT", "u");

    private static readonly PackInfo[] Catalog =
    [
        Ocr("ocr-latin", "en", "fr"),
        Ocr("ocr-fa", "fa"),
        Ocr("ocr-ar", "ar"),
        Ocr("ocr-ru", "ru"),
        new("voice-fa", ["fa"], PackKind.Voice, "1", 1, "x", "MIT", "u"),
    ];

    private static InstalledVoicePack Installed(string id, string language) =>
        new(id, new PackManifest(id, PackKind.Ocr, language, "1", "paddle", []));

    [Fact]
    public void No_pending_page_asks_nothing()
    {
        Assert.Empty(PageLanguagePrompt.Offer([OcrSource.MlKit, OcrSource.PaddleOcr], null, Catalog, []));
        Assert.False(PageLanguagePrompt.NeedsPack([OcrSource.PlatformFallback]));
    }

    [Fact]
    public void A_pending_page_offers_the_non_latin_catalog_languages_in_order()
    {
        Assert.Equal(["fa", "ar", "ru"], PageLanguagePrompt.Offer([OcrSource.MlKit, OcrSource.MlKitPackPending], null, Catalog, []));
    }

    [Fact]
    public void Installed_languages_are_not_offered()
    {
        Assert.Equal(["fa", "ru"], PageLanguagePrompt.Offer([OcrSource.MlKitPackPending], null, Catalog, [Installed("ocr-ar", "ar")]));
    }

    [Fact]
    public void Nothing_left_to_download_asks_nothing()
    {
        InstalledVoicePack[] all = [Installed("a", "fa"), Installed("b", "ar"), Installed("c", "ru")];
        Assert.Empty(PageLanguagePrompt.Offer([OcrSource.MlKitPackPending], null, Catalog, all));
    }

    private static ScriptShare Share(TextScript script, int letters) =>
        new(new Dictionary<TextScript, int> { [script] = letters });

    [Fact]
    public void Languages_of_the_pages_script_come_first()
    {
        Assert.Equal(["ru", "fa", "ar"], PageLanguagePrompt.Offer([OcrSource.MlKitPackPending], TextScript.Cyrillic, Catalog, []));
        Assert.Equal(["fa", "ar", "ru"], PageLanguagePrompt.Offer([OcrSource.MlKitPackPending], TextScript.Arabic, Catalog, []));
    }

    [Fact]
    public void Catalog_languages_outside_the_script_table_are_not_offered()
    {
        PackInfo[] catalog = [Ocr("ocr-ur", "ur"), Ocr("ocr-tr", "tr"), Ocr("ocr-ar", "ar")];
        Assert.Equal(["ar"], PageLanguagePrompt.Offer([OcrSource.MlKitPackPending], TextScript.Arabic, catalog, []));
    }

    [Fact]
    public void Page_script_is_the_non_latin_script_with_most_letters()
    {
        Assert.Equal(TextScript.Arabic, PageLanguagePrompt.PageScript([Share(TextScript.Latin, 50), Share(TextScript.Arabic, 3), Share(TextScript.Cyrillic, 2)]));
        Assert.Null(PageLanguagePrompt.PageScript([Share(TextScript.Latin, 50), ScriptShare.Empty]));
    }

    [Fact]
    public void A_failed_catalog_needs_a_connection_and_an_empty_offer_proceeds()
    {
        Assert.Equal(PageLanguageStep.NeedsConnection, PageLanguagePrompt.Decide(new PageLanguageOffer(PackDownloadResult.Offline, [])));
        Assert.Equal(PageLanguageStep.Proceed, PageLanguagePrompt.Decide(new PageLanguageOffer(PackDownloadResult.Ok, [])));
        Assert.Equal(PageLanguageStep.AskLanguage, PageLanguagePrompt.Decide(new PageLanguageOffer(PackDownloadResult.Ok, ["fa"])));
    }

    [Fact]
    public void A_page_still_pending_after_the_download_is_not_resolved()
    {
        Assert.False(PageLanguagePrompt.IsResolved(OcrSource.MlKitPackPending));
        Assert.True(PageLanguagePrompt.IsResolved(OcrSource.PaddleOcr));
        Assert.True(PageLanguagePrompt.IsResolved(OcrSource.MlKit));
    }

    [Fact]
    public void Retry_is_capped_and_never_offered_for_an_invalid_pack()
    {
        Assert.Equal(PackFailureKind.Offline, PagePackRetry.Classify(PackDownloadResult.Offline));
        Assert.Equal(PackFailureKind.Retryable, PagePackRetry.Classify(PackDownloadResult.HashMismatch));
        Assert.True(PagePackRetry.OffersRetry(PackDownloadResult.Failed, 1));
        Assert.False(PagePackRetry.OffersRetry(PackDownloadResult.Failed, PagePackRetry.MaxAttempts));
        Assert.False(PagePackRetry.OffersRetry(PackDownloadResult.InvalidPack, 1));
        Assert.Equal(PackFailureKind.Permanent, PagePackRetry.Classify(PackDownloadResult.InvalidPack));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("fr")]
    [InlineData("es")]
    [InlineData("pt")]
    [InlineData("ru")]
    [InlineData("ar")]
    [InlineData("fa")]
    [InlineData("zh")]
    [InlineData("ja")]
    public void Every_language_of_a_released_ocr_pack_has_a_known_script(string language)
    {
        Assert.NotEqual(TextScript.Unknown, ScriptDetector.ScriptOf(language));
    }
}
