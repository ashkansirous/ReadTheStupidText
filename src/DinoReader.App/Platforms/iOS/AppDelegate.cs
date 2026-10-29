using Foundation;
using ReadTheStupidText.Mobile.Shared;

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

	// Slice 183: the system relaunched the app for a finished background pack download; the downloader calls the handler when its events are done.
	// MauiUIApplicationDelegate derives from UIResponder and only implements IUIApplicationDelegate, so there is no base method to
	// override: the [Export] on this one is what registers the selector (an override fails to compile: CS0115).
	[Export("application:handleEventsForBackgroundURLSession:completionHandler:")]
	public void HandleEventsForBackgroundUrl(UIKit.UIApplication application, string sessionIdentifier, Action completionHandler) =>
		IosBackgroundDownloader.HandleEventsForBackgroundUrl(completionHandler);
}
