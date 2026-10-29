using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// The foreground service that keeps a big pack download running behind the lock screen (Slice 183). Behind a PIN lock Android
/// blocks the network of a background app (<c>blocked_state=APP_BACKGROUND</c>); a foreground service of type dataSync is not
/// background. The download itself runs where it always did (<see cref="MobileVoicePackService"/>): this service only holds the
/// process and shows the ongoing notification (the app's own status line, one Cancel action). It is started with plain StartService while the app is visible and stops itself when the registry of
/// active downloads is empty (it is the only owner of its lifetime). Nothing here may throw: a service that cannot go foreground stops itself
/// and the download carries on in the process.
/// </summary>
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeDataSync)]
public sealed class PackDownloadService : Service
{
    internal const string CancelAction = "uk.sirous.packdownload.CANCEL";
    internal const string ChannelId = "pack_downloads";
    internal const int NotificationId = 1830;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == CancelAction)
        {
            HandleCancel();
            return StartCommandResult.NotSticky;
        }

        try
        {
            AndroidPackKeepAlive.EnterForeground(this);
        }
        catch (Exception ex)
        {
            // The app started this service with plain StartService, so no foreground promise was made and StopSelf is legal:
            // ForegroundServiceStartNotAllowedException (background by now, dataSync quota) or a bad notification only means no notification.
            PackDownloadHost.LogService("start-refused", ex.GetType().Name);
            AndroidPackKeepAlive.ServiceStopped(this);
            StopSelf();
        }

        return StartCommandResult.NotSticky;
    }

    // Cancel from the notification: the downloads stop themselves and the registry emptying ends the service; a service with nothing to cancel (stale notification) stops now.
    private void HandleCancel()
    {
        try
        {
            bool running = AndroidPackKeepAlive.HasActive();
            AndroidPackKeepAlive.CancelAll();
            if (!running)
            {
                StopSelf();
            }
        }
        catch (Exception ex)
        {
            PackDownloadHost.LogService("cancel-failed", ex.GetType().Name);
        }
    }

    internal void GoForeground(Notification notification)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            StartForeground(NotificationId, notification, ForegroundService.TypeDataSync);
        }
        else
        {
            StartForeground(NotificationId, notification);
        }
    }

    internal void StopForegroundAndSelf()
    {
        StopForeground(StopForegroundFlags.Remove);
        StopSelf();
    }

    public override void OnDestroy()
    {
        AndroidPackKeepAlive.ServiceStopped(this);
        base.OnDestroy();
    }

    // Android 15 limits dataSync services to six hours a day; the downloads carry on as a plain background run (Slice 182's resume covers a kill).
    public override void OnTimeout(int startId, ForegroundService fgsType)
    {
        PackDownloadHost.LogService("timeout", "all");
        AndroidPackKeepAlive.ServiceStopped(this);
        StopSelf();
    }

    internal static void EnsureChannel(Context context)
    {
        var manager = (NotificationManager)context.GetSystemService(NotificationService)!;
        if (manager.GetNotificationChannel(ChannelId) is null)
        {
            manager.CreateNotificationChannel(new NotificationChannel(ChannelId, PackDownloadHost.ChannelName(), NotificationImportance.Low));
        }
    }
}
