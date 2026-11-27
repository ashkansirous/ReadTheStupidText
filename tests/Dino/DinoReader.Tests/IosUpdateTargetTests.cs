using DinoReader.Resources.Strings;
using Sirous.Reading.Application.Crashes;

namespace DinoReader.Tests;

public class IosUpdateTargetTests
{
    [Theory]
    [InlineData(InstallChannel.Store)]
    [InlineData(InstallChannel.TestFlight)]
    [InlineData(InstallChannel.Dev)]
    public void EveryChannelOpensTheAppStore(InstallChannel channel)
    {
        (string url, string key) = IosUpdateTarget.For(channel);

        Assert.Equal("https://apps.apple.com/app/id6825053435", url);
        Assert.Equal(nameof(AppResources.Update_OpenAppStore), key);
    }
}
