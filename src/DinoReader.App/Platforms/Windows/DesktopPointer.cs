using System.Reflection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;

namespace DinoReader;

/// <summary>
/// The mouse pointer over a Dino element on Windows (Slice 235 part C, mock Batch 46 frame 151: "the pointer becomes a hand" over a book and a button). WinUI only lets a control author set the cursor
/// (<c>UIElement.ProtectedCursor</c> is protected), and MAUI has no cursor property, so it is set by reflection once the element has its native view; if that ever fails the cursor simply stays the arrow.
/// </summary>
internal static class DesktopPointer
{
	private static readonly PropertyInfo? CursorProperty =
		typeof(UIElement).GetProperty("ProtectedCursor", BindingFlags.Instance | BindingFlags.NonPublic);

	private static int _failures;

	/// <summary>Shows the hand over <paramref name="element"/> now, or as soon as it has a native view.</summary>
	public static void UseHand(VisualElement element)
	{
		if (!Apply(element))
		{
			element.HandlerChanged += (_, _) => Apply(element);
		}
	}

	private static bool Apply(VisualElement element)
	{
		if (element.Handler?.PlatformView is not UIElement native)
		{
			return false;
		}

		try
		{
			CursorProperty?.SetValue(native, InputSystemCursor.Create(InputSystemCursorShape.Hand));
		}
		catch (Exception ex) when (_failures++ == 0)
		{
			WindowTrace.Write($"Dino pointer: the hand cursor could not be set ({ex.GetType().Name}), the arrow stays");
		}
		catch (Exception)
		{
			// Already traced once; the arrow stays.
		}

		return true;
	}
}
