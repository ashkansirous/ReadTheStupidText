using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>One pack download kept alive in the background (Slice 183); dispose it when the download ends.</summary>
public interface IPackKeepAliveScope : IDisposable
{
    /// <summary>The fraction done, 0..1, for the notification.</summary>
    void Progress(double fraction);
}

/// <summary>Keeps the process alive and the network open while a big pack downloads with the phone locked (Android: a foreground service).</summary>
public interface IPackKeepAlive
{
    IPackKeepAliveScope Begin(string packId);
}

/// <summary>Fetches a pack's body into its <c>.partial</c> file by the platform's own background transfer (iOS: a background URLSession).</summary>
public interface IPackBodyFetcher
{
    /// <summary>Appends from <paramref name="offset"/> (or replaces the file when the server sends the whole body) until the file is complete.</summary>
    Task FetchAsync(PackInfo pack, string partial, long offset, IProgress<double>? progress, CancellationToken cancellationToken);
}

/// <summary>
/// The platform hooks for background pack downloads (Slice 183). Each platform sets what it has at start-up; where a hook is
/// null (Windows) the download runs as before. Pack downloads all go through <see cref="MobileVoicePackService"/>, so one
/// scope there covers voice, storyteller, translation and scan packs in both apps.
/// </summary>
public static class PackDownloadHost
{
    public const string ServiceEvent = "service";
    public const string StartAction = "start";
    public const string StopAction = "stop";

    public static IPackKeepAlive? KeepAlive { get; set; }

    public static IPackBodyFetcher? BodyFetcher { get; set; }

    /// <summary>
    /// Asks the notification question (<see cref="PackNotificationAsk"/>) at a pack download that did not start from the first-run
    /// page, for an app that has no other place to ask. Never awaited by the download, never throws.
    /// </summary>
    public static Func<Task>? AskNotification { get; set; }

    /// <summary>The notification text for a fraction done; an app replaces it with its own localized status line.</summary>
    public static Func<double, string> NotificationText { get; set; } = fraction => $"Downloading voice… {(int)(fraction * 100)}%";

    /// <summary>The Cancel action's label.</summary>
    public static Func<string> CancelLabel { get; set; } = () => "Cancel";

    /// <summary>The name of the notification channel, shown in the system notification settings.</summary>
    public static Func<string> ChannelName { get; set; } = () => "Pack downloads";

    /// <summary>Raised with the pack id when the user taps Cancel on the notification. Owners of a run stop it as their in-app Cancel does.</summary>
    public static event Action<string>? CancelRequested;

    /// <summary>
    /// Raises <see cref="CancelRequested"/> one handler at a time, newest first (a download's own listener subscribes last), so a
    /// throwing handler never stops the others: Cancel is final.
    /// </summary>
    public static void RaiseCancelRequested(string packId)
    {
        Delegate[] handlers = CancelRequested?.GetInvocationList() ?? [];
        for (int i = handlers.Length - 1; i >= 0; i--)
        {
            try
            {
                ((Action<string>)handlers[i])(packId);
            }
            catch (Exception ex)
            {
                LogService("cancel-handler-failed", ex.GetType().Name);
            }
        }
    }

    // Same line format and sink as PackDownloadLog (which is shared with Windows and not edited here).
    internal static void LogService(string action, string packId)
    {
        string line = $"{PackDownloadLog.Prefix} {ServiceEvent} {action} pack={packId}";
        System.Diagnostics.Debug.WriteLine(line);
        try
        {
            PackDownloadLog.Sink?.Invoke(line);
        }
        catch (Exception)
        {
            // A diagnostic sink must never break a download.
        }
    }
}
