using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class VoiceRoutingTests
{
    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    [InlineData(true, true, null, false)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    public void PhoneVoiceGivesWayOnlyWhenKnownUnableAndPackReady(bool chosen, bool packReady, bool? canRead, bool expected)
    {
        Assert.Equal(expected, VoiceRouting.ShouldLeavePhoneVoice(chosen, packReady, canRead));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    public void PhoneReadsWhenChosenOrNoPackIsReady(bool chosen, bool packReady, bool expected)
    {
        Assert.Equal(expected, VoiceRouting.PhoneReads(chosen, packReady));
    }

    [Theory]
    [InlineData(true, false, false, VoiceRoute.NoVoice)]
    [InlineData(true, false, true, VoiceRoute.Downloading)]
    [InlineData(true, true, true, VoiceRoute.Read)]
    [InlineData(true, null, false, VoiceRoute.Read)]
    [InlineData(false, false, false, VoiceRoute.Read)]
    public void RouteBlocksOnlyAKnownMissingPhoneVoice(bool phoneReads, bool? canRead, bool downloading, VoiceRoute expected)
    {
        Assert.Equal(expected, VoiceRouting.Route(phoneReads, canRead, downloading));
    }

    [Theory]
    [InlineData(false, true, false, false, VoiceRoute.Read)]   // natural pack for the language
    [InlineData(false, false, true, false, VoiceRoute.Read)]   // no pack, the phone speaks it
    [InlineData(false, false, false, false, VoiceRoute.NoVoice)]
    [InlineData(false, false, false, true, VoiceRoute.Downloading)]
    [InlineData(true, true, true, false, VoiceRoute.Read)]     // phone voice kept when it can speak
    [InlineData(true, true, false, false, VoiceRoute.NoVoice)] // phone chosen but cannot: caller moves to natural first
    [InlineData(false, false, null, false, VoiceRoute.Read)]   // unknown phone never blocks
    public void ResolveNeverReadsWithAVoiceForAnotherLanguage(bool chosen, bool natural, bool? phoneCan, bool downloading, VoiceRoute expected)
    {
        Assert.Equal(expected, VoiceRouting.Resolve(chosen, natural, phoneCan, downloading));
    }

    [Theory]
    [InlineData(false, "fa", null, "en", "Hello there", "en")]
    [InlineData(true, "fa", null, "en", "Hello there", "fa")]
    [InlineData(false, "fa", "es", "en", "Hola", "es")]
    [InlineData(false, "fa", "pt-BR", null, "Ola", "pt")]
    [InlineData(false, "fa", "", "en-GB", "Hello there", "en")]
    [InlineData(false, "fa", null, null, "OK", "en")]            // unsure Latin in a Farsi app: English
    [InlineData(false, "fa", null, null, "سلام", "fa")]           // Arabic script, Farsi app: Farsi
    [InlineData(false, "en", null, null, "سلام", "ar")]           // Arabic script, English app: Arabic
    [InlineData(false, "en", null, null, "Привет", "ru")]
    [InlineData(false, "en", null, null, "你好", "zh")]
    [InlineData(false, "es", null, null, "OK", "es")]             // Latin script in a Latin-script app: the app language
    [InlineData(false, "fa", null, null, "12345 http://x.y", "en")] // Latin letters only in a URL
    [InlineData(false, "fa", null, null, "12345 !!!", "fa")]      // no letters: the app language
    [InlineData(false, "en", null, null, "こんにちは", "ja")]       // kana: Japanese
    [InlineData(false, "zh", null, null, "これは日本語です", "ja")]  // kana wins even in a Chinese app
    [InlineData(false, "en", null, null, "漢字", "zh")]            // Han only: Chinese
    [InlineData(false, "ja", null, null, "日本語", "ja")]           // Han only in a Japanese app: Japanese
    [InlineData(false, "en", null, null, "안녕하세요", "ko")]
    [InlineData(false, "ko", null, null, "안녕하세요", "ko")]
    [InlineData(false, "ar", null, null, "Hello world", "en")]    // Latin text in an Arabic app: English
    public void ReadingLanguageIsTheTargetWhenTranslatedElseTheTextsOwn(bool translated, string target, string? choice, string? detected, string text, string expected)
    {
        Assert.Equal(expected, VoiceRouting.ReadingLanguage(translated, target, choice, detected, text));
    }

    [Theory]
    [InlineData(true, "fa", "fa", true)]
    [InlineData(true, "fa", "en", false)]   // the Farsi download says nothing about an English voice
    [InlineData(true, "fa-IR", "fa", true)]
    [InlineData(false, "fa", "fa", false)]
    [InlineData(true, null, "fa", false)]
    public void DownloadingIsTrueOnlyForTheReadingLanguagesOwnPack(bool busy, string? installerLanguage, string reading, bool expected)
    {
        Assert.Equal(expected, VoiceRouting.DownloadingFor(busy, installerLanguage, reading));
    }

    [Theory]
    [InlineData(true, false)]   // the app language never needs a scope: the ordinary neural path
    [InlineData(false, true)]
    public void OnlyAnotherLanguageUsesAScope(bool appLanguage, bool expected)
    {
        Assert.Equal(expected, VoiceRouting.UsesScope(appLanguage));
    }

    [Theory]
    [InlineData(true, true, false, true)]    // iOS: the app language's pack still reads, with the head start
    [InlineData(false, true, false, false)]  // iOS: another language's pack is unusable, the phone voice decides
    [InlineData(false, true, true, true)]    // Android: any installed pack reads
    [InlineData(false, false, true, false)]
    public void NaturalPackUsableNeedsScopedReadsOnlyForAnotherLanguage(bool appLanguage, bool installed, bool scopedReads, bool expected)
    {
        Assert.Equal(expected, VoiceRouting.NaturalPackUsable(appLanguage, installed, scopedReads));
    }
}
