#if ANDROID
using AView = Android.Views.View;
using Android.Views.Accessibility;

namespace DinoReader;

/// <summary>Tells TalkBack the <see cref="ToggleSwitch"/> is a switch with a checked state (Slice 117), so its on/off is spoken in the device's language.</summary>
internal sealed class ToggleSwitchAccessibility(ToggleSwitch toggle) : AView.AccessibilityDelegate
{
	private const string SwitchClassName = "android.widget.Switch";

	public override void OnInitializeAccessibilityNodeInfo(AView? host, AccessibilityNodeInfo? info)
	{
		base.OnInitializeAccessibilityNodeInfo(host, info);
		if (info is null)
		{
			return;
		}

		info.ClassName = SwitchClassName;
		info.Checkable = true;
		info.Checked = toggle.IsOn;
		info.ContentDescription = Microsoft.Maui.Controls.SemanticProperties.GetDescription(toggle);
	}

	/// <summary>Installs the delegate on the native view of <paramref name="toggle"/> once it has one.</summary>
	public static void Attach(ToggleSwitch toggle)
	{
		toggle.HandlerChanged += (_, _) => Install(toggle);
		Install(toggle);
	}

	/// <summary>Tells the screen reader the state changed.</summary>
	public static void Announce(ToggleSwitch toggle) =>
		(toggle.Handler?.PlatformView as AView)?.SendAccessibilityEvent(Android.Views.Accessibility.EventTypes.ViewClicked);

	private static void Install(ToggleSwitch toggle)
	{
		if (toggle.Handler?.PlatformView is AView view)
		{
			view.SetAccessibilityDelegate(new ToggleSwitchAccessibility(toggle));
		}
	}
}
#elif IOS
using Foundation;
using UIKit;

namespace DinoReader;

/// <summary>
/// The VoiceOver twin of the Android <c>ToggleSwitchAccessibility</c> (Slice 160): the switch is one accessibility element named like the
/// Android one, a button that VoiceOver calls a switch (iOS 17 and later: <c>ToggleButton</c>, which speaks on or off in the device's language)
/// or, before that, a selected or unselected button. No string of ours is spoken, so there is nothing to translate.
/// </summary>
internal static class ToggleSwitchAccessibility
{
	// UIAccessibilityTraitToggleButton (iOS 17), 1 << 53; the binding has no name for it yet.
	private static readonly UIAccessibilityTrait ToggleButtonTrait = (UIAccessibilityTrait)(1UL << 53);

	/// <summary>Installs the accessibility settings on the native view of <paramref name="toggle"/> once it has one.</summary>
	public static void Attach(ToggleSwitch toggle)
	{
		toggle.HandlerChanged += (_, _) => Apply(toggle);
		Apply(toggle);
	}

	/// <summary>Refreshes the state VoiceOver reads, without moving VoiceOver's focus (for changes the user did not make).</summary>
	public static void Refresh(ToggleSwitch toggle) => Apply(toggle);

	/// <summary>Refreshes the state and, when VoiceOver is running, makes it read the element again. Only for a toggle by the user.</summary>
	public static void Announce(ToggleSwitch toggle)
	{
		if (Apply(toggle) is { } view && UIAccessibility.IsVoiceOverRunning)
		{
			UIAccessibility.PostNotification(UIAccessibilityPostNotification.LayoutChanged, view);
		}
	}

	private static UIView? Apply(ToggleSwitch toggle)
	{
		if (toggle.Handler?.PlatformView is not UIView view)
		{
			return null;
		}

		view.IsAccessibilityElement = true;
		view.AccessibilityLabel = Microsoft.Maui.Controls.SemanticProperties.GetDescription(toggle);
		view.AccessibilityTraits = Traits(toggle);
		view.AccessibilityValue = StateValue(toggle);
		return view;
	}

	// One state carrier per iOS version, so VoiceOver says the state once: iOS 17 and later reads on or off from the value "1"/"0" of the
	// ToggleButton trait (as UISwitch does) and gets no Selected trait; before that the Selected trait is the only carrier and there is no value.
	private static string? StateValue(ToggleSwitch toggle) =>
		OperatingSystem.IsIOSVersionAtLeast(17) ? (toggle.IsOn ? "1" : "0") : null;

	private static UIAccessibilityTrait Traits(ToggleSwitch toggle)
	{
		UIAccessibilityTrait traits;
		if (OperatingSystem.IsIOSVersionAtLeast(17))
		{
			traits = ToggleButtonTrait;
		}
		else
		{
			traits = UIAccessibilityTrait.Button | (toggle.IsOn ? UIAccessibilityTrait.Selected : UIAccessibilityTrait.None);
		}

		return toggle.IsEnabled ? traits : traits | UIAccessibilityTrait.NotEnabled;
	}
}
#endif
