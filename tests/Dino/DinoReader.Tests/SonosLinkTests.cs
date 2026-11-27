using System.Text.Json;
using DinoReader.Core;
using Xunit;

namespace DinoReader.Tests;

// Slice 438: the pure part of linking a Sonos home to Dino.
public class SonosLinkTests
{
    private const string Code = "0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData("dinoreader://sonos-link?code=0123456789abcdef0123456789abcdef")]
    [InlineData("dinoreader://sonos-link?x=1&code=0123456789abcdef0123456789abcdef")]
    [InlineData("https://dinoreader.sirous.uk/dino/sonos-link.html?code=0123456789abcdef0123456789abcdef")]
    public void CodeOf_ReadsTheCodeFromAnAppLinkOrTheSitePage(string link) => Assert.Equal(Code, SonosLink.CodeOf(link));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a link")]
    [InlineData("dinoreader://sonos-link")]
    [InlineData("dinoreader://sonos-link?code=SHORT")]
    [InlineData("dinoreader://sonos-link?code=0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("dinoreader://sonos-link?code=0123456789abcdef0123456789abcdeg")]
    [InlineData("dinoreader://open-book?code=0123456789abcdef0123456789abcdef")]
    [InlineData("other://sonos-link?code=0123456789abcdef0123456789abcdef")]
    [InlineData("https://evil.example/dino/sonos-link.html?code=0123456789abcdef0123456789abcdef")]
    [InlineData("https://evil.example/sonos-link?code=0123456789abcdef0123456789abcdef")]
    [InlineData("http://dinoreader.sirous.uk/dino/sonos-link.html?code=0123456789abcdef0123456789abcdef")]
    public void CodeOf_RefusesAnythingElse(string? link) => Assert.Null(SonosLink.CodeOf(link));

    [Theory]
    [InlineData("fa", "fa")]
    [InlineData("fa-IR", "fa")]
    [InlineData("pt-BR", "pt")]
    [InlineData("xx", "en")]
    [InlineData(null, "en")]
    public void LanguagesFor_PutsTheReadingLanguageFirstAndKeepsEveryLanguageOnce(string? reading, string expectedFirst)
    {
        IReadOnlyList<string> languages = SonosLink.LanguagesFor(reading);
        Assert.Equal(expectedFirst, languages[0]);
        Assert.Equal(UiLanguage.Supported.Count, languages.Count);
        Assert.Equal(UiLanguage.Supported.OrderBy(c => c), languages.OrderBy(c => c));
    }

    [Fact]
    public void ConfirmBody_CarriesTheOperationCodeAndLanguage_AndTheProofOnlyWhenThereIsOne()
    {
        using JsonDocument plain = JsonDocument.Parse(SonosLink.ConfirmBody(Code, "fa"));
        Assert.Equal("confirm", plain.RootElement.GetProperty("op").GetString());
        Assert.Equal(Code, plain.RootElement.GetProperty("code").GetString());
        Assert.Equal("fa", plain.RootElement.GetProperty("language").GetString());
        Assert.False(plain.RootElement.TryGetProperty("proof", out _));

        using JsonDocument proved = JsonDocument.Parse(SonosLink.ConfirmBody(Code, "fa", "proof-123456"));
        Assert.Equal("proof-123456", proved.RootElement.GetProperty("proof").GetString());
    }

    [Theory]
    [InlineData("{\"callback\":\"sonos://sonos.com/applink?state=S&code=abc\"}", "sonos://sonos.com/applink?state=S&code=abc")]
    [InlineData("{\"callback\":\"sonos-app://x?state=S\"}", "sonos-app://x?state=S")]
    [InlineData("{\"callback\":\"https://play.sonos.com/cb?state=S\"}", "https://play.sonos.com/cb?state=S")]
    public void CallbackOf_ReturnsSonosAddresses(string json, string expected) => Assert.Equal(new Uri(expected), SonosLink.CallbackOf(json));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"callback\":5}")]
    [InlineData("{\"callback\":\"https://evil.example/?state=S\"}")]
    [InlineData("{\"callback\":\"https://sonos.com.evil.example/\"}")]
    [InlineData("{\"callback\":\"http://sonos.com/x\"}")]
    [InlineData("{\"callback\":\"javascript:alert(1)\"}")]
    [InlineData("{\"callback\":\"file:///etc/passwd\"}")]
    [InlineData("{\"callback\":\"relative/path\"}")]
    public void CallbackOf_RefusesAnythingThatIsNotSonos(string? json) => Assert.Null(SonosLink.CallbackOf(json));
}
