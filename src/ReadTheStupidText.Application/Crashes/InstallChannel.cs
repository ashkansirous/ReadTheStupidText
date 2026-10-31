namespace ReadTheStupidText.Application.Crashes;

/// <summary>Where this install came from (Slice 227): the crash gate's view. Crash reports accept <see cref="Store"/> and <see cref="TestFlight"/> (the user's exception, for crash reports only); usage counts keep their own, older rule (<see cref="UsageFacts.IsNonStoreInstall"/>).</summary>
public enum InstallChannel
{
    /// <summary>The store the person installed from: Google Play, the App Store.</summary>
    Store,

    /// <summary>An iOS build that is not an App Store install: TestFlight, App Review, or a receipt that could not be read. iOS never returns <see cref="Dev"/>.</summary>
    TestFlight,

    /// <summary>Android only: a sideloaded APK, adb, or another store; its installer could not be read. Sends no crash reports.</summary>
    Dev,
}

/// <summary>Decides the <see cref="InstallChannel"/> from the one fact each platform can read, and names it for the Worker (Slice 227). Pure, so the rules are tested.</summary>
public static class InstallChannels
{
    /// <summary>The last path component of the iOS app store receipt for an App Store install.</summary>
    public const string IosStoreReceipt = "receipt";

    /// <summary>The last path component of the iOS receipt for a TestFlight (or App Review, or sandbox) install.</summary>
    public const string IosSandboxReceipt = "sandboxReceipt";

    /// <summary>The package that installs from Google Play.</summary>
    public const string PlayStorePackage = "com.android.vending";

    public const string StoreName = "store";
    public const string TestFlightName = "testflight";

    /// <summary>
    /// iOS crash channel: <c>receipt</c> is the App Store; anything else (<c>sandboxReceipt</c>, a missing receipt, an unknown name) is TestFlight. Debug builds,
    /// simulators and our harnesses are dropped by the other rules, so a beta build whose receipt cannot be read still reports, tagged <c>testflight</c>.
    /// StoreKit 2's <c>AppTransaction.environment</c> is Swift-only and is not bound in .NET for iOS, so the receipt name is the signal.
    /// </summary>
    public static InstallChannel FromIosReceipt(string? lastPathComponent) =>
        lastPathComponent == IosStoreReceipt ? InstallChannel.Store : InstallChannel.TestFlight;

    /// <summary>
    /// The usage-count rule for iOS, unchanged since Slice 179: only a <c>sandboxReceipt</c> is not a store install. A missing or other name still counts,
    /// because dropping those would hide App Store installs whose receipt is named differently.
    /// </summary>
    public static bool IsIosSandboxReceipt(string? lastPathComponent) => lastPathComponent == IosSandboxReceipt;

    /// <summary>Android: an install whose installer is Google Play is <see cref="InstallChannel.Store"/>; anything else (sideload, adb, another store) is <see cref="InstallChannel.Dev"/>.</summary>
    public static InstallChannel FromAndroidInstaller(string? installerPackage) =>
        installerPackage == PlayStorePackage ? InstallChannel.Store : InstallChannel.Dev;

    /// <summary>The name the Worker's closed list uses; <see cref="InstallChannel.Dev"/> has none because a dev install never sends.</summary>
    public static string? NameOf(InstallChannel channel) => channel switch
    {
        InstallChannel.Store => StoreName,
        InstallChannel.TestFlight => TestFlightName,
        _ => null,
    };
}
