using System.Diagnostics;

namespace DinoReader;

public static partial class StoreUpdater
{
	public static partial Task StartUpdateAsync() => OpenStoreAsync();

	public static async partial Task OpenStoreAsync()
	{
		try
		{
			// Slice 276: only an App Store install opens the App Store; any other install (TestFlight, unreadable) opens TestFlight, as Dino has no App Store app yet.
			string url = IosUpdateTarget.For(Sirous.Reading.Mobile.Shared.Crashes.CrashGate.Channel).Url;
			await Launcher.Default.OpenAsync(new Uri(url));
		}
		catch (Exception ex) when (ex is InvalidOperationException or FeatureNotSupportedException)
		{
			// no-fallback: logged only; the store page is not opened
			Debug.WriteLine($"Dino Reader could not open the store: {ex.Message}");
		}
	}
}
