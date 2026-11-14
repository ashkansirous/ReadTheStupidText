using DinoReader.Core;

namespace DinoReader.Tests;

public class AppConfigTests
{
    [Theory]
    [InlineData("1.41.9", "1.42.0", -1)]
    [InlineData("1.42.0", "1.42.0", 0)]
    [InlineData("1.42", "1.42.0.0", 0)]
    [InlineData("1.10.0", "1.9.0", 1)]
    [InlineData("2.0.0", "1.99.99", 1)]
    public void Versions_compare_by_number_not_text(string a, string b, int sign) =>
        Assert.Equal(sign, Math.Sign(AppVersion.Compare(a, b)!.Value));

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1.x")]
    [InlineData("1.2.3.4.5")]
    [InlineData("1.-2")]
    [InlineData(null)]
    public void An_unreadable_version_has_no_order(string? text) => Assert.Null(AppVersion.Compare(text, "1.0.0"));

    [Theory]
    [InlineData("")]
    [InlineData("<html>Sign in to the Wi-Fi</html>")]
    [InlineData("[1,2]")]
    [InlineData("{}")]
    [InlineData("""{"schemaVersion":"1"}""")]
    public void A_body_that_is_not_a_served_config_is_refused(string json) => Assert.False(AppConfig.TryParseServed(json, out _));

    [Fact]
    public void A_newer_schema_with_a_readable_min_version_is_still_honoured()
    {
        Assert.True(AppConfig.TryParseServed("""{"schemaVersion":2,"android":{"minVersion":"1.2.0"}}""", out AppConfig config));
        Assert.Equal("1.2.0", config.Android.MinVersion);
    }

    [Fact]
    public void A_newer_schema_with_no_readable_min_version_is_refused() =>
        Assert.False(AppConfig.TryParseServed("""{"schemaVersion":2,"droid":{"min":"1.2.0"}}""", out _));

    [Fact]
    public void A_served_config_is_accepted_and_checked()
    {
        Assert.True(AppConfig.TryParseServed("""{"schemaVersion":1,"android":{"minVersion":"1.2.0","latestVersion":"1.4.0"}}""", out AppConfig config));
        Assert.Equal("1.2.0", config.Android.MinVersion);
    }

    [Fact]
    public void A_full_config_is_read()
    {
        AppConfig config = AppConfig.Parse("""
            {"android":{"minVersion":"1.2.0","latestVersion":"1.3.0"},"ios":{"minVersion":"1.0.0","latestVersion":"1.1.0"},
             "telemetry":{"enabled":true,"sampleRate":0.5},"extra":1}
            """);
        Assert.Equal(new PlatformVersions("1.2.0", "1.3.0"), config.Android);
        Assert.Equal(new PlatformVersions("1.0.0", "1.1.0"), config.Ios);
        Assert.Equal(PlatformVersions.None, config.Windows);
        Assert.Equal(new TelemetryConfig(true, 0.5), config.Telemetry);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("{}")]
    [InlineData("""{"android":"1.0.0","telemetry":5}""")]
    public void A_bad_body_gives_the_safe_default(string json) => Assert.Equal(AppConfig.Default, AppConfig.Parse(json));

    [Fact]
    public void A_bad_field_defaults_alone()
    {
        AppConfig config = AppConfig.Parse("""{"android":{"minVersion":"soon","latestVersion":"1.5.0"},"telemetry":{"enabled":true,"sampleRate":2}}""");
        Assert.Equal(new PlatformVersions(AppConfig.NoVersion, "1.5.0"), config.Android);
        Assert.Equal(TelemetryConfig.Off, config.Telemetry);
        Assert.Equal(TelemetryConfig.Off, AppConfig.Parse("""{"telemetry":{"enabled":"true","sampleRate":1}}""").Telemetry);
    }

    [Fact]
    public void Below_min_forces_an_update_and_below_latest_only_notes_it()
    {
        AppConfig config = AppConfig.Parse("""{"android":{"minVersion":"1.2.0","latestVersion":"1.4.0"}}""");
        Assert.True(config.RequiresUpdate(AppPlatform.Android, "1.1.9"));
        Assert.False(config.RequiresUpdate(AppPlatform.Android, "1.2.0"));
        Assert.True(config.HasNewerVersion(AppPlatform.Android, "1.3.0"));
        Assert.False(config.HasNewerVersion(AppPlatform.Android, "1.4.0"));
        Assert.False(config.RequiresUpdate(AppPlatform.Ios, "0.0.1"));
        Assert.False(AppConfig.Default.RequiresUpdate(AppPlatform.Windows, "garbage"));
    }
}
