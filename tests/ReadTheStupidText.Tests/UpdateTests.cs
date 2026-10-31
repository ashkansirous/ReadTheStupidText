using ReadTheStupidText.Application.AppConfig;
using ReadTheStupidText.Application.Usage;

namespace ReadTheStupidText.Tests;

public class AppVersionTests
{
    [Theory]
    [InlineData("1.73.2", "1.74.0", -1)]
    [InlineData("1.74.0", "1.74", 0)]
    [InlineData("1.74.1", "1.74.0", 1)]
    [InlineData("1.9", "1.10", -1)]
    [InlineData("2.0.0.0", "1.99.99.99", 1)]
    public void Versions_compare_part_by_part(string a, string b, int sign)
    {
        Assert.Equal(sign, Math.Sign(AppVersion.Compare(a, b)!.Value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1.2.3.4.5")]
    [InlineData("1.x")]
    [InlineData("1.-2")]
    public void A_version_that_does_not_parse_compares_as_null(string? text)
    {
        Assert.Null(AppVersion.Compare(text, "1.0"));
        Assert.Null(AppVersion.Compare("1.0", text));
    }
}

public class UpdateConfigParseTests
{
    private const string Served = """{"schemaVersion":1,"android":{"minVersion":"1.70.0","latestVersion":"1.75.0"},"ios":{"minVersion":"1.60.0","latestVersion":"1.75.0"},"windows":{"minVersion":"0.0.0","latestVersion":"1.75.0"},"telemetry":{"enabled":true,"sampleRate":1}}""";

    [Fact]
    public void Each_platform_reads_its_own_versions()
    {
        UsageConfig.TryParseServed(Served, out UsageConfig config);
        Assert.Equal(new PlatformVersions("1.70.0", "1.75.0"), config.For(AppPlatform.Android));
        Assert.Equal(new PlatformVersions("1.60.0", "1.75.0"), config.For(AppPlatform.Ios));
        Assert.Equal(new PlatformVersions("0.0.0", "1.75.0"), config.For(AppPlatform.Windows));
        Assert.True(config.Telemetry.Enabled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("[]")]
    [InlineData("<html>captive portal")]
    [InlineData("""{"android":"1.0.0"}""")]
    [InlineData("""{"android":{"minVersion":99,"latestVersion":"abc"}}""")]
    [InlineData("""{"android":{"minVersion":"1.2.3.4.5"}}""")]
    public void A_missing_or_bad_config_forces_nothing(string? json)
    {
        UsageConfig config = UsageConfig.Parse(json);
        Assert.False(config.RequiresUpdate(AppPlatform.Android, "0.1"));
        Assert.False(config.HasNewerVersion(AppPlatform.Android, "0.1"));
    }

    [Fact]
    public void A_bad_field_does_not_spoil_the_good_one()
    {
        UsageConfig config = UsageConfig.Parse("""{"android":{"minVersion":"nope","latestVersion":"1.75.0"}}""");
        Assert.False(config.RequiresUpdate(AppPlatform.Android, "1.0.0"));
        Assert.True(config.HasNewerVersion(AppPlatform.Android, "1.0.0"));
    }

    [Fact]
    public void An_unreadable_installed_version_never_forces()
    {
        UsageConfig config = UsageConfig.Parse("""{"android":{"minVersion":"9.0.0","latestVersion":"9.0.0"}}""");
        Assert.False(config.RequiresUpdate(AppPlatform.Android, "dev"));
        Assert.False(config.HasNewerVersion(AppPlatform.Android, ""));
    }
}

public class UpdateDecisionTests
{
    private static readonly UsageConfig Config = UsageConfig.Parse("""{"android":{"minVersion":"1.70.0","latestVersion":"1.75.0"}}""");

    [Theory]
    [InlineData("1.69.9", UpdateState.Required)]
    [InlineData("1.70.0", UpdateState.Available)]
    [InlineData("1.74.9", UpdateState.Available)]
    [InlineData("1.75.0", UpdateState.None)]
    [InlineData("2.0.0", UpdateState.None)]
    public void The_installed_version_picks_the_state(string installed, UpdateState expected)
    {
        Assert.Equal(expected, UpdateDecision.Evaluate(Config, AppPlatform.Android, installed, true));
    }

    [Fact]
    public void Another_platforms_versions_do_not_apply()
    {
        Assert.Equal(UpdateState.None, UpdateDecision.Evaluate(Config, AppPlatform.Ios, "1.0.0", true));
    }

    [Fact]
    public void A_build_that_may_not_update_stays_at_none()
    {
        Assert.Equal(UpdateState.None, UpdateDecision.Evaluate(Config, AppPlatform.Android, "1.0.0", false));
    }

    [Fact]
    public void Debug_and_unstamped_builds_may_not_update_but_emulators_and_test_installs_may()
    {
        Assert.False(UpdateDecision.BuildMayUpdate(new UsageFacts(true, "1.75.0", false, false)));
        Assert.False(UpdateDecision.BuildMayUpdate(new UsageFacts(false, "1.0", false, false)));
        Assert.False(UpdateDecision.BuildMayUpdate(new UsageFacts(false, "0.0.0", false, false)));
        Assert.True(UpdateDecision.BuildMayUpdate(new UsageFacts(false, "1.69.0", true, true, true, true)));
    }

    [Fact]
    public void A_dismissal_holds_only_for_the_version_it_was_made_for()
    {
        Assert.True(UpdateDecision.IsDismissed("1.75.0", Config, AppPlatform.Android));
        Assert.False(UpdateDecision.IsDismissed("1.74.0", Config, AppPlatform.Android));
        Assert.False(UpdateDecision.IsDismissed(null, Config, AppPlatform.Android));
    }
}

public class UpdateConfigServiceTests
{
    private const string Forced = """{"schemaVersion":1,"windows":{"minVersion":"1.70.0","latestVersion":"1.75.0"}}""";

    private sealed class FakeStore : IUsageStore
    {
        public string? Config { get; set; }

        public bool Enabled { get; set; }

        public string? LoadQueue() => null;

        public void SaveQueue(string? json)
        {
        }

        public string? LoadConfig() => Config;

        public void SaveConfig(string json) => Config = json;
    }

    private sealed class FakeTransport(Func<string> body) : IUsageTransport
    {
        public int Calls { get; private set; }

        public Task<string> FetchConfigAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(body());
        }

        public Task<UsageSendResult> PostEventsAsync(string json, CancellationToken cancellationToken) => Task.FromResult(UsageSendResult.Sent);
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private static UpdateConfigService Service(FakeStore store, FakeTransport transport, bool mayUpdate = true, string? test = null) =>
        new(store, transport, AppPlatform.Windows, "1.67.1", mayUpdate, test);

    [Fact]
    public async Task A_fetched_config_forces_an_update_and_is_kept()
    {
        var store = new FakeStore();
        UpdateConfigService service = Service(store, new FakeTransport(() => Forced));
        Assert.Equal(UpdateState.None, service.State);

        await service.RefreshAsync();

        Assert.Equal(UpdateState.Required, service.State);
        Assert.Equal(Forced, store.Config);
    }

    [Fact]
    public void A_kept_copy_is_used_offline()
    {
        UpdateConfigService service = Service(new FakeStore { Config = Forced }, new FakeTransport(() => throw new HttpRequestException()));
        Assert.Equal(UpdateState.Required, service.State);
    }

    [Fact]
    public void No_copy_and_no_network_forces_nothing()
    {
        Assert.Equal(UpdateState.None, Service(new FakeStore(), new FakeTransport(() => throw new HttpRequestException())).State);
    }

    [Fact]
    public async Task A_failed_or_foreign_body_leaves_the_kept_copy()
    {
        var store = new FakeStore { Config = Forced };
        UpdateConfigService service = Service(store, new FakeTransport(() => "<html>captive portal"));

        await service.RefreshAsync();

        Assert.Equal(UpdateState.Required, service.State);
        Assert.Equal(Forced, store.Config);
    }

    [Fact]
    public async Task A_second_refresh_inside_the_interval_does_not_fetch_again()
    {
        var transport = new FakeTransport(() => Forced);
        UpdateConfigService service = Service(new FakeStore(), transport);

        await service.RefreshAsync();
        await service.RefreshAsync();

        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    public async Task Changed_is_raised_only_when_the_config_differs()
    {
        string body = Forced;
        var transport = new FakeTransport(() => body);
        var clock = new ManualClock();
        var service = new UpdateConfigService(new FakeStore(), transport, AppPlatform.Windows, "1.67.1", true, null, clock);
        int raised = 0;
        service.Changed += (_, _) => raised++;

        await service.RefreshAsync();
        clock.Advance(TimeSpan.FromMinutes(20));
        await service.RefreshAsync();
        Assert.Equal(1, raised);

        body = Forced.Replace("1.75.0", "1.80.0", StringComparison.Ordinal);
        clock.Advance(TimeSpan.FromMinutes(20));
        await service.RefreshAsync();
        Assert.Equal(2, raised);
    }

    [Fact]
    public async Task A_debug_build_is_never_forced_by_the_live_config()
    {
        UpdateConfigService service = Service(new FakeStore(), new FakeTransport(() => Forced), mayUpdate: false);
        await service.RefreshAsync();
        Assert.Equal(UpdateState.None, service.State);
    }

    [Fact]
    public async Task A_test_config_replaces_the_live_one_and_fetches_nothing()
    {
        var store = new FakeStore();
        var transport = new FakeTransport(() => "{}");
        UpdateConfigService service = Service(store, transport, mayUpdate: false, test: Forced);

        await service.RefreshAsync();

        Assert.Equal(UpdateState.Required, service.State);
        Assert.Equal(0, transport.Calls);
        Assert.Null(store.Config);
    }
}
