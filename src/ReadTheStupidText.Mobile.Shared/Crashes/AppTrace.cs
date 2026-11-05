using ReadTheStupidText.Application.Logging;

namespace ReadTheStupidText.Mobile.Shared.Crashes;

/// <summary>
/// Writes the rolling log's trace lines (Slice 243 item 5, built in Slice 249): the screen shown or left, Back, reading events, lifecycle, memory warnings, the synthesis queue.
/// Both apps and both phone platforms go through here with the words of <see cref="TraceLines"/>. The lines go to the platform's system log (logcat / unified log, so a device
/// check can read them) and, while crash reporting is allowed, to the rolling file whose tail goes out with a report. Never throws, and never takes text: callers pass kinds only.
/// </summary>
public static class AppTrace
{
    private static readonly object AttachGate = new();
    private static bool attached;
    private static ISystemLog? log;
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NavigationPage, object> Watched = new();

    /// <summary>Writes one trace line.</summary>
    public static void Line(string message)
    {
        try
        {
            Log().Info(message);
        }
        catch (Exception)
        {
            // A trace line must never break the caller.
        }
    }

    /// <summary>A reading event with a fixed reason, for example <c>Reading(ReadingEvent.Stopped, ReadingReason.PageLeft)</c>.</summary>
    public static void Reading(string verb, string? reason = null) => Line(TraceLines.Reading(verb, reason));

    /// <summary>Starts the page lines for <paramref name="application"/> once (page shown and left). Never throws.</summary>
    public static void AttachTo(Microsoft.Maui.Controls.Application? application)
    {
        try
        {
            lock (AttachGate)
            {
                if (attached || application is null)
                {
                    return;
                }

                attached = true;
            }

            application.PageAppearing += (_, page) => Line(TraceLines.PageShown(page.GetType().Name));
            application.PageDisappearing += (_, page) => Line(TraceLines.PageLeft(page.GetType().Name));
        }
        catch (Exception)
        {
            // Tracing is best effort.
        }
    }

    /// <summary>Adds the window's lifecycle lines and, whenever its root page is a navigation page (now or after a later swap), a line for each page popped (toolbar Back, the iOS swipe and a system Back all pop). Never throws.</summary>
    public static void AttachTo(Window window)
    {
        try
        {
            window.Activated += (_, _) => Line(TraceLines.AppForeground);
            window.Stopped += (_, _) => Line(TraceLines.AppBackground);
            WatchPops(window.Page);
            window.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(Window.Page))
                {
                    WatchPops(window.Page); // Dino swaps its root (new navigation page) after the first-run flow and a language change
                }
            };
        }
        catch (Exception)
        {
            // Tracing is best effort.
        }
    }

    // Each navigation page that becomes a window's root gets one Popped handler; a root that is not one (the RTST Mobile shell) has no pops to trace.
    private static void WatchPops(Page? root)
    {
        if (root is NavigationPage navigation && Watched.TryGetValue(navigation, out _) == false)
        {
            Watched.Add(navigation, new object());
            navigation.Popped += (_, e) => Line(TraceLines.PagePopped(e.Page.GetType().Name));
        }
    }

    private static ISystemLog Log() => log ??= Create();

    private static ISystemLog Create()
    {
#if ANDROID
        return new AndroidSystemLog();
#elif IOS
        return new IosSystemLog();
#else
        return ReadTheStupidText.Application.Crashes.RollingFileSystemLog.ToCurrent();
#endif
    }
}
