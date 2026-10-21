using Foundation;
using ReadTheStupidText.Mobile;

namespace DinoReader;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	// Marks the voice-pack folder as excluded from backups before any download (as the RTST app does).
	public override bool FinishedLaunching(UIKit.UIApplication application, NSDictionary? launchOptions)
	{
		IosPackStorage.ExcludePacksFromBackup();
		return base.FinishedLaunching(application, launchOptions);
	}
}
