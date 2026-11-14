#if ANDROID
using Android.AccessibilityServices;
using Android.Content;
using Android.Views.Accessibility;
#endif
#if IOS
using UIKit;
#endif
#if WINDOWS
using System.Diagnostics;
using System.Runtime.InteropServices;
#endif

namespace DinoReader;

/// <summary>
/// Whether a screen reader or Switch Access is driving the phone. The grown-up gate's "hold for
/// 3 seconds" step becomes a plain button then (mock screen 6b); the sum stays. TalkBack shows
/// as touch exploration; Switch Access is found by its exact Google service id. Other accessibility services
/// (a password manager, a dictation or autofill tool) do not count, so they don't weaken the
/// gate. On iOS, VoiceOver and Switch Control count; on Windows, any screen reader counts: Narrator via its running process, NVDA/JAWS via the system flag (Slice 67).
/// </summary>
public static class ScreenReader
{
#if ANDROID
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
#elif IOS
	public static bool IsOn() => UIAccessibility.IsVoiceOverRunning || UIAccessibility.IsSwitchControlRunning;
#elif WINDOWS
	// Narrator does not set SPI_GETSCREENREADER (NVDA, JAWS and other third-party readers do), so
	// that flag alone misses it; a running Narrator.exe covers it. A UI Automation listener check is
	// not used: IMEs, recorders and remote-desktop tools attach UIA clients too, and a false positive
	// weakens the grown-up gate, so a false negative is the preferred error.
	private const uint GetScreenReader = 0x0046;
	private const string NarratorProcessName = "Narrator";

	public static bool IsOn() => IsScreenReaderFlagSet() || IsNarratorRunning();

	private static bool IsScreenReaderFlagSet() => SystemParametersInfo(GetScreenReader, 0, out bool on, 0) && on;

	private static bool IsNarratorRunning()
	{
		Process[] found = Process.GetProcessesByName(NarratorProcessName);
		foreach (Process process in found)
		{
			process.Dispose();
		}

		return found.Length > 0;
	}

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SystemParametersInfo(uint action, uint param, [MarshalAs(UnmanagedType.Bool)] out bool value, uint winIni);
#else
	public static bool IsOn() => false;
#endif
}
