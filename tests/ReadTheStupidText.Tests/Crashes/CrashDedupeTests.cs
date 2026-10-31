using System.Text;
using ReadTheStupidText.Application.Crashes;
using ReadTheStupidText.Application.Usage;
using Xunit;

namespace ReadTheStupidText.Tests.Crashes;

/// <summary>Slice 227: one crash gives one report, nothing is kept while reporting may not send, and the caps hold after JSON escaping.</summary>
public sealed class CrashDedupeTests : IDisposable
{
    private static readonly DateTimeOffset Crash = new(2026, 10, 31, 14, 5, 40, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "rtst-dedupe-" + Guid.NewGuid().ToString("N"));

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

    private static CrashReport Managed(string minute = "2026-10-31T14:05Z", string kind = CrashKind.Managed) =>
        new("dino", "android", "1.83.0", "store", kind, "T", "m", "s", "", minute);

    [Fact]
    public void A_native_exit_seconds_after_a_managed_report_is_the_same_crash()
    {
        Assert.True(CrashDedupe.ManagedReportCovers([Managed()], Crash.AddSeconds(3)));
        Assert.True(CrashDedupe.ManagedReportCovers([Managed()], Crash.AddMinutes(1)));
    }

    [Fact]
    public void A_native_exit_far_from_any_managed_report_or_next_to_another_kind_is_its_own_crash()
    {
        Assert.False(CrashDedupe.ManagedReportCovers([Managed()], Crash.AddMinutes(30)));
        Assert.False(CrashDedupe.ManagedReportCovers([Managed(kind: CrashKind.UncleanExit)], Crash));
        Assert.False(CrashDedupe.ManagedReportCovers([Managed("not a time")], Crash));
        Assert.False(CrashDedupe.ManagedReportCovers([], Crash));
    }

    [Fact]
    public void A_run_marker_is_explained_only_by_a_managed_report_written_after_the_app_came_to_the_front()
    {
        DateTimeOffset front = Crash.AddMinutes(-20);

        Assert.True(CrashDedupe.ManagedReportSince([Managed()], front));
        Assert.False(CrashDedupe.ManagedReportSince([Managed("2026-10-31T13:00Z")], front));
        Assert.False(CrashDedupe.ManagedReportSince([], front));
    }

    [Fact]
    public void A_ledger_entry_silences_one_diagnostic_whose_period_covers_it_and_no_more()
    {
        var ledger = new ReportedCrashLedger(Path.Combine(_folder, "reported.txt"));
        ledger.Record("a.json", Crash, Crash);
        ledger.Record("b.json", Crash.AddHours(1), Crash);
        DateTimeOffset begin = Crash.AddHours(-12);
        DateTimeOffset end = Crash.AddHours(12);

        Assert.True(ledger.TryClaim(begin, end, Crash.AddHours(13)));
        Assert.True(ledger.TryClaim(begin, end, Crash.AddHours(13)));
        Assert.False(ledger.TryClaim(begin, end, Crash.AddHours(13)));
    }

    [Fact]
    public void A_ledger_entry_outside_the_period_is_not_claimed_and_a_claimed_one_is_not_added_again()
    {
        var ledger = new ReportedCrashLedger(Path.Combine(_folder, "reported.txt"));
        ledger.Record("a.json", Crash, Crash);

        Assert.False(ledger.TryClaim(Crash.AddDays(1), Crash.AddDays(2), Crash.AddDays(2)));
        Assert.True(ledger.TryClaim(Crash.AddHours(-1), Crash.AddHours(1), Crash.AddHours(2)));
        ledger.Record("a.json", Crash, Crash.AddHours(3));
        Assert.False(ledger.TryClaim(Crash.AddHours(-1), Crash.AddHours(1), Crash.AddHours(3)));
    }

    [Fact]
    public void An_old_ledger_entry_is_forgotten_and_a_cleared_ledger_claims_nothing()
    {
        var ledger = new ReportedCrashLedger(Path.Combine(_folder, "reported.txt"));
        ledger.Record("old.json", Crash, Crash);
        ledger.Record("new.json", Crash.AddDays(5), Crash.AddDays(5));

        Assert.False(ledger.TryClaim(Crash.AddHours(-1), Crash.AddHours(1), Crash.AddDays(5)));

        ledger.Clear();
        Assert.False(ledger.TryClaim(Crash.AddDays(5).AddHours(-1), Crash.AddDays(5).AddHours(1), Crash.AddDays(5)));
    }

