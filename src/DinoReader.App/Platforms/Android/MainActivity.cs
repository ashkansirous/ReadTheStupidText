using Android.App;
using Android.Content.PM;
using ReadTheStupidText.Mobile;

namespace DinoReader;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
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
