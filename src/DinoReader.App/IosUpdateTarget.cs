using ReadTheStupidText.Application.Crashes;

namespace DinoReader;

/// <summary>Where an iOS install's update button goes and what it says (Slice 276). Pure: the channel comes from the one install-channel source (<c>CrashGate.Channel</c>).</summary>
public static class IosUpdateTarget
{
	/// <summary>Dino Reader's App Store Connect app id (same record for the App Store page and TestFlight).</summary>
	public const string AppId = "6825053435";

	public const string AppStorePage = "https://apps.apple.com/app/id" + AppId;

	/// <summary>Dino's public TestFlight link (also on site/dino/index.html); on a device with TestFlight it opens the app's page there. Confirm on a device.</summary>
	public const string TestFlightPage = "https://testflight.apple.com/join/WnF7M53D";

	/// <summary>The link and the button text key for an install channel. Dino has no App Store app yet, so only a positively App Store install opens the App Store; every other channel (TestFlight, unreadable) opens TestFlight.</summary>
	public static (string Url, string TextKey) For(InstallChannel channel) => channel == InstallChannel.Store
		? (AppStorePage, nameof(Resources.Strings.AppResources.Update_OpenAppStore))
		: (TestFlightPage, nameof(Resources.Strings.AppResources.Update_OpenTestFlight));
}
