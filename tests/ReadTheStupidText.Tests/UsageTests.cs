using System.Text.Json;
using System.Text.RegularExpressions;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Usage;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Tests;

public class UsageBucketsTests
{
    [Theory]
    [InlineData(0, "<0.5")]
    [InlineData(499, "<0.5")]
    [InlineData(500, "0.5-1")]
    [InlineData(999, "0.5-1")]
    [InlineData(1000, "1-2")]
    [InlineData(2000, "2-5")]
    [InlineData(4999, "2-5")]
    [InlineData(5000, "5-10")]
    [InlineData(10000, "10-20")]
    [InlineData(19999, "10-20")]
    [InlineData(20000, ">20")]
    [InlineData(600000, ">20")]
    public void Duration_buckets_put_each_limit_in_the_bucket_above_it(int milliseconds, string expected)
    {
        Assert.Equal(expected, UsageBuckets.Duration(TimeSpan.FromMilliseconds(milliseconds)));
    }

    [Theory]
    [InlineData(0, "<200")]
    [InlineData(199, "<200")]
    [InlineData(200, "200-1000")]
    [InlineData(999, "200-1000")]
    [InlineData(1000, "1000-5000")]
    [InlineData(4999, "1000-5000")]
    [InlineData(5000, ">5000")]
    [InlineData(1_000_000, ">5000")]
    public void Text_length_buckets_never_hold_an_exact_length(int characters, string expected)
    {
        Assert.Equal(expected, UsageBuckets.TextLength(characters));
    }
}

public class UsageEventsTests
{
    private static readonly UsageContext Context = new("1.4.0", "android", 0.5, new DateTimeOffset(2026, 10, 26, 23, 59, 0, TimeSpan.FromHours(-5)));

