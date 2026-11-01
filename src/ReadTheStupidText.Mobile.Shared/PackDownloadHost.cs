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

/// <summary>Shows and removes the "Download paused. Tap to finish." notification (Slice 208); each platform brings its own (Android: a normal notification on the pack channel; iOS: a quiet local notification).</summary>
public interface IPackPausedNotifier
{
    /// <summary>Posts the notice for a pack whose download the system stopped; <paramref name="fraction"/> is null when the bytes done are not known.</summary>
    void Post(string packId, double? fraction);

    /// <summary>Removes the pack's notice (the download resumed, finished or was cancelled); nothing happens when there is none.</summary>
    void Clear(string packId);
}

/// <summary>The language and size a download notification names: "French", 124 (MB).</summary>
public sealed record PackLabel(string Language, int Megabytes);

/// <summary>Fetches a pack's body into its <c>.partial</c> file by the platform's own background transfer (iOS: a background URLSession).</summary>
public interface IPackBodyFetcher
{
    /// <summary>Appends from <paramref name="offset"/> (or replaces the file when the server sends the whole body) until the file is complete.</summary>
    Task FetchAsync(PackInfo pack, string partial, long offset, IProgress<double>? progress, CancellationToken cancellationToken);

    /// <summary>Whether a platform transfer (iOS: a background task, even one the system kept across a relaunch) still writes this partial file; the launch scan then leaves it alone.</summary>
    Task<bool> HasLiveTransferAsync(string partial);

    /// <summary>Removes what the platform keeps beside a partial file (iOS: the <c>.resume</c> sidecar) when the scan deletes that partial.</summary>
    void DiscardLeftovers(string partial);
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

    private static IPackKeepAlive? _keepAlive;
    private static IPackBodyFetcher? _bodyFetcher;
    private static IReadOnlyList<PackInfo> _catalog = [];

    public static IPackKeepAlive? KeepAlive
    {
        get => _keepAlive;
        set
        {
            _keepAlive = value;
            AdoptNotifier(value);
        }
    }

    public static IPackBodyFetcher? BodyFetcher
    {
        get => _bodyFetcher;
        set
        {
            _bodyFetcher = value;
            AdoptNotifier(value);
        }
    }

    /// <summary>The platform's paused notice (Slice 208); set by whichever platform hook above also implements <see cref="IPackPausedNotifier"/>, so no registration file needs to know about it. Null on Windows.</summary>
    public static IPackPausedNotifier? PausedNotifier { get; set; }

    private static void AdoptNotifier(object? platformHook)
    {
        if (platformHook is IPackPausedNotifier notifier)
        {
            PausedNotifier = notifier;
        }
    }

    /// <summary>
    /// Asks the notification question (<see cref="PackNotificationAsk"/>) at a pack download that did not start from the first-run
    /// page, for an app that has no other place to ask. Gets the id of the pack that started (its language and size can be named in the
    /// question, <see cref="LabelOf"/>). Never awaited by the download, never throws.
    /// </summary>
    public static Func<string, Task>? AskNotification { get; set; }

    /// <summary>The notification text for a fraction done; an app replaces it with its own localized status line.</summary>
    public static Func<double, string> NotificationText { get; set; } = fraction => $"Downloading voice… {(int)(fraction * 100)}%";

    /// <summary>The download notification's text naming the pack's language and size (Slice 208), e.g. "Downloading the French voice (124 MB)… 45%"; null keeps <see cref="NotificationText"/>.</summary>
    public static Func<PackLabel, double, string>? NamedNotificationText { get; set; }

    /// <summary>The paused notification's title.</summary>
    public static Func<string> PausedTitle { get; set; } = () => "Download paused. Tap to finish.";

    /// <summary>The paused notification's second line: "French voice (124 MB) · 45% done"; the label is null for a pack that has no language to name, the fraction is null when the bytes done are not known.</summary>
    public static Func<PackLabel?, double?, string> PausedNotificationText { get; set; } = DefaultPausedText;

    /// <summary>The name of a language code in the app's own style (RTST: English, Dino: its endonym).</summary>
    public static Func<string, string> LanguageName { get; set; } = code => code;

    /// <summary>The language the app is set to; a pack that serves several languages is named by it.</summary>
    public static Func<string?> CurrentLanguage { get; set; } = () => null;

    /// <summary>The Cancel action's label.</summary>
    public static Func<string> CancelLabel { get; set; } = () => "Cancel";

    /// <summary>The name of the notification channel, shown in the system notification settings.</summary>
    public static Func<string> ChannelName { get; set; } = () => "Pack downloads";

    /// <summary>Cancels every download in flight exactly as the notification's Cancel does (Slice 186: the Debug test hook's <c>cancel-pack-downloads</c>); null where there is no notification.</summary>
    public static Action? CancelAllActive { get; set; }

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