    [Fact]
    public void The_log_is_written_only_while_the_gate_allows_and_deleting_it_leaves_a_fresh_start()
    {
        var file = new RollingLogFile(_folder);
        bool allowed = true;
        CrashLog.Use(file, () => allowed);
        var log = RollingFileSystemLog.ToCurrent();

        log.Info("first");
        allowed = false;
        log.Info("while off");
        file.DeleteFiles();
        allowed = true;
        log.Warning("after on");

        string tail = file.Tail();
        Assert.Contains("after on", tail);
        Assert.DoesNotContain("first", tail);
        Assert.DoesNotContain("while off", tail);
    }

    [Fact]
    public void Capping_a_long_multibyte_text_is_one_pass_and_stays_on_a_character()
    {
        string persian = string.Concat(Enumerable.Repeat("سلام😀", 200_000));

        var watch = System.Diagnostics.Stopwatch.StartNew();
        string head = TextCap.Head(persian, 16 * 1024);
        string tail = TextCap.Tail(persian, 32 * 1024);
        watch.Stop();

        Assert.True(watch.ElapsedMilliseconds < 500, $"capping took {watch.ElapsedMilliseconds} ms");
        Assert.InRange(Encoding.UTF8.GetByteCount(head), 16 * 1024 - 4, 16 * 1024);
        Assert.InRange(Encoding.UTF8.GetByteCount(tail), 32 * 1024 - 4, 32 * 1024);
        Assert.False(char.IsHighSurrogate(head[^1]));
        Assert.False(char.IsLowSurrogate(tail[0]));
        Assert.Equal("abc", TextCap.Head("abc", 10));
        Assert.Equal("abc", TextCap.Tail("abc", 10));
        Assert.Equal("bc", TextCap.Tail("abc", 2));
        Assert.Equal("ab", TextCap.Head("abc", 2));
    }

    [Fact]
    public void A_stack_of_quotes_control_characters_and_non_ascii_still_fits_the_workers_body_cap_after_escaping()
    {
        string quotes = new('"', 20000);
        string controls = new('\u0001', 20000);
        string wide = string.Concat(Enumerable.Repeat("😀é", 8000));
        var report = new CrashReport("dino", "ios", "1.83.0", "testflight", "managed", "T", quotes, controls + wide, wide + controls, "2026-10-31T14:05Z");

        string json = CrashReportJson.ToJson(report);

        Assert.True(Encoding.UTF8.GetByteCount(json) <= CrashReportLimits.BodyBytes, $"{Encoding.UTF8.GetByteCount(json)} bytes");
        CrashReport? read = CrashReportJson.Parse(json);
        Assert.NotNull(read);
        Assert.True(read.Stack.Length > 0);
    }

    [Theory]
    [InlineData("Could not translate 'my secret sentence' to Farsi", "Could not translate <text> to Farsi")]
    [InlineData("Bad input \"line one of my diary\" at 3", "Bad input <text> at 3")]
    [InlineData("Unexpected «mon texte privé» token", "Unexpected <text> token")]
    [InlineData("Failed on “private words” now", "Failed on <text> now")]
    [InlineData("can't read it, don't retry", "can't read it, don't retry")]
    public void Quoted_text_in_an_exception_message_becomes_a_placeholder(string message, string expected) =>
        Assert.Equal(expected, LogLineFormatter.ScrubMessage(message));

    [Fact]
    public void A_user_string_inside_an_exception_message_reaches_neither_the_log_line_nor_the_report()
    {
        const string Secret = "my secret diary sentence";
        var error = new InvalidOperationException($"The engine rejected '{Secret}' and \"{Secret}\" and «{Secret}» and “{Secret}”.");

        string line = LogLineFormatter.Format(Crash, 'E', "speech failed", null, error);
        CrashReport? report = CrashReportBuilder.FromException(
            new CrashContext(CrashApp.Dino, "android", "1.83.0", InstallChannel.Store, null), CrashKind.Managed, error, "", Crash);

        Assert.DoesNotContain("secret", line);
        Assert.DoesNotContain("diary", line);
        Assert.NotNull(report);
        Assert.DoesNotContain("secret", report.Message);
        Assert.DoesNotContain("diary", report.Message);
        Assert.Contains("<text>", report.Message);
    }

    [Fact]
    public void A_message_is_cut_to_200_characters_and_never_between_two_halves_of_an_emoji()
    {
        string cut = LogLineFormatter.ScrubMessage(new string('x', 199) + "😀tail");

        Assert.Equal(199, cut.Length);
        Assert.Equal(200, LogLineFormatter.ScrubMessage(new string('y', 900)).Length);
    }
}
