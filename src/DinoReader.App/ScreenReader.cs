using Android.AccessibilityServices;
using Android.Content;
using Android.Views.Accessibility;

namespace DinoReader;

/// <summary>
/// Whether a screen reader or Switch Access is driving the phone. The grown-up gate's "hold for
/// 3 seconds" step becomes a plain button then (mock screen 6b); the sum stays. TalkBack shows
/// as touch exploration; Switch Access is found by its exact Google service id. Other accessibility services
/// (a password manager, a dictation or autofill tool) do not count, so they don't weaken the
/// gate. iOS (VoiceOver) joins when Slice 67 adds that target.
/// </summary>
public static class ScreenReader
{
	private const string SwitchAccessServicePrefix = "com.google.android.accessibility.switchaccess/";

	public static bool IsOn()
	{
		var manager = (AccessibilityManager?)Platform.AppContext.GetSystemService(Context.AccessibilityService);
		return manager is not null && (manager.IsTouchExplorationEnabled || IsSwitchAccessOn(manager));
	}

	private static bool IsSwitchAccessOn(AccessibilityManager manager)
	{
		IList<AccessibilityServiceInfo>? services = manager.GetEnabledAccessibilityServiceList(FeedbackFlags.AllMask);
		return services?.Any(s => s.Id?.StartsWith(SwitchAccessServicePrefix, StringComparison.Ordinal) == true) == true;
	}
}
