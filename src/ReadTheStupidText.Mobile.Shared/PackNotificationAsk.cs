namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// The one-time question before the first big pack download on Android 13+ (Slice 183, mock frame R13): the app's own sheet
/// first (Not now / Continue), and only Continue opens Android's permission prompt. Asked once per install and only while
/// notifications are not allowed; either answer lets the download run (Android just hides the notification). iOS never asks.
/// The question never throws and never stops a download: a sheet that cannot be shown (the activity is gone) is simply not asked
/// and not counted, so it is asked at the next chance.
/// </summary>
public static class PackNotificationAsk
{
#if ANDROID
    private const string AskedKey = "pack_notification_asked";
    private const int AndroidWithRuntimeNotificationPermission = 33;

    private static readonly TimeSpan SheetTimeout = TimeSpan.FromMinutes(5);
    private static int _asking;
#endif

    /// <param name="showSheet">Shows the app's sheet and returns true for Continue.</param>
    public static async Task AskOnceAsync(Func<Task<bool>> showSheet)
    {
#if ANDROID
        if (!OperatingSystem.IsAndroidVersionAtLeast(AndroidWithRuntimeNotificationPermission) || !AppIsInForeground() || Interlocked.Exchange(ref _asking, 1) == 1)
        {
            return; // not needed here, no page is visible to ask on, or another download is already asking
        }

        try
        {
            await AskAsync(showSheet);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Notification question skipped: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            Volatile.Write(ref _asking, 0);
        }
#else
        await Task.CompletedTask;
#endif
    }

#if ANDROID
    // A sheet queued with no visible page would never complete and would pop up over unrelated UI later.
    private static bool AppIsInForeground()
    {
        var info = new global::Android.App.ActivityManager.RunningAppProcessInfo();
        global::Android.App.ActivityManager.GetMyMemoryState(info);
        return info.Importance == global::Android.App.Importance.Foreground;
    }

    private static async Task AskAsync(Func<Task<bool>> showSheet)
    {
        if (Preferences.Default.Get(AskedKey, false)
            || await Permissions.CheckStatusAsync<Permissions.PostNotifications>() == PermissionStatus.Granted)
        {
            return;
        }

        Task<bool> sheet = showSheet();
        if (await Task.WhenAny(sheet, Task.Delay(SheetTimeout)) != sheet)
        {
            return; // never shown: give up so a later download can ask; Not now was not chosen, so it is asked again
        }

        bool continued = await sheet;
        Preferences.Default.Set(AskedKey, true); // Not now is final: never asked again
        if (continued)
        {
            await Permissions.RequestAsync<Permissions.PostNotifications>();
        }
    }
#endif
}
