using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;

namespace DinoReader;

/// <summary>
/// Windows only. WinUI keeps <c>UIElement.ProtectedCursor</c> protected, for control authors, so it is set by reflection on the MAUI view's native element.
/// WinUI has no grab-hand shapes: the open hand is its pointing <c>Hand</c> and the closed hand its four-way <c>SizeAll</c>, the nearest it offers.
/// </summary>
internal static class DinoCursor
{
	private static readonly PropertyInfo? CursorProperty = FindCursorProperty();
	private static InputCursor? _openHand; // created once, on first use on the UI thread, and kept: a new cursor per press would never be disposed
	private static InputCursor? _closedHand;

	public static void Set(Microsoft.Maui.Controls.VisualElement view, DinoCursorShape shape)
	{
		if (view.Handler?.PlatformView is UIElement element)
		{
			Set(element, shape);
		}
	}

	/// <summary>The same on a native element, for containers the page does not own as MAUI views (the shelf's native list items).</summary>
	public static void Set(UIElement element, DinoCursorShape shape)
	{
		CursorProperty?.SetValue(element, CursorFor(shape));
	}

	/// <summary>Shows the open hand over <paramref name="view"/> now, or as soon as it has a native view (Slice 235 part C: buttons).</summary>
	public static void UseOpenHand(Microsoft.Maui.Controls.VisualElement view)
	{
		if (view.Handler?.PlatformView is UIElement)
		{
			Set(view, DinoCursorShape.OpenHand);
			return;
		}

		view.HandlerChanged += (_, _) => Set(view, DinoCursorShape.OpenHand);
	}

	// The attribute keeps the protected property when the app is trimmed.
	[DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicProperties, typeof(UIElement))]
	private static PropertyInfo? FindCursorProperty()
	{
		PropertyInfo? property = typeof(UIElement).GetProperty("ProtectedCursor", BindingFlags.Instance | BindingFlags.NonPublic);
		if (property is null)
		{
			Debug.WriteLine("Dino Reader: UIElement.ProtectedCursor was not found; the reorder cursors stay the default arrow");
		}

		return property;
	}

	private static InputCursor? CursorFor(DinoCursorShape shape) => shape switch
	{
		DinoCursorShape.OpenHand => _openHand ??= InputSystemCursor.Create(InputSystemCursorShape.Hand),
		DinoCursorShape.ClosedHand => _closedHand ??= InputSystemCursor.Create(InputSystemCursorShape.SizeAll),
		_ => null,
	};
}
