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
#endif
