namespace ReadTheStupidText.Mobile.Shared.Waiting;

/// <summary>Whether the OS asks apps to cut animation (Slice 143): the wait overlay then draws its animation standing still.</summary>
public static class ReduceMotion
{
    /// <summary>True when the user turned animations off (Android's animator scale 0, iOS Reduce Motion, Windows animations off).</summary>
    public static bool IsOn
    {
        get
        {
            try
            {
                return Read();
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    private static bool Read()
    {
#if ANDROID
        var resolver = Microsoft.Maui.ApplicationModel.Platform.AppContext.ContentResolver;
        return global::Android.Provider.Settings.Global.GetFloat(resolver, global::Android.Provider.Settings.Global.AnimatorDurationScale, 1f) == 0f;
#elif IOS
        return UIKit.UIAccessibility.IsReduceMotionEnabled;
#elif WINDOWS
        return !new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
#else
        return false;
#endif
    }
}
