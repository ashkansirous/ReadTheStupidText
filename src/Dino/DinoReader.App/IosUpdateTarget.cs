using Sirous.Reading.Application.Crashes;

namespace DinoReader;

/// <summary>Where an iOS install's update button goes and what it says (Slice 276). Pure: the channel comes from the one install-channel source (<c>CrashGate.Channel</c>).</summary>
public static class IosUpdateTarget
{
	/// <summary>Dino Reader's App Store Connect app id (same record for the App Store page and TestFlight).</summary>
	public const string AppId = "6825053435";

	public const string AppStorePage = "https://apps.apple.com/app/id" + AppId;

	/// <summary>The link and the button text key for an install channel. Dino's public TestFlight link is switched off (Slice 417 item 4), so every channel (App Store, TestFlight, unreadable) opens the App Store page.</summary>
	public static (string Url, string TextKey) For(InstallChannel channel) =>
		(AppStorePage, nameof(Resources.Strings.AppResources.Update_OpenAppStore));
}
