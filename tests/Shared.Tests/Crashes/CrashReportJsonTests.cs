using System.Text;
using ReadTheStupidText.Application.Crashes;
using ReadTheStupidText.Application.Usage;
using Xunit;

namespace ReadTheStupidText.Tests.Crashes;

public class CrashReportJsonTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 14, 5, 42, TimeSpan.Zero);
    private static readonly DeviceContext Phone = new(26, "phone", "arm64", "5-8", "4-6");

    private static CrashContext Context(InstallChannel channel = InstallChannel.TestFlight) => new(CrashApp.Dino, "ios", "1.83.0", channel, Phone);

    [Fact]
    public void A_report_round_trips_through_json()
    {
        var report = new CrashReport("dino", "ios", "1.83.0", "testflight", "managed", "System.NullReferenceException", "boom", "at A.B()", "12:00:00.000 I hello", "2026-10-31T14:05Z", Phone);

        CrashReport? read = CrashReportJson.Parse(CrashReportJson.ToJson(report));

        Assert.Equal(report, read);
    }

    [Fact]
    public void Text_fields_are_cut_to_their_byte_caps_and_the_log_keeps_its_newest_end()
    {
        var report = new CrashReport("dino", "android", "1.83.0", "store", "managed",
            new string('t', 500), new string('m', 5000), new string('s', 40000), "OLD" + new string('x', 40000) + "NEWEST", "2026-10-31T14:05Z");

        CrashReport capped = CrashReportJson.Capped(report);

        Assert.Equal(CrashReportLimits.ExceptionTypeBytes, Encoding.UTF8.GetByteCount(capped.ExceptionType));
        Assert.Equal(CrashReportLimits.MessageBytes, Encoding.UTF8.GetByteCount(capped.Message));
        Assert.Equal(CrashReportLimits.StackBytes, Encoding.UTF8.GetByteCount(capped.Stack));
        Assert.True(Encoding.UTF8.GetByteCount(capped.LogTail) <= CrashReportLimits.LogTailBytes);
        Assert.EndsWith("NEWEST", capped.LogTail);
        Assert.DoesNotContain("OLD", capped.LogTail);
    }

    [Fact]
    public void The_body_always_fits_the_workers_limit_even_with_every_field_full_of_escapes()
    {
        string noisy = string.Concat(Enumerable.Repeat("\"\n\\", 20000));
        var report = new CrashReport("rtst", "android", "1.83.0", "store", "managed", "T", noisy, noisy, noisy, "2026-10-31T14:05Z", Phone);

        string json = CrashReportJson.ToJson(report);

        Assert.True(Encoding.UTF8.GetByteCount(json) <= CrashReportLimits.BodyBytes);
        Assert.NotNull(CrashReportJson.Parse(json));
    }

    [Fact]
    public void Non_ascii_text_stays_utf8_and_a_cut_never_splits_a_character()
    {
        string persian = string.Concat(Enumerable.Repeat("سلام😀", 600));

        string cut = TextCap.Head(persian, 1001);

        Assert.True(Encoding.UTF8.GetByteCount(cut) <= 1001);
        Assert.DoesNotContain('�', cut);
        Assert.False(char.IsHighSurrogate(cut[^1]));
        Assert.Contains("سلام", CrashReportJson.ToJson(new CrashReport("dino", "ios", "1.83.0", "store", "managed", "T", "سلام", "", "", "m")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"schemaVersion":2,"app":"dino","platform":"ios","version":"1.0.1","channel":"store","kind":"managed"}""")]
    public void A_damaged_or_foreign_file_parses_to_nothing(string? json) => Assert.Null(CrashReportJson.Parse(json));

    [Fact]
    public void An_exception_report_has_its_type_scrubbed_message_and_stack_and_the_minute()
    {
        Exception error;
        try
        {
            throw new FileNotFoundException("Could not find file '/data/user/0/uk.sirous.dinoreader/files/stories/My Secret Diary.pdf'.");
        }
        catch (Exception ex)
        {
            error = ex;
        }

        CrashReport? report = CrashReportBuilder.FromException(Context(), CrashKind.Managed, error, "tail", Now);

        Assert.NotNull(report);
        Assert.Equal("System.IO.FileNotFoundException", report.ExceptionType);
        Assert.DoesNotContain("Secret", report.Message);
        Assert.Contains("<text>", report.Message);
        Assert.Contains(nameof(An_exception_report_has_its_type_scrubbed_message_and_stack_and_the_minute), report.Stack);
        Assert.Equal("2026-10-31T14:05Z", report.Minute);
        Assert.Equal("testflight", report.Channel);
        Assert.Equal(Phone, report.Device);
    }

    [Fact]
    public void A_dev_install_builds_no_report() =>
        Assert.Null(CrashReportBuilder.FromDescription(Context(InstallChannel.Dev), CrashKind.Native, "SIGSEGV", "m", "s", "t", Now));
}
