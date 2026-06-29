using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Domain.Sanitizing;
using ReadTheStupidText.Infrastructure.Sanitizing;

namespace ReadTheStupidText.Tests;

public class TextSanitizerTests
{
    private static TextSanitizer WithAll() => new(new FakeSettings { EnabledSanitizers = SanitizerCategory.All });

    [Theory]
    [InlineData("www.google.com/sub/sub2/somepage?path=xxx&gen=yaz", "somepage on google.com")]
    [InlineData("Visit https://github.com for code.", "Visit github.com for code.")]
    [InlineData("see http://example.org/docs/intro-guide here", "see intro guide on example.org here")]
    public void Urls_collapse_to_page_on_host(string input, string expected)
    {
        Assert.Equal(expected, WithAll().Sanitize(input));
    }

    [Theory]
    [InlineData("login with password=hunter2 now", "login with a password now")]
    [InlineData("api_key=AKIA12345SECRETVALUE", "a secret token")]
    [InlineData("use Bearer abc.def.ghi please", "use a secret token please")]
    public void Secrets_are_redacted(string input, string expected)
    {
        Assert.Equal(expected, WithAll().Sanitize(input));
    }

    [Fact]
    public void High_entropy_token_becomes_secret()
    {
        Assert.Equal("a secret token", WithAll().Sanitize("aB3xK9zQ7mN2pL5rT8wY1vC4dF6gH0jS"));
    }

    [Fact]
    public void Email_is_redacted()
    {
        Assert.Equal("contact an email address today", WithAll().Sanitize("contact jane.doe@example.com today"));
    }

    [Fact]
    public void Markdown_link_keeps_only_its_text()
    {
        Assert.Equal("see Google here", WithAll().Sanitize("see [Google](https://google.com) here"));
    }

    [Fact]
    public void Emphasis_and_html_are_stripped()
    {
        Assert.Equal("bold and tagged", WithAll().Sanitize("**bold** and <span>tagged</span>"));
    }

    [Fact]
    public void Windows_path_collapses_to_file_name()
    {
        Assert.Equal("opened report.pdf ok", WithAll().Sanitize(@"opened C:\Users\jane\Documents\report.pdf ok"));
    }

    [Fact]
    public void Guid_becomes_identifier()
    {
        Assert.Equal("id an identifier set", WithAll().Sanitize("id 12345678-1234-1234-1234-123456789abc set"));
    }

    [Theory]
    [InlineData("call 5551234567 please", "call a phone number please")]
    [InlineData("card 4111 1111 1111 1111 charged", "card a card number charged")]
    public void Long_numbers_are_classified(string input, string expected)
    {
        Assert.Equal(expected, WithAll().Sanitize(input));
    }

    [Fact]
    public void Plain_prose_is_untouched()
    {
        const string prose = "The quick brown fox jumps over the lazy dog.";
        Assert.Equal(prose, WithAll().Sanitize(prose));
    }

    [Fact]
    public void Disabled_category_leaves_text_alone()
    {
        var sanitizer = new TextSanitizer(new FakeSettings { EnabledSanitizers = SanitizerCategory.All & ~SanitizerCategory.Urls });
        Assert.Contains("https://google.com", sanitizer.Sanitize("go https://google.com now"));
    }

    [Fact]
    public void None_returns_input_verbatim()
    {
        var sanitizer = new TextSanitizer(new FakeSettings { EnabledSanitizers = SanitizerCategory.None });
        const string input = "password=hunter2 https://x.com a@b.com";
        Assert.Equal(input, sanitizer.Sanitize(input));
    }

    private sealed class FakeSettings : ISettingsStore
    {
        public PlaybackRate Speed { get; set; } = PlaybackRate.Default;
        public bool AutoReadOnSelection { get; set; } = true;
        public bool AutoReadOnCopy { get; set; } = true;
        public string? VoiceId { get; set; }
        public SanitizerCategory EnabledSanitizers { get; set; } = SanitizerCategory.All;
    }
}
