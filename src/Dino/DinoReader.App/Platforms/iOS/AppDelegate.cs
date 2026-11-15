using Foundation;
using Sirous.Reading.Mobile.Shared;

namespace DinoReader;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	// Marks the pack folder as excluded from backups before any download (as the RTST app does); Dino downloads no voice pack since Slice 278, the shared call is harmless.
	public override bool FinishedLaunching(UIKit.UIApplication application, NSDictionary? launchOptions)
	{
		IosPackStorage.ExcludePacksFromBackup();
		_ = StoreKit2Bridge.Load(); // Slice 390: the Transaction.updates listener runs from launch, so an Ask to Buy approval or renewal arriving before the first refresh is not finished unseen (the first refresh reads Apple's list anyway)
		return base.FinishedLaunching(application, launchOptions);
	}

	// Slice 183: the system relaunched the app for a finished background pack download; the downloader calls the handler when its events are done.
	// MauiUIApplicationDelegate derives from UIResponder and only implements IUIApplicationDelegate, so there is no base method to
	// override: the [Export] on this one is what registers the selector (an override fails to compile: CS0115).
	[Export("application:handleEventsForBackgroundURLSession:completionHandler:")]
	public void HandleEventsForBackgroundUrl(UIKit.UIApplication application, string sessionIdentifier, Action completionHandler) =>
		IosBackgroundDownloader.HandleEventsForBackgroundUrl(completionHandler);
}
