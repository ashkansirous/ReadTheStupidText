using System.Net;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Usage;
using ReadTheStupidText.Domain.Activity;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Infrastructure.Usage;

namespace ReadTheStupidText.Tests;

public class UsageServiceTests
{
    private const string ConfigOn = """{"schemaVersion":1,"telemetry":{"enabled":true,"sampleRate":1}}""";
    private const string ConfigOff = """{"schemaVersion":1,"telemetry":{"enabled":false,"sampleRate":0}}""";
    private const string ConfigHalf = """{"schemaVersion":1,"telemetry":{"enabled":true,"sampleRate":0.5}}""";
    private static readonly UsageBuild Build = new("1.4.0", "windows", 11);

    [Fact]
    public async Task A_count_is_queued_only_while_the_switch_and_the_config_are_on()
    {
        var store = new FakeStore { Config = ConfigOn };
        var service = Service(store, new FakeTransport());

        await service.RecordAsync(Read);
        Assert.Equal(1, service.QueuedCount);

        service.Enabled = false;
        await service.Settled;
        await service.RecordAsync(Read);
        Assert.Equal(0, service.QueuedCount);
        Assert.Null(store.Queue);
    }

    [Fact]
    public async Task Nothing_is_queued_when_the_config_is_off_or_the_launch_was_not_sampled()
    {
        var off = Service(new FakeStore { Config = ConfigOff }, new FakeTransport());
        await off.RecordAsync(Read);
        Assert.Equal(0, off.QueuedCount);

        var halfSampled = Service(new FakeStore { Config = ConfigHalf }, new FakeTransport(), roll: 0.7);
        await halfSampled.RecordAsync(Read);
        Assert.Equal(0, halfSampled.QueuedCount);
    }

    [Fact]
    public async Task Counts_are_held_while_no_config_was_seen_then_sent_once_one_says_on()
    {
        var store = new FakeStore();
        var transport = new FakeTransport { ConfigFails = true };
        var service = Service(store, transport);

        await service.RecordAsync(Read);
        Assert.Equal(1, service.QueuedCount);

        transport.ConfigFails = false;
        transport.ConfigBody = ConfigOn;
        await service.FlushAsync();

        string body = Assert.Single(transport.Batches);
        Assert.Contains("\"sampleRate\":1", body);
        Assert.Equal(0, service.QueuedCount);
    }

    [Fact]
    public async Task A_test_build_builds_its_batches_but_never_posts_them()
    {
        var transport = new FakeTransport { ConfigBody = ConfigOn };
        var service = new UsageService(new FakeStore(), transport, Build with { NotCounted = UsageSkipReason.DebugBuild }, new FixedClock(), 0);
        await service.RecordAsync(Read);

        await service.FlushAsync();

        Assert.Empty(transport.Batches);
        Assert.Equal(0, service.QueuedCount);
    }

    [Fact]
    public async Task Held_counts_are_forgotten_when_the_config_arrives_and_this_launch_is_not_sampled()
    {
        var transport = new FakeTransport { ConfigBody = ConfigHalf };
        var service = Service(new FakeStore(), transport, roll: 0.7);
        await service.RecordAsync(Read);
        Assert.Equal(1, service.QueuedCount);

        await service.FlushAsync();

        Assert.Empty(transport.Batches);
        Assert.Equal(0, service.QueuedCount);
    }

    [Fact]
    public async Task A_batch_is_out_of_the_saved_queue_while_it_is_posted_so_a_quit_cannot_send_it_twice()
    {
        var store = new FakeStore { Config = ConfigOn };
        var transport = new FakeTransport { Hang = true };
        var service = Service(store, transport);
        await service.RecordAsync(Read);

        await service.FlushForExitAsync(TimeSpan.FromMilliseconds(100));

        Assert.Single(transport.Batches);
        Assert.Null(store.Queue);
    }

    [Fact]
    public async Task A_flush_waits_behind_a_send_that_is_running_and_then_sends_what_was_queued_meanwhile()
    {
        var store = new FakeStore { Config = ConfigOn };
        var transport = new FakeTransport { Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        var service = Service(store, transport);
        await service.RecordAsync(Read);
        Task running = service.FlushAsync();
        await WaitUntilAsync(() => transport.Batches.Count == 1);
        await service.RecordAsync(Read);

        Task waiting = service.FlushAsync();
        await Task.Delay(150);

        Assert.False(waiting.IsCompleted);

        transport.Gate.SetResult();
        await Task.WhenAll(running, waiting).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, transport.Batches.Sum(CountEvents));
        Assert.Equal(0, service.QueuedCount);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(20);
        }

