using Debug = System.Diagnostics.Debug;
using Android.App;
using Android.Content;
using Android.Gms.Extensions;
using Xamarin.Google.Android.Play.Core.AppUpdate;
using Xamarin.Google.Android.Play.Core.AppUpdate.Install.Model;

namespace DinoReader;

public static partial class StoreUpdater
{
	/// <summary>The request code of Play's update screen; a cancelled or failed flow ends in <see cref="HandleActivityResult"/>, and the update page's button starts it again.</summary>
	public const int PlayUpdateRequestCode = 4132;

	// Set once a Play flow has been started, so a resume only asks Play about an update this app began.
	private static bool _playFlowStarted;

	private const string MarketUri = "market://details?id=" + AndroidPackageId;
	private const string WebUri = "https://play.google.com/store/apps/details?id=" + AndroidPackageId;

	// Play's immediate update covers the screen until the new version is installed; a build that Play did not install (sideloaded
	// APK, emulator) or one with no newer release published gets the store page instead.
	public static async partial Task StartUpdateAsync()
	{
		if (Platform.CurrentActivity is { } activity && await TryStartPlayUpdateAsync(activity))
		{
			return;
		}

		await OpenStoreAsync();
	}

	public static async partial Task OpenStoreAsync()
	{
		foreach (string uri in new[] { MarketUri, WebUri })
		{
			if (TryView(uri))
			{
				return;
			}
		}

		await Task.CompletedTask;
	}

	/// <summary>Called with every activity result: when Play's update screen did not finish OK (cancelled or failed), a later resume must not relaunch it.</summary>
	public static void HandleActivityResult(int requestCode, Result resultCode)
	{
		if (requestCode == PlayUpdateRequestCode && resultCode != Result.Ok)
		{
			_playFlowStarted = false;
		}
	}

	/// <summary>Called when the activity comes back: an immediate update Play already began is shown again, as Play requires.</summary>
	public static async Task ResumePlayUpdateAsync(Activity activity)
	{
		if (!_playFlowStarted)
		{
			return;
		}

		try
		{
			AppUpdateInfo info = await ManagerFor(activity).GetAppUpdateInfo().AsAsync<AppUpdateInfo>();
			if (info.UpdateAvailability() == UpdateAvailability.DeveloperTriggeredUpdateInProgress)
			{
				StartFlow(activity, info);
			}
		}
		catch (Exception ex) when (ex is Java.Lang.Exception or InvalidOperationException or TaskCanceledException)
		{
			Debug.WriteLine($"Dino Reader could not resume the Play update: {ex.Message}");
		}
	}

	private static async Task<bool> TryStartPlayUpdateAsync(Activity activity)
	{
		try
		{
			AppUpdateInfo info = await ManagerFor(activity).GetAppUpdateInfo().AsAsync<AppUpdateInfo>();
			if (info.UpdateAvailability() != UpdateAvailability.UpdateAvailable || !info.IsUpdateTypeAllowed(AppUpdateType.Immediate))
			{
				return false;
			}

			StartFlow(activity, info);
			return true;
		}
		catch (Exception ex) when (ex is Java.Lang.Exception or InvalidOperationException or TaskCanceledException)
		{
			Debug.WriteLine($"Dino Reader could not start the Play update: {ex.Message}");
			return false;
		}
	}

	private static IAppUpdateManager ManagerFor(Activity activity) => AppUpdateManagerFactory.Create(activity);

	private static void StartFlow(Activity activity, AppUpdateInfo info)
	{
		_playFlowStarted = true;
		ManagerFor(activity).StartUpdateFlowForResult(info, activity, AppUpdateOptions.NewBuilder(AppUpdateType.Immediate).Build(), PlayUpdateRequestCode);
	}

	private static bool TryView(string uri)
	{
		try
		{
			var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse(uri)).AddFlags(ActivityFlags.NewTask);
			Android.App.Application.Context.StartActivity(intent);
			return true;
		}
		catch (ActivityNotFoundException ex)
		{
			Debug.WriteLine($"Dino Reader could not open {uri}: {ex.Message}");
			return false;
		}
	}
}
