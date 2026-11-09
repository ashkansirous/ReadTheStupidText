using UIKit;

namespace DinoReader;

internal static partial class SystemBarIcons
{
	private const int RetryDelayMs = 300;
	private const int MaxRetries = 10;

	// Per-page status-bar style (Slice 325 A4): the top page's view controller (StatusBarPageViewController) answers PreferredStatusBarStyle, so only the icons change;
	// the window's interface style and MAUI's Light UserAppTheme pin are never touched. Runs on the main thread; with no key window yet (launch, a modal coming up) it tries again shortly.
	static partial void ApplyPlatform(bool darkIcons) => MainThread.BeginInvokeOnMainThread(() => TryApply(darkIcons, 0));

	private static void TryApply(bool darkIcons, int attempt)
	{
		UIViewController? top = TopViewController();
		if (top is null)
		{
			if (attempt < MaxRetries)
			{
				Dispatcher.GetForCurrentThread()?.DispatchDelayed(TimeSpan.FromMilliseconds(RetryDelayMs), () => TryApply(darkIcons, attempt + 1));
			}

			return;
		}

		if (top is StatusBarPageViewController page)
		{
			page.DarkIcons = darkIcons;
		}

		top.SetNeedsStatusBarAppearanceUpdate();
	}

	private static UIViewController? TopViewController()
	{
		UIViewController? top = UIApplication.SharedApplication.ConnectedScenes
			.OfType<UIWindowScene>()
			.SelectMany(scene => scene.Windows)
			.FirstOrDefault(w => w.IsKeyWindow)?.RootViewController;
		while (top?.PresentedViewController is { } presented)
		{
			top = presented;
		}

		return top is UINavigationController nav ? nav.TopViewController : top;
	}
}
