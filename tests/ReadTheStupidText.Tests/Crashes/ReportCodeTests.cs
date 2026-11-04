using ReadTheStupidText.Application.Crashes;
using ReadTheStupidText.Application.Usage;
using ReadTheStupidText.Mobile.Shared.Crashes;
using Xunit;

namespace ReadTheStupidText.Tests.Crashes;

// Slice 249: the reference code and the user report.
public class ReportCodeTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 9, 30, 12, TimeSpan.Zero);
    private static readonly CrashContext Context = new(CrashApp.Dino, "ios", "1.85.3", InstallChannel.TestFlight, new DeviceContext(26, "phone", "arm64", "5-8", "4-6"));

    [Fact]
    public void The_alphabet_has_no_look_alikes()
    {
        foreach (char banned in "01OIL")
        {
            Assert.DoesNotContain(banned, ReportCode.Alphabet);
        }

        Assert.Equal(ReportCode.Alphabet.Length, ReportCode.Alphabet.Distinct().Count());
        Assert.All(ReportCode.Alphabet, c => Assert.True(char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c)));
    }

    [Fact]
    public void A_generated_code_is_eight_characters_from_the_alphabet_and_valid()
    {
        for (int i = 0; i < 200; i++)
        {
            string code = ReportCode.Generate();
            Assert.Equal(ReportCode.Length, code.Length);
            Assert.All(code, c => Assert.Contains(c, ReportCode.Alphabet));
            Assert.True(ReportCode.IsValid(code));
        }
    }

    [Fact]
    public void Codes_differ_between_calls()
    {
        Assert.True(Enumerable.Range(0, 50).Select(_ => ReportCode.Generate()).Distinct().Count() > 45);
    }

    [Fact]
    public void Every_alphabet_character_can_be_picked()
    {
        int next = 0;
        string all = string.Concat(Enumerable.Range(0, 4).Select(_ => ReportCode.Generate(_ => next++ % ReportCode.Alphabet.Length)));
        Assert.Equal(ReportCode.Alphabet.Length, all.Distinct().Count());
    }

    [Fact]
    public void A_code_is_shown_in_two_groups_of_four()
    {
        Assert.Equal("K7QX-2MRD", ReportCode.Display("K7QX2MRD"));
        Assert.Equal("not a code", ReportCode.Display("not a code"));
    }

    [Theory]
    [InlineData("k7qx-2mrd", "K7QX2MRD")]
    [InlineData(" K7QX 2MRD ", "K7QX2MRD")]
    [InlineData("K7QX2MRD", "K7QX2MRD")]
    public void A_typed_code_ignores_dash_space_and_case(string typed, string expected) => Assert.Equal(expected, ReportCode.Normalize(typed));

    [Theory]
    [InlineData("")]
    [InlineData("K7QX2MR")]
    [InlineData("K7QX2MRDD")]
    [InlineData("K7QX2MR0")]
    [InlineData("K7QX2MRO")]
    [InlineData("K7QX2MR1")]
    [InlineData("K7QX2MRI")]
    [InlineData("K7QX2MRL")]
    [InlineData("k7qx2mrd")]
    [InlineData("K7QX-2MRD")]
    public void A_wrong_code_is_not_valid(string code) => Assert.False(ReportCode.IsValid(code));

    [Fact]
    public void The_message_round_trips_the_code_and_nothing_else_reads_as_one()
    {
        Assert.Equal("user report K7QX2MRD", ReportCode.MessageFor("K7QX2MRD"));
        Assert.Equal("K7QX2MRD", ReportCode.FromMessage("user report K7QX2MRD"));
        Assert.Null(ReportCode.FromMessage("user report k7qx2mrd"));
        Assert.Null(ReportCode.FromMessage("Slice 227 crash test"));
        Assert.Null(ReportCode.FromMessage(string.Empty));
    }

    [Fact]
    public void A_user_report_has_the_kind_the_code_the_log_tail_and_the_build_facts_but_no_exception_or_stack()
    {
        CrashReport? report = UserReportBuilder.Build(Context, "K7QX2MRD", "09:29:58.000 I page shown ReadingPage", Now);

        Assert.NotNull(report);
        Assert.Equal(CrashKind.UserReport, report.Kind);
        Assert.Equal("user_report", report.Kind);
        Assert.Equal("user report K7QX2MRD", report.Message);
        Assert.Equal(string.Empty, report.Stack);
        Assert.Equal("09:29:58.000 I page shown ReadingPage", report.LogTail);
        Assert.Equal(("dino", "ios", "1.85.3", "testflight", "2026-11-04T09:30Z"), (report.App, report.Platform, report.Version, report.Channel, report.Minute));
        Assert.Equal(Context.Device, report.Device);
    }

    [Fact]
    public void A_dev_install_or_a_bad_code_builds_no_report()
    {
        Assert.Null(UserReportBuilder.Build(Context with { Channel = InstallChannel.Dev }, "K7QX2MRD", string.Empty, Now));
        Assert.Null(UserReportBuilder.Build(Context, "K7QX2MR0", string.Empty, Now));
    }

    [Fact]
    public void A_user_report_survives_the_json_round_trip_within_the_body_cap()
    {
        string tail = string.Join('\n', Enumerable.Range(0, 3000).Select(i => $"09:29:{i % 60:D2}.000 I page shown ReadingPage"));
        CrashReport report = UserReportBuilder.Build(Context, "K7QX2MRD", tail, Now)!;

        string json = CrashReportJson.ToJson(report);
        CrashReport? read = CrashReportJson.Parse(json);

        Assert.True(System.Text.Encoding.UTF8.GetByteCount(json) <= CrashReportLimits.BodyBytes);
        Assert.Equal(CrashKind.UserReport, read?.Kind);
        Assert.Equal("user report K7QX2MRD", read?.Message);
        Assert.EndsWith("ReadingPage", read?.LogTail);
    }
}