    [Fact]
    public void The_day_is_the_utc_day_and_nothing_finer()
    {
        UsageEvent? opened = UsageEvents.AppOpened(Context, "en", 15);

        Assert.Equal("2026-10-27", opened!.Day);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void An_app_opened_count_without_a_readable_os_version_is_not_made(int osMajor)
    {
        Assert.Null(UsageEvents.AppOpened(Context, "en", osMajor));
    }

    [Theory]
    [InlineData("fa-IR", "fa")]
    [InlineData("EN", "en")]
    [InlineData("pt_BR", "pt")]
    [InlineData(" fr ", "fr")]
    [InlineData("fil", "fil")]
    [InlineData("zh-Hans", "zh")]
    public void Language_keeps_the_lower_case_primary_code(string code, string expected)
    {
        Assert.Equal(expected, UsageEvents.Language(code));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("e")]
    [InlineData("english")]
    [InlineData("e1")]
    public void Language_refuses_anything_that_is_not_a_two_or_three_letter_code(string? code)
    {
        Assert.Null(UsageEvents.Language(code));
    }

    [Fact]
    public void A_read_names_languages_mode_engine_and_a_length_bucket_but_never_the_text()
    {
        UsageEvent? read = UsageEvents.ReadStarted(Context, "fr", "en", translated: true, UsageEngine.Supertonic, voiceDefault: true, textLength: 1234);

        Assert.Equal(UsageEventName.ReadStarted, read!.Name);
        Assert.Equal("en", read.Language);
        Assert.Equal("fr", read.SourceLanguage);
        Assert.Equal(UsageMode.Translation, read.Mode);
        Assert.Equal("1000-5000", read.LengthBucket);
        Assert.True(read.VoiceDefault);
    }

    [Fact]
    public void A_read_with_an_unsure_source_leaves_the_source_out_and_one_without_a_read_language_is_not_counted()
    {
        UsageEvent? read = UsageEvents.ReadStarted(Context, null, "en", translated: false, UsageEngine.Phone, voiceDefault: false, textLength: 10);

        Assert.Null(read!.SourceLanguage);
        Assert.Equal(UsageMode.Original, read.Mode);
        Assert.Null(UsageEvents.ReadStarted(Context, "fr", "?", translated: false, UsageEngine.Phone, voiceDefault: false, textLength: 10));
    }

    [Fact]
    public void A_translate_offer_and_choice_are_the_language_pair()
    {
        UsageEvent? offered = UsageEvents.TranslateOffered(Context, "fr", "en");
        UsageEvent? chosen = UsageEvents.TranslateChosen(Context, "fr", "en");

        Assert.Equal(UsageEventName.TranslateOffered, offered!.Name);
        Assert.Equal(UsageEventName.TranslateChosen, chosen!.Name);
        Assert.Equal(("fr", "en"), (chosen.SourceLanguage, chosen.Language));
        Assert.Null(UsageEvents.TranslateOffered(Context, null, "en"));
    }

    [Fact]
    public void A_timing_carries_the_stage_the_outcome_and_a_duration_bucket()
    {
        UsageEvent timing = UsageEvents.Timing(Context, UsageStage.Translate, UsageOutcome.Cancelled, TimeSpan.FromSeconds(7), "fr", "en");

        Assert.Equal(UsageStage.Translate, timing.Stage);
        Assert.Equal(UsageOutcome.Cancelled, timing.Outcome);
        Assert.Equal("5-10", timing.DurationBucket);
    }

    [Fact]
    public void The_engine_is_the_phone_voice_or_the_packs_engine()
    {
        Assert.Equal(UsageEngine.Phone, UsageEvents.EngineFor(true, VoiceEngineKind.Supertonic));
        Assert.Equal(UsageEngine.Supertonic, UsageEvents.EngineFor(false, VoiceEngineKind.Supertonic));
        Assert.Equal(UsageEngine.Piper, UsageEvents.EngineFor(false, VoiceEngineKind.Vits));
        Assert.Equal(UsageEngine.Melo, UsageEvents.EngineFor(false, VoiceEngineKind.Melo));
        Assert.Equal(UsageEngine.Kokoro, UsageEvents.EngineFor(false, VoiceEngineKind.Kokoro));
        Assert.Equal(UsageEngine.Phone, UsageEvents.EngineFor(false, null));
    }

    [Theory]
    [InlineData(null, "F1", true)]
    [InlineData("F1", "F1", true)]
    [InlineData("M3", "F1", false)]
    [InlineData("system", "F1", false)]
    public void A_voice_is_the_default_when_never_picked_or_the_models_own_first_choice(string? stored, string? modelDefault, bool expected)
    {
        Assert.Equal(expected, UsageEvents.VoiceIsDefault(stored, modelDefault));
    }

    [Fact]
    public void A_timing_carries_early_only_when_the_head_start_had_run()
    {
        UsageEvent plain = UsageEvents.Timing(Context, UsageStage.FirstAudio, UsageOutcome.Done, TimeSpan.FromSeconds(1), null, "en");
        UsageEvent early = UsageEvents.Timing(Context, UsageStage.FirstAudio, UsageOutcome.Done, TimeSpan.FromSeconds(1), null, "en", early: true);

        Assert.Null(plain.Early);
        Assert.True(early.Early);
        Assert.DoesNotContain("early", UsageBatch.QueueToJson([plain]));
        Assert.Contains("\"early\":true", UsageBatch.QueueToJson([early]));
    }

    [Fact]
    public void Every_event_name_used_is_one_the_Worker_knows()
    {
        string source = File.ReadAllText(Path.Combine(RepoRoot(), "worker", "events.ts"));
        Match rules = Regex.Match(source, @"RTST_EVENT_RULES[^=]*= \{(?<body>.*?)\n\};", RegexOptions.Singleline);
        Assert.True(rules.Success);
        string[] inWorker = Regex.Matches(rules.Groups["body"].Value, @"^\s{2}(?<name>[a-z_]+):", RegexOptions.Multiline).Select(m => m.Groups["name"].Value).ToArray();

        string[] inApp = [UsageEventName.AppOpened, UsageEventName.ReadStarted, UsageEventName.TranslateOffered, UsageEventName.TranslateChosen, UsageEventName.VoiceChanged, UsageEventName.Timing, UsageEventName.WaitShown, UsageEventName.WaitCancelled];
        Assert.Equal(inApp.Order(), inWorker.Order());
    }

    [Fact]
    public void Every_closed_value_the_app_can_send_is_in_the_Workers_lists()
    {
        string source = File.ReadAllText(Path.Combine(RepoRoot(), "worker", "events.ts"));
        string[] durations = Enumerable.Range(0, 7).Select(i => UsageBuckets.Duration(TimeSpan.FromSeconds(new[] { 0.1, 0.6, 1.5, 3, 7, 15, 30 }[i]))).ToArray();
        string[] lengths = [UsageBuckets.TextLength(0), UsageBuckets.TextLength(200), UsageBuckets.TextLength(1000), UsageBuckets.TextLength(5000)];
        string[] engines = [UsageEngine.Phone, UsageEngine.Supertonic, UsageEngine.Piper, UsageEngine.Melo, UsageEngine.Kokoro, UsageEngine.Windows];
        string[] sources = [UsageSource.Selection, UsageSource.Clipboard, UsageSource.Hotkey, UsageSource.Panel];
        string[] stages = [UsageStage.ModelDownload, UsageStage.Translate, UsageStage.FirstAudio];
        string[] outcomes = [UsageOutcome.Done, UsageOutcome.Cancelled, UsageOutcome.Failed];
        string[] modes = [UsageMode.Original, UsageMode.Translation];

        AssertListed(source, "RTST_DURATION_BUCKETS", durations);
        AssertListed(source, "RTST_LENGTH_BUCKETS", lengths);
        AssertListed(source, "RTST_ENGINES", engines);
        AssertListed(source, "RTST_STAGES", stages);
        AssertListed(source, "RTST_OUTCOMES", outcomes);
        AssertListed(source, "RTST_MODES", modes);
        AssertListed(source, "RTST_SOURCES", sources);
        AssertListed(source, "WAIT_STAGES", UsageWaitStage.All.ToArray());
    }

    [Fact]
    public void Every_wait_stage_has_its_own_wire_name()
    {
        string[] names = Enum.GetValues<WaitStage>().Select(UsageWaitStage.Of).ToArray();

        Assert.Equal(names.Distinct().Count(), names.Length);
        Assert.Equal(UsageWaitStage.All.Order(), names.Order());
    }

    [Fact]
    public void A_wait_stage_with_no_wire_name_throws_instead_of_counting_as_a_file_wait()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => UsageWaitStage.Of((WaitStage)999));
    }

    [Fact]
    public void A_wait_count_carries_only_its_stage_and_a_coarse_duration()
    {
        UsageEvent shown = UsageEvents.WaitShown(Context, WaitStage.Stalled, TimeSpan.FromMilliseconds(2500));
        UsageEvent cancelled = UsageEvents.WaitCancelled(Context, WaitStage.PreparingVoice, TimeSpan.FromMilliseconds(100));

        Assert.Equal(("wait_shown", "stalled", "2-5"), (shown.Name, shown.WaitStage, shown.DurationBucket));
        Assert.Equal(("wait_cancelled", "preparing_voice", "<0.5"), (cancelled.Name, cancelled.WaitStage, cancelled.DurationBucket));
        Assert.Null(shown.Language);
        Assert.Contains("\"waitStage\":\"stalled\"", UsageBatch.QueueToJson([shown]));
    }

    private static void AssertListed(string source, string list, string[] inApp)
    {
        Match match = Regex.Match(source, list + @" = \[(?<values>[^\]]*)\]");
        Assert.True(match.Success, list);
        string[] inWorker = Regex.Matches(match.Groups["values"].Value, "\"(?<v>[^\"]+)\"").Select(m => m.Groups["v"].Value).ToArray();
        Assert.Equal(inWorker.Order(), inApp.Order());
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ReadTheStupidText.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Repo root (ReadTheStupidText.slnx) not found.");
    }
}

