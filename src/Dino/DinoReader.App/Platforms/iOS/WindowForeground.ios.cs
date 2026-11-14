using UIKit;

namespace DinoReader;

public static partial class WindowForeground
{
	// Inactive (a notification or Control Centre pulled over the app) still shows the page; only Background hides it, which is what DidEnterBackground (Window.Stopped) raises.
	public static partial bool? IsInForeground() => UIApplication.SharedApplication.ApplicationState != UIApplicationState.Background;
}
