using Foundation;

// Namespace matches the Android files (and the rest of Mobile), not
// "...Platforms.iOS" — see the namespace note in AndroidSpeechReader.cs.
namespace ReadTheStupidText.Mobile;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
