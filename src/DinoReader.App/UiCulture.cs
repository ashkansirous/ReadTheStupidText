using System.Globalization;
using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// Picks and applies the culture the screens speak (Slice 66, Decision 74): the device language when it is one
/// of the nine, else English. Dino Reader has no in-app UI language picker: a child's device already has one.
/// </summary>
public static class UiCulture
{
	/// <summary>The culture for a device culture: one of the nine languages, English when the device speaks another.</summary>
	public static CultureInfo Resolve(CultureInfo device) => CultureInfo.GetCultureInfo(UiLanguage.Resolve(device.Name));

	/// <summary>
	/// Makes <paramref name="culture"/> the UI culture of every thread. Only the process-wide default is set (the per-flow
	/// <c>CurrentUICulture</c> is undone when an async continuation returns), and number and date formats stay the device's.
	/// </summary>
	public static void Apply(CultureInfo culture) => CultureInfo.DefaultThreadCurrentUICulture = culture;

	public static bool IsRightToLeft => UiLanguage.IsRightToLeft(CultureInfo.CurrentUICulture.Name);
}

/// <summary>Maps the UI culture to a MAUI <see cref="FlowDirection"/>, and mirrors the physical paddings a design gives in left/right terms.</summary>
public static class UiFlow
{
	public static FlowDirection ForCurrentCulture() =>
		UiCulture.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

	/// <summary>
	/// Swaps left and right for right-to-left screens. A <see cref="Thickness"/> is physical, not start/end, so a design that
	/// pads or indents one side must be mirrored by hand.
	/// </summary>
	public static Thickness Mirror(Thickness thickness) =>
		UiCulture.IsRightToLeft ? new Thickness(thickness.Right, thickness.Top, thickness.Left, thickness.Bottom) : thickness;
}
