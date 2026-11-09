using DinoReader.Core;
using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>
/// A page that answers the Windows window's keyboard (Slice 235 part B): it says which screen it is and what each <see cref="DinoKeyAction"/> does there. Shared code with no platform types;
/// only the Windows head feeds it keys (<c>Platforms/Windows/DinoKeyboard.cs</c>), so phones and tablets never call it.
/// </summary>
internal interface IDinoKeyTarget
{
	/// <summary>Which kind of screen this is right now (a shelf shows <see cref="DinoScreen.Sheet"/> while its options sheet is open).</summary>
	DinoScreen Screen { get; }

	/// <summary>Runs <paramref name="action"/> (made by <paramref name="key"/>, which a gate digit needs); false when it does not apply right now, so the key goes on to the focused control as usual.</summary>
	bool Handle(DinoKeyAction action, DinoKey key);
}

/// <summary>Finds the page that has the keyboard and runs a key press through the key map.</summary>
internal static class DinoKeyTargets
{
	/// <summary>The page the user is looking at: the top modal (the gate, the intro), else the navigation stack's current page.</summary>
	public static Page? TopPage(Microsoft.Maui.Controls.Window? window)
	{
		if (window?.Page is not NavigationPage navigation)
		{
			return window?.Page;
		}

		Page? modal = navigation.Navigation.ModalStack.LastOrDefault();
		return modal is NavigationPage modalNavigation ? modalNavigation.CurrentPage : modal ?? navigation.CurrentPage;
	}

	private static readonly BackInFlight BackGate = new();
	private static readonly TimeSpan HandledBackWait = TimeSpan.FromSeconds(1);

	// Esc on a page that is not a key target goes back one page, when it is the top of a stack with more than one page (the shelf is a target, so it never gets here).
	// One back at a time: while one is in flight every further Esc is swallowed, so a held or repeated Esc can never pop a second page.
	private static bool PopOne(Page? page)
	{
		if (page is null || page.Navigation.NavigationStack.Count <= 1 || page.Navigation.NavigationStack[^1] != page)
		{
			return false;
		}

		if (BackGate.TryBegin())
		{
			_ = GoBackAsync(page);
		}

		return true;
	}

	private static async Task GoBackAsync(Page page)
	{
		try
		{
			// The same path the system Back takes: the page's own OnBackButtonPressed (language commit, picker close, Settings CloseAsync, unsaved edits) runs first, and we pop only if it did not handle it.
			var left = new TaskCompletionSource();
			page.Disappearing += (_, _) => left.TrySetResult();
			if (!page.SendBackButtonPressed())
			{
				await page.Navigation.PopAsync();
				return;
			}

			await Task.WhenAny(left.Task, Task.Delay(HandledBackWait)); // the page handled it itself (its own async close): stay in flight until it has left, or a second back would pop the page below
		}
		finally
		{
			BackGate.End();
		}
	}

	/// <summary>True when the key was used and must not reach the focused control.</summary>
	public static bool Route(Microsoft.Maui.Controls.Window? window, DinoKey key, bool keyDown, bool textFieldFocused)
	{
		Page? top = TopPage(window);
		if (top is not IDinoKeyTarget target)
		{
			return keyDown && DinoKeyMap.Resolve(key, DinoScreen.Other, textFieldFocused, keyDown) == DinoKeyAction.Back && PopOne(top);
		}

		DinoKeyAction action = DinoKeyMap.Resolve(key, target.Screen, textFieldFocused, keyDown);
		if (action == DinoKeyAction.None)
		{
			return false;
		}

		return target.Handle(action, key);
	}
}

/// <summary>A view laid over a page's root grid while it is open (a sheet, a menu, a picker). A page asks whether any of them is visible before it lets the arrow keys move the focus (Slice 235 part C, Windows).</summary>
internal interface IPageOverlay
{
}

/// <summary>Desktop-only hints (Slice 235 part B): tooltips with the key in brackets. Every call is a no-op off Windows, so phones and tablets keep what they have.</summary>
internal static class DesktopTips
{
	private const string WithKey = "{0} ({1})";