public class DeviceBucketsTests
{
    private const long Gb = 1024L * 1024 * 1024;

    [Theory]
    [InlineData(1, "1-4")]
    [InlineData(4, "1-4")]
    [InlineData(5, "5-8")]
    [InlineData(8, "5-8")]
    [InlineData(9, "9+")]
    [InlineData(32, "9+")]
    public void Cores_fall_into_their_bucket_at_each_boundary(int cores, string expected)
    {
        Assert.Equal(expected, DeviceBuckets.Cores(cores));
    }

    [Fact]
    public void An_unreadable_core_count_is_the_lowest_bucket()
    {
        Assert.Equal("1-4", DeviceBuckets.Cores(0));
    }

    [Theory]
    [InlineData(1L, "<3")]
    [InlineData(2 * Gb, "<3")]
    [InlineData(2 * Gb + 1, "3-4")]
    [InlineData(3 * Gb, "3-4")]
    [InlineData(3 * Gb + 1, "4-6")]
    [InlineData(4 * Gb, "4-6")]
    [InlineData(5 * Gb, "4-6")]
    [InlineData(5 * Gb + 1, "6-8")]
    [InlineData(8 * Gb, "8+")]
    [InlineData(7 * Gb + 1, "8+")]
    [InlineData(64 * Gb, "8+")]
    [InlineData(2_900_000_000L, "3-4")]
    [InlineData(3_970_000_000L, "4-6")]
    [InlineData(6_000_000_000L, "6-8")]
    [InlineData(8_000_000_000L, "8+")]
    public void Memory_falls_into_its_bucket_at_each_boundary(long bytes, string expected)
    {
        Assert.Equal(expected, DeviceBuckets.Ram(bytes));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-5L)]
    public void An_unreadable_memory_size_has_no_bucket(long bytes)
    {
        Assert.Null(DeviceBuckets.Ram(bytes));
    }

    [Theory]
    [InlineData(System.Runtime.InteropServices.Architecture.Arm64, "arm64")]
    [InlineData(System.Runtime.InteropServices.Architecture.X64, "x64")]
    [InlineData(System.Runtime.InteropServices.Architecture.X86, "x86")]
    [InlineData(System.Runtime.InteropServices.Architecture.Arm, "arm")]
    [InlineData(System.Runtime.InteropServices.Architecture.Wasm, "other")]
    [InlineData(System.Runtime.InteropServices.Architecture.RiscV64, "other")]
    [InlineData((System.Runtime.InteropServices.Architecture)999, "other")]
    public void An_architecture_off_the_list_is_other(System.Runtime.InteropServices.Architecture architecture, string expected)
    {
        Assert.Equal(expected, DeviceBuckets.Arch(architecture));
    }

    [Fact]
    public void A_context_with_unreadable_memory_has_an_empty_ram_field()
    {
        DeviceContext context = DeviceBuckets.Build(15, DeviceFormFactor.Phone, System.Runtime.InteropServices.Architecture.Arm64, 8, 0);

        Assert.Equal(new DeviceContext(15, "phone", "arm64", "5-8", string.Empty), context);
    }
}

