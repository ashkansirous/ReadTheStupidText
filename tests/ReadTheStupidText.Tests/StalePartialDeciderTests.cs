using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile.Shared;

namespace ReadTheStupidText.Tests;

/// <summary>Slice 225 item 9: the launch scan's choice of which half-finished pack downloads are deleted (DecideAll, Slice 208).</summary>
public class StalePartialDeciderTests
{
    private static PackInfo Pack(string id, PackKind kind, string[] languages, string version = "1") =>
        new(id, languages, kind, version, 1000, "x", "MIT", "https://example.test/" + id);

    // en: the specific English voice wins over the multilingual one (the fewest-language pack is selected); fr and ja are served by the multilingual voice.
    private static readonly PackInfo[] Catalog =
    [
        Pack("piper-en", PackKind.Voice, ["en"]),
        Pack("supertonic-3", PackKind.Voice, ["en", "fr", "ja"]),
        Pack("ocr-arabic", PackKind.Ocr, ["ar"]),
        Pack("ocr-latin", PackKind.Ocr, ["en", "fr"]),
        Pack("translation-en-ja", PackKind.Translation, ["en", "ja"]),
        Pack("kokoro-en", PackKind.Storyteller, ["en"]),
    ];

    private static FoundPartial Partial(string name) => new(name, 1000);

    private static StalePartialAction ActionOf(string partialName, params string[] languages) =>
        Assert.Single(StalePartialDecider.DecideAll([Partial(partialName)], Catalog, languages)).Action;

    [Theory]
    [InlineData("en", "piper-en-1.zip.partial")]
    [InlineData("fr", "supertonic-3-1.zip.partial")]
    [InlineData("ja", "supertonic-3-1.zip.partial")]
    public void A_wanted_packs_partial_is_never_deleted_for_any_wanted_language(string language, string partial)
    {
        Assert.Equal(StalePartialAction.Resume, ActionOf(partial, language));
    }

    [Fact]
    public void A_partial_of_a_pack_wanted_for_one_of_several_languages_is_kept_for_each()
    {
        Assert.Equal(StalePartialAction.Resume, ActionOf("piper-en-1.zip.partial", "fr", "en"));
        Assert.Equal(StalePartialAction.Resume, ActionOf("supertonic-3-1.zip.partial", "ja", "fr"));
    }

    [Fact]
    public void An_unselected_voice_pack_of_a_wanted_language_is_deleted()
    {
        // English is wanted and piper-en is the selected English pack: the multilingual voice is not what the start would pick, so its partial is dead weight.
        Assert.Equal(StalePartialAction.Delete, ActionOf("supertonic-3-1.zip.partial", "en"));
    }

    [Fact]
    public void A_voice_pack_of_a_language_nobody_wants_is_deleted()
    {
        Assert.Equal(StalePartialAction.Delete, ActionOf("piper-en-1.zip.partial", "ja"));
    }

    [Fact]
    public void A_wanted_scan_packs_partial_is_never_deleted()
    {
        Assert.Equal(StalePartialAction.Resume, ActionOf("ocr-latin-1.zip.partial", "en"));
    }

    [Fact]
    public void A_scan_pack_of_a_language_not_asked_for_is_kept_because_a_scanned_page_can_be_in_any_language()
    {
        // The OCR keep rule: remove it and this partial is deleted like a voice pack nobody wants.
        Assert.Equal(StalePartialAction.Keep, ActionOf("ocr-arabic-1.zip.partial", "en"));
    }

    [Fact]
    public void Translation_and_storyteller_partials_are_kept_whatever_the_languages_are()
    {
        Assert.Equal(StalePartialAction.Keep, ActionOf("translation-en-ja-1.zip.partial", "en"));
        Assert.Equal(StalePartialAction.Keep, ActionOf("kokoro-en-1.zip.partial", "fr"));
        Assert.Equal(StalePartialAction.Keep, ActionOf("translation-en-ja-1.zip.partial", "de"));
    }

    [Fact]
    public void A_partial_of_a_pack_missing_from_the_catalog_is_deleted_when_nothing_wants_it()
    {
        Assert.Equal(StalePartialAction.Delete, ActionOf("piper-xx-1.zip.partial", "en"));
    }
}
