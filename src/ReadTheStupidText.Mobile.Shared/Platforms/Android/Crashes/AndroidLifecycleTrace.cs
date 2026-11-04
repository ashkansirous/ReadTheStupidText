using Android.Content;
using Android.Content.Res;
using Android.Runtime;

namespace ReadTheStupidText.Mobile.Shared.Crashes;

/// <summary>
/// Android's trace lines for the rolling log (Slice 243 item 5, built in Slice 249): memory trim levels. They go through <see cref="AppTrace"/> with the words of <see cref="TraceLines"/>;
/// they carry no text. Back is not hooked here: an always-enabled <c>OnBackPressedCallback</c> would switch off Android 13+ predictive back, so a Back shows as <c>page popped</c> only.
/// </summary>
public static class AndroidLifecycleTrace
{
    private static bool memoryHooked;

    /// <summary>Logs every memory trim level the system sends the app. Once per process; never throws.</summary>
    public static void HookMemory()
    {
        try
        {
            if (memoryHooked)
            {
                return;
            }

            memoryHooked = true;
            global::Android.App.Application.Context.RegisterComponentCallbacks(new MemoryCallbacks());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Memory trace could not start: {ex.Message}");
        }
    }

    private sealed class MemoryCallbacks : Java.Lang.Object, IComponentCallbacks2
    {
        public void OnConfigurationChanged(Configuration newConfig)
        {
        }

        public void OnLowMemory() => AppTrace.Line(TraceLines.MemoryWarning("low"));

        public void OnTrimMemory(TrimMemory level) => AppTrace.Line(TraceLines.MemoryWarning(level.ToString()));
    }
}
