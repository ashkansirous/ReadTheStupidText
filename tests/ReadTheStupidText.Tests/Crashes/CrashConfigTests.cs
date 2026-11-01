using ReadTheStupidText.Application.Usage;
using Xunit;

namespace ReadTheStupidText.Tests.Crashes;

/// <summary>Slice 227: the remote config key <c>crashReports.enabled</c> is off unless it is a literal true.</summary>
public class CrashConfigTests
{
    [Theory]
    [InlineData("""{"schemaVersion":1,"crashReports":{"enabled":true}}""", true)]
    [InlineData("""{"schemaVersion":1,"crashReports":{"enabled":false}}""", false)]
    [InlineData("""{"schemaVersion":1,"crashReports":{"enabled":"true"}}""", false)]
    [InlineData("""{"schemaVersion":1,"crashReports":{"enabled":1}}""", false)]
    [InlineData("""{"schemaVersion":1,"crashReports":true}""", false)]
    [InlineData("""{"schemaVersion":1,"crashReports":{}}""", false)]
    [InlineData("""{"schemaVersion":1}""", false)]
    [InlineData("""{"schemaVersion":1,"telemetry":{"enabled":true,"sampleRate":1}}""", false)]
    [InlineData("not json", false)]
    [InlineData(null, false)]
    public void Crash_reports_are_on_only_for_a_literal_true(string? json, bool expected)
    {
        Assert.Equal(expected, UsageConfig.Parse(json).CrashReportsEnabled);
        if (json is not null && json.Contains("schemaVersion"))
        {
            Assert.True(UsageConfig.TryParseServed(json, out UsageConfig served));
            Assert.Equal(expected, served.CrashReportsEnabled);
        }
    }

    [Fact]
    public void The_default_config_has_crash_reports_off() => Assert.False(UsageConfig.Default.CrashReportsEnabled);
}
