using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 262: the network permission of a shelf audio run is decided once from the voice request's outcome.
public sealed class ShelfAudioNetworkTests
{
    [Theory]
    [InlineData(VoiceRequestOutcome.DownloadNow, true)]
    [InlineData(VoiceRequestOutcome.WaitForWifi, false)]
    [InlineData(VoiceRequestOutcome.Started, false)]
    [InlineData(VoiceRequestOutcome.NothingAsked, false)]
    [InlineData(VoiceRequestOutcome.Cancelled, false)]
    public void Only_download_now_allows_mobile_data(VoiceRequestOutcome outcome, bool expected) =>
        Assert.Equal(expected, ShelfAudioNetwork.AllowsMobileData(outcome));

    [Fact]
    public void A_cancelled_request_starts_no_run()
    {
        Assert.False(ShelfAudioNetwork.ShouldStartRun(VoiceRequestOutcome.Cancelled));
        Assert.True(ShelfAudioNetwork.ShouldStartRun(VoiceRequestOutcome.WaitForWifi));
        Assert.True(ShelfAudioNetwork.ShouldStartRun(VoiceRequestOutcome.DownloadNow));
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    public void Waits_for_wifi_only_on_mobile_data_only_without_permission(bool allow, bool mobileOnly, bool expected) =>
        Assert.Equal(expected, ShelfAudioNetwork.NeedsWifi(allow, mobileOnly));
}

// Slice 262: the one rule for starting a run.
public sealed class ShelfAudioStartTests
{
    [Theory]
    [InlineData(false, ShelfAudioStartDecision.Start)]
    [InlineData(true, ShelfAudioStartDecision.Skip)]
    public void A_quiet_run_needs_no_cancel_and_no_installed_voice(bool cancelled, ShelfAudioStartDecision expected) =>
        Assert.Equal(expected, ShelfAudioStart.Decide(cancelled, visible: false, VoiceRequestOutcome.NothingAsked));

    [Theory]
    [InlineData(VoiceRequestOutcome.NothingAsked, ShelfAudioStartDecision.Start)]
    [InlineData(VoiceRequestOutcome.Started, ShelfAudioStartDecision.Start)]
    [InlineData(VoiceRequestOutcome.DownloadNow, ShelfAudioStartDecision.Start)]
    [InlineData(VoiceRequestOutcome.WaitForWifi, ShelfAudioStartDecision.Start)]
    [InlineData(VoiceRequestOutcome.Cancelled, ShelfAudioStartDecision.Skip)]
    public void A_visible_run_follows_the_voice_request_even_when_cancelled_before_or_voice_not_ready(VoiceRequestOutcome outcome, ShelfAudioStartDecision expected) =>
        Assert.Equal(expected, ShelfAudioStart.Decide(cancelled: true, visible: true, outcome));
}