    public const string PausedNotifiedAction = "paused-notified";
    public const string PausedFailedAction = "paused-failed";
    public const string PausedClearFailedAction = "paused-clear-failed";
    public const string PausedSkippedAction = "paused-skipped";
    public const string VisibilityUnknownAction = "visibility-unknown";
    public const string NotificationAuthFailedAction = "notification-auth-failed";
    public const string AskDeferredAction = "ask-deferred";
    public const string AskAtSettingsAction = "ask-at-settings";
    public const string OnScreenDetail = "on-screen";
    private const double PercentScale = 100;

    /// <summary>The share done as the notifications print it ("45%"), in the given culture; both apps use it so <c>{2}</c> of the download text means the same thing.</summary>
    public static string PercentText(double fraction, System.Globalization.CultureInfo culture) => fraction.ToString("P0", culture);

    /// <summary>
    /// The paused notice's second line, from the app's two resource templates ({0} language, {1} MB, and {2} the whole percent done for <paramref name="detailFormat"/>):
    /// the named form with the share when it is known, the named form alone otherwise, and the plain status line for a pack with no language to name.
    /// </summary>
    public static string ComposePausedText(PackLabel? label, double? fraction, string detailFormat, string namedFormat, System.Globalization.CultureInfo culture)
    {
        if (label is null)
        {
            return fraction is { } plain ? NotificationText(plain) : string.Empty;
        }

        return fraction is { } done
            ? string.Format(culture, detailFormat, label.Language, label.Megabytes, (int)(done * PercentScale))
            : string.Format(culture, namedFormat, label.Language, label.Megabytes);
    }

    /// <summary>True when the platform has a transfer running for this partial file (see <see cref="IPackBodyFetcher.HasLiveTransferAsync"/>); false where there is none.</summary>
    public static Task<bool> HasLiveTransferAsync(string partial) => _bodyFetcher?.HasLiveTransferAsync(partial) ?? Task.FromResult(false);

    /// <summary>Removes the platform's files beside a deleted partial; never throws.</summary>
    public static void DiscardTransferLeftovers(string partial)
    {
        try
        {
            _bodyFetcher?.DiscardLeftovers(partial);
        }
        catch (Exception ex)
        {
            LogService("discard-failed", ex.GetType().Name);
        }
    }

    private static string DefaultPausedText(PackLabel? label, double? fraction)
    {
        string name = label is null ? "" : $"{label.Language} voice ({label.Megabytes} MB)";
        return fraction is { } done ? $"{name} · {(int)(done * PercentScale)}% done".Trim(' ', '·') : name;
    }

    /// <summary>Remembers the catalog's packs so a notification can name a pack's language and size. Called whenever the catalog is fetched.</summary>
    public static void NoteCatalog(IReadOnlyList<PackInfo> packs) => Volatile.Write(ref _catalog, packs);

    /// <summary>
    /// The language and size to name for a pack, or null when it cannot be named honestly: not in the catalog, not a voice, or a
    /// multilingual pack the app is not set to (naming one of its languages at random would be wrong).
    /// </summary>
    public static PackLabel? LabelOf(string packId)
    {
        PackInfo? pack = Volatile.Read(ref _catalog).FirstOrDefault(p => p.Id == packId);
        if (pack is null || pack.Kind is not (PackKind.Voice or PackKind.Storyteller) || pack.Languages.Count == 0)
        {
            return null;
        }

        string? current = CurrentLanguage();
        string? language = pack.Languages.FirstOrDefault(l => string.Equals(l, current, StringComparison.OrdinalIgnoreCase))
            ?? (pack.Languages.Count == 1 ? pack.Languages[0] : null);
        return language is null ? null : new PackLabel(LanguageName(language), StorytellerOffer.Megabytes(pack.SizeBytes));
    }

    /// <summary>The ongoing download notification's text for the pack in front: its named form when it can be named, else the app's plain status line.</summary>
    public static string DownloadingText(string? packId, double fraction)
    {
        PackLabel? label = packId is null ? null : LabelOf(packId);
        return label is not null && NamedNotificationText is { } named ? named(label, fraction) : NotificationText(fraction);
    }

    /// <summary>The system stopped this pack's download: logs the marker and shows the paused notice. Never throws.</summary>
    public static void Paused(string packId, double? fraction)
    {
        try
        {
            if (PausedNotifier is not { } notifier)
            {
                return;
            }

            notifier.Post(packId, fraction);
            LogService(PausedNotifiedAction, packId);
        }
        catch (Exception ex)
        {
            LogService(PausedFailedAction, ex.GetType().Name);
        }
    }

    /// <summary>The pack's download resumed, finished or was cancelled: removes its paused notice. Never throws.</summary>
    public static void ClearPaused(string packId)
    {
        try
        {
            PausedNotifier?.Clear(packId);
        }
        catch (Exception ex)
        {
            LogService(PausedClearFailedAction, ex.GetType().Name);
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
