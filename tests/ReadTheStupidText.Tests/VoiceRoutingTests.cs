using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class VoiceRoutingTests
{
    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    [InlineData(true, true, null, false)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    public void PhoneVoiceGivesWayOnlyWhenKnownUnableAndPackReady(bool chosen, bool packReady, bool? canRead, bool expected)
    {
        Assert.Equal(expected, VoiceRouting.ShouldLeavePhoneVoice(chosen, packReady, canRead));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    public void PhoneReadsWhenChosenOrNoPackIsReady(bool chosen, bool packReady, bool expected)
    {
        Assert.Equal(expected, VoiceRouting.PhoneReads(chosen, packReady));
    }

    [Theory]
    [InlineData(true, false, false, VoiceRoute.NoVoice)]
    [InlineData(true, false, true, VoiceRoute.Downloading)]
    [InlineData(true, true, true, VoiceRoute.Read)]
    [InlineData(true, null, false, VoiceRoute.Read)]
    [InlineData(false, false, false, VoiceRoute.Read)]
    public void RouteBlocksOnlyAKnownMissingPhoneVoice(bool phoneReads, bool? canRead, bool downloading, VoiceRoute expected)
    {
        Assert.Equal(expected, VoiceRouting.Route(phoneReads, canRead, downloading));
    }
}
