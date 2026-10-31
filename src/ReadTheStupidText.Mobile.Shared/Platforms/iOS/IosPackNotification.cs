using Foundation;
using UIKit;
using UserNotifications;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// iOS's <see cref="IPackPausedNotifier"/> (Slice 208): when a pack download run ends unfinished (a terminal transport error: no network, Wi-Fi gone with
/// mobile data off, after the retries; with or without a waiter alive) while the app is in the background, the same "Download paused. Tap to finish." text
/// the Android notice uses is posted as a local notification. Authorisation is <b>provisional</b> (asked once, the first time a pack
/// download starts): there is no permission prompt, and the notice is delivered quietly to Notification Center only. Tapping it opens
/// the app, and the foreground resume (Slice 182) finishes the download. When notifications are off nothing is posted and the
/// foreground resume still works. Nothing here may throw into a download.
/// </summary>
internal sealed class IosPackNotification : IPackPausedNotifier
{
    private const string IdentifierPrefix = "uk.sirous.packpaused.";

    private static int _authorizationRequested;

    // Bumped by every Clear: a Post that began before a Clear (its awaits still pending) must not land after it, or the notice would stay up over a running download.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> Generations = new();

    /// <summary>Asks for provisional authorisation once per process (iOS remembers the answer); never shows a prompt, never throws.</summary>
    public static async Task EnsureProvisionalAuthorizationAsync()
    {
        if (Interlocked.Exchange(ref _authorizationRequested, 1) == 1)
        {
            return;
        }

        try
        {
            await UNUserNotificationCenter.Current.RequestAuthorizationAsync(UNAuthorizationOptions.Provisional).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            PackDownloadHost.LogService(PackDownloadHost.NotificationAuthFailedAction, ex.GetType().Name);
        }
    }

    public void Post(string packId, double? fraction)
    {
        _ = PostAsync(packId, fraction, Generations.GetValueOrDefault(packId));
    }

    public void Clear(string packId)
    {
        lock (NoticeLock)
        {
            Generations.AddOrUpdate(packId, 1, (_, generation) => generation + 1);
            RemoveNotice(packId);
        }
    }

    // Serialises Clear's bump-and-remove with Post's check-and-remove, so a Clear can never fall between the two halves of either.
    private static readonly object NoticeLock = new();

    private static bool IsCurrent(string packId, int generation)
    {
        lock (NoticeLock)
        {
            return Generations.GetValueOrDefault(packId) == generation;
        }
    }

    private static void RemoveNotice(string packId)
    {
        string[] ids = [IdentifierPrefix + packId];
        UNUserNotificationCenter center = UNUserNotificationCenter.Current;
        center.RemoveDeliveredNotifications(ids);
        center.RemovePendingNotificationRequests(ids);
    }

    // Only while the app is in the background (not Active, not Inactive): otherwise the resume is already running and a notice would be noise.
    private static async Task PostAsync(string packId, double? fraction, int generation)
    {
        try
        {
            if (!await IsInBackgroundAsync().ConfigureAwait(false))
            {
                PackDownloadHost.LogService(PackDownloadHost.PausedSkippedAction, PackDownloadHost.OnScreenDetail);
                return;
            }

            UNNotificationSettings settings = await UNUserNotificationCenter.Current.GetNotificationSettingsAsync().ConfigureAwait(false);
            if (settings.AuthorizationStatus is UNAuthorizationStatus.Denied or UNAuthorizationStatus.NotDetermined)
            {
                return; // notifications off: the foreground resume still works
            }

            if (!IsCurrent(packId, generation))
            {
                return; // the download resumed while the settings were read
            }

            using var content = new UNMutableNotificationContent
            {
                Title = PackDownloadHost.PausedTitle(),
                Body = PackDownloadHost.PausedNotificationText(PackDownloadHost.LabelOf(packId), fraction),
            };
            using UNNotificationRequest request = UNNotificationRequest.FromIdentifier(IdentifierPrefix + packId, content, null);
            await UNUserNotificationCenter.Current.AddNotificationRequestAsync(request).ConfigureAwait(false);
            lock (NoticeLock)
            {
                if (Generations.GetValueOrDefault(packId) != generation)
                {
                    RemoveNotice(packId); // the download resumed while this was being posted
                }
            }
        }
        catch (Exception ex)
        {
            PackDownloadHost.LogService(PackDownloadHost.PausedFailedAction, ex.GetType().Name);
        }
    }

    // Inactive (the app switcher, a call banner) is not "away": a short drop there is retried and the notice would be noise.
    private static Task<bool> IsInBackgroundAsync() =>
        MainThread.InvokeOnMainThreadAsync(() => UIApplication.SharedApplication.ApplicationState == UIApplicationState.Background);
}
