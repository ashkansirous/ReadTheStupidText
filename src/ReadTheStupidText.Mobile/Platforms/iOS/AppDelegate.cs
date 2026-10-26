using Foundation;
using ReadTheStupidText.Mobile.Shared;

// Namespace matches the Android files (and the rest of Mobile), not
// "...Platforms.iOS" — see the namespace note in AndroidSpeechReader.cs.
namespace ReadTheStupidText.Mobile;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	// The explicit startup step: marks the packs folder as excluded from backups before any download.
	public override bool FinishedLaunching(UIKit.UIApplication application, NSDictionary? launchOptions)
	{
		IosPackStorage.ExcludePacksFromBackup();
		return base.FinishedLaunching(application, launchOptions);
	}
}