	/// <summary>True on the Windows head, the only place with a mouse pointer and a keyboard reaching the pages.</summary>
	public static bool IsWindows => DeviceInfo.Platform == DevicePlatform.WinUI;

	/// <summary>Sets the tooltip of an icon-only control to <paramref name="text"/>, followed by <paramref name="key"/> in brackets when there is one.</summary>
	public static void Set(BindableObject element, string text, DinoKey? key = null)
	{
		if (!IsWindows)
		{
			return;
		}

		ToolTipProperties.SetText(element, key is { } shortcut ? string.Format(AppResources.Culture, WithKey, text, KeyName(shortcut)) : text);
	}

	// Mock Batch 46 frame 159: an icon button darkens one step under the pointer (#ECE7FF to #DCD5FF; from no fill it takes the first step).
	private static readonly Color HoverFromNothing = Color.FromArgb("#ECE7FF");
	private const float LightFillLuminosity = 0.5f;
	private const float HoverStep = 0.07f;

	// One step from the button's own fill (a coral Play stays coral): a light fill gets a little darker, a dark one a little lighter.
	private static Color OneStepFrom(Color? fill)
	{
		if (fill is null || fill.Alpha == 0)
		{
			return HoverFromNothing;
		}

		float luminosity = fill.GetLuminosity();
		return fill.WithLuminosity(Math.Clamp(luminosity + (luminosity > LightFillLuminosity ? -HoverStep : HoverStep), 0f, 1f));
	}

	/// <summary>
	/// Slice 235 part C: the pointer is a hand over <paramref name="button"/> and its fill darkens one step while the mouse is over it. A no-op off Windows, so touch screens keep what they have.
	/// The resting fill is read when the pointer arrives, so a fill the page changes in between (a selected state) is put back as it was.
	/// </summary>
	public static void Hover(Border button)
	{
		if (!IsWindows)
		{
			return;
		}

		bool hovering = false;
		Color? restColor = null; // the button's own BackgroundColor, null included: put back as it was
		Brush? restBrush = null; // or its own Background brush, put back as the same object
		SolidColorBrush? ourBrush = null; // set only when the rest fill was a brush
		Color? tint = null;
		void Clear()
		{
			if (!hovering)
			{
				return;
			}

			hovering = false;
			// A fill the page set while the pointer was over the button (a theme change, the Play button's state) is not ours to undo.
			if (ourBrush is not null)
			{
				if (ReferenceEquals(button.Background, ourBrush))
				{
					button.Background = restBrush;
				}
			}
			else if (button.BackgroundColor is { } shown && tint is { } ours && shown.Equals(ours))
			{
				button.BackgroundColor = restColor;
			}

			ourBrush = null;
		}

		var pointer = new PointerGestureRecognizer();
		pointer.PointerEntered += (_, _) =>
		{
			if (hovering)
			{
				return; // a second Enter without an Exit: the first one saved the real fill
			}

			if (button.BackgroundColor is null && button.Background is not null and not SolidColorBrush)
			{
				return; // a gradient or image fill is not ours to flatten: no tint
			}

			hovering = true;
			restColor = button.BackgroundColor;
			restBrush = button.Background;
			bool fillIsBrush = restColor is null && restBrush is not null;
			tint = OneStepFrom(restColor ?? (restBrush as SolidColorBrush)?.Color);
			if (fillIsBrush)
			{
				ourBrush = new SolidColorBrush(tint);
				button.Background = ourBrush;
			}
			else
			{
				button.BackgroundColor = tint;
			}
		};
		pointer.PointerExited += (_, _) => Clear();
		button.GestureRecognizers.Add(pointer);
		button.Unloaded += (_, _) => Clear();
#if WINDOWS
		DinoCursor.UseOpenHand(button);
#endif
	}

	/// <summary>The key as written in the tooltip, in the app language.</summary>
	public static string KeyName(DinoKey key) => key switch
	{
		DinoKey.Space => AppResources.Key_Space,
		DinoKey.Left => AppResources.Key_Left,
		DinoKey.Right => AppResources.Key_Right,
		DinoKey.Escape => AppResources.Key_Esc,
		_ => key.ToString(),
	};
}
