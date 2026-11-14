using System.Text.RegularExpressions;
using DinoReader.Core;
using Sirous.Reading.Application.Crashes;
using Sirous.Reading.Application.Reading;

namespace DinoReader.Tests;

/// <summary>Slice 250: the usage session id, the sequence, per-session sampling and the playback events' closed names.</summary>
public partial class UsagePlaybackTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    private static Story ShelfStory() =>
        new("local-id", "A private title", "en", Now, [], [], CoverStyle.FirstPage, 0, string.Empty, SourceId: "0123456789abcdef0123456789abcdef");

    [Fact]
    public void A_session_has_one_id_of_32_lower_case_hex_digits_for_its_whole_life()
    {
        var session = new UsageSession();

        Assert.Matches("^[0-9a-f]{32}$", session.Id);
        Assert.Equal(session.Id, session.Id);
    }

    [Fact]
    public void A_new_session_a_cold_start_has_a_new_id()
    {
        var ids = Enumerable.Range(0, 20).Select(_ => new UsageSession().Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void The_sequence_counts_up_from_one_inside_a_session_and_starts_again_in_the_next()
    {
        var first = new UsageSession();
        var second = new UsageSession();

        Assert.Equal([1, 2, 3], [first.NextSeq(), first.NextSeq(), first.NextSeq()]);
        Assert.Equal(1, second.NextSeq());
    }

    [Fact]
    public void The_sequence_is_unique_across_threads()
    {
        var session = new UsageSession();

        int[] seen = Enumerable.Range(0, 400).AsParallel().Select(_ => session.NextSeq()).ToArray();

        Assert.Equal(Enumerable.Range(1, 400), seen.Order());
    }

    [Fact]
    public void Sampling_is_decided_once_per_session_so_a_sampled_session_is_complete_and_an_unsampled_one_is_empty()
    {
        var session = new UsageSession();
        var telemetry = new TelemetryConfig(true, 0.5);
        bool decided = UsageBatch.Samples(telemetry, session.Roll);

        Assert.InRange(session.Roll, 0, 0.999999999);
        for (int count = 0; count < 50; count++)
        {
            Assert.Equal(decided, UsageBatch.Samples(telemetry, session.Roll));
        }
    }

    [Fact]
    public void The_sampled_fraction_of_sessions_equals_the_configured_rate()
    {
        const int sessions = 20000;
        const double rate = 0.3;
        var telemetry = new TelemetryConfig(true, rate);

        int sampled = Enumerable.Range(0, sessions).Count(_ => UsageBatch.Samples(telemetry, new UsageSession().Roll));

        Assert.InRange(sampled / (double)sessions, rate - 0.02, rate + 0.02); // one roll per launch: a second roll would give 0.09
    }

    [Fact]
    public void A_new_session_rolls_again()
    {
        var rolls = Enumerable.Range(0, 20).Select(_ => new UsageSession().Roll).Distinct().Count();

        Assert.True(rolls > 1);
    }

    [Fact]
    public void A_count_carries_its_session_and_sequence_on_the_wire_and_an_older_count_carries_neither()
    {
        var session = new UsageSession();
        UsageEvent plain = UsageEvent.For(UsageEventName.ReadStarted, ShelfStory(), true, 1, "1.90.0", AppPlatform.Android, Now);
        UsageEvent stamped = plain with { Session = session.Id, Seq = session.NextSeq() };

        string json = UsageBatch.ToJson([plain, stamped]);

        Assert.Equal(1, Regex.Matches(json, "\"session\"").Count);
        Assert.Contains($"\"session\":\"{session.Id}\",\"seq\":1", json);
        UsageEvent back = UsageBatch.ParseQueue(UsageBatch.QueueToJson([stamped]))[0];
        Assert.Equal(session.Id, back.Session);
        Assert.Equal(1, back.Seq);
        Assert.Null(UsageBatch.ParseQueue(UsageBatch.QueueToJson([plain]))[0].Session);
    }

    [Fact]
    public void Every_stop_reason_has_its_own_closed_detail_and_the_set_is_the_nine_the_Worker_takes()
    {
        string[] details = Enum.GetValues<ReadStopReason>().Select(ReadPlaybackNames.DetailOf).ToArray();

        Assert.Equal(details.Length, details.Distinct().Count());
        Assert.Equal(9, details.Length);
        string source = File.ReadAllText(Path.Combine(RepoRoot(), "worker", "events.ts"));
        Match list = Regex.Match(source, @"READ_STOP_REASONS = \[(?<names>[^\]]*)\]");
        Assert.True(list.Success);
        string[] inWorker = Regex.Matches(list.Groups["names"].Value, "\"(?<name>[a-z_]+)\"").Select(m => m.Groups["name"].Value).ToArray();
        Assert.Equal(details.Order(), inWorker.Order());
    }

    [Theory]
    [InlineData(ReadStopReason.LeftBook, "left_book")]
    [InlineData(ReadStopReason.Finished, "finished")]
    [InlineData(ReadStopReason.Cancelled, "cancelled")]
    [InlineData(ReadStopReason.LanguageChanged, "language_changed")]
    [InlineData(ReadStopReason.Restarted, "restarted")]
    [InlineData(ReadStopReason.WindowStopped, "window_stopped")]
    [InlineData(ReadStopReason.Skipped, "skipped")]
    [InlineData(ReadStopReason.Failed, "failed")]
    [InlineData(ReadStopReason.Other, "other")]
    public void A_stop_reason_maps_to_its_detail(ReadStopReason reason, string detail) => Assert.Equal(detail, ReadPlaybackNames.DetailOf(reason));

    [Theory]
    [InlineData(ReadingEventKind.Started, "read_started")]
    [InlineData(ReadingEventKind.Paused, "read_paused")]
    [InlineData(ReadingEventKind.Resumed, "read_resumed")]
    [InlineData(ReadingEventKind.Stopped, "read_stopped")]
    public void A_playback_verb_maps_to_its_event_name(ReadingEventKind kind, string name) => Assert.Equal(name, ReadPlaybackNames.EventOf(kind));

    [Fact]
    public void The_Worker_takes_the_five_playback_names()
    {
        string source = File.ReadAllText(Path.Combine(RepoRoot(), "worker", "events.ts"));

        foreach (string name in new[] { UsageEventName.ReadStarted, UsageEventName.ReadPaused, UsageEventName.ReadResumed, UsageEventName.ReadStopped, UsageEventName.BookLeft })
        {
            Assert.Contains($"  {name}: {{", source);
        }
    }

    [Theory]
    [InlineData(UsageEventName.ReadStarted)]
    [InlineData(UsageEventName.ReadPaused)]
    [InlineData(UsageEventName.ReadResumed)]
    [InlineData(UsageEventName.ReadStopped)]
    [InlineData(UsageEventName.BookLeft)]
    public void A_demo_read_counts_no_playback_event(string name) => Assert.False(DemoReadCounting.Counts(name, true));

    [Fact]
    public void Every_call_that_stops_a_session_passes_a_ReadStopReason()
    {
        string appFolder = Path.Combine(RepoRoot(), "src", "Dino", "DinoReader.App");
        var calls = new List<string>();
        foreach (string file in Directory.EnumerateFiles(appFolder, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            calls.AddRange(SessionStop().Matches(File.ReadAllText(file)).Select(m => $"{Path.GetFileName(file)}: {m.Value}"));
        }

        Assert.NotEmpty(calls);
        Assert.All(calls, call => Assert.Contains("ReadStopReason.", call));
    }

    [Fact]
    public void A_crash_report_round_trips_its_session_and_a_report_without_one_stays_without()
    {
        string session = new UsageSession().Id;
        var withSession = new CrashReport("dino", "android", "1.90.0", "store", "managed", "T", "m", "", "", "2026-11-04T09:00Z", null, session);
        var without = withSession with { Session = null };

        string json = CrashReportJson.ToJson(withSession);

        Assert.Contains($"\"session\":\"{session}\"", json);
        Assert.Equal(session, CrashReportJson.Parse(json)?.Session);
        Assert.DoesNotContain("session", CrashReportJson.ToJson(without));
        Assert.Null(CrashReportJson.Parse(CrashReportJson.ToJson(without))?.Session);
    }

    [Fact]
    public void The_builder_puts_the_context_session_on_the_report_and_Read_The_Stupid_Text_sends_none()
    {
        string session = new UsageSession().Id;
        var dino = new CrashContext("dino", "android", "1.90.0", InstallChannel.Store, null, session);
        var rtst = new CrashContext("rtst", "android", "1.90.0", InstallChannel.Store, null);

        CrashReport? dinoReport = CrashReportBuilder.FromException(dino, CrashKind.Managed, new InvalidOperationException("x"), "", Now);
        CrashReport? rtstReport = CrashReportBuilder.FromException(rtst, CrashKind.Managed, new InvalidOperationException("x"), "", Now);

        Assert.Equal(session, dinoReport?.Session);
        Assert.Null(rtstReport?.Session);
        Assert.DoesNotContain("session", CrashReportJson.ToJson(rtstReport!));
    }

    [GeneratedRegex(@"[sS]ession\.Stop\([^)]*\)")]
    private static partial Regex SessionStop();

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ReadTheStupidText.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Repo root not found.");
    }
}
