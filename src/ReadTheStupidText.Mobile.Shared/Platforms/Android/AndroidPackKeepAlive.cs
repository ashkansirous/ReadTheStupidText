using Android.App;
using Android.Content;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// Android's <see cref="IPackKeepAlive"/> (Slice 183): a thread-safe registry of the downloads in flight. The app side only adds
/// and removes entries and posts an update; <see cref="PackDownloadService"/> owns its own lifetime and stops itself when the
/// registry is empty. The service is started with plain <c>StartService</c> (never <c>StartForegroundService</c>, which makes a
/// promise to call StartForeground that a fast-ending download could break) and only for a cold start while the app is visible (a running service, even in its grace period, is simply joined); a start the
/// system refuses only means no notification, and the download runs as before.
/// </summary>
internal sealed class AndroidPackKeepAlive : IPackKeepAlive
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, double> Active = new();
    private static PackDownloadService? _service;
    private static int _lastPercent = -1;
    private static int _graceGeneration;

    public IPackKeepAliveScope Begin(string packId)
    {
        bool serviceRunning;
        lock (Gate)
        {
            Active[packId] = 0;
            _graceGeneration++; // a pending grace stop no longer applies
            serviceRunning = _service is not null;
        }

        if (serviceRunning)
        {
            PackDownloadHost.LogService("joined", packId); // the running service (active or in its grace period) carries this pack too
            Refresh();
        }
        else if (PackKeepAlivePolicy.ShouldStartService(serviceRunning, AppIsVisible()))
        {
            TryStartService();
        }

        return new Scope(packId);
    }

    internal static void CancelAll()
    {
        string[] ids;
        lock (Gate)
        {
            ids = Active.Keys.ToArray();
        }

        foreach (string id in ids)
        {
            try
            {
                PackDownloadHost.RaiseCancelRequested(id);
            }
            catch (Exception ex)
            {
                PackDownloadHost.LogService("cancel-failed", ex.GetType().Name); // one pack failing to cancel never skips the rest
            }
        }
    }

    /// <summary>True while a visible or foreground-service app can start the service: the app has a visible activity, or the service already runs.</summary>
    internal static bool AppIsVisible()
    {
        try
        {
            var info = new ActivityManager.RunningAppProcessInfo();
            ActivityManager.GetMyMemoryState(info);
            return info.Importance <= Importance.Visible;
        }
        catch (Exception ex)
        {
            PackDownloadHost.LogService("visibility-unknown", ex.GetType().Name);
            return false;
        }
    }

    /// <summary>Called by the service in OnStartCommand: goes foreground (may throw; the service then stops itself) and stops again at once when every download already ended.</summary>
    internal static void EnterForeground(PackDownloadService service)
    {
        lock (Gate)
        {
            if (!ReferenceEquals(_service, service)) // a second start command of a service that already runs only re-checks the registry
            {
                PackDownloadService.EnsureChannel(service);
                service.GoForeground(BuildNotification(service));
                _service = service;
                _lastPercent = -1;
            }
        }

        Refresh();
    }

    /// <summary>The service could not go foreground, was destroyed or timed out: the downloads carry on without it, and a later Begin may start it again.</summary>
    internal static void ServiceStopped(PackDownloadService service)
    {
        lock (Gate)
        {
            if (ReferenceEquals(_service, service))
            {
                _service = null;
                _lastPercent = -1;
            }
        }
    }

    /// <summary>True while the service runs with downloads to cancel.</summary>
    internal static bool HasActive()
    {
        lock (Gate)
        {
            return Active.Count > 0 && _service is not null;
        }
    }

    internal static Notification BuildNotification(Context context)
    {
        double fraction;
        lock (Gate)
        {
            fraction = Active.Count == 0 ? 0 : Active.Values.Average();
        }

        int percent = (int)(fraction * 100);
        var cancelIntent = new Intent(context, typeof(PackDownloadService)).SetAction(PackDownloadService.CancelAction);
        var cancel = PendingIntent.GetService(context, 1, cancelIntent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        var icon = global::Android.Graphics.Drawables.Icon.CreateWithResource(context, global::Android.Resource.Drawable.StatSysDownload);
        var cancelAction = new Notification.Action.Builder(icon, PackDownloadHost.CancelLabel(), cancel).Build();

        var builder = new Notification.Builder(context, PackDownloadService.ChannelId)
            .SetSmallIcon(global::Android.Resource.Drawable.StatSysDownload)!
            .SetContentTitle(PackDownloadHost.NotificationText(fraction))!
            .SetProgress(100, percent, false)!
            .SetOngoing(true)!
            .SetOnlyAlertOnce(true)!
            .AddAction(cancelAction)!;
        if (context.PackageManager?.GetLaunchIntentForPackage(context.PackageName!) is { } launch)
        {
            builder.SetContentIntent(PendingIntent.GetActivity(context, 0, launch, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent));
        }

        return builder.Build()!;
    }

    // Plain StartService while the app is visible. Refused (a background start) only means this download runs without a notification.
    private static void TryStartService()
    {
        try
        {
            Context context = global::Android.App.Application.Context;
            context.StartService(new Intent(context, typeof(PackDownloadService)));
        }
        catch (Exception ex)
        {
            PackDownloadHost.LogService("start-refused", ex.GetType().Name);
        }
    }

    private static void Update(string packId, double fraction)
    {
        lock (Gate)
        {
            if (!Active.ContainsKey(packId))
            {
                return;
            }

            Active[packId] = fraction;
        }

        Refresh();
    }

    private static void End(string packId)
    {
        lock (Gate)
        {
            Active.Remove(packId);
        }

        Refresh();
    }

    // The service's own view of the registry: it stops itself (foreground off, then stop) when nothing is left, else posts the percent.
    private static void Refresh()
    {
        lock (Gate)
        {
            if (_service is not { } service)
            {
                return;
            }

            try
            {
                if (Active.Count == 0)
                {
                    ScheduleGraceStop();
                    return;
                }

                int percent = (int)(Active.Values.Average() * 100);
                if (percent != _lastPercent)
                {
                    _lastPercent = percent;
                    Notify(service);
                }
            }
            catch (Exception ex)
            {
                PackDownloadHost.LogService("refresh-failed", ex.GetType().Name);
            }
        }
    }

    // The service outlives the last download by the grace period, so the next pack of a chain joins it instead of starting unprotected.
    private static void ScheduleGraceStop()
    {
        int generation = ++_graceGeneration;
        Task.Delay(PackKeepAlivePolicy.GracePeriod).ContinueWith(_ => GraceExpired(generation), TaskScheduler.Default);
    }

    private static void GraceExpired(int generation)
    {
        lock (Gate)
        {
            if (_service is not { } service || !PackKeepAlivePolicy.ShouldStopAfterGrace(Active.Count, generation, _graceGeneration))
            {
                return;
            }

            _service = null;
            _lastPercent = -1;
            try
            {
                PackDownloadHost.LogService("grace-stop", "all");
                service.StopForegroundAndSelf();
            }
            catch (Exception ex)
            {
                PackDownloadHost.LogService("refresh-failed", ex.GetType().Name);
            }
        }
    }

    private static void Notify(Context context)
    {
        var manager = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
        manager.Notify(PackDownloadService.NotificationId, BuildNotification(context));
    }

    private sealed class Scope(string packId) : IPackKeepAliveScope
    {
        private int _disposed;

        public void Progress(double fraction) => Update(packId, fraction);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                End(packId);
            }
        }
    }
}
