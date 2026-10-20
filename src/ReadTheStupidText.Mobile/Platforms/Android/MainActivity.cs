using Android.App;
using Android.Content.PM;
using Android.OS;

namespace ReadTheStupidText.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    // The ML Kit document scanner returns through the legacy activity-result channel;
    // AndroidDocumentScanner owns the pending request and ignores other request codes.
    // Legacy API, but the ML Kit scanner returns only through it.
#pragma warning disable CS0618
    protected override void OnActivityResult(int requestCode, Result resultCode, Android.Content.Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        AndroidDocumentScanner.HandleActivityResult(requestCode, resultCode, data);
    }
#pragma warning restore CS0618
}
