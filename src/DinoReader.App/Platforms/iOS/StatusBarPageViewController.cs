using Microsoft.Maui;
using Microsoft.Maui.Platform;
using UIKit;

namespace DinoReader;

/// <summary>A page's view controller that carries the status-bar icon colour, so only the icons change (no window trait, no theme). Slice 325 A4.</summary>
internal sealed class StatusBarPageViewController : PageViewController
{
	public StatusBarPageViewController(IView page, IMauiContext mauiContext) : base(page, mauiContext)
	{
	}

	public bool DarkIcons { get; set; } = true;

	public override UIStatusBarStyle PreferredStatusBarStyle() =>
		DarkIcons ? UIStatusBarStyle.DarkContent : UIStatusBarStyle.LightContent;
}

/// <summary>Makes every ContentPage use <see cref="StatusBarPageViewController"/>.</summary>
internal sealed class StatusBarPageHandler : Microsoft.Maui.Handlers.PageHandler
{
	protected override Microsoft.Maui.Platform.ContentView CreatePlatformView()
	{
		ViewController ??= new StatusBarPageViewController(VirtualView, MauiContext!);
		return base.CreatePlatformView();
	}
}
