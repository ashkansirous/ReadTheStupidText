using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;
using ReadTheStupidText.Mobile;

namespace DinoReader;

[Activity(WindowSoftInputMode = SoftInput.AdjustResize, Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	// MAUI sets the bar icons from the system night mode, but only the reading screen follows it.
	// SystemBarIcons picks them from the page background actually shown; a UI mode change makes the
	// framework reset the bars, so the last choice is applied again.
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		SystemBarIcons.Applier = ApplyDarkIcons;
	}

	protected override void OnDestroy()
	{
		SystemBarIcons.Applier = null;
		base.OnDestroy();
	}

	// MAUI's window handler sets the bars when the window connects, which can land after the first
	// page appeared: apply the page's choice again once the window has focus.
	public override void OnWindowFocusChanged(bool hasFocus)
	{
		base.OnWindowFocusChanged(hasFocus);
		if (hasFocus)
		{
			SystemBarIcons.Reapply();
		}
	}

	public override void OnConfigurationChanged(Configuration newConfig)
	{
		base.OnConfigurationChanged(newConfig);
		SystemBarIcons.Reapply();
	}

	private void ApplyDarkIcons(bool dark)
	{
		if (Window is not { } window || WindowCompat.GetInsetsController(window, window.DecorView) is not { } controller)
		{
			return;
		}

		controller.AppearanceLightStatusBars = dark;
		controller.AppearanceLightNavigationBars = dark;
	}

	// The ML Kit document scanner returns only through the legacy activity-result channel.
	// Without this forward its pending scan never completed (cancel, failure or a missing module
	// alike) and the Add page stayed busy until restart. AndroidDocumentScanner ignores other codes.
#pragma warning disable CS0618
	protected override void OnActivityResult(int requestCode, Result resultCode, Android.Content.Intent? data)
	{
		base.OnActivityResult(requestCode, resultCode, data);
		AndroidDocumentScanner.HandleActivityResult(requestCode, resultCode, data);
	}
#pragma warning restore CS0618
}