public class UsageBatchTests
{
    private static readonly UsageContext Context = new("1.4.0", "android", 1, new DateTimeOffset(2026, 10, 26, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public void The_body_leaves_out_every_field_an_event_does_not_use_and_names_no_id()
    {
        UsageEvent opened = UsageEvents.AppOpened(Context, "en", 15)!;

        string json = UsageBatch.ToJson([opened]);

        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        JsonElement only = document.RootElement.GetProperty("events")[0];
        string[] names = only.EnumerateObject().Select(p => p.Name).Order().ToArray();
        Assert.Equal(["day", "language", "name", "osMajor", "platform", "sampleRate", "version"], names);
    }

    [Fact]
    public void The_device_object_sits_next_to_the_events_and_is_left_out_when_there_is_none()
    {
        UsageEvent opened = UsageEvents.AppOpened(Context, "en", 15)!;
        var device = new DeviceContext(15, DeviceFormFactor.Tablet, DeviceArch.Arm64, "5-8", "4-6");

        using JsonDocument with = JsonDocument.Parse(UsageBatch.ToJson([opened], device));
        JsonElement sent = with.RootElement.GetProperty("device");
        Assert.Equal(["arch", "cores", "formFactor", "osMajor", "ram"], sent.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal("tablet", sent.GetProperty("formFactor").GetString());

        using JsonDocument without = JsonDocument.Parse(UsageBatch.ToJson([opened]));
        Assert.False(without.RootElement.TryGetProperty("device", out _));
    }

    [Fact]
    public void A_stored_queue_reads_back_equal_and_a_damaged_one_reads_as_empty()
    {
        UsageEvent read = UsageEvents.ReadStarted(Context, "fr", "en", true, UsageEngine.Kokoro, false, 300)!;
        UsageEvent timing = UsageEvents.Timing(Context, UsageStage.FirstAudio, UsageOutcome.Done, TimeSpan.FromSeconds(1.2), null, "en");

        List<UsageEvent> back = UsageBatch.ParseQueue(UsageBatch.QueueToJson([read, timing]));

        Assert.Equal([read, timing], back);
        Assert.Empty(UsageBatch.ParseQueue("{not json"));
        Assert.Empty(UsageBatch.ParseQueue(null));
    }
}

public class UsageConfigTests
{
    [Fact]
    public void Nothing_is_on_without_a_served_config()
    {
        Assert.False(UsageConfig.Default.Telemetry.Enabled);
        Assert.False(UsageConfig.Parse(null).Telemetry.Enabled);
        Assert.False(UsageConfig.Parse("[]").Telemetry.Enabled);
        Assert.False(UsageConfig.Parse("<html>captive portal").Telemetry.Enabled);
    }

    [Fact]
    public void Telemetry_is_on_only_for_a_literal_true_with_a_rate_from_zero_to_one()
    {
        Assert.Equal(new TelemetrySettings(true, 0.25), UsageConfig.Parse("""{"telemetry":{"enabled":true,"sampleRate":0.25}}""").Telemetry);
        Assert.False(UsageConfig.Parse("""{"telemetry":{"enabled":"true","sampleRate":1}}""").Telemetry.Enabled);
        Assert.False(UsageConfig.Parse("""{"telemetry":{"enabled":true,"sampleRate":2}}""").Telemetry.Enabled);
        Assert.False(UsageConfig.Parse("""{"telemetry":{"enabled":true}}""").Telemetry.Enabled);
        Assert.False(UsageConfig.Parse("""{"telemetry":{"enabled":false,"sampleRate":1}}""").Telemetry.Enabled);
    }

    [Fact]
    public void Only_a_body_with_a_known_schema_version_counts_as_served()
    {
        Assert.True(UsageConfig.TryParseServed("""{"schemaVersion":1,"telemetry":{"enabled":false,"sampleRate":0}}""", out UsageConfig served));
        Assert.False(served.Telemetry.Enabled);
        Assert.False(UsageConfig.TryParseServed("""{"telemetry":{"enabled":true,"sampleRate":1}}""", out _));
        Assert.False(UsageConfig.TryParseServed("""{"schemaVersion":2,"telemetry":{"enabled":true,"sampleRate":1}}""", out _));
        Assert.False(UsageConfig.TryParseServed("<html>", out _));
    }
}
