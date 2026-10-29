using ReadTheStupidText.Application.Reading;
using Xunit;

namespace ReadTheStupidText.Tests;

public class PackFailureDecisionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Offline_keeps_the_language_ticked_and_waiting(bool needsUserRetry) =>
        Assert.Equal(PackFailureAction.KeepTickedAndWait, PackFailureDecision.Decide(PackDownloadResult.Offline, needsUserRetry));

    [Fact]
    public void A_failure_with_its_automatic_retry_still_to_come_waits() =>
        Assert.Equal(PackFailureAction.KeepTickedAndWait, PackFailureDecision.Decide(PackDownloadResult.Failed, false));

    [Fact]
    public void A_failure_after_the_retry_is_spent_unticks() =>
        Assert.Equal(PackFailureAction.UntickAndShowRetry, PackFailureDecision.Decide(PackDownloadResult.Failed, true));

    [Theory]
    [InlineData(PackDownloadResult.HashMismatch)]
    [InlineData(PackDownloadResult.InvalidPack)]
    [InlineData(PackDownloadResult.Ok)] // a plain exception leaves no result
    public void A_failure_nothing_retries_unticks(PackDownloadResult last) =>
        Assert.Equal(PackFailureAction.UntickAndShowRetry, PackFailureDecision.Decide(last, false));

    [Fact]
    public void A_dropped_connection_is_a_network_loss()
    {
        Assert.True(PackFailureDecision.IsNetworkLoss(new HttpRequestException("gone")));
        Assert.True(PackFailureDecision.IsNetworkLoss(new System.Net.Sockets.SocketException()));
        Assert.True(PackFailureDecision.IsNetworkLoss(new System.IO.IOException("read", new System.Net.Sockets.SocketException())));
    }

    [Fact]
    public void Other_exceptions_are_not_a_network_loss()
    {
        Assert.False(PackFailureDecision.IsNetworkLoss(new InvalidOperationException()));
        Assert.False(PackFailureDecision.IsNetworkLoss(new System.IO.IOException("disk full")));
        Assert.False(PackFailureDecision.IsNetworkLoss(new HttpRequestException("not found", null, System.Net.HttpStatusCode.NotFound)));
        Assert.False(PackFailureDecision.IsNetworkLoss(new HttpRequestException("server", null, System.Net.HttpStatusCode.InternalServerError)));
    }
}
