using ReadTheStupidText.Application.Net;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText_App;

namespace ReadTheStupidText.Tests;

/// <summary>Slice 373 (Missing States mock RT2): what each language row shows, from the one pure row rule.</summary>
public class PackRowTextTests
{
    private const string NotDownloaded = "not downloaded";

    private static RowText Row(bool isRunRow, PackSetupState state, bool? installed, bool canRetry = false) =>
        PackRowText.Compose(isRunRow, new RunView(state, 0.5, state == PackSetupState.Failed ? HttpErrorKind.NotFound : null, canRetry),
            installed, NotDownloaded, r => $"state:{r.State}", r => $"error:{r.State}");

    [Theory]
    [InlineData(PackSetupState.Failed)]
    [InlineData(PackSetupState.Idle)]
    public void Stale_failed_or_idle_publish_keeps_a_start_and_an_escaped_failure(PackSetupState state)
    {
        RunFlags flags = RunStateRule.OnPublish(new RunFlags(true, true), state);

        Assert.Equal(new RunFlags(true, true), flags);
        Assert.Equal(RunShown.Resolving, RunStateRule.Decide(flags));
    }

    [Theory]
    [InlineData(PackSetupState.Resolving)]
    [InlineData(PackSetupState.Downloading)]
    [InlineData(PackSetupState.Ready)]
    public void Live_publish_ends_the_start_and_clears_the_escaped_failure(PackSetupState state)
    {
        RunFlags flags = RunStateRule.OnPublish(new RunFlags(true, true), state);

        Assert.Equal(RunShown.Installer, RunStateRule.Decide(flags));
    }

    [Fact]
    public void Escaped_failure_shows_until_a_live_publish()
    {
        Assert.Equal(RunShown.EscapedFailure, RunStateRule.Decide(new RunFlags(false, true)));
        Assert.Equal(RunShown.EscapedFailure, RunStateRule.Decide(RunStateRule.OnPublish(new RunFlags(false, true), PackSetupState.Failed)));
    }

    [Theory]
    [InlineData(false, NotDownloaded)]
    [InlineData(true, "")]
    [InlineData(null, "")]
    public void Other_row_shows_only_not_downloaded_when_known_not_installed(bool? installed, string detail)
    {
        RowText row = Row(false, PackSetupState.Failed, installed, canRetry: true);

        Assert.Equal(new RowText(detail, string.Empty, false), row);
    }

    [Fact]
    public void Failed_run_row_shows_error_and_retry_and_not_downloaded_only_when_not_installed()
    {
        Assert.Equal(new RowText(NotDownloaded, "error:Failed", true), Row(true, PackSetupState.Failed, false, canRetry: true));
        Assert.Equal(new RowText(string.Empty, "error:Failed", true), Row(true, PackSetupState.Failed, true, canRetry: true));
        Assert.Equal(new RowText(string.Empty, "error:Failed", true), Row(true, PackSetupState.Failed, null, canRetry: true));
    }

    [Fact]
    public void Starting_run_shown_as_resolving_has_no_retry_and_no_error()
    {
        RowText row = Row(true, PackSetupState.Resolving, false, canRetry: false);

        Assert.Equal(new RowText("state:Resolving", string.Empty, false), row);
    }

    [Fact]
    public void Failed_row_of_the_previous_language_is_not_the_new_run_row()
    {
        Assert.Equal(new RowText(NotDownloaded, string.Empty, false), Row(false, PackSetupState.Failed, false, canRetry: true));
    }

    [Fact]
    public void Idle_run_row_follows_the_installed_state()
    {
        Assert.Equal(new RowText(NotDownloaded, string.Empty, false), Row(true, PackSetupState.Idle, false));
        Assert.Equal(new RowText(string.Empty, string.Empty, false), Row(true, PackSetupState.Idle, true));
    }
}