        Assert.True(condition());
    }

    [Fact]
    public async Task A_flush_sends_the_queue_in_batches_of_fifty_and_empties_it()
    {
        var store = new FakeStore { Config = ConfigOn };
        var transport = new FakeTransport();
        var service = Service(store, transport);
        for (int i = 0; i < 120; i++)
        {
            await service.RecordAsync(Read);
        }

        await service.FlushAsync();

        Assert.Equal(120, transport.Batches.Sum(CountEvents));
        Assert.All(transport.Batches, batch => Assert.True(CountEvents(batch) <= UsageBatch.MaxEvents));
        Assert.Equal(0, service.QueuedCount);
        Assert.Null(store.Queue);
    }

    [Fact]
    public async Task A_sent_count_carries_the_platform_and_closed_values_but_no_id_and_no_text()
    {
        var transport = new FakeTransport();
        var service = Service(new FakeStore { Config = ConfigOn }, transport);
        await service.RecordAsync(Read);

        await service.FlushAsync();

        string body = Assert.Single(transport.Batches);
        Assert.Contains("\"platform\":\"windows\"", body);
        Assert.Contains("\"source\":\"hotkey\"", body);
        Assert.Contains("\"day\":\"2026-10-26\"", body);
        Assert.DoesNotContain("\"id\"", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("text", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task With_no_config_ever_seen_the_queue_is_kept_not_cleared()
    {
        var store = new FakeStore { Queue = UsageBatch.QueueToJson([Event()]) };
        var transport = new FakeTransport { ConfigFails = true };
        var service = Service(store, transport);

        await service.FlushAsync();

        Assert.Empty(transport.Batches);
        Assert.NotNull(store.Queue);
        Assert.Equal(1, service.QueuedCount);
    }

    [Fact]
    public async Task A_config_that_says_off_clears_the_queue_and_sends_nothing()
    {
        var store = new FakeStore { Config = ConfigOff, Queue = UsageBatch.QueueToJson([Event()]) };
        var transport = new FakeTransport();
        var service = Service(store, transport);

        await service.FlushAsync();

        Assert.Empty(transport.Batches);
        Assert.Null(store.Queue);
    }

    [Fact]
    public async Task Turning_the_switch_off_forgets_the_waiting_counts_and_keeps_them_forgotten()
    {
        var store = new FakeStore { Config = ConfigOn };
        var transport = new FakeTransport();
        var service = Service(store, transport);
        await service.RecordAsync(Read);

        service.Enabled = false;
        await service.Settled;
        await service.FlushAsync();

        Assert.False(store.Enabled);
        Assert.Null(store.Queue);
        Assert.Empty(transport.Batches);
    }

    [Fact]
    public async Task A_batch_to_retry_stays_queued_and_the_next_flush_waits_out_the_back_off()
    {
        var store = new FakeStore { Config = ConfigOn };
        var transport = new FakeTransport { Result = UsageSendResult.Retry };
        var service = Service(store, transport);
        await service.RecordAsync(Read);

        await service.FlushAsync();
        await service.FlushAsync();

        Assert.Single(transport.Batches);
        Assert.Equal(1, service.QueuedCount);
    }

    [Fact]
    public async Task A_batch_the_worker_refuses_for_good_is_dropped()
    {
        var store = new FakeStore { Config = ConfigOn };
        var transport = new FakeTransport { Result = UsageSendResult.Rejected };
        var service = Service(store, transport);
        await service.RecordAsync(Read);

        await service.FlushAsync();

        Assert.Equal(0, service.QueuedCount);
    }

    [Fact]
    public async Task The_launch_is_counted_once_a_config_is_fetched_and_kept_for_next_time()
    {
        var store = new FakeStore();
        var transport = new FakeTransport { ConfigBody = ConfigOn };
        var service = Service(store, transport);

        await service.StartAsync(context => UsageEvents.AppOpened(context, "fr", service.OsMajor));

        Assert.Equal(ConfigOn, store.Config);
        string body = Assert.Single(transport.Batches);
        Assert.Contains("\"name\":\"app_opened\"", body);
        Assert.Contains("\"osMajor\":11", body);
    }

    [Fact]
    public async Task A_fetched_body_that_is_not_a_config_never_replaces_the_good_copy()
    {
        var store = new FakeStore { Config = ConfigOn };
        var transport = new FakeTransport { ConfigBody = "<html>captive portal</html>" };
        var service = Service(store, transport);

        await service.StartAsync(context => UsageEvents.AppOpened(context, "fr", service.OsMajor));

        Assert.Equal(ConfigOn, store.Config);
        Assert.Contains("app_opened", Assert.Single(transport.Batches));
    }

    [Fact]
    public async Task Recording_never_throws_when_the_store_fails()
    {
        var store = new FakeStore { Config = ConfigOn, FailWrites = true };
        var service = Service(store, new FakeTransport());

        await service.RecordAsync(Read);
    }

    [Fact]
    public async Task The_exit_flush_returns_within_its_wait_even_when_the_network_hangs()
    {
        var store = new FakeStore { Config = ConfigOn };
        var service = Service(store, new FakeTransport { Hang = true });
        await service.RecordAsync(Read);

        Task exit = service.FlushForExitAsync(TimeSpan.FromMilliseconds(100));

        Assert.Same(exit, await Task.WhenAny(exit, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task While_the_switch_is_off_a_launch_asks_for_no_config_and_sends_nothing()
    {
        var store = new FakeStore { Config = ConfigOn, Enabled = false };
        var transport = new FakeTransport { ConfigBody = ConfigOn };
        var service = Service(store, transport);

        await service.StartAsync(context => UsageEvents.AppOpened(context, "en", 11));

        Assert.Equal(0, transport.ConfigFetches);
        Assert.Empty(transport.Batches);
        Assert.Null(store.Queue);
    }

    [Fact]
    public async Task The_last_send_at_exit_uses_the_held_config_and_asks_for_no_new_one()
    {
        var store = new FakeStore { Config = ConfigOn };
        var transport = new FakeTransport { ConfigBody = ConfigOn };
        var service = Service(store, transport);
        await service.RecordAsync(Read);

        await service.FlushForExitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(0, transport.ConfigFetches);
        Assert.Single(transport.Batches);
    }

    [Fact]
    public async Task A_batch_carries_the_device_object_from_the_build_and_none_without_one()
    {
        var withDevice = new FakeTransport { ConfigBody = ConfigOn };
        var build = Build with { Device = DeviceBuckets.Build(11, DeviceFormFactor.Desktop, System.Runtime.InteropServices.Architecture.X64, 12, 16L * 1024 * 1024 * 1024) };
        var service = new UsageService(new FakeStore { Config = ConfigOn }, withDevice, build, new FixedClock(), 0);
        await service.RecordAsync(Read);
        await service.FlushAsync();
        string body = Assert.Single(withDevice.Batches);
        System.Text.Json.JsonElement device = System.Text.Json.JsonDocument.Parse(body).RootElement.GetProperty("device");
        Assert.Equal(
            ["11", "desktop", "x64", "9+", "8+"],
            new[] { "osMajor", "formFactor", "arch", "cores", "ram" }.Select(name => device.GetProperty(name).ToString()));

        var without = new FakeTransport { ConfigBody = ConfigOn };
        var plain = Service(new FakeStore { Config = ConfigOn }, without);
        await plain.RecordAsync(Read);
        await plain.FlushAsync();
        Assert.DoesNotContain("device", Assert.Single(without.Batches));
    }

    private static UsageService Service(FakeStore store, FakeTransport transport, double roll = 0) =>
        new(store, transport, Build, new FixedClock(), roll);

    private static UsageEvent? Read(UsageContext context) =>
        UsageEvents.ReadStarted(context, null, "en", false, UsageEngine.Windows, true, 120, UsageSource.Hotkey);

    private static UsageEvent Event() =>
        Read(new UsageContext("1.4.0", "windows", 1, new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero)))!;

    private static int CountEvents(string body) => System.Text.Json.JsonDocument.Parse(body).RootElement.GetProperty("events").GetArrayLength();

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeStore : IUsageStore
    {
        public bool Enabled { get; set; } = true;
        public string? Queue { get; set; }
        public string? Config { get; set; }
        public bool FailWrites { get; set; }

        public string? LoadQueue() => Queue;

        public string? LoadConfig() => Config;

        public void SaveQueue(string? json)
        {
            if (FailWrites)
            {
                throw new IOException("disk full");
            }

            Queue = json;
        }

        public void SaveConfig(string json) => Config = json;
    }

    private sealed class FakeTransport : IUsageTransport
    {
        public List<string> Batches { get; } = [];
        public UsageSendResult Result { get; set; } = UsageSendResult.Sent;
        public string? ConfigBody { get; set; }
        public bool ConfigFails { get; set; }
        public bool Hang { get; set; }
        public TaskCompletionSource? Gate { get; set; }
        public int ConfigFetches { get; private set; }

        public Task<string> FetchConfigAsync(CancellationToken cancellationToken)
        {
            ConfigFetches++;
            return ConfigFails || ConfigBody is null ? Task.FromException<string>(new HttpRequestException("offline")) : Task.FromResult(ConfigBody);
        }

        public async Task<UsageSendResult> PostEventsAsync(string json, CancellationToken cancellationToken)
        {
            Batches.Add(json);
            if (Gate is { } gate)
            {
                await gate.Task;
            }

            if (Hang)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return Result;
        }
    }
}

public class UsageWindowsEventsTests
{
    private static readonly UsageContext Context = new("1.4.0", "windows", 1, new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero));

    [Theory]
    [InlineData(ActivityTrigger.AutoRead, UsageSource.Selection)]
    [InlineData(ActivityTrigger.Clipboard, UsageSource.Clipboard)]
    [InlineData(ActivityTrigger.Hotkey, UsageSource.Hotkey)]
    [InlineData(ActivityTrigger.Manual, UsageSource.Panel)]
    [InlineData(ActivityTrigger.FileUpload, UsageSource.Panel)]
    public void Each_trigger_maps_to_one_of_the_four_closed_sources(ActivityTrigger trigger, string expected)
    {
        Assert.Equal(expected, UsageEvents.SourceOf(trigger));
    }

    [Fact]
    public void Every_trigger_has_a_row_above()
    {
        Assert.Equal(5, Enum.GetValues<ActivityTrigger>().Length); // a new trigger fails here until it is mapped on purpose in SourceOf
    }

    [Fact]
    public void The_Windows_voice_and_a_missing_voice_are_the_windows_engine_and_a_pack_voice_keeps_its_own()
    {
        Assert.Equal(UsageEngine.Windows, UsageEvents.EngineOf(null));
        Assert.Equal(UsageEngine.Windows, UsageEvents.EngineOf(new VoiceInfo(SystemVoice.Id, "Windows voice", string.Empty)));
        Assert.Equal(UsageEngine.Supertonic, UsageEvents.EngineOf(new VoiceInfo("F1", "Female 1", "en", VoiceEngineKind.Supertonic)));
        Assert.Equal(UsageEngine.Piper, UsageEvents.EngineOf(new VoiceInfo("fa", "Persian", "fa", VoiceEngineKind.Vits)));
    }

    [Fact]
    public void A_read_count_keeps_its_source_and_the_phone_read_has_none()
    {
        UsageEvent windows = UsageEvents.ReadStarted(Context, "fr", "en", true, UsageEngine.Windows, true, 500, UsageSource.Selection)!;
        UsageEvent phone = UsageEvents.ReadStarted(Context, "fr", "en", true, UsageEngine.Phone, true, 500)!;

        Assert.Equal(UsageSource.Selection, windows.Source);
        Assert.Null(phone.Source);
        Assert.Equal("200-1000", windows.LengthBucket);
    }

    [Fact]
    public void Switching_translation_on_needs_only_the_target_language()
    {
        UsageEvent count = UsageEvents.TranslateSwitchedOn(Context, null, "fa-IR")!;

        Assert.Equal(UsageEventName.TranslateChosen, count.Name);
        Assert.Equal("fa", count.Language);
        Assert.Null(count.SourceLanguage);
        Assert.Null(UsageEvents.TranslateSwitchedOn(Context, "fr", null));
    }

    [Theory]
    [InlineData(19045, 10)]
    [InlineData(21999, 10)]
    [InlineData(22000, 11)]
    [InlineData(26300, 11)]
    public void Windows_10_and_11_are_told_apart_by_build_number(int build, int expected)
    {
        Assert.Equal(expected, WindowsUsage.OsMajor(build));
    }

    [Fact]
    public void The_version_drops_the_store_revision()
    {
        Assert.Equal("1.4.2", WindowsUsage.VersionText(1, 4, 2));
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent, UsageSendResult.Sent)]
    [InlineData(HttpStatusCode.OK, UsageSendResult.Sent)]
    [InlineData(HttpStatusCode.TooManyRequests, UsageSendResult.Retry)]
    [InlineData(HttpStatusCode.RequestTimeout, UsageSendResult.Retry)]
    [InlineData(HttpStatusCode.ServiceUnavailable, UsageSendResult.Retry)]
    [InlineData(HttpStatusCode.BadRequest, UsageSendResult.Rejected)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, UsageSendResult.Rejected)]
    [InlineData(HttpStatusCode.NotFound, UsageSendResult.Rejected)]
    [InlineData(HttpStatusCode.Found, UsageSendResult.Retry)]
    [InlineData(HttpStatusCode.ProxyAuthenticationRequired, UsageSendResult.Retry)]
    public void The_status_decides_between_sent_retry_and_dropped(HttpStatusCode status, UsageSendResult expected)
    {
        Assert.Equal(expected, UsageHttpTransport.Classify(status));
    }
}
