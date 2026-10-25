using ReadTheStupidText.Application.Reading;
using Xunit;

namespace ReadTheStupidText.Tests;

public class StorytellerNetworkPolicyTests
{
    [Theory]
    [InlineData(new[] { NetworkProfile.WiFi }, true)]
    [InlineData(new[] { NetworkProfile.Ethernet }, true)]
    [InlineData(new[] { NetworkProfile.WiFi, NetworkProfile.Cellular }, true)]
    [InlineData(new[] { NetworkProfile.Cellular }, false)]
    [InlineData(new[] { NetworkProfile.Other }, false)]
    [InlineData(new NetworkProfile[0], false)]
    public void AllowsAutomaticDownload_FollowsWifiOrEthernet(NetworkProfile[] profiles, bool expected)
    {
        Assert.Equal(expected, StorytellerNetworkPolicy.AllowsAutomaticDownload(profiles));
    }
}
