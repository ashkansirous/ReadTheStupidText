namespace DinoReader.Core;

/// <summary>What kind of device the app runs on, for the picture size (Slice 375 C). Per device, never per window.</summary>
public enum PictureDeviceClass
{
    Phone,
    Tablet,
    Desktop,
}

/// <summary>The connection, as far as the picture size cares: unmetered (Wi-Fi, Ethernet), limited (mobile data, metered, roaming) or none.</summary>
public enum PictureNetwork
{
    Unmetered,
    Limited,
    None,
}

/// <summary>Which package a book is fetched as: the standard one, the high-res one, or the standard one now and the high-res one later on an unmetered connection (Slice 375 D).</summary>
public enum PictureSize
{
    Standard,
    High,
    StandardThenHigh,
}

/// <summary>The platforms the device class is read from.</summary>
public enum PictureDevicePlatform
{
    Android,
    Ios,
    Windows,
}

/// <summary>
/// Slice 375 C: the one rule for which package of a book a device downloads. A phone always takes the standard pack; a tablet or Windows PC takes the high-res pack on an unmetered connection
/// and the standard one first (high-res later, see <c>HighResUpgrades</c>) on a limited one; a book with no high-res pack is standard. Offline there is no download, so the answer is the same as for a limited
/// connection (the caller never fetches).
/// </summary>
public static class PictureSizeChoice
{
    public static PictureSize Choose(PictureDeviceClass device, PictureNetwork network, bool hasHighRes)
    {
        if (!hasHighRes || device == PictureDeviceClass.Phone)
        {
            return PictureSize.Standard;
        }

        return network == PictureNetwork.Unmetered ? PictureSize.High : PictureSize.StandardThenHigh;
    }

    /// <summary>
    /// The device class of the running device: Android by the screen's shorter side in dp (at least <see cref="LandscapeLayout.TabletShortSide"/>), iOS by the idiom, Windows is a desktop.
    /// <paramref name="shortSideDp"/> is read from the display, not the window.
    /// </summary>
    public static PictureDeviceClass DeviceClassOf(PictureDevicePlatform platform, double shortSideDp, bool idiomIsTablet) => platform switch
    {
        PictureDevicePlatform.Windows => PictureDeviceClass.Desktop,
        PictureDevicePlatform.Ios => idiomIsTablet ? PictureDeviceClass.Tablet : PictureDeviceClass.Phone,
        _ => shortSideDp >= LandscapeLayout.TabletShortSide ? PictureDeviceClass.Tablet : PictureDeviceClass.Phone,
    };
}
