using Foundation;
using UIKit;

namespace Sirous.Reading.Mobile.Shared.Crashes;

/// <summary>iOS's trace line for the rolling log (Slice 243 item 5, built in Slice 249): the system's memory warning. No text, only the fixed words of <see cref="TraceLines"/>.</summary>
internal static class IosLifecycleTrace
{
    private const string Level = "warning";
    private static NSObject? observer;

    /// <summary>Logs every <c>UIApplicationDidReceiveMemoryWarning</c>. Once per process; never throws.</summary>
    public static void HookMemory()
    {
        try
        {
            observer ??= NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.DidReceiveMemoryWarningNotification, _ => AppTrace.Line(TraceLines.MemoryWarning(Level)));
        }
        catch (Exception ex)
        {
            // no-fallback: lifecycle trace only; no user-visible feature depends on it
            System.Diagnostics.Debug.WriteLine($"Memory trace could not start: {ex.Message}");
        }
    }
}
