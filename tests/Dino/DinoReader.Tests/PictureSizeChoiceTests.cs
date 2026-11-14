using System.Text.Json;
using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 375 C: which package of a book a device downloads.</summary>
public sealed class PictureSizeChoiceTests
{
    [Theory]
    [InlineData(PictureDeviceClass.Phone, PictureNetwork.Unmetered, true, PictureSize.Standard)]
    [InlineData(PictureDeviceClass.Phone, PictureNetwork.Limited, true, PictureSize.Standard)]
    [InlineData(PictureDeviceClass.Phone, PictureNetwork.None, true, PictureSize.Standard)]
    [InlineData(PictureDeviceClass.Tablet, PictureNetwork.Unmetered, true, PictureSize.High)]
    [InlineData(PictureDeviceClass.Tablet, PictureNetwork.Limited, true, PictureSize.StandardThenHigh)]
    [InlineData(PictureDeviceClass.Tablet, PictureNetwork.None, true, PictureSize.StandardThenHigh)]
    [InlineData(PictureDeviceClass.Desktop, PictureNetwork.Unmetered, true, PictureSize.High)]
    [InlineData(PictureDeviceClass.Desktop, PictureNetwork.Limited, true, PictureSize.StandardThenHigh)]
    [InlineData(PictureDeviceClass.Tablet, PictureNetwork.Unmetered, false, PictureSize.Standard)]
    [InlineData(PictureDeviceClass.Tablet, PictureNetwork.Limited, false, PictureSize.Standard)]
    [InlineData(PictureDeviceClass.Desktop, PictureNetwork.Unmetered, false, PictureSize.Standard)]
    public void Choose_FollowsTheDeviceTheConnectionAndWhetherABookHasAHighResPack(PictureDeviceClass device, PictureNetwork network, bool hasHighRes, PictureSize expected)
    {
        Assert.Equal(expected, PictureSizeChoice.Choose(device, network, hasHighRes));
    }

    [Theory]
    [InlineData(PictureDevicePlatform.Android, 411, false, PictureDeviceClass.Phone)]
    [InlineData(PictureDevicePlatform.Android, 599.9, false, PictureDeviceClass.Phone)]
    [InlineData(PictureDevicePlatform.Android, 600, false, PictureDeviceClass.Tablet)]
    [InlineData(PictureDevicePlatform.Android, 800, true, PictureDeviceClass.Tablet)]
    [InlineData(PictureDevicePlatform.Android, 500, true, PictureDeviceClass.Phone)] // Android goes by dp, never the idiom
    [InlineData(PictureDevicePlatform.Ios, 0, true, PictureDeviceClass.Tablet)]
    [InlineData(PictureDevicePlatform.Ios, 900, false, PictureDeviceClass.Phone)] // iOS goes by the idiom, never the size
    [InlineData(PictureDevicePlatform.Windows, 300, false, PictureDeviceClass.Desktop)]
    public void DeviceClassOf_UsesTheShorterSideOnAndroidTheIdiomOnIosAndIsDesktopOnWindows(PictureDevicePlatform platform, double shortSideDp, bool idiomTablet, PictureDeviceClass expected)
    {
        Assert.Equal(expected, PictureSizeChoice.DeviceClassOf(platform, shortSideDp, idiomTablet));
    }

    [Fact]
    public void TheTabletLine_IsTheSameSixHundredDpTheLayoutsUse()
    {
        Assert.Equal(PictureDeviceClass.Tablet, PictureSizeChoice.DeviceClassOf(PictureDevicePlatform.Android, LandscapeLayout.TabletShortSide, false));
        Assert.Equal(PictureDeviceClass.Phone, PictureSizeChoice.DeviceClassOf(PictureDevicePlatform.Android, LandscapeLayout.TabletShortSide - 0.5, false));
    }

    [Fact]
    public void TheListing_CarriesTheHighResAddressAndSize_OnlyWhenTheWorkerSendsThem()
    {
        const string Json = """
            {"stories":[
              {"id":"7d0e5a4e000040008000000000000001","title":"A","language":"en","schemaVersion":1,"version":2,"downloadUrl":"/api/stories/x/download?v=2","bytes":3000000,"hdDownloadUrl":"/api/stories/x/download?v=2&size=hd","hdBytes":10000000},
              {"id":"7d0e5a4e000040008000000000000002","title":"B","language":"en","schemaVersion":1,"version":1,"downloadUrl":"/api/stories/y/download?v=1","bytes":3000000},
              {"id":"7d0e5a4e000040008000000000000003","title":"C","language":"en","schemaVersion":1,"version":1,"downloadUrl":"/d","hdDownloadUrl":"  ","hdBytes":"big"}
            ]}
            """;

        IReadOnlyList<StoreItem> items = StoreCatalog.Parse(Json);

        Assert.True(items[0].HasHighRes);
        Assert.Equal("/api/stories/x/download?v=2&size=hd", items[0].HdDownloadUrl);
        Assert.Equal(10_000_000, items[0].BytesFor(PictureSize.High));
        Assert.Equal(3_000_000, items[0].BytesFor(PictureSize.Standard));
        Assert.Equal(3_000_000, items[0].BytesFor(PictureSize.StandardThenHigh));
        Assert.False(items[1].HasHighRes);
        Assert.Equal(3_000_000, items[1].BytesFor(PictureSize.High));
        Assert.False(items[2].HasHighRes);
        Assert.Null(items[2].HdBytes);
    }

    [Fact]
    public void AMalformedListing_StillThrowsOnlyForANonObject()
    {
        Assert.Throws<JsonException>(() => StoreCatalog.Parse("[]"));
    }
}
