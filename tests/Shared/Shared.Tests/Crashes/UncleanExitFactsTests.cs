using Sirous.Reading.Application.Crashes;
using Xunit;

namespace ReadTheStupidText.Tests.Crashes;

/// <summary>Slice 424 items 3 to 6: the unclean exit's own facts, a current log tail, one death one report.</summary>
public sealed class UncleanExitFactsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "rtst-unclean-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, true);
        }
        catch (IOException)
        {
            // a temp folder left behind is harmless
        }
    }

    [Fact]
    public void A_buffered_line_reaches_the_disk_on_the_next_append_after_the_flush_interval()
    {
        long now = 1000;
        using var log = new RollingLogFile(_folder, flushEvery: TimeSpan.FromSeconds(5), nowMilliseconds: () => now);
        log.Append("one", flush: false);
        Assert.Equal(string.Empty, new RollingLogFile(_folder).Tail()); // still buffered

        now = 5999;
        log.Append("two", flush: false);
        Assert.Equal(string.Empty, new RollingLogFile(_folder).Tail()); // 4.999 s since the file was opened: not due

        now = 6000;
        log.Append("three", flush: false);
        Assert.Equal("one\ntwo\nthree", new RollingLogFile(_folder).Tail());
    }

    [Fact]
    public void A_zero_interval_turns_the_timed_flush_off()
    {
        long now = 0;
        using var log = new RollingLogFile(_folder, flushEvery: TimeSpan.Zero, nowMilliseconds: () => now);
        log.Append("one", flush: false);
        now = 1_000_000;
        log.Append("two", flush: false);

        Assert.Equal(string.Empty, new RollingLogFile(_folder).Tail());
    }

    [Fact]
    public void The_timer_tick_writes_a_quiet_apps_last_line_and_does_nothing_when_nothing_is_buffered()
    {
        // The timer's tick is called directly (a clock is injected, so no real timer runs): no waiting, no polling.
        using var log = new RollingLogFile(_folder, flushEvery: TimeSpan.FromSeconds(5), nowMilliseconds: () => 1000);
        log.FlushIfDirty();
        log.Append("last line before the kill", flush: false);
        Assert.Equal(string.Empty, new RollingLogFile(_folder).Tail()); // still buffered

        log.FlushIfDirty();
        Assert.Equal("last line before the kill", new RollingLogFile(_folder).Tail());
        log.FlushIfDirty();
    }

    [Fact]
    public void The_same_launch_and_tail_is_reported_once_so_one_death_gives_one_report()
    {
        var store = new PendingCrashStore(_folder);
        var launch = new DateTimeOffset(2026, 11, 20, 14, 3, 0, TimeSpan.Zero);
        const string Tail = "a\nb\nmemory warning Background";

        Assert.False(store.IsUncleanReported(Tail, launch));
        store.NoteUncleanReported(Tail, launch);
        Assert.True(store.IsUncleanReported(Tail, launch));
        Assert.True(new PendingCrashStore(_folder).IsUncleanReported(Tail, launch)); // after a restart of the app too
        Assert.False(store.IsUncleanReported("a\nb\nc\nanother death", launch));
    }

    [Fact]
    public void The_same_tail_from_another_launch_is_another_death_and_still_reports()
    {
        var store = new PendingCrashStore(_folder);
        const string Tail = "a\nb\nmemory warning Background";

        store.NoteUncleanReported(Tail, new DateTimeOffset(2026, 11, 20, 14, 3, 0, TimeSpan.Zero));

        Assert.False(store.IsUncleanReported(Tail, new DateTimeOffset(2026, 11, 20, 14, 3, 2, TimeSpan.Zero))); // died within seconds, wrote nothing new
    }

    [Fact]
    public void A_report_that_was_not_stored_leaves_no_note_so_the_next_death_is_not_hidden()
    {
        var store = new PendingCrashStore(_folder);
        var launch = new DateTimeOffset(2026, 11, 20, 14, 3, 0, TimeSpan.Zero);

        Assert.False(store.IsUncleanReported("tail", launch)); // asked, but NoteUncleanReported is only called after the save
        Assert.False(store.IsUncleanReported("tail", launch));
    }

    [Fact]
    public void An_empty_tail_or_unknown_launch_cannot_be_told_apart_and_is_never_a_repeat_and_clear_forgets_the_note()
    {
        var store = new PendingCrashStore(_folder);
        var launch = DateTimeOffset.UtcNow;

        store.NoteUncleanReported(string.Empty, launch);
        store.NoteUncleanReported("tail", null);
        Assert.False(store.IsUncleanReported(string.Empty, launch));
        Assert.False(store.IsUncleanReported("tail", null));
        store.NoteUncleanReported("tail", launch);
        Assert.True(store.IsUncleanReported("tail", launch));
        store.Clear();
        Assert.False(store.IsUncleanReported("tail", launch));
    }

    [Fact]
    public void The_facts_file_keeps_the_session_last_page_and_state_and_the_report_context_carries_them()
    {
        var file = new LaunchFactsFile(_folder);
        var launch = new LaunchFacts("1.136.0", "1136000", null);
        LaunchFacts begun = file.Begin(launch, DateTimeOffset.UtcNow);
        file.Write(begun.WithState("0123456789abcdef0123456789abcdef", "SettingsPage", CrashAppState.Foreground));
        file.Write(file.ReadCurrent()!.WithState(null, "BooksPage", null)); // a later page keeps the session and state

        LaunchFacts? read = file.ReadCurrent();
        Assert.Equal("0123456789abcdef0123456789abcdef", read!.Session);
        Assert.Equal("BooksPage", read.LastPage);
        Assert.Equal(CrashAppState.Foreground, read.State);

        var now = new CrashContext("dino", "android", "1.137.0", InstallChannel.Store, null, "ffffffffffffffffffffffffffffffff", new CrashFacts("1137000", null, "256-512", "<128", CrashAppState.Background));
        CrashContext about = LaunchFacts.About(now, read);
        Assert.Equal("0123456789abcdef0123456789abcdef", about.Session);
        Assert.Equal(CrashAppState.Foreground, about.Facts!.AppState);
        Assert.Null(about.Facts.MemoryHeadroom); // the running launch's memory never describes the dead one
    }

    [Fact]
    public void Exit_counts_name_only_abnormal_exits_with_a_count_and_feed_the_unclean_exit_message()
    {
        Assert.Null(CrashReportBuilder.ExitCountsText([("foreground watchdog", 0), ("background memory limit", 0)]));
        string? text = CrashReportBuilder.ExitCountsText([("foreground watchdog", 0), ("background memory limit", 2), ("background watchdog", 1)]);

        Assert.Equal("context, not this exit's cause - exits in the last day: background memory limit 2, background watchdog 1", text);
        Assert.EndsWith("; " + text, CrashReportBuilder.UncleanExitMessage(text, "ReadingPage"));
    }

    [Fact]
    public void A_worst_case_unclean_exit_message_still_fits_and_keeps_the_exit_reason()
    {
        string reason = "exit reason SIGNALED, importance 100, description " + new string('d', 400);
        string message = CrashReportBuilder.UncleanExitMessage(reason, new string('P', 300));

        Assert.True(message.Length <= 200);
        Assert.Contains("; exit reason SIGNALED, importance 100, description d", message);
        Assert.Equal(message, LogLineFormatter.ScrubMessage(message));
    }

    [Fact]
    public void The_unclean_exit_message_names_the_last_page_and_the_system_exit_when_known()
    {
        Assert.Equal("The run marker was left behind", CrashReportBuilder.UncleanExitMessage(null, null));
        Assert.Equal("The run marker was left behind; last page ReadingPage", CrashReportBuilder.UncleanExitMessage(" ", "ReadingPage"));
        Assert.Equal(
            "The run marker was left behind; last page SettingsPage; exit reason UserRequested, importance 100, description none",
            CrashReportBuilder.UncleanExitMessage("exit reason UserRequested, importance 100, description none", "SettingsPage"));
    }

    [Theory]
    [InlineData(0, "Unknown")]
    [InlineData(1, "ExitSelf")]
    [InlineData(2, "Signaled")]
    [InlineData(3, "LowMemory")]
    [InlineData(4, "Crash")]
    [InlineData(5, "CrashNative")]
    [InlineData(6, "Anr")]
    [InlineData(10, "UserRequested")]
    [InlineData(16, "PackageUpdated")]
    [InlineData(17, "17")]
    [InlineData(-1, "-1")]
    public void An_android_exit_reason_code_is_named_and_an_unknown_one_is_its_number(int code, string name)
    {
        Assert.Equal(name, CrashReportBuilder.AndroidExitReasonName(code));
    }

    [Fact]
    public void The_android_exit_text_names_the_reason_and_status_and_leaves_out_an_empty_description()
    {
        Assert.Equal("exit reason ExitSelf, status 137, importance 100", CrashReportBuilder.AndroidExitText(1, 137, 100, null));
        Assert.Equal("exit reason ExitSelf, status 137, importance 100", CrashReportBuilder.AndroidExitText(1, 137, 100, "  "));
        Assert.Equal("exit reason Anr, status 0, importance 125, description input dispatching timed out", CrashReportBuilder.AndroidExitText(6, 0, 125, "input dispatching timed out"));
    }
}
