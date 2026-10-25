using System.Diagnostics;

namespace DinoReader;

public static partial class StoreUpdater
{
	// Dino Reader's App Store Connect app record (SKU uk.sirous.dinoreader, D:\secrets dinoreader/secrets.env).
	private const string StorePage = "https://apps.apple.com/app/id6825053435";

	public static partial Task StartUpdateAsync() => OpenStoreAsync();

	public static async partial Task OpenStoreAsync()
	{
		try
		{
			await Launcher.Default.OpenAsync(new Uri(StorePage));
		}
		catch (Exception ex) when (ex is InvalidOperationException or FeatureNotSupportedException)
		{
			Debug.WriteLine($"Dino Reader could not open the store: {ex.Message}");
		}
	}
}
