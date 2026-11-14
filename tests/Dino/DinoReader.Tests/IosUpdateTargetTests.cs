using DinoReader.Resources.Strings;
using Sirous.Reading.Application.Crashes;

namespace DinoReader.Tests;

public class IosUpdateTargetTests
{
    [Theory]
    [InlineData(InstallChannel.TestFlight)]
    [InlineData(InstallChannel.Dev)]
    public void AnythingButAnAppStoreInstallOpensTestFlight(InstallChannel channel)
    {
        (string url, string key) = IosUpdateTarget.For(channel);

        Assert.Equal(IosUpdateTarget.TestFlightPage, url);
        Assert.Equal(nameof(AppResources.Update_OpenTestFlight), key);
    }

    [Fact]
    public void AppStoreInstallOpensTheAppStore()
    {
        (string url, string key) = IosUpdateTarget.For(InstallChannel.Store);

        Assert.Equal(IosUpdateTarget.AppStorePage, url);
        Assert.Equal(nameof(AppResources.Update_OpenAppStore), key);
    }
}
