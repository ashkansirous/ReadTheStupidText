using System.Diagnostics;
using Windows.Services.Store;

namespace DinoReader;

public static partial class StoreUpdater
{
	private const string StoreProductUri = "ms-windows-store://pdp/?productid=" + WindowsStoreId;

	// The Store's own update request installs the newer package; when it finds none or fails, the Store page opens instead; a user-cancelled update does nothing.
	public static async partial Task StartUpdateAsync()
	{
		if (await TryRequestStoreUpdateAsync())
		{
			return;
		}

		await OpenStoreAsync();
	}

	public static async partial Task OpenStoreAsync()
	{
		try
		{
			await Launcher.Default.OpenAsync(new Uri(StoreProductUri));
		}
		catch (Exception ex) when (ex is InvalidOperationException or FeatureNotSupportedException)
		{
			Debug.WriteLine($"Dino Reader could not open the Store: {ex.Message}");
		}
	}

	private static async Task<bool> TryRequestStoreUpdateAsync()
	{
		try
		{
			StoreContext context = StoreContext.GetDefault();
			if (Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView is not MauiWinUIWindow window)
			{
				return false;
			}

			WinRT.Interop.InitializeWithWindow.Initialize(context, WinRT.Interop.WindowNative.GetWindowHandle(window));
			var updates = await context.GetAppAndOptionalStorePackageUpdatesAsync();
			if (updates.Count == 0)
			{
				return false;
			}

			var result = await context.RequestDownloadAndInstallStorePackageUpdatesAsync(updates).AsTask();
			// A cancel is the user's answer: nothing more happens. Only an error falls back to the Store page.
			return result.OverallState is StorePackageUpdateState.Completed or StorePackageUpdateState.Canceled;
		}
		catch (Exception ex)
		{
			// Whatever the Store APIs throw (no sign-in, unpackaged, bad handle), the caller falls back to the Store page.
			Debug.WriteLine($"Dino Reader could not request the Store update: {ex.Message}");
			return false;
		}
	}
}
