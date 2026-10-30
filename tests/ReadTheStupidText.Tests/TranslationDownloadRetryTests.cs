using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Translation;

namespace ReadTheStupidText.Tests;

public class TranslationDownloadRetryTests
{
    [Theory]
    [InlineData(true, true, TranslationDownloadState.Done)]
    [InlineData(true, false, TranslationDownloadState.Done)]
    [InlineData(false, true, TranslationDownloadState.Failed)]
    [InlineData(false, false, TranslationDownloadState.WaitingForInternet)]
    public void An_ended_attempt_turns_into_the_next_state(bool succeeded, bool online, TranslationDownloadState expected)
    {
        Assert.Equal(expected, TranslationDownloadRetry.AfterAttempt(TranslationDownloadState.Downloading, succeeded, online));
    }

    [Fact]
    public void An_attempt_the_network_restarted_is_judged_by_the_internet_when_it_ended_too()
    {
        Assert.Equal(TranslationDownloadState.WaitingForInternet, TranslationDownloadRetry.AfterAttempt(TranslationDownloadState.Downloading, false, online: false));
        Assert.Equal(TranslationDownloadState.Failed, TranslationDownloadRetry.AfterAttempt(TranslationDownloadState.Downloading, false, online: true));
    }

    [Theory]
    [InlineData(TranslationDownloadState.Done)]
    [InlineData(TranslationDownloadState.Failed)]
    [InlineData(TranslationDownloadState.Cancelled)]
    public void A_final_state_never_moves(TranslationDownloadState state)
    {
        Assert.Equal(state, TranslationDownloadRetry.AfterAttempt(state, false, false));
        Assert.Equal(state, TranslationDownloadRetry.AfterAttempt(state, true, true));
        Assert.Equal(state, TranslationDownloadRetry.AfterCancel(state));
        Assert.False(TranslationDownloadRetry.CanRetry(state));
        Assert.False(TranslationDownloadRetry.ShouldRestartOnNetwork(state, true, [NetworkProfile.WiFi], true));
    }

    [Fact]
    public void Cancel_ends_a_running_or_waiting_download()
    {
        Assert.Equal(TranslationDownloadState.Cancelled, TranslationDownloadRetry.AfterCancel(TranslationDownloadState.Downloading));
        Assert.Equal(TranslationDownloadState.Cancelled, TranslationDownloadRetry.AfterCancel(TranslationDownloadState.WaitingForInternet));
    }

    [Fact]
    public void Only_a_waiting_download_can_be_retried()
    {
        Assert.True(TranslationDownloadRetry.CanRetry(TranslationDownloadState.WaitingForInternet));
        Assert.False(TranslationDownloadRetry.CanRetry(TranslationDownloadState.Downloading));
    }

    [Theory]
    [InlineData(NetworkProfile.WiFi, false, true)]
    [InlineData(NetworkProfile.Ethernet, false, true)]
    [InlineData(NetworkProfile.Cellular, false, false)]
    [InlineData(NetworkProfile.Cellular, true, true)]
    public void A_network_change_restarts_on_wifi_or_when_mobile_data_was_accepted(NetworkProfile profile, bool mobileDataAllowed, bool expected)
    {
        Assert.Equal(expected, TranslationDownloadRetry.ShouldRestartOnNetwork(TranslationDownloadState.WaitingForInternet, true, [profile], mobileDataAllowed));
    }

    [Fact]
    public void No_restart_while_still_offline_or_while_downloading()
    {
        Assert.False(TranslationDownloadRetry.ShouldRestartOnNetwork(TranslationDownloadState.WaitingForInternet, false, [NetworkProfile.WiFi], true));
        Assert.False(TranslationDownloadRetry.ShouldRestartOnNetwork(TranslationDownloadState.Downloading, true, [NetworkProfile.WiFi], true));
    }
}
